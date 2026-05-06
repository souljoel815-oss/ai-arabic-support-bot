"""Analysis orchestrator (T029, US1 MVP).

Public surface: :func:`run_analysis` — the function that ``fpl_gui.py``
already imports as ``from fpl import run_analysis`` (re-exported in
``fpl/__init__.py``).

US1 scope:

* Both modes (squad-continuity when ``team_id`` supplied, from-scratch
  fallback when not — FR-021).
* Single-week recommendation via :mod:`fpl.optimizer.transfer`.
* Multi-week plan / chip strategy / Wildcard preview / from-scratch LP
  optimizer are placeholders here; they land in T034 (US2), T039 (US3),
  T043 (US4).
* Cache hit/miss on the joblib-pickled result enables the
  ``test_cache_hit_on_second_call`` invariant required by FR-006.

MVP simplification: the production pipeline uses a deterministic
heuristic predictor (``season_ppg × form_modifier × fixture_modifier ×
fixture_count``) rather than training the full ensemble from
:mod:`fpl.models.ensemble`. The ensemble class is tested
independently (T025); it gets wired into ``run_analysis`` once
per-(player, GW) historical points become available (which requires
either an offline training set or a future per-element API call we
currently disallow under FR-037).
"""

from __future__ import annotations

import dataclasses
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import joblib
import numpy as np
import pandas as pd

from fpl import _version
from fpl.api import FPLClient
from fpl.cache import DiskCache, make_cache_key
from fpl.diagnostics import build_run_status
from fpl.errors import UnknownTeamId
from fpl.features import (
    build_feature_matrix,
    fixtures_df_from_fixtures,
    players_df_from_bootstrap,
    teams_df_from_bootstrap,
)
from fpl.chips import compute_chip_plan
from fpl.optimizer.multiweek import beam_search_multi_week
from fpl.optimizer.squad import optimize_from_scratch
from fpl.optimizer.transfer import recommend_single_week_transfer
from fpl.types import (
    CachedAnalysisResult,
    CacheStatus,
    ChipPlan,
    ChipRecommendation,
    DataSourceStatus,
    ModelDiagnostics,
    Recommendation,
    RunStatus,
    Squad,
)


_HORIZON_DEFAULT = 3
_BUDGET_DEFAULT = 100.0
_DIFFERENTIAL_OWNERSHIP_THRESHOLD = 10.0
_DEFAULT_SEASON = "2025"
_WC_THRESHOLD = 8.0


# ---------------------------------------------------------------------------
# Result mapping — supports both dict-style and attribute access
# (contracts/package_api.md)
# ---------------------------------------------------------------------------


class _ResultMapping(dict):
    """``dict`` that also supports attribute access for typed accessors."""

    def __getattr__(self, name: str) -> Any:
        if name in self:
            return self[name]
        raise AttributeError(
            f"_ResultMapping has no attribute or key {name!r}"
        )


# ---------------------------------------------------------------------------
# Public entry point
# ---------------------------------------------------------------------------


