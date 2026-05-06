"""FPL public API client (T014, FR-001/002/003/004/035/037).

Polite-client policy:

* Descriptive ``User-Agent`` header identifies the analyzer.
* ``urllib3.Retry`` adapter retries 429 / 5xx with exponential backoff.
* ~100 ms inter-request pacing keeps cold-run traffic civil.
* Connect timeout 10 s, read timeout 20 s.
* Bulk endpoints only (FR-037): four documented HTTP calls per cold
  run; per-player loops are not exposed.

The client is intentionally low-level — it returns raw JSON shapes.
DataFrame-shaping happens in :mod:`fpl.features` (T020) where it can be
unit-tested without HTTP mocks.
"""

from __future__ import annotations

import json
import re
import time
from typing import Any

import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

from fpl._version import __version__
from fpl.errors import FPLApiError, UnknownTeamId

# ---------------------------------------------------------------------------
# Constants — exposed because tests assert on them.
# ---------------------------------------------------------------------------

FPL_BASE = "https://fantasy.premierleague.com"
UNDERSTAT_BASE = "https://understat.com"

DEFAULT_USER_AGENT = f"fpl-analyzer/{__version__}"
DEFAULT_PACING_SEC = 0.1
DEFAULT_TIMEOUT = (10.0, 20.0)  # (connect, read)
DEFAULT_RETRY_TOTAL = 3
DEFAULT_RETRY_BACKOFF = 1.0
RETRYABLE_STATUSES: frozenset[int] = frozenset({429, 500, 502, 503, 504})


# ---------------------------------------------------------------------------
# Session factory
# ---------------------------------------------------------------------------


def make_session(
    *,
    user_agent: str = DEFAULT_USER_AGENT,
    retry_total: int = DEFAULT_RETRY_TOTAL,
    retry_backoff: float = DEFAULT_RETRY_BACKOFF,
) -> requests.Session:
    """Create a ``requests.Session`` configured for the FPL public API.

    Tests pass ``retry_backoff=0`` to keep the suite fast; production
    code uses the default of 1.0 (1s, 2s, 4s … delays).
    """
    session = requests.Session()
    session.headers.update(
        {
            "User-Agent": user_agent,
            "Accept": "application/json",
        }
    )
    retry = Retry(
        total=retry_total,
        backoff_factor=retry_backoff,
        status_forcelist=sorted(RETRYABLE_STATUSES),
        allowed_methods=("GET",),
        raise_on_status=False,
    )
    adapter = HTTPAdapter(max_retries=retry)
    session.mount("https://", adapter)
    session.mount("http://", adapter)
    return session


# ---------------------------------------------------------------------------
# Understat parser
# ---------------------------------------------------------------------------


# JS source format:
#   var playersData = JSON.parse('[\
#       {"id":"u101", ...},\
#       ...
#   ]');
# We need the JSON inside JSON.parse('...').
_UNDERSTAT_RE = re.compile(
    r"var\s+playersData\s*=\s*JSON\.parse\(\s*'(?P<json>.+?)'\s*\)\s*;",
    re.DOTALL,
)


def _parse_understat_html(html: str) -> dict | None:
    """Best-effort parse of an Understat EPL season page."""
    match = _UNDERSTAT_RE.search(html)
    if match is None:
        return None
    raw = match.group("json")
    # Strip JS line-continuation backslashes (`\` followed by whitespace+newline).
    cleaned = re.sub(r"\\\s*\n\s*", "", raw)
    try:
        players = json.loads(cleaned)
    except json.JSONDecodeError:
        return None
    if not isinstance(players, list):
        return None
    return {"players": players}


# ---------------------------------------------------------------------------
# Client
# ---------------------------------------------------------------------------


