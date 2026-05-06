"""FPL API consumption contract tests (T012) — locks ``contracts/fpl_api.md``.

Verifies:

* :class:`fpl.api.FPLClient` exposes the four documented methods.
* Outbound requests carry the descriptive ``User-Agent`` from
  ``fpl-analyzer/<version>`` (FR-035).
* Only the documented endpoints are called — no per-element follow-ups
  (FR-037 ≤10 cold-run cap).
* Cold-run request budget: ≤ 4 requests with all four sources, ≤ 2
  without picks/Understat.
* HTTP 404 on the picks endpoint raises :class:`UnknownTeamId` so the
  CLI can map to exit code 5.
"""

from __future__ import annotations

import pytest
import responses

from fpl import _version
from fpl.api import (
    DEFAULT_USER_AGENT,
    FPL_BASE,
    UNDERSTAT_BASE,
    FPLClient,
)
from fpl.errors import UnknownTeamId


pytestmark = pytest.mark.contract


# ---------------------------------------------------------------------------
# Public surface
# ---------------------------------------------------------------------------


class TestPublicSurface:
    def test_module_exposes_required_names(self):
        from fpl import api

        for name in ("FPLClient", "DEFAULT_USER_AGENT", "FPL_BASE", "UNDERSTAT_BASE"):
            assert hasattr(api, name), f"fpl.api missing {name!r}"

    def test_client_has_documented_methods(self):
        client = FPLClient()
        for name in (
            "fetch_bootstrap_static",
            "fetch_fixtures",
            "fetch_team_picks",
            "fetch_understat_optional",
        ):
            assert hasattr(client, name), f"FPLClient missing method {name!r}"

    def test_user_agent_matches_package_version(self):
        # FR-035: the User-Agent must clearly identify the analyzer.
        assert DEFAULT_USER_AGENT == f"fpl-analyzer/{_version.__version__}"


# ---------------------------------------------------------------------------
# Documented endpoints
# ---------------------------------------------------------------------------


class TestBootstrapStaticEndpoint:
    @responses.activate
    def test_calls_documented_endpoint(self, bootstrap_static_payload):
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/bootstrap-static/",
            json=bootstrap_static_payload,
            status=200,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        assert client.fetch_bootstrap_static() == bootstrap_static_payload
        assert len(responses.calls) == 1

    @responses.activate
    def test_sends_descriptive_user_agent(self, bootstrap_static_payload):
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/bootstrap-static/",
            json=bootstrap_static_payload,
            status=200,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        client.fetch_bootstrap_static()
        assert responses.calls[0].request.headers["User-Agent"] == DEFAULT_USER_AGENT


class TestFixturesEndpoint:
    @responses.activate
    def test_calls_documented_endpoint(self, fixtures_payload):
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/fixtures/",
            json=fixtures_payload,
            status=200,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        assert client.fetch_fixtures() == fixtures_payload
        assert len(responses.calls) == 1


class TestTeamPicksEndpoint:
    @responses.activate
    def test_calls_documented_endpoint(self, picks_payload):
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/entry/12345/event/30/picks/",
            json=picks_payload,
            status=200,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        assert client.fetch_team_picks(12345, 30) == picks_payload

    @responses.activate
    def test_404_raises_unknown_team_id(self):
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/entry/99999/event/30/picks/",
            status=404,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        with pytest.raises(UnknownTeamId):
            client.fetch_team_picks(99999, 30)


class TestUnderstatOptional:
    @responses.activate
    def test_returns_parsed_data_on_success(self, understat_html):
        responses.add(
            responses.GET,
            f"{UNDERSTAT_BASE}/league/EPL/2025",
            body=understat_html,
            status=200,
            content_type="text/html",
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        data = client.fetch_understat_optional("2025")
        assert data is not None
        assert "players" in data
        assert len(data["players"]) >= 1
        # Spot-check that a known field landed in the parsed data.
        names = {p["player_name"] for p in data["players"]}
        assert "Alvarez" in names

    @responses.activate
    def test_returns_none_on_404(self):
        # FR-005: graceful degradation. The Run Status panel records the
        # outcome via the diagnostics layer; the api itself just signals None.
        responses.add(responses.GET, f"{UNDERSTAT_BASE}/league/EPL/2025", status=404)
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        assert client.fetch_understat_optional("2025") is None

    @responses.activate
    def test_returns_none_on_unparseable_html(self):
        responses.add(
            responses.GET,
            f"{UNDERSTAT_BASE}/league/EPL/2025",
            body="<html>no playersData here</html>",
            status=200,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        assert client.fetch_understat_optional("2025") is None


# ---------------------------------------------------------------------------
# Request budget (FR-037)
# ---------------------------------------------------------------------------


class TestRequestBudget:
    @responses.activate
    def test_full_cold_run_uses_at_most_four_requests(
        self,
        bootstrap_static_payload,
        fixtures_payload,
        picks_payload,
        understat_html,
    ):
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/bootstrap-static/",
            json=bootstrap_static_payload,
            status=200,
        )
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/fixtures/",
            json=fixtures_payload,
            status=200,
        )
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/entry/12345/event/30/picks/",
            json=picks_payload,
            status=200,
        )
        responses.add(
            responses.GET,
            f"{UNDERSTAT_BASE}/league/EPL/2025",
            body=understat_html,
            status=200,
            content_type="text/html",
        )

        client = FPLClient(pacing_sec=0, retry_backoff=0)
        client.fetch_bootstrap_static()
        client.fetch_fixtures()
        client.fetch_team_picks(12345, 30)
        client.fetch_understat_optional("2025")

        # Total = 4 (well under FR-037's ≤10 cap).
        assert len(responses.calls) == 4

    @responses.activate
    def test_minimal_cold_run_uses_two_requests(
        self, bootstrap_static_payload, fixtures_payload
    ):
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/bootstrap-static/",
            json=bootstrap_static_payload,
            status=200,
        )
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/fixtures/",
            json=fixtures_payload,
            status=200,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        client.fetch_bootstrap_static()
        client.fetch_fixtures()
        assert len(responses.calls) == 2


# ---------------------------------------------------------------------------
# Forbidden endpoints — defensive guard (FR-037)
# ---------------------------------------------------------------------------


class TestForbiddenEndpoints:
    def test_no_per_element_summary_method(self):
        # contracts/fpl_api.md § Forbidden: per-player history would require ~600 calls.
        client = FPLClient()
        for forbidden in (
            "fetch_element_summary",
            "fetch_element_history",
            "fetch_per_player_history",
            "fetch_me",
            "fetch_login",
        ):
            assert not hasattr(client, forbidden), (
                f"FPLClient must NOT expose {forbidden!r} per FR-037 / contracts/fpl_api.md"
            )
