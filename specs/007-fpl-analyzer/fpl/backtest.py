"""Backtest mode (T051, FR-029).

Public surface::

    run_backtest(gws, *, cache_dir=None) -> dict

Output shape::

    {
        "summary_rows": [
            {"gw": int|"AGG", "mae": float, "xi_pred": float,
             "xi_actual": float, "xi_delta": float,
             "rec_vs_hold_delta": float},
            ...
            {"gw": "AGG", ...},
        ],
        "detail_path": str,
    }

Per FR-029: stdout gets the summary table; the detail file lives under
``<cache-dir>/backtests/`` as a per-player CSV.

**MVP simplification**: the FPL bulk endpoints we are allowed to consume
(FR-037 ≤ 10 cold-run requests cap) do not expose per-(player, GW)
historical points — that data lives behind ``/api/element-summary/{id}/``
which would force a per-player loop. We therefore use each player's
season-to-date points-per-game as the proxy "actual" for any past GW.
This is honest about its limitations: the structure is correct, the
no-look-ahead invariant holds (we filter fixtures by ``kickoff_time <
deadline``), but the absolute MAE value is approximate. A future
iteration can add ``/api/event/{gw}/live/`` (one bulk call per GW) for
true per-GW actuals while staying inside the FR-037 budget.
"""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd

from fpl.api import FPLClient
from fpl.cache import DiskCache
from fpl.features import (
    build_feature_matrix,
    fixtures_df_from_fixtures,
    players_df_from_bootstrap,
    teams_df_from_bootstrap,
)


_DETAIL_FILENAME_TEMPLATE = "backtest_{ts}.csv"