class FPLClient:
    """Bulk-endpoint client for the FPL public API (FR-037).

    Construct once per cold-run; the disk cache (``fpl.cache``) sits
    above this layer and only invokes a fetcher on a cache miss.
    """

    def __init__(
        self,
        *,
        session: requests.Session | None = None,
        pacing_sec: float = DEFAULT_PACING_SEC,
        retry_total: int = DEFAULT_RETRY_TOTAL,
        retry_backoff: float = DEFAULT_RETRY_BACKOFF,
    ) -> None:
        self.session = session or make_session(
            retry_total=retry_total, retry_backoff=retry_backoff
        )
        self.pacing_sec = pacing_sec
        self._last_request_at: float | None = None

    # -- Public surface -----------------------------------------------------

    def fetch_bootstrap_static(self) -> dict:
        """``GET /api/bootstrap-static/`` — players, teams, events, types."""
        url = f"{FPL_BASE}/api/bootstrap-static/"
        resp = self._get(url)
        self._raise_for_status(resp, url)
        return resp.json()

    def fetch_fixtures(self) -> list:
        """``GET /api/fixtures/`` — fixtures across the season."""
        url = f"{FPL_BASE}/api/fixtures/"
        resp = self._get(url)
        self._raise_for_status(resp, url)
        return resp.json()

    def fetch_team_picks(self, team_id: int, gw: int) -> dict:
        """``GET /api/entry/{team_id}/event/{gw}/picks/`` — squad as of ``gw``.

        Raises :class:`UnknownTeamId` on HTTP 404 so the CLI can map to
        exit 5 and the orchestrator can fall back to from-scratch mode
        per FR-034.
        """
        url = f"{FPL_BASE}/api/entry/{team_id}/event/{gw}/picks/"
        resp = self._get(url)
        if resp.status_code == 404:
            raise UnknownTeamId(team_id)
        self._raise_for_status(resp, url)
        return resp.json()

    def fetch_understat_optional(self, season: str) -> dict | None:
        """``GET https://understat.com/league/EPL/{season}`` — best-effort.

        Per FR-005, any failure (HTTP error, parse error) returns
        ``None`` rather than raising, so the orchestrator can record the
        skip in the Run Status panel and proceed without xG/xA features.
        """
        url = f"{UNDERSTAT_BASE}/league/EPL/{season}"
        self._pace()
        try:
            resp = self.session.get(url, timeout=DEFAULT_TIMEOUT)
        except requests.RequestException:
            self._last_request_at = time.monotonic()
            return None
        self._last_request_at = time.monotonic()
        if resp.status_code != 200:
            return None
        return _parse_understat_html(resp.text)

    # -- Internals ----------------------------------------------------------

    def _pace(self) -> None:
        """Sleep so consecutive requests are at least ``pacing_sec`` apart."""
        if self._last_request_at is None or self.pacing_sec <= 0:
            return
        elapsed = time.monotonic() - self._last_request_at
        wait = self.pacing_sec - elapsed
        if wait > 0:
            time.sleep(wait)

    def _get(self, url: str) -> requests.Response:
        """Issue a paced GET, translating network errors to ``FPLApiError``."""
        self._pace()
        try:
            resp = self.session.get(url, timeout=DEFAULT_TIMEOUT)
        except requests.RequestException as exc:
            self._last_request_at = time.monotonic()
            raise FPLApiError(f"Network error fetching {url}: {exc}", endpoint=url) from exc
        self._last_request_at = time.monotonic()
        return resp

    @staticmethod
    def _raise_for_status(resp: requests.Response, url: str) -> None:
        """Translate any non-success status to ``FPLApiError``.

        Retryable statuses (429, 5xx) at this point indicate the
        ``urllib3.Retry`` adapter has already exhausted its budget.
        """
        if resp.status_code in RETRYABLE_STATUSES:
            raise FPLApiError(
                f"FPL API failed with HTTP {resp.status_code} after retry exhaustion",
                endpoint=url,
                status=resp.status_code,
            )
        if resp.status_code >= 400:
            snippet = resp.text[:200] if resp.text else ""
            raise FPLApiError(
                f"FPL API HTTP {resp.status_code} at {url}: {snippet}",
                endpoint=url,
                status=resp.status_code,
            )


__all__ = [
    "DEFAULT_PACING_SEC",
    "DEFAULT_RETRY_BACKOFF",
    "DEFAULT_RETRY_TOTAL",
    "DEFAULT_TIMEOUT",
    "DEFAULT_USER_AGENT",
    "FPL_BASE",
    "FPLClient",
    "RETRYABLE_STATUSES",
    "UNDERSTAT_BASE",
    "make_session",
]
