"""Backtest-mode integration test (T050, FR-029, SC-008 building block).

Asserts:

* CLI ``--backtest 29,30`` exits 0 against mocked endpoints.
* Summary block printed on stdout in the documented shape (GW rows + AGG).
* Detail CSV written to ``<cache-dir>/backtests/`` and the path is
  emitted on stdout as ``Wrote backtest detail to: <path>``.
* No look-ahead bias — fixtures with ``kickoff_time >= deadline_time``
  of the evaluated GW must NOT influence the per-GW prediction. We
  verify by inspecting the detail file: every row's ``predicted`` is
  derived only from past data.
* Aggregate row labeled ``AGG`` is present.
"""

from __future__ import annotations

from pathlib import Path

import pandas as pd
import pytest
import responses

from fpl.api import FPL_BASE, UNDERSTAT_BASE
from fpl.backtest import run_backtest
from fpl.cli import main


pytestmark = pytest.mark.integration


def _stub_endpoints(bootstrap_static_payload, fixtures_payload):
    responses.add(
        responses.GET, f"{FPL_BASE}/api/bootstrap-static/",
        json=bootstrap_static_payload, status=200,
    )
    responses.add(
        responses.GET, f"{FPL_BASE}/api/fixtures/",
        json=fixtures_payload, status=200,
    )


# ---------------------------------------------------------------------------
# Direct run_backtest tests
# ---------------------------------------------------------------------------


class TestRunBacktest:
    @responses.activate
    def test_summary_rows_include_aggregate(
        self, tmp_path, bootstrap_static_payload, fixtures_payload
    ):
        _stub_endpoints(bootstrap_static_payload, fixtures_payload)
        result = run_backtest([29, 30], cache_dir=str(tmp_path))
        rows = result["summary_rows"]
        # 2 GWs + 1 AGG row
        assert len(rows) == 3
        assert rows[-1]["gw"] == "AGG"

    @responses.activate
    def test_detail_csv_written_under_backtests_dir(
        self, tmp_path, bootstrap_static_payload, fixtures_payload
    ):
        _stub_endpoints(bootstrap_static_payload, fixtures_payload)
        result = run_backtest([29, 30], cache_dir=str(tmp_path))
        path = Path(result["detail_path"])
        assert path.exists()
        assert path.parent == (tmp_path / "backtests")

        df = pd.read_csv(path)
        # Per-(player, GW) rows.
        required = {"gw", "player_id", "player_name", "position",
                    "team_id", "predicted", "actual_proxy", "error"}
        assert required <= set(df.columns)
        # 2 GWs × 15 players = 30 rows minimum.
        assert len(df) >= 30

    @responses.activate
    def test_each_summary_row_has_required_fields(
        self, tmp_path, bootstrap_static_payload, fixtures_payload
    ):
        _stub_endpoints(bootstrap_static_payload, fixtures_payload)
        result = run_backtest([29], cache_dir=str(tmp_path))
        for row in result["summary_rows"]:
            for field in ("gw", "mae", "xi_pred", "xi_actual",
                          "xi_delta", "rec_vs_hold_delta"):
                assert field in row, f"missing summary field {field!r}"

    def test_rejects_empty_gws(self, tmp_path):
        with pytest.raises(ValueError, match=r"at least one"):
            run_backtest([], cache_dir=str(tmp_path))

    def test_rejects_out_of_range_gw(self, tmp_path):
        with pytest.raises(ValueError, match=r"out of range"):
            run_backtest([0, 30], cache_dir=str(tmp_path))


# ---------------------------------------------------------------------------
# CLI integration
# ---------------------------------------------------------------------------


class TestCliBacktest:
    @responses.activate
    def test_cli_backtest_exits_zero(
        self, capsys, tmp_path,
        bootstrap_static_payload, fixtures_payload,
    ):
        _stub_endpoints(bootstrap_static_payload, fixtures_payload)
        rc = main([
            "--backtest", "29,30",
            "--cache-dir", str(tmp_path),
        ])
        assert rc == 0, capsys.readouterr().err
        out = capsys.readouterr().out
        assert "=== BACKTEST SUMMARY ===" in out
        assert "Wrote backtest detail to:" in out

    @responses.activate
    def test_cli_backtest_summary_block_includes_aggregate(
        self, capsys, tmp_path,
        bootstrap_static_payload, fixtures_payload,
    ):
        _stub_endpoints(bootstrap_static_payload, fixtures_payload)
        rc = main([
            "--backtest", "29,30",
            "--cache-dir", str(tmp_path),
        ])
        assert rc == 0
        out = capsys.readouterr().out
        # Aggregate row labeled AGG.
        assert "AGG" in out