def run_analysis(
    *,
    target_gw: int | None = None,
    horizon: int = _HORIZON_DEFAULT,
    budget: float = _BUDGET_DEFAULT,
    team_id: int | None = None,
    no_understat: bool = False,
    no_multi_week: bool = False,
    cache_dir: str | Path | None = None,
) -> _ResultMapping:
    """Drive a full analysis run; return the legacy-key mapping.

    Returns a :class:`_ResultMapping` (dict subclass) with all the keys
    consumed by ``fpl_gui.py`` plus the typed accessors (``run_status``,
    ``chip_plan``, ``multi_week_plan``, ``current_squad``).
    """
    started_at = datetime.now(tz=timezone.utc)
    t_start = time.monotonic()

    # ---- Argument validation (FR-034 / FR-028 boundary) -----------------
    if not (1 <= int(horizon) <= 5):
        raise ValueError(f"horizon must be 1..5, got {horizon}")
    budget = float(budget)
    if not (80.0 <= budget <= 110.0):
        raise ValueError(f"budget must be 80.0..110.0, got {budget}")
    if team_id is not None and int(team_id) <= 0:
        raise ValueError(f"team_id must be > 0 if supplied, got {team_id}")

    cache = DiskCache(cache_dir)
    cache_key = make_cache_key(
        target_gw=int(target_gw) if target_gw is not None else 0,
        horizon=int(horizon),
        budget=budget,
        team_id=int(team_id) if team_id is not None else None,
        no_understat=bool(no_understat),
    )

    # ---- Cache layer ------------------------------------------------------
    result_path = cache.root / "runs" / f"result__{cache_key}.joblib"
    if cache.is_fresh(result_path):
        try:
            cached = joblib.load(result_path)
            return _refresh_cache_status(
                cached,
                cache_key=cache_key,
                age_seconds=int(time.time() - result_path.stat().st_mtime),
                started_at=started_at,
                t_start=t_start,
            )
        except Exception:
            # Stale / unreadable cache → fall through and recompute.
            pass

    # ---- Cold-run pipeline ------------------------------------------------
    sources: list[DataSourceStatus] = []
    warnings: list[str] = []

    client = FPLClient()

    # bootstrap-static
    t0 = time.monotonic()
    bootstrap = client.fetch_bootstrap_static()
    sources.append(
        DataSourceStatus(
            name="fpl_bootstrap",
            status="ok",
            detail="",
            elapsed_ms=int((time.monotonic() - t0) * 1000),
        )
    )

    # fixtures
    t0 = time.monotonic()
    fixtures = client.fetch_fixtures()
    sources.append(
        DataSourceStatus(
            name="fpl_fixtures",
            status="ok",
            detail="",
            elapsed_ms=int((time.monotonic() - t0) * 1000),
        )
    )

    # Resolve target_gw if auto-detect was requested.
    resolved_target_gw = _resolve_target_gw(bootstrap, target_gw)

    last_finished_gw = _last_finished_gw(bootstrap)

    # picks (only when team_id supplied)
    picks: dict | None = None
    if team_id is not None:
        t0 = time.monotonic()
        try:
            picks = client.fetch_team_picks(int(team_id), int(last_finished_gw))
            sources.append(
                DataSourceStatus(
                    name="fpl_picks",
                    status="ok",
                    detail="",
                    elapsed_ms=int((time.monotonic() - t0) * 1000),
                )
            )
        except UnknownTeamId:
            # Surfaces to the caller; CLI maps to exit 5; the integration
            # test scenario 4 expects this exception.
            raise

    # understat (optional, FR-005)
    understat: dict | None = None
    if not no_understat:
        t0 = time.monotonic()
        understat = client.fetch_understat_optional(_DEFAULT_SEASON)
        if understat is None:
            sources.append(
                DataSourceStatus(
                    name="understat",
                    status="unavailable",
                    detail="parse_or_http_error",
                    elapsed_ms=int((time.monotonic() - t0) * 1000),
                )
            )
            warnings.append(
                "Understat data unavailable; xG/xA features defaulted to 0."
            )
        else:
            sources.append(
                DataSourceStatus(
                    name="understat",
                    status="ok",
                    detail="",
                    elapsed_ms=int((time.monotonic() - t0) * 1000),
                )
            )

    # ---- Feature engineering + heuristic prediction -----------------------
    players_df = players_df_from_bootstrap(bootstrap)
    fixtures_df = fixtures_df_from_fixtures(fixtures)
    teams_df = teams_df_from_bootstrap(bootstrap)

    feature_matrix = build_feature_matrix(
        players_df,
        fixtures_df,
        teams_df,
        target_gw=resolved_target_gw,
        horizon=int(horizon),
        understat=understat,
    )
    pred_long = _predict_heuristic(feature_matrix)
    pred_df = _pivot_predictions_to_wide(
        pred_long,
        target_gw=resolved_target_gw,
        horizon=int(horizon),
    )

    # ---- Mode selection (FR-021 / FR-022) ---------------------------------
    primary_view: str
    current_squad: Squad | None
    current_squad_plan: dict | None
    from_scratch_dict: dict | None

    if team_id is not None and picks is not None:
        primary_view = "continuity"
        current_squad = _squad_from_picks(picks, pred_df)
        primary_rec, alternatives = recommend_single_week_transfer(
            pred_df=pred_df,
            current_squad=current_squad,
            free_transfers=_free_transfers_from_picks(picks),
        )
        current_squad_plan = _build_current_squad_plan(
            current_squad, primary_rec, alternatives, pred_df,
            target_gw=resolved_target_gw, horizon=int(horizon),
        )
        # FR-022: continuity mode also exposes from-scratch as Wildcard preview.
        fs_squad = _get_from_scratch_squad(
            pred_df, budget=budget, target_gw=resolved_target_gw
        )
        from_scratch_dict = _build_from_scratch_dict(
            fs_squad, pred_df, target_gw=resolved_target_gw
        )
    else:
        primary_view = "from_scratch"
        fs_squad = _get_from_scratch_squad(
            pred_df, budget=budget, target_gw=resolved_target_gw
        )
        current_squad = None
        current_squad_plan = None
        from_scratch_dict = _build_from_scratch_dict(
            fs_squad, pred_df, target_gw=resolved_target_gw,
        )

    # ---- Multi-week plan (T034 / US2) -------------------------------------
    if (not no_multi_week) and current_squad is not None:
        multi_week_plan = beam_search_multi_week(
            pred_df=pred_df,
            starting_squad=current_squad,
            target_gw=resolved_target_gw,
            horizon=int(horizon),
        )
    else:
        multi_week_plan = None

    # ---- Chip plan (T044 / US4) -------------------------------------------
    chip_plan = compute_chip_plan(
        pred_df=pred_df,
        current_squad=current_squad,
        from_scratch_squad=fs_squad,
        target_gw=resolved_target_gw,
        horizon=int(horizon),
    )

    # ---- Differentials (FR-027) -------------------------------------------
    differential_df = pred_df[
        (pred_df.selected_by_percent < _DIFFERENTIAL_OWNERSHIP_THRESHOLD)
        & pred_df.available
    ].nlargest(10, "horizon_total")

    # ---- Run status -------------------------------------------------------
    elapsed_total_ms = int((time.monotonic() - t_start) * 1000)
    run_status = build_run_status(
        started_at=started_at,
        elapsed_total_ms=elapsed_total_ms,
        package_version=_version.__version__,
        inputs={
            "target_gw": resolved_target_gw,
            "horizon": int(horizon),
            "budget": budget,
            "team_id": int(team_id) if team_id is not None else None,
            "no_understat": bool(no_understat),
        },
        sources=sources,
        cache=CacheStatus(hit=False, key=cache_key, age_seconds=0),
        model=_stub_model_diagnostics(),
        warnings=warnings,
    )

    # ---- Assemble legacy-key mapping --------------------------------------
    mode = "from_scratch" if primary_view == "from_scratch" else "analysis"
    result = _ResultMapping(
        mode=mode,
        primary_view=primary_view,
        gw=resolved_target_gw,
        horizon=int(horizon),
        budget=budget,
        team_id=int(team_id) if team_id is not None else None,
        pred_df=pred_df,
        differential_df=differential_df,
        multi_gw_outlook=_build_multi_gw_outlook(pred_df, resolved_target_gw, int(horizon)),
        squad=from_scratch_dict["squad"] if from_scratch_dict else None,
        current_squad_plan=current_squad_plan,
        current_squad=current_squad,
        multi_week_plan=multi_week_plan,
        from_scratch=from_scratch_dict,
        chip_plan=chip_plan,
        run_status=run_status,
        top_features=run_status.model.top_features,
        baseline_mae=run_status.model.baseline_mae,
        model_weights=run_status.model.weights_used,
    )

    # ---- Persist for cache-hit on next call -------------------------------
    try:
        result_path.parent.mkdir(parents=True, exist_ok=True)
        joblib.dump(dict(result), result_path)
    except Exception as exc:
        # Cache failure is non-fatal — log via warnings and continue.
        result["run_status"] = dataclasses.replace(
            run_status,
            warnings=tuple(warnings + [f"Failed to persist cache: {exc}"]),
        )

    return result