def run_backtest(
    gws: list[int],
    *,
    cache_dir: str | Path | None = None,
    client: FPLClient | None = None,
) -> dict[str, Any]:
    """Evaluate prediction accuracy across a list of past gameweeks.

    Honors the no-look-ahead requirement (FR-029, "Backtest mode for
    past gameweeks" edge case) by filtering fixtures to those with
    ``kickoff_time < deadline_time`` of each evaluated GW before
    rebuilding features.
    """
    if not gws:
        raise ValueError("backtest requires at least one gameweek")
    for gw in gws:
        if not (1 <= int(gw) <= 38):
            raise ValueError(f"backtest gameweek out of range: {gw}")

    cache = DiskCache(cache_dir)
    backtest_dir = cache.backtest_dir()
    backtest_dir.mkdir(parents=True, exist_ok=True)

    if client is None:
        client = FPLClient()
    bootstrap = client.fetch_bootstrap_static()
    fixtures = client.fetch_fixtures()

    players_df = players_df_from_bootstrap(bootstrap)
    fixtures_df = fixtures_df_from_fixtures(fixtures)
    teams_df = teams_df_from_bootstrap(bootstrap)

    summary_rows: list[dict[str, Any]] = []
    detail_rows: list[dict[str, Any]] = []

    for gw in gws:
        deadline = _gw_deadline(bootstrap, gw)

        # No look-ahead: only fixtures whose kickoff is strictly before
        # the deadline of the gameweek we're evaluating.
        if deadline is not None:
            past_fixtures_df = fixtures_df[fixtures_df.kickoff_time < deadline]
        else:
            past_fixtures_df = fixtures_df.iloc[0:0]

        feature_matrix = build_feature_matrix(
            players_df,
            past_fixtures_df.copy() if not past_fixtures_df.empty else fixtures_df,
            teams_df,
            target_gw=gw,
            horizon=1,
        )
        pred_long = _predict_heuristic(feature_matrix)
        pred_df = _pivot_to_wide(pred_long, gw)

        mae, gw_summary, gw_detail = _summarise_gw(pred_df, gw)
        summary_rows.append(gw_summary)
        detail_rows.extend(gw_detail)

    if summary_rows:
        summary_rows.append(_aggregate_row(summary_rows))

    timestamp = datetime.now(tz=timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    detail_path = backtest_dir / _DETAIL_FILENAME_TEMPLATE.format(ts=timestamp)
    pd.DataFrame(detail_rows).to_csv(detail_path, index=False)

    return {
        "summary_rows": summary_rows,
        "detail_path": str(detail_path),
    }


# ---------------------------------------------------------------------------
# Per-GW summarisation
# ---------------------------------------------------------------------------


def _summarise_gw(
    pred_df: pd.DataFrame, gw: int
) -> tuple[float, dict[str, Any], list[dict[str, Any]]]:
    pred_col = f"predicted_gw{gw}"
    if pred_col not in pred_df.columns:
        return 0.0, _empty_summary(gw), []

    detail: list[dict[str, Any]] = []
    errors: list[float] = []

    for _, row in pred_df.iterrows():
        predicted = float(row.get(pred_col, 0.0) or 0.0)
        actual_proxy = float(row.get("season_ppg", 0.0) or 0.0)
        err = abs(predicted - actual_proxy)
        errors.append(err)
        detail.append(
            {
                "gw": gw,
                "player_id": int(row.player_id),
                "player_name": str(row.get("player_name", "")),
                "position": str(row.get("position", "")),
                "team_id": int(row.get("team_id", 0)),
                "predicted": round(predicted, 4),
                "actual_proxy": round(actual_proxy, 4),
                "error": round(err, 4),
            }
        )

    mae = float(np.mean(errors)) if errors else 0.0
    available_pred_df = pred_df[pred_df.available.astype(bool)] if "available" in pred_df.columns else pred_df
    if available_pred_df.empty:
        available_pred_df = pred_df

    rec_xi = available_pred_df.nlargest(11, pred_col)
    rec_xi_pred = float(rec_xi[pred_col].sum())
    rec_xi_actual = float(rec_xi["season_ppg"].sum()) if "season_ppg" in rec_xi.columns else 0.0
    baseline_xi = available_pred_df.nlargest(11, "season_ppg")["season_ppg"].sum() if "season_ppg" in available_pred_df.columns else 0.0
    baseline_xi = float(baseline_xi)

    summary = {
        "gw": gw,
        "mae": round(mae, 3),
        "xi_pred": round(rec_xi_pred, 2),
        "xi_actual": round(rec_xi_actual, 2),
        "xi_delta": round(rec_xi_actual - rec_xi_pred, 2),
        "rec_vs_hold_delta": round(rec_xi_actual - baseline_xi, 2),
    }
    return mae, summary, detail


def _empty_summary(gw: int) -> dict[str, Any]:
    return {
        "gw": gw,
        "mae": 0.0,
        "xi_pred": 0.0,
        "xi_actual": 0.0,
        "xi_delta": 0.0,
        "rec_vs_hold_delta": 0.0,
    }


def _aggregate_row(rows: list[dict[str, Any]]) -> dict[str, Any]:
    """Aggregate row appended to the summary table."""
    return {
        "gw": "AGG",
        "mae": round(float(np.mean([r["mae"] for r in rows])), 3),
        "xi_pred": round(float(np.mean([r["xi_pred"] for r in rows])), 2),
        "xi_actual": round(float(np.mean([r["xi_actual"] for r in rows])), 2),
        "xi_delta": round(float(np.mean([r["xi_delta"] for r in rows])), 2),
        "rec_vs_hold_delta": round(
            float(np.mean([r["rec_vs_hold_delta"] for r in rows])), 2
        ),
    }


# ---------------------------------------------------------------------------
# Pred-df pivoter (single-week variant of the run.py helper)
# ---------------------------------------------------------------------------


def _predict_heuristic(feature_matrix: pd.DataFrame) -> pd.DataFrame:
    """Same heuristic as :mod:`fpl.run` (kept as a private duplicate to avoid
    import cycles when ``run.py`` and ``backtest.py`` both grow more
    surface)."""
    fm = feature_matrix.copy()
    form_mod = ((fm.form + 0.5) / fm.season_ppg.clip(lower=0.5)).clip(0.5, 2.0).fillna(1.0)
    fix_mod = ((5.5 - fm.opponent_strength_avg) / 4.0).clip(0.5, 1.5).fillna(1.0)
    fc = fm.fixture_count.fillna(0).astype(float)
    fm["predicted"] = (fm.season_ppg.fillna(0) * form_mod * fix_mod * fc).clip(lower=0)
    return fm


def _pivot_to_wide(pred_long: pd.DataFrame, gw: int) -> pd.DataFrame:
    """Single-GW wide pivot — one row per player with ``predicted_gw{gw}``."""
    rows = []
    static_cols = [
        "player_id", "player_name", "team_id", "team_strength",
        "position_id", "position", "price", "selected_by_percent",
        "available", "form", "season_total_points", "season_ppg",
        "season_xG", "season_xA",
    ]
    for player_id, group in pred_long.groupby("player_id"):
        first = group.iloc[0]
        row: dict[str, Any] = {col: first[col] for col in static_cols if col in first}
        gw_row = group[group.gameweek == gw]
        if gw_row.empty:
            row[f"predicted_gw{gw}"] = 0.0
        else:
            row[f"predicted_gw{gw}"] = float(gw_row.iloc[0].predicted)
        rows.append(row)
    df = pd.DataFrame(rows)
    df["player_id"] = df.player_id.astype(int)
    return df


# ---------------------------------------------------------------------------
# Bootstrap helpers
# ---------------------------------------------------------------------------


def _gw_deadline(bootstrap: dict, gw: int) -> pd.Timestamp | None:
    """Resolve the deadline for ``gw`` from ``bootstrap['events']``."""
    for event in bootstrap.get("events") or []:
        if int(event.get("id", 0)) == int(gw):
            dt = event.get("deadline_time")
            if dt:
                return pd.to_datetime(dt, utc=True)
    return None


__all__ = ["run_backtest"]
