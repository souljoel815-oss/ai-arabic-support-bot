"""LP from-scratch squad optimizer tests (T037, FR-019, SC-006).

Asserts on the output of :func:`fpl.optimizer.squad.optimize_from_scratch`:

* Exactly 15 distinct players.
* Position split exactly 2 / 5 / 5 / 3 (GK / DEF / MID / FWD).
* No more than 3 players from any single club.
* Total cost ≤ supplied budget.
* Starting XI is exactly 11, valid formation (1 GK, 3..5 DEF, 1..3 FWD).
* Captain ∈ XI; vice ∈ XI; captain ≠ vice.
* Optimizer raises ``ValueError`` when the available pool is too small
  to satisfy the position quotas (run.py falls back to its greedy stub
  in that case — verified in test_p3_from_scratch.py).
"""

from __future__ import annotations

import numpy as np
import pandas as pd
import pytest

from fpl.optimizer.squad import optimize_from_scratch


pytestmark = pytest.mark.unit


# ---------------------------------------------------------------------------
# Synthetic pred_df: 30 players, 6 clubs, full 8/15/15/8 split available
# ---------------------------------------------------------------------------


def _build_test_pred_df(target_gw: int = 31, horizon: int = 3) -> pd.DataFrame:
    rng = np.random.default_rng(seed=1)
    rows = []
    pid = 100
    quotas = {1: 4, 2: 10, 3: 10, 4: 6}      # plenty of headroom
    pos_labels = {1: "GK", 2: "DEF", 3: "MID", 4: "FWD"}
    n_clubs = 6
    for pos in (1, 2, 3, 4):
        for k in range(quotas[pos]):
            club = (pid % n_clubs) + 1
            pid += 1
            base_score = {1: 3.0, 2: 4.0, 3: 5.0, 4: 6.0}[pos] + rng.uniform(0, 3)
            row = {
                "player_id": pid,
                "player_name": f"P{pid}",
                "team_id": club,
                "position_id": pos,
                "position": pos_labels[pos],
                "price": float(rng.uniform(4.0, 9.0)),
                "selected_by_percent": float(rng.uniform(0, 50)),
                "available": True,
                "form": base_score,
                "season_ppg": base_score,
            }
            for offset in range(horizon):
                gw = target_gw + offset
                row[f"predicted_gw{gw}"] = float(base_score + rng.uniform(-0.5, 0.5))
                row[f"ceiling_gw{gw}"] = row[f"predicted_gw{gw}"] * 1.5
            row["horizon_total"] = sum(
                row[f"predicted_gw{target_gw + i}"] for i in range(horizon)
            )
            rows.append(row)
    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# Invariants
# ---------------------------------------------------------------------------


class TestOptimizeFromScratch:
    def test_returns_squad_with_fifteen_distinct_players(self):
        pred = _build_test_pred_df()
        squad = optimize_from_scratch(pred, budget=100.0, target_gw=31)
        assert len(squad.player_ids) == 15
        assert len(set(squad.player_ids)) == 15

    def test_position_split_is_2_5_5_3(self):
        pred = _build_test_pred_df()
        squad = optimize_from_scratch(pred, budget=100.0, target_gw=31)
        pos_by_pid = {int(r.player_id): int(r.position_id) for _, r in pred.iterrows()}
        counts = {1: 0, 2: 0, 3: 0, 4: 0}
        for pid in squad.player_ids:
            counts[pos_by_pid[pid]] += 1
        assert counts == {1: 2, 2: 5, 3: 5, 4: 3}

    def test_max_three_per_club(self):
        pred = _build_test_pred_df()
        squad = optimize_from_scratch(pred, budget=100.0, target_gw=31)
        team_by_pid = {int(r.player_id): int(r.team_id) for _, r in pred.iterrows()}
        club_counts: dict[int, int] = {}
        for pid in squad.player_ids:
            club = team_by_pid[pid]
            club_counts[club] = club_counts.get(club, 0) + 1
        assert all(n <= 3 for n in club_counts.values()), (
            f"per-club violation: {club_counts}"
        )

    def test_total_cost_within_budget(self):
        pred = _build_test_pred_df()
        budget = 100.0
        squad = optimize_from_scratch(pred, budget=budget, target_gw=31)
        price_by_pid = {int(r.player_id): float(r.price) for _, r in pred.iterrows()}
        total = sum(price_by_pid[p] for p in squad.player_ids)
        assert total <= budget + 1e-6

    def test_starting_xi_valid_formation(self):
        pred = _build_test_pred_df()
        squad = optimize_from_scratch(pred, budget=100.0, target_gw=31)
        pos_by_pid = {int(r.player_id): int(r.position_id) for _, r in pred.iterrows()}
        xi_pos = [pos_by_pid[p] for p in squad.starting_xi]
        n_gk = xi_pos.count(1)
        n_def = xi_pos.count(2)
        n_mid = xi_pos.count(3)
        n_fwd = xi_pos.count(4)
        assert n_gk == 1
        assert 3 <= n_def <= 5
        assert 1 <= n_fwd <= 3
        assert n_gk + n_def + n_mid + n_fwd == 11

    def test_captain_and_vice_in_starting_xi(self):
        pred = _build_test_pred_df()
        squad = optimize_from_scratch(pred, budget=100.0, target_gw=31)
        assert squad.captain_id in set(squad.starting_xi)
        assert squad.vice_captain_id in set(squad.starting_xi)
        assert squad.captain_id != squad.vice_captain_id

    def test_lower_budget_still_feasible(self):
        # Tighter budget should still yield a valid squad (given the price
        # range in our synthetic data).
        pred = _build_test_pred_df()
        squad = optimize_from_scratch(pred, budget=85.0, target_gw=31)
        assert len(squad.player_ids) == 15
        price_by_pid = {int(r.player_id): float(r.price) for _, r in pred.iterrows()}
        assert sum(price_by_pid[p] for p in squad.player_ids) <= 85.0 + 1e-6

    def test_infeasible_pool_raises_value_error(self):
        # Tiny pool: only 4 GK, 4 DEF, 4 MID, 4 FWD = 16 players but with
        # only 4 GKs we can satisfy 2/5/5/3 only if we have ≥5 DEF / ≥5 MID.
        # Drop all but 4 DEFs → can't pick 5 → infeasible.
        pred = _build_test_pred_df()
        # Mark all but 4 DEFs as unavailable.
        defs = pred[pred.position_id == 2]
        keep = set(defs.iloc[:4].player_id.tolist())
        pred = pred.copy()
        pred.loc[(pred.position_id == 2) & (~pred.player_id.isin(keep)), "available"] = False
        with pytest.raises(ValueError, match=r"(infeasible|optimizer)"):
            optimize_from_scratch(pred, budget=100.0, target_gw=31)

    def test_excludes_unavailable_players(self):
        # FR-010: available=False players must NOT appear in the squad.
        pred = _build_test_pred_df()
        pred = pred.copy()
        # Mark the highest-scoring forward as unavailable.
        forwards = pred[pred.position_id == 4].sort_values("horizon_total", ascending=False)
        unavailable_pid = int(forwards.iloc[0].player_id)
        pred.loc[pred.player_id == unavailable_pid, "available"] = False

        squad = optimize_from_scratch(pred, budget=100.0, target_gw=31)
        assert unavailable_pid not in squad.player_ids