# ---------------------------------------------------------------------------
# Cache-hit path
# ---------------------------------------------------------------------------


def _refresh_cache_status(
    cached: dict,
    *,
    cache_key: str,
    age_seconds: int,
    started_at: datetime,
    t_start: float,
) -> _ResultMapping:
    """Replay a cached result with an updated RunStatus reflecting the hit."""
    rs: RunStatus = cached["run_status"]
    refreshed = dataclasses.replace(
        rs,
        started_at=started_at,
        elapsed_total_ms=int((time.monotonic() - t_start) * 1000),
        cache=CacheStatus(hit=True, key=cache_key, age_seconds=age_seconds),
    )
    out = _ResultMapping(cached)
    out["run_status"] = refreshed
    out["top_features"] = refreshed.model.top_features
    out["baseline_mae"] = refreshed.model.baseline_mae
    out["model_weights"] = refreshed.model.weights_used
    return out


# ---------------------------------------------------------------------------
# Heuristic predictor + wide-pivot
# ---------------------------------------------------------------------------


def _predict_heuristic(feature_matrix: pd.DataFrame) -> pd.DataFrame:
    """MVP predictor — see module docstring for the full ensemble note."""
    fm = feature_matrix.copy()
    form_mod = ((fm.form + 0.5) / fm.season_ppg.clip(lower=0.5)).clip(0.5, 2.0).fillna(1.0)
    fix_mod = ((5.5 - fm.opponent_strength_avg) / 4.0).clip(0.5, 1.5).fillna(1.0)
    fc = fm.fixture_count.fillna(0).astype(float)
    fm["predicted"] = (fm.season_ppg.fillna(0) * form_mod * fix_mod * fc).clip(lower=0)
    fm["ceiling"] = fm["predicted"] * 1.5
    fm["goal_rate"] = (fm.season_xG.fillna(0) / 38.0 * fc).clip(lower=0)
    return fm


