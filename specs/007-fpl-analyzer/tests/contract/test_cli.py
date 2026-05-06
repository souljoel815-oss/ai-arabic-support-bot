"""CLI contract tests (T022) — locks ``contracts/cli.md``.

Covers:

* All documented flags parse; ``--help`` exits 0.
* Invalid arg / mutually-exclusive combinations exit 2.
* Exit code mapping: 0 success, 2 invalid, 3 FPL error, 4 cache, 5 unknown team.
* In analysis mode the stdout block layout is RUN STATUS → RECOMMENDATION
  → MULTI-WEEK PLAN (omitted with ``--no-multi-week``) → CHIP PLAN →
  DIFFERENTIALS.
"""

from __future__ import annotations

import pytest
import responses

from fpl.api import FPL_BASE, UNDERSTAT_BASE
from fpl.cli import main
from fpl.errors import FPLApiError


pytestmark = pytest.mark.contract


# ---------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------


class TestArgumentParsing:
    def test_help_exits_zero(self, capsys):
        with pytest.raises(SystemExit) as excinfo:
            main(["--help"])
        assert excinfo.value.code == 0

    def test_invalid_int_exits_two(self, capsys):
        rc = main(["--gw", "abc"])
        assert rc == 2

    def test_gw_out_of_range_exits_two(self, capsys, tmp_path):
        rc = main(["--gw", "100", "--cache-dir", str(tmp_path)])
        assert rc == 2

    def test_horizon_out_of_range_exits_two(self, capsys, tmp_path):
        rc = main(["--horizon", "10", "--cache-dir", str(tmp_path)])
        assert rc == 2

    def test_budget_out_of_range_exits_two(self, capsys, tmp_path):
        rc = main(["--budget", "200", "--cache-dir", str(tmp_path)])
        assert rc == 2


class TestMutuallyExclusiveFlags:
    def test_backtest_with_team_id_rejected(self, capsys, tmp_path):
        rc = main(["--backtest", "10,15", "--team-id", "12345",
                   "--cache-dir", str(tmp_path)])
        assert rc == 2

    def test_backtest_with_horizon_rejected(self, capsys, tmp_path):
        rc = main(["--backtest", "10,15", "--horizon", "4",
                   "--cache-dir", str(tmp_path)])
        assert rc == 2


# ---------------------------------------------------------------------------
# Cache clear
# ---------------------------------------------------------------------------


class TestClearCache:
    def test_clear_cache_returns_zero(self, tmp_path):
        rc = main(["--clear-cache", "--cache-dir", str(tmp_path)])
        assert rc == 0
        # Skeleton dirs must exist after clear.
        for sub in ("data", "models", "runs", "backtests"):
            assert (tmp_path / sub).is_dir()


# ---------------------------------------------------------------------------
# End-to-end analysis (mocks all 4 endpoints, uses tmp cache dir)
# ---------------------------------------------------------------------------


def _stub_all_endpoints(
    rsps,
    bootstrap_static_payload,
    fixtures_payload,
    picks_payload,
    understat_html,
):
    rsps.add(
        responses.GET,
        f"{FPL_BASE}/api/bootstrap-static/",
        json=bootstrap_static_payload,
        status=200,
    )
    rsps.add(
        responses.GET,
        f"{FPL_BASE}/api/fixtures/",
        json=fixtures_payload,
        status=200,
    )
    rsps.add(
        responses.GET,
        f"{FPL_BASE}/api/entry/12345/event/30/picks/",
        json=picks_payload,
        status=200,
    )
    rsps.add(
        responses.GET,
        f"{UNDERSTAT_BASE}/league/EPL/2025",
        body=understat_html,
        status=200,
    )


class TestAnalysisStdoutLayout:
    @responses.activate
    def test_continuity_run_emits_required_blocks(
        self, capsys, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload, understat_html,
    ):
        _stub_all_endpoints(responses, bootstrap_static_payload, fixtures_payload,
                              picks_payload, understat_html)
        rc = main([
            "--team-id", "12345",
            "--gw", "31",
            "--horizon", "2",
            "--no-multi-week",
            "--cache-dir", str(tmp_path),
        ])
        assert rc == 0, capsys.readouterr().err
        out = capsys.readouterr().out
        # Required blocks per contracts/cli.md (analysis mode, --no-multi-week).
        for header in [
            "=== RUN STATUS ===",
            "=== RECOMMENDATION ===",
            "=== CHIP PLAN ===",
            "=== DIFFERENTIALS ===",
        ]:
            assert header in out, f"missing block header: {header}"
        # Multi-week plan suppressed by --no-multi-week.
        assert "=== MULTI-WEEK PLAN ===" not in out

    @responses.activate
    def test_run_emits_multi_week_plan_block_by_default(
        self, capsys, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload, understat_html,
    ):
        _stub_all_endpoints(responses, bootstrap_static_payload, fixtures_payload,
                              picks_payload, understat_html)
        rc = main([
            "--team-id", "12345",
            "--gw", "31",
            "--horizon", "2",
            "--cache-dir", str(tmp_path),
        ])
        assert rc == 0
        out = capsys.readouterr().out
        # MVP placeholder: block header is always emitted, even though the
        # plan body itself may say "Multi-week plan: not implemented yet"
        # until US2 lands. The test pins the contract for the layout.
        assert "=== MULTI-WEEK PLAN ===" in out


# ---------------------------------------------------------------------------
# Exit-code mapping
# ---------------------------------------------------------------------------


class TestExitCodes:
    @responses.activate
    def test_unknown_team_id_returns_five(
        self, capsys, tmp_path,
        bootstrap_static_payload, fixtures_payload,
    ):
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/",
                       json=bootstrap_static_payload, status=200)
        responses.add(responses.GET, f"{FPL_BASE}/api/fixtures/",
                       json=fixtures_payload, status=200)
        responses.add(responses.GET, f"{FPL_BASE}/api/entry/99999/event/30/picks/",
                       status=404)
        rc = main([
            "--team-id", "99999",
            "--gw", "31",
            "--no-multi-week",
            "--no-understat",
            "--cache-dir", str(tmp_path),
        ])
        assert rc == 5

    @responses.activate
    def test_fpl_api_error_returns_three(
        self, capsys, tmp_path,
    ):
        # All retries 503.
        for _ in range(4):
            responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/", status=503)
        rc = main([
            "--gw", "31",
            "--no-multi-week",
            "--no-understat",
            "--cache-dir", str(tmp_path),
        ])
        assert rc == 3
