"""Multi-week beam-search invariant tests (T032, FR-016 / FR-017 / FR-018, SC-007).

Asserts:

* Every step's ``fts_after ≤ 5`` (FR-016 cap).
* Every step's ``bank_after ≥ 0``.
* ``hit_cost`` on every step is a non-negative multiple of 4 (FR-012).
* ``len(steps) == horizon`` and step gameweeks are strictly sequential.
* ``len(alternatives) ≤ 3`` (FR-018) and alternatives don't nest.
* ``net_gain_vs_baseline = total_score − cumulative_hits × 4 − baseline_score``
  (rounded; tolerance built in).
"""

from __future__ import annotations

import numpy as np
import pandas as pd
import pytest

from fpl.optimizer.multiweek import beam_search_multi_week
from fpl.types import Squad


pytestmark = pytest.mark.unit


# ---------------------------------------------------------------------------
# Hand-constructed pred_df + Squad (avoids the full run_analysis stack)
# ---------------------------------------------------------------------------


def _build_test_pred_df(target_gw: int, horizon: int) -> pd.DataFrame:
    """30 players (2 GK / 10 DEF / 10 MID / 8 FWD), 5 clubs, predictable scores."""
    rng = np.random.default_rng(seed=42)

    rows = []
    pid = 100
    quotas = {1: 2, 2: 10, 3: 10, 4: 8}
    pos_labels = {1: "GK", 2: "DEF", 3: "MID", 4: "FWD"}
    clubs = [1, 2, 3, 4, 5]
    club_count = {c: 0 for c in clubs}

    for pos in (1, 2, 3, 4):
        for _ in range(quotas[pos]):
            # Pick a club that hasn't hit any cap yet (no constraint on test pred_df).
            club = clubs[pid % 5]
            pid += 1
            club_count[club] += 1

            base = {1: 3.0, 2: 4.0, 3: 5.0, 4: 6.0}[pos] + rng.uniform(0, 3)
            row = {
                "player_id": pid,
                "player_name": f"P{pid}",
                "team_id": club,
                "position_id": pos,
                "position": pos_labels[pos],
                "price": float(rng.uniform(4.0, 12.0)),
                "selected_by_percent": float(rng.uniform(0, 50)),
                "available": True,
            }
            for offset in range(horizon):
                gw = target_gw + offset
                # Slight noise per-GW so different GWs see different best players.
                row[f"predicted_gw{gw}"] = float(base + rng.uniform(-0.5, 0.5))
                row[f"ceiling_gw{gw}"] = row[f"predicted_gw{gw}"] * 1.5
            row["horizon_total"] = sum(
                row[f"predicted_gw{target_gw + i}"] for i in range(horizon)
            )
            rows.append(row)
    return pd.DataFrame(rows)


def _starting_squad_from_pred_df(pred_df: pd.DataFrame) -> Squad:
    """Pick a legal 15-player squad and XI from the test pred_df."""
    quotas = {1: 2, 2: 5, 3: 5, 4: 3}
    picks: list[int] = []
    club_count: dict[int, int] = {}
    for pos in (1, 2, 3, 4):
        pool = pred_df[pred_df.position_id == pos].sort_values("horizon_total", ascending=False)
        for _, row in pool.iterrows():
            club = int(row.team_id)
            if club_count.get(club, 0) >= 3:
                continue
            picks.append(int(row.player_id))
            club_count[club] = club_count.get(club, 0) + 1
            if sum(1 for p in picks if int(pred_df[pred_df.player_id == p].iloc[0].position_id) == pos) >= quotas[pos]:
                break

    pid_to_pos = {int(r.player_id): int(r.position_id) for _, r in pred_df.iterrows()}
    pid_to_h = {int(r.player_id): float(r.horizon_total) for _, r in pred_df.iterrows()}

    formation = {1: 1, 2: 4, 3: 4, 4: 2}
    xi: list[int] = []
    for pos, n in formation.items():
        candidates = [p for p in picks if pid_to_pos[p] == pos]
        candidates.sort(key=lambda p: pid_to_h[p], reverse=True)
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
# Invariants
# ---------------------------------------------------------------------------


class TestMultiWeekPlanInvariants:
    @pytest.mark.parametrize("horizon", [1, 2, 3, 4, 5])
    def test_step_count_matches_horizon(self, horizon):
        pred_df = _build_test_pred_df(target_gw=31, horizon=horizon)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df,
            starting_squad=squad,
            target_gw=31,
            horizon=horizon,
        )
        assert len(plan.steps) == horizon

    def test_steps_are_sequential_by_gameweek(self):
        pred_df = _build_test_pred_df(target_gw=31, horizon=4)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df, starting_squad=squad, target_gw=31, horizon=4
        )
        for prev, cur in zip(plan.steps, plan.steps[1:]):
            assert cur.gw == prev.gw + 1

    def test_fts_capped_at_five(self):
        pred_df = _build_test_pred_df(target_gw=31, horizon=5)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df, starting_squad=squad, target_gw=31, horizon=5
        )
        for step in plan.steps:
            assert 0 <= step.fts_after <= 5, (
                f"step gw={step.gw} fts_after={step.fts_after} out of [0, 5]"
            )

    def test_bank_never_negative(self):
        pred_df = _build_test_pred_df(target_gw=31, horizon=4)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df, starting_squad=squad, target_gw=31, horizon=4
        )
        for step in plan.steps:
            assert step.bank_after >= 0, (
                f"step gw={step.gw} bank_after={step.bank_after} negative"
            )

    def test_hit_cost_multiple_of_four(self):
        pred_df = _build_test_pred_df(target_gw=31, horizon=4)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df, starting_squad=squad, target_gw=31, horizon=4
        )
        for step in plan.steps:
            assert step.action.hit_cost >= 0
            assert step.action.hit_cost % 4 == 0

    def test_alternatives_capped_at_three(self):
        pred_df = _build_test_pred_df(target_gw=31, horizon=3)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df, starting_squad=squad, target_gw=31, horizon=3
        )
        assert len(plan.alternatives) <= 3
        # Alternatives must NOT nest (data-model.md § 6 invariant).
        for alt in plan.alternatives:
            assert alt.alternatives == ()

    def test_cumulative_hits_non_negative(self):
        pred_df = _build_test_pred_df(target_gw=31, horizon=3)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df, starting_squad=squad, target_gw=31, horizon=3
        )
        assert plan.cumulative_hits >= 0
        # Per-step hits sum to cumulative_hits.
        assert sum(s.action.hit_cost // 4 for s in plan.steps) == plan.cumulative_hits

    def test_net_gain_relationship_holds(self):
        pred_df = _build_test_pred_df(target_gw=31, horizon=3)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df, starting_squad=squad, target_gw=31, horizon=3
        )
        # net_gain_vs_baseline = total_score − hits×4 − baseline_score
        expected = plan.total_score - plan.cumulative_hits * 4 - plan.baseline_score
        assert plan.net_gain_vs_baseline == pytest.approx(expected, abs=0.05)

    def test_horizon_one_no_alternatives_required(self):
        pred_df = _build_test_pred_df(target_gw=31, horizon=1)
        squad = _starting_squad_from_pred_df(pred_df)
        plan = beam_search_multi_week(
            pred_df=pred_df, starting_squad=squad, target_gw=31, horizon=1
        )
        assert len(plan.steps) == 1
        # alternatives are optional; the impl may emit none for trivial horizons.
        assert len(plan.alternatives) <= 3