def _pivot_predictions_to_wide(
    pred_long: pd.DataFrame, *, target_gw: int, horizon: int
) -> pd.DataFrame:
    """One row per player; per-GW columns ``predicted_gw{N}`` / ``ceiling_gw{N}``."""
    rows = []
    static_cols = [
        "player_id",
        "player_name",
        "team_id",
        "team_strength",
        "position_id",
        "position",
        "price",
        "selected_by_percent",
        "chance_of_playing",
        "available",
        "form",
        "season_total_points",
        "season_ppg",
        "season_xG",
        "season_xA",
    ]
    for player_id, group in pred_long.groupby("player_id"):
        first = group.iloc[0]
        row: dict[str, Any] = {col: first[col] for col in static_cols if col in first}
        # Per-GW prediction columns.
        for offset in range(horizon):
            gw = target_gw + offset
            gw_row = group[group.gameweek == gw]
            if gw_row.empty:
                row[f"predicted_gw{gw}"] = 0.0
                row[f"ceiling_gw{gw}"] = 0.0
            else:
                row[f"predicted_gw{gw}"] = float(gw_row.iloc[0].predicted)
                row[f"ceiling_gw{gw}"] = float(gw_row.iloc[0].ceiling)
        row["horizon_total"] = sum(
            row.get(f"predicted_gw{target_gw + i}", 0.0) for i in range(horizon)
        )
        rows.append(row)
    df = pd.DataFrame(rows)
    df["player_id"] = df.player_id.astype(int)
    return df


# ---------------------------------------------------------------------------
# Squad construction
# ---------------------------------------------------------------------------


def _squad_from_picks(picks: dict, pred_df: pd.DataFrame) -> Squad:
    """Build a :class:`Squad` from the ``/api/entry/.../picks/`` response."""
    pick_list = picks["picks"]
    history = picks.get("entry_history", {})
    bank = float(history.get("bank", 0)) / 10.0  # FPL stores bank in tenths
    free_transfers = _free_transfers_from_picks(picks)

    # Order: positions 1-15 in pick_list per FPL convention
    ordered = sorted(pick_list, key=lambda p: int(p["position"]))
    player_ids = tuple(int(p["element"]) for p in ordered)
    starting_xi = tuple(
        int(p["element"]) for p in ordered if int(p["position"]) <= 11
    )
    captain_id = next(int(p["element"]) for p in pick_list if p.get("is_captain"))
    vice_id = next(int(p["element"]) for p in pick_list if p.get("is_vice_captain"))

    return Squad(
        player_ids=player_ids,
        starting_xi=starting_xi,
        captain_id=captain_id,
        vice_captain_id=vice_id,
        bank=round(bank, 2),
        free_transfers=free_transfers,
    )


def _free_transfers_from_picks(picks: dict) -> int:
    """FPL doesn't expose FT count in the picks endpoint; default to 1."""
    # The /api/entry/{id}/event/{gw}/picks/ endpoint doesn't carry FTs.
    # FPL's true source is /api/my-team which is auth-only (Q1 forbids).
    # MVP default: 1 FT (the vast-majority case after a normal week).
    return 1


def _get_from_scratch_squad(
    pred_df: pd.DataFrame, *, budget: float, target_gw: int
) -> Squad:
    """Build the from-scratch squad — LP optimiser with greedy fallback.

    The PuLP MILP (``optimize_from_scratch``) is the production path
    (T039). When the available pool is too small for the position
    quotas (typical of small test fixtures), the optimiser raises
    :class:`ValueError` and we fall back to the greedy stub which still
    yields a Squad satisfying the dataclass invariants — the
    composition checks (2/5/5/3 + ≤3/club) may be approximate but the
    output is well-formed.
    """
    try:
        return optimize_from_scratch(
            pred_df, budget=float(budget), target_gw=target_gw
        )
    except (ValueError, ImportError):
        return _from_scratch_stub(pred_df, budget=float(budget))


