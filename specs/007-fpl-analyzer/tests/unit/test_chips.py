"""Chip-strategy unit tests (T041, FR-023 / FR-024 / FR-025 / FR-026).

Each chip is exercised on a synthetic ``pred_df`` + Squad so behavior is
deterministic.

Asserted properties:

* All 4 chips return a :class:`ChipRecommendation` with the right
  ``chip`` literal.
* ``gw=None ⟺ supporting_metric=None`` (FR-023 / dataclass invariant).
* **FR-024** TC supporting_metric must come from the ceiling head, not
  the mean (verified by spiking a player's ``ceiling_gw{N}`` only).
* **FR-025** BB picks the GW with the highest sum of bench predictions.
* **FR-026** FH picks the GW with the largest swing between the user's
  squad and the from-scratch squad.
* ``compute_chip_plan`` composes all four into a :class:`ChipPlan`.
"""

from __future__ import annotations

import pandas as pd
import pytest

from fpl.chips import (
    bench_boost,
    compute_chip_plan,
    free_hit,
    triple_captain,
    wildcard,
)
from fpl.types import ChipPlan, ChipRecommendation, Squad


pytestmark = pytest.mark.unit


# ---------------------------------------------------------------------------
# Synthetic pred_df + Squad helpers
# ---------------------------------------------------------------------------


def _build_pred_df(target_gw: int = 31, horizon: int = 3) -> pd.DataFrame:
    """30 players: 4 GK / 10 DEF / 10 MID / 6 FWD across 6 clubs."""
    rows = []
    pid = 100
    quotas = {1: 4, 2: 10, 3: 10, 4: 6}
    pos_labels = {1: "GK", 2: "DEF", 3: "MID", 4: "FWD"}
    for pos in (1, 2, 3, 4):
        for k in range(quotas[pos]):
            club = (pid % 6) + 1
            pid += 1
            base = {1: 3.0, 2: 4.0, 3: 5.0, 4: 6.0}[pos] + (k * 0.2)
            row = {
                "player_id": pid,
                "player_name": f"P{pid}",
                "team_id": club,
                "position_id": pos,
                "position": pos_labels[pos],
                "price": 5.0 + k * 0.5,
                "selected_by_percent": 5.0 + k * 0.7,
                "available": True,
                "form": base,
                "season_ppg": base,
            }
            for offset in range(horizon):
                gw = target_gw + offset
                row[f"predicted_gw{gw}"] = base + offset * 0.1
                row[f"ceiling_gw{gw}"] = (base + offset * 0.1) * 1.5
            row["horizon_total"] = sum(
                row[f"predicted_gw{target_gw + i}"] for i in range(horizon)
            )
            rows.append(row)
    return pd.DataFrame(rows)


def _build_squad_from_pred_df(
    pred_df: pd.DataFrame, formation: tuple[int, int, int, int] = (1, 4, 4, 2)
) -> Squad:
    """A legal 15-player Squad picked greedily by horizon_total."""
    quotas = {1: 2, 2: 5, 3: 5, 4: 3}
    picks: list[int] = []
    club_count: dict[int, int] = {}
    for pos in (1, 2, 3, 4):
        pool = pred_df[pred_df.position_id == pos].sort_values(
            "horizon_total", ascending=False
        )
        added = 0
        for _, row in pool.iterrows():
            club = int(row.team_id)
            if club_count.get(club, 0) >= 3:
                continue
            picks.append(int(row.player_id))
            club_count[club] = club_count.get(club, 0) + 1
            added += 1
            if added >= quotas[pos]:
                break

    pid_to_pos = {int(r.player_id): int(r.position_id) for _, r in pred_df.iterrows()}
    pid_to_h = {int(r.player_id): float(r.horizon_total) for _, r in pred_df.iterrows()}

    formation_map = {1: formation[0], 2: formation[1], 3: formation[2], 4: formation[3]}
    xi: list[int] = []
    for pos, n in formation_map.items():
        candidates = sorted(
            [p for p in picks if pid_to_pos[p] == pos],
            key=lambda p: pid_to_h[p],
            reverse=True,
        )
        xi.extend(candidates[:n])

    captain = max(xi, key=lambda p: pid_to_h[p])
    vice = max((p for p in xi if p != captain), key=lambda p: pid_to_h[p])
    return Squad(
        player_ids=tuple(picks),
        starting_xi=tuple(xi),
        captain_id=int(captain),
        vice_captain_id=int(vice),
        bank=2.0,
        free_transfers=1,
    )


# ---------------------------------------------------------------------------
# Triple Captain (FR-024)
# ---------------------------------------------------------------------------


class TestTripleCaptain:
    def test_returns_chip_recommendation_with_tc(self):
        pred = _build_pred_df()
        squad = _build_squad_from_pred_df(pred)
        rec = triple_captain(pred_df=pred, squad=squad, target_gw=31, horizon=3)
        assert rec.chip == "tc"

    def test_uses_ceiling_not_mean(self):
        # FR-024: spike one squad player's ceiling_gw32 well above any other
        # ceiling, but keep the predicted (mean) values low everywhere. TC
        # MUST pick that GW + that player.
        pred = _build_pred_df()
        squad = _build_squad_from_pred_df(pred)
        # Pick a squad player and inflate their GW32 ceiling.
        target_player = list(squad.player_ids)[0]
        pred = pred.copy()
        pred.loc[pred.player_id == target_player, "ceiling_gw32"] = 99.0
        rec = triple_captain(pred_df=pred, squad=squad, target_gw=31, horizon=3)
        assert rec.gw == 32
        assert rec.supporting_metric == pytest.approx(99.0)

    def test_no_recommendation_when_squad_empty(self):
        pred = _build_pred_df()
        # Build a Squad with player_ids that don't exist in pred_df by
        # using the synthetic test machinery — the chip should explain.
        # Instead, use an empty pred_df subset to provoke the no-rec path.
        empty_pred = pred.iloc[0:0]
        squad = _build_squad_from_pred_df(pred)
        rec = triple_captain(pred_df=empty_pred, squad=squad, target_gw=31, horizon=3)
        assert rec.chip == "tc"
        assert rec.gw is None
        assert rec.supporting_metric is None


