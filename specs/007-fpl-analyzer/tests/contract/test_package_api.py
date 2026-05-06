"""Package API contract tests (T023) — locks ``contracts/package_api.md``.

Verifies:

* :func:`fpl.run_analysis` exists and has the keyword-only signature.
* Returned mapping exposes every legacy key consumed by ``fpl_gui.py``.
* Returned mapping also supports attribute access for the typed
  accessors (``result.run_status``, ``result.chip_plan``, etc.).
"""

from __future__ import annotations

import inspect

import pytest
import responses

from fpl import run_analysis
from fpl.api import FPL_BASE, UNDERSTAT_BASE
from fpl.types import ChipPlan, RunStatus


pytestmark = pytest.mark.contract


# ---------------------------------------------------------------------------
# Signature
# ---------------------------------------------------------------------------


class TestSignature:
    def test_run_analysis_is_keyword_only(self):
        sig = inspect.signature(run_analysis)
        params = sig.parameters
        # All declared parameters MUST be keyword-only or have defaults.
        for name, param in params.items():
            if name in ("args", "kwargs"):
                continue
            if param.kind == inspect.Parameter.VAR_POSITIONAL:
                # *args slot consumed by the keyword-only marker.
                continue
            if param.kind not in (
                inspect.Parameter.KEYWORD_ONLY,
                inspect.Parameter.POSITIONAL_OR_KEYWORD,
            ):
                pytest.fail(f"parameter {name!r} should be keyword-only or have a default")


# ---------------------------------------------------------------------------
# End-to-end legacy keys
# ---------------------------------------------------------------------------


def _stub_endpoints(
    bootstrap_static_payload, fixtures_payload, picks_payload, understat_html
):
    responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/",
                   json=bootstrap_static_payload, status=200)
    responses.add(responses.GET, f"{FPL_BASE}/api/fixtures/",
                   json=fixtures_payload, status=200)
    responses.add(responses.GET, f"{FPL_BASE}/api/entry/12345/event/30/picks/",
                   json=picks_payload, status=200)
    responses.add(responses.GET, f"{UNDERSTAT_BASE}/league/EPL/2025",
                   body=understat_html, status=200)


class TestLegacyKeys:
    REQUIRED_KEYS = (
        "mode",
        "gw",
        "horizon",
        "budget",
        "team_id",
        "pred_df",
        "differential_df",
        "current_squad_plan",
        "multi_week_plan",
        "chip_plan",
        "primary_view",
        "top_features",
        "baseline_mae",
        "model_weights",
        "run_status",
    )

    @responses.activate
    def test_continuity_returns_all_legacy_keys(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload, understat_html,
    ):
        _stub_endpoints(bootstrap_static_payload, fixtures_payload, picks_payload, understat_html)
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=12345,
            no_multi_week=True,
            cache_dir=str(tmp_path),
        )
        for key in self.REQUIRED_KEYS:
            assert key in result, f"missing legacy key: {key!r}"
        # primary_view value reflects the supplied team_id.
        assert result["primary_view"] == "continuity"
        assert result["mode"] == "analysis"
        assert result["team_id"] == 12345

    @responses.activate
    def test_from_scratch_returns_all_legacy_keys(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, understat_html,
    ):
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/",
                       json=bootstrap_static_payload, status=200)
        responses.add(responses.GET, f"{FPL_BASE}/api/fixtures/",
                       json=fixtures_payload, status=200)
        responses.add(responses.GET, f"{UNDERSTAT_BASE}/league/EPL/2025",
                       body=understat_html, status=200)
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=None,
            no_multi_week=True,
            cache_dir=str(tmp_path),
        )
        for key in self.REQUIRED_KEYS:
            assert key in result, f"missing legacy key: {key!r}"
        assert result["primary_view"] == "from_scratch"
        # Per the contract, mode is "from_scratch" when no team_id.
        assert result["mode"] == "from_scratch"
        assert result["team_id"] is None

    @responses.activate
    def test_typed_accessors_work_alongside_dict_access(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload, understat_html,
    ):
        _stub_endpoints(bootstrap_static_payload, fixtures_payload, picks_payload, understat_html)
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=12345,
            no_multi_week=True,
            cache_dir=str(tmp_path),
        )
        # contracts/package_api.md: result.run_status, result.chip_plan, etc.
        assert isinstance(result.run_status, RunStatus)
        assert isinstance(result.chip_plan, ChipPlan)
        # Same value via dict access.
        assert result.run_status is result["run_status"]
        assert result.chip_plan is result["chip_plan"]