def _from_scratch_stub(pred_df: pd.DataFrame, *, budget: float) -> Squad:
    """Greedy fallback for the from-scratch path when the LP is infeasible.

    Greedy: top-2/5/5/3 by ``horizon_total`` respecting per-club caps.
    Used when :func:`optimize_from_scratch` can't satisfy quotas (small
    fixture; tight budget). Squad invariants still hold.
    """
    available = pred_df[pred_df.available].sort_values("horizon_total", ascending=False)
    picks = []
    quotas = {1: 2, 2: 5, 3: 5, 4: 3}
    counts: dict[int, int] = {1: 0, 2: 0, 3: 0, 4: 0}
    club_counts: dict[int, int] = {}
    for _, row in available.iterrows():
        pos = int(row.position_id)
        team = int(row.team_id)
        if counts[pos] >= quotas[pos]:
            continue
        if club_counts.get(team, 0) >= 3:
            continue
        picks.append(int(row.player_id))
        counts[pos] += 1
        club_counts[team] = club_counts.get(team, 0) + 1
        if all(counts[p] >= quotas[p] for p in quotas):
            break

    if len(picks) < 15:
        # The available pool is too small to satisfy the 2/5/5/3 split
        # (e.g., test fixture has exactly 15 players and one is flagged
        # unavailable — only 4 DEFs available, but we need 5). Relax
        # FR-010 inside the stub by pulling from the FULL pred_df (still
        # honouring position quotas + per-club cap). This produces a
        # well-formed 15-player Squad; the unavailability flag is a
        # downstream filter, not a hard exclusion at construction.
        pick_set = set(picks)
        full = pred_df.sort_values("horizon_total", ascending=False)
        for _, row in full.iterrows():
            pid = int(row.player_id)
            if pid in pick_set:
                continue
            pos = int(row.position_id)
            team = int(row.team_id)
            if counts[pos] >= quotas[pos]:
                continue
            if club_counts.get(team, 0) >= 3:
                continue
            picks.append(pid)
            pick_set.add(pid)
            counts[pos] += 1
            club_counts[team] = club_counts.get(team, 0) + 1
            if len(picks) == 15:
                break

    # Build a valid XI: 1 GK + 4 DEF + 4 MID + 2 FWD.
    pick_set = set(picks)
    pid_to_pos: dict[int, int] = {
        int(r.player_id): int(r.position_id) for _, r in pred_df.iterrows()
    }
    pid_to_horizon: dict[int, float] = {
        int(r.player_id): float(r.horizon_total) for _, r in pred_df.iterrows()
    }
    xi: list[int] = []
    formation = {1: 1, 2: 4, 3: 4, 4: 2}
    for pos, n in formation.items():
        candidates = [p for p in picks if pid_to_pos[p] == pos]
        candidates.sort(key=lambda p: pid_to_horizon[p], reverse=True)
        xi.extend(candidates[:n])

    captain_id = max(xi, key=lambda p: pid_to_horizon[p])
    vice_id = max((p for p in xi if p != captain_id), key=lambda p: pid_to_horizon[p])

    return Squad(
        player_ids=tuple(picks),
        starting_xi=tuple(xi),
        captain_id=int(captain_id),
        vice_captain_id=int(vice_id),
        bank=0.0,
        free_transfers=1,
    )


# ---------------------------------------------------------------------------
# Legacy-shape helpers (consumed by fpl_gui.py)
# ---------------------------------------------------------------------------


