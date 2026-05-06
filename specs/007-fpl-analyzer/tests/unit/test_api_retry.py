"""FPL API retry / backoff unit tests (T013).

Verifies the polite-client policy from FR-035:

* Retries on HTTP 429 and 5xx using urllib3's ``Retry`` adapter.
* Total of 3 retries (so up to 4 attempts per call).
* Exhausting the retry budget surfaces an :class:`FPLApiError` with the
  failing status.
* 4xx responses (other than 429) are NOT retried; they raise
  immediately.
* Modest inter-request pacing — ``time.sleep`` is invoked between
  consecutive calls when ``pacing_sec > 0``.

Tests use ``retry_backoff=0`` to keep the suite fast; the production
default of ``backoff_factor=1.0`` is exercised by the documented
behaviour, not the test runtime.
"""

from __future__ import annotations

import pytest
import responses

from fpl.api import FPL_BASE, FPLClient
from fpl.errors import FPLApiError


pytestmark = pytest.mark.unit


# ---------------------------------------------------------------------------
# Retry behaviour
# ---------------------------------------------------------------------------


class TestRetryBehaviour:
    @responses.activate
    def test_retries_on_503_then_succeeds(self):
        payload = {"events": [], "teams": [], "elements": [], "element_types": []}
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/", status=503)
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/", status=503)
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/bootstrap-static/",
            json=payload,
            status=200,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        assert client.fetch_bootstrap_static() == payload
        assert len(responses.calls) == 3

    @responses.activate
    def test_retries_on_429_then_succeeds(self):
        payload = {"events": [], "teams": [], "elements": [], "element_types": []}
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/", status=429)
        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/bootstrap-static/",
            json=payload,
            status=200,
        )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        assert client.fetch_bootstrap_static() == payload
        assert len(responses.calls) == 2

    @responses.activate
    def test_exhausts_retries_then_raises_fpl_api_error(self):
        # urllib3 Retry total=3 means up to 4 attempts (1 original + 3 retries).
        for _ in range(4):
            responses.add(
                responses.GET, f"{FPL_BASE}/api/bootstrap-static/", status=503
            )
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        with pytest.raises(FPLApiError) as excinfo:
            client.fetch_bootstrap_static()
        # The FPLApiError carries the failing status for diagnostics.
        assert excinfo.value.status == 503
        assert len(responses.calls) == 4

    @responses.activate
    def test_does_not_retry_on_400(self):
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/", status=400)
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        with pytest.raises(FPLApiError) as excinfo:
            client.fetch_bootstrap_static()
        assert excinfo.value.status == 400
        # Single attempt — no retries on 4xx (other than 429).
        assert len(responses.calls) == 1

    @responses.activate
    def test_does_not_retry_on_403(self):
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/", status=403)
        client = FPLClient(pacing_sec=0, retry_backoff=0)
        with pytest.raises(FPLApiError):
            client.fetch_bootstrap_static()
        assert len(responses.calls) == 1


# ---------------------------------------------------------------------------
# Pacing — modest inter-request gap (FR-035)
# ---------------------------------------------------------------------------


class TestPacing:
    @responses.activate
    def test_no_sleep_on_first_request(self, monkeypatch):
        from fpl import api as api_mod

        sleep_calls: list[float] = []
        monkeypatch.setattr(api_mod.time, "sleep", lambda s: sleep_calls.append(s))

        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/bootstrap-static/",
            json={"events": [], "teams": [], "elements": [], "element_types": []},
            status=200,
        )
        client = FPLClient(pacing_sec=0.1, retry_backoff=0)
        client.fetch_bootstrap_static()
        # No pacing sleep before the very first request.
        assert sleep_calls == [] or all(s <= 0 for s in sleep_calls)

    @responses.activate
    def test_pacing_invoked_between_consecutive_requests(self, monkeypatch):
        # We don't assert on the sleep duration (depends on actual elapsed
        # wall-clock between calls); we only assert the pacing branch ran
        # at least once for the second call.
        from fpl import api as api_mod

        sleep_calls: list[float] = []
        monkeypatch.setattr(api_mod.time, "sleep", lambda s: sleep_calls.append(s))

        responses.add(
            responses.GET,
            f"{FPL_BASE}/api/bootstrap-static/",
            json={"events": [], "teams": [], "elements": [], "element_types": []},
            status=200,
        )
        responses.add(
            responses.GET, f"{FPL_BASE}/api/fixtures/", json=[], status=200
        )

        client = FPLClient(pacing_sec=1.0, retry_backoff=0)
        client.fetch_bootstrap_static()
        client.fetch_fixtures()
        # Second call should have triggered a pacing sleep > 0
        # (the 1.0s pacing was much longer than the elapsed time).
        assert any(s > 0 for s in sleep_calls), (
            f"Expected at least one positive sleep, got {sleep_calls}"
        )