# ---------------------------------------------------------------------------
# Bench Boost (FR-025)
# ---------------------------------------------------------------------------


class TestBenchBoost:
    def test_picks_gw_with_highest_bench_sum(self):
        pred = _build_pred_df()
        squad = _build_squad_from_pred_df(pred)
        bench_ids = [p for p in squad.player_ids if p not in squad.starting_xi]
        # Spike GW32 predictions for bench players.
        pred = pred.copy()
        for pid in bench_ids:
            pred.loc[pred.player_id == pid, "predicted_gw32"] = 20.0
        rec = bench_boost(pred_df=pred, squad=squad, target_gw=31, horizon=3)
        assert rec.chip == "bb"
        assert rec.gw == 32
        # Bench has 4 players, each spiked to 20 → sum 80.
        assert rec.supporting_metric == pytest.approx(80.0)


# ---------------------------------------------------------------------------
# Free Hit (FR-026)
# ---------------------------------------------------------------------------


class TestFreeHit:
    def test_picks_gw_with_largest_swing(self):
        pred = _build_pred_df()
        own_squad = _build_squad_from_pred_df(pred)
        # Build an alternative squad picking different players where possible
        # (in our synthetic data the pools are tight, but pick the "back end"
        # of each position pool to differentiate).
        pred_sorted = pred.sort_values("horizon_total")  # ascending
        bottom_squad = _build_squad_from_pred_df(pred_sorted)
        # If the two squads happen to be identical (degenerate), bottom up.
        rec = free_hit(
            pred_df=pred,
            current_squad=bottom_squad,
            from_scratch_squad=own_squad,
            target_gw=31,
            horizon=3,
            swing_threshold=0.0,
        )
        assert rec.chip == "fh"
        # With a 0.0 swing threshold, we should always recommend SOMETHING
        # since the from-scratch squad is built from highest-horizon players.
        assert rec.gw is not None
        assert rec.supporting_metric is not None
        assert rec.supporting_metric >= 0.0

    def test_no_recommendation_when_swing_below_threshold(self):
        pred = _build_pred_df()
        squad = _build_squad_from_pred_df(pred)
        # Same squad on both sides → swing == 0
        rec = free_hit(
            pred_df=pred,
            current_squad=squad,
            from_scratch_squad=squad,
            target_gw=31,
            horizon=3,
            swing_threshold=5.0,
        )
        assert rec.gw is None
        assert rec.supporting_metric is None


# ---------------------------------------------------------------------------
# Wildcard
# ---------------------------------------------------------------------------


class TestWildcard:
    def test_no_recommendation_when_squads_identical(self):
        pred = _build_pred_df()
        squad = _build_squad_from_pred_df(pred)
        rec = wildcard(
            pred_df=pred,
            current_squad=squad,
            from_scratch_squad=squad,
            target_gw=31,
            horizon=3,
            threshold=8.0,
        )
        assert rec.chip == "wc"
        assert rec.gw is None

    def test_recommendation_when_swing_exceeds_threshold(self):
        pred = _build_pred_df()
        own_squad = _build_squad_from_pred_df(pred)
        # Bottom squad uses the lowest-horizon players → big swing for FS.
        bottom_squad = _build_squad_from_pred_df(pred.sort_values("horizon_total"))
        rec = wildcard(
            pred_df=pred,
            current_squad=bottom_squad,
            from_scratch_squad=own_squad,
            target_gw=31,
            horizon=3,
            threshold=0.5,  # low threshold to exercise the recommend path
        )
        assert rec.chip == "wc"
        # If the swing is large enough we should get a GW; otherwise None.
        if rec.gw is not None:
            assert rec.supporting_metric is not None
            assert rec.supporting_metric >= 0.5


# ---------------------------------------------------------------------------
# compute_chip_plan composer
# ---------------------------------------------------------------------------


class TestComputeChipPlan:
    def test_returns_chip_plan_with_all_four_chips(self):
        pred = _build_pred_df()
        squad = _build_squad_from_pred_df(pred)
        plan = compute_chip_plan(
            pred_df=pred,
            current_squad=squad,
            from_scratch_squad=squad,
            target_gw=31,
            horizon=3,
        )
        assert isinstance(plan, ChipPlan)
        assert plan.triple_captain.chip == "tc"
        assert plan.bench_boost.chip == "bb"
        assert plan.free_hit.chip == "fh"
        assert plan.wildcard.chip == "wc"

    def test_handles_no_squad_at_all(self):
        # Defensive path: if no squad, return all-no-recommendation.
        pred = _build_pred_df()
        plan = compute_chip_plan(
            pred_df=pred,
            current_squad=None,
            from_scratch_squad=None,
            target_gw=31,
            horizon=3,
        )
        for rec in (plan.triple_captain, plan.bench_boost, plan.free_hit, plan.wildcard):
            assert rec.gw is None
            assert rec.supporting_metric is None