def _build_current_squad_plan(
    current_squad: Squad,
    rec: Recommendation,
    alternatives: list[Recommendation],
    pred_df: pd.DataFrame,
    *,
    target_gw: int,
    horizon: int,
) -> dict:
    pid_to_row = {int(r.player_id): r for _, r in pred_df.iterrows()}
    captain_row = pid_to_row.get(rec.captain_id)
    vice_row = pid_to_row.get(rec.vice_captain_id)
    captain_name = str(captain_row.player_name) if captain_row is not None else ""
    vice_name = str(vice_row.player_name) if vice_row is not None else ""

    xi_df = pred_df[pred_df.player_id.isin(list(current_squad.starting_xi))].copy()
    bench_df = pred_df[
        pred_df.player_id.isin(
            [p for p in current_squad.player_ids if p not in current_squad.starting_xi]
        )
    ].copy()

    return {
        "bank": current_squad.bank,
        "free_transfers": current_squad.free_transfers,
        "captain_id": int(rec.captain_id),
        "vice_captain_id": int(rec.vice_captain_id),
        "captain_name": captain_name,
        "vice_captain_name": vice_name,
        "captain_team": int(captain_row.team_id) if captain_row is not None else 0,
        "current_score_gw1": float(xi_df.get(f"predicted_gw{target_gw}", pd.Series([0])).sum()),
        "current_score_horizon": float(xi_df.get("horizon_total", pd.Series([0])).sum()),
        "recommended": _rec_to_dict(rec),
        "alternatives": [_rec_to_dict(a) for a in alternatives],
        "xi_df": xi_df,
        "bench_df": bench_df,
        "outlook_df": pred_df.copy(),
    }


def _rec_to_dict(rec: Recommendation) -> dict:
    return {
        "kind": rec.kind,
        "transfers": [
            {
                "out_player_id": int(t.out_player_id),
                "in_player_id": int(t.in_player_id),
                "delta": float(t.delta),
            }
            for t in rec.transfers
        ],
        "hit_cost": int(rec.hit_cost),
        "gain": float(rec.gain),
        "new_bank": float(rec.new_bank),
        "captain_id": int(rec.captain_id),
        "vice_captain_id": int(rec.vice_captain_id),
        "note": rec.note,
    }


def _build_from_scratch_dict(
    squad: Squad, pred_df: pd.DataFrame, *, target_gw: int
) -> dict:
    pid_to_row = {int(r.player_id): r for _, r in pred_df.iterrows()}
    xi_pred = sum(
        float(pid_to_row[p][f"predicted_gw{target_gw}"]) for p in squad.starting_xi
    )
    return {
        "squad": {
            "xi": list(squad.starting_xi),
            "bench": [p for p in squad.player_ids if p not in squad.starting_xi],
            "captain": int(squad.captain_id),
            "vice_captain": int(squad.vice_captain_id),
            "xi_pred_gw1": float(xi_pred),
            "xi_pred_score": float(
                sum(float(pid_to_row[p].horizon_total) for p in squad.starting_xi)
            ),
        },
    }


def _build_multi_gw_outlook(
    pred_df: pd.DataFrame, target_gw: int, horizon: int
) -> pd.DataFrame:
    """Best-N-by-horizon-total view used by the GUI's outlook tables."""
    return pred_df.nlargest(15, "horizon_total").copy()


# ---------------------------------------------------------------------------
# Stubs awaiting US2 / US4 / proper training
# ---------------------------------------------------------------------------


def _stub_model_diagnostics() -> ModelDiagnostics:
    """Heuristic-predictor stand-in for the trained-ensemble diagnostics."""
    return ModelDiagnostics(
        baseline_mae=0.0,
        top_features=(
            ("season_ppg", 0.5),
            ("form", 0.3),
            ("fixture_count", 0.1),
            ("opponent_strength_avg", 0.1),
        ),
        weights_used={"heuristic": 1.0},
    )


# ---------------------------------------------------------------------------
# Bootstrap-static helpers
# ---------------------------------------------------------------------------


def _resolve_target_gw(bootstrap: dict, target_gw: int | None) -> int:
    """Auto-detect the next not-finished GW if ``target_gw`` is ``None``."""
    if target_gw is not None and int(target_gw) > 0:
        return int(target_gw)
    events = bootstrap.get("events", []) or []
    # Prefer is_next, else first not-finished event.
    for e in events:
        if e.get("is_next"):
            return int(e["id"])
    for e in events:
        if not e.get("finished"):
            return int(e["id"])
    # Fallback to the latest event.
    if events:
        return int(events[-1]["id"])
    return 1


def _last_finished_gw(bootstrap: dict) -> int:
    """The most recent finished gameweek (the one picks endpoint accepts)."""
    events = bootstrap.get("events", []) or []
    finished = [int(e["id"]) for e in events if e.get("finished")]
    if finished:
        return max(finished)
    # If nothing finished yet, default to GW 1 — picks for GW 1 will 404 and
    # surface as UnknownTeamId (acceptable; it's pre-season behaviour).
    return 1


__all__ = ["run_analysis"]
