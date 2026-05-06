"""From-scratch 15-player squad optimizer (T039, US3, research.md § 6).

Public surface::

    optimize_from_scratch(
        pred_df, *, budget=100.0, target_gw=None,
    ) -> Squad

PuLP MILP formulation:

* Decision variables (per available player ``i``):
    * ``x_i ∈ {0, 1}`` — in 15-man squad
    * ``s_i ∈ {0, 1}`` — in starting XI
    * ``c_i ∈ {0, 1}`` — captain
* Constraints:
    * ``sum(x_i) == 15``
    * Per-position quotas: 2 / 5 / 5 / 3 (GK / DEF / MID / FWD)
    * ``sum(price_i × x_i) <= budget``
    * Per-club: ``sum(x_i for i in club_k) <= 3`` for each club ``k``
    * ``s_i <= x_i``; ``sum(s_i) == 11``
    * Valid XI formation: 1 GK; 3..5 DEF; 1..3 FWD
    * ``c_i <= s_i``; ``sum(c_i) == 1``
* Objective:
    ``maximise sum(horizon_total_i × s_i) + sum(ceiling_i × c_i)``

Available filter (FR-010): only rows with ``available=True`` enter the
LP. Vice-captain is the second-highest-scoring XI player by
``horizon_total`` (no LP variable needed).

Raises :class:`ValueError` when the solver fails (infeasibility
typically means too few available players to satisfy the position
quotas — :mod:`fpl.run` falls back to its greedy stub in that case).
"""

from __future__ import annotations

from typing import Any

import pandas as pd

from fpl.types import Squad


_POSITION_QUOTAS: dict[int, int] = {1: 2, 2: 5, 3: 5, 4: 3}
_DEFAULT_BUDGET = 100.0
_DEFAULT_FREE_TRANSFERS = 1


def optimize_from_scratch(
    pred_df: pd.DataFrame,
    *,
    budget: float = _DEFAULT_BUDGET,
    target_gw: int | None = None,
) -> Squad:
    """Build the optimal 15-player squad and starting XI within ``budget``.

    Solver: PuLP's bundled CBC. Imported at call time so the rest of
    the package (which also runs in environments without PuLP wheels —
    notably contract test envs) doesn't pay the import cost.
    """
    from pulp import (
        PULP_CBC_CMD,
        LpMaximize,
        LpProblem,
        LpStatusOptimal,
        LpVariable,
        lpSum,
    )

    df = pred_df[pred_df.available.astype(bool)].reset_index(drop=True)
    n = len(df)
    if n < 15:
        raise ValueError(
            f"LP optimizer needs at least 15 available players, got {n}"
        )

    player_ids: list[int] = df.player_id.astype(int).tolist()
    positions: list[int] = df.position_id.astype(int).tolist()
    teams: list[int] = df.team_id.astype(int).tolist()
    prices: list[float] = df.price.astype(float).tolist()
    horizon_totals: list[float] = df.horizon_total.astype(float).tolist()
    ceilings: list[float] = _ceiling_column(df, target_gw)

    prob = LpProblem("FPLSquadOptimizer", LpMaximize)
    x = [LpVariable(f"x_{i}", cat="Binary") for i in range(n)]
    s = [LpVariable(f"s_{i}", cat="Binary") for i in range(n)]
    c = [LpVariable(f"c_{i}", cat="Binary") for i in range(n)]

    # ----------------------------------------------------------------
    # Objective: starting-XI horizon score + captaincy ceiling bonus
    # ----------------------------------------------------------------
    prob += (
        lpSum(horizon_totals[i] * s[i] for i in range(n))
        + lpSum(ceilings[i] * c[i] for i in range(n))
    ), "objective_total_xi_plus_captain_bonus"

    # ----------------------------------------------------------------
    # Squad-level constraints
    # ----------------------------------------------------------------
    prob += lpSum(x[i] for i in range(n)) == 15, "squad_size"

    for pos, quota in _POSITION_QUOTAS.items():
        prob += (
            lpSum(x[i] for i in range(n) if positions[i] == pos) == quota
        ), f"position_quota_{pos}"

    prob += (
        lpSum(prices[i] * x[i] for i in range(n)) <= budget
    ), "budget"

    for club in set(teams):
        prob += (
            lpSum(x[i] for i in range(n) if teams[i] == club) <= 3
        ), f"club_cap_{club}"

    # ----------------------------------------------------------------
    # Starting-XI constraints
    # ----------------------------------------------------------------
    for i in range(n):
        prob += s[i] <= x[i], f"xi_in_squad_{i}"

    prob += lpSum(s[i] for i in range(n)) == 11, "xi_size"
    prob += lpSum(s[i] for i in range(n) if positions[i] == 1) == 1, "xi_one_gk"
    prob += lpSum(s[i] for i in range(n) if positions[i] == 2) >= 3, "xi_def_min"
    prob += lpSum(s[i] for i in range(n) if positions[i] == 2) <= 5, "xi_def_max"
    prob += lpSum(s[i] for i in range(n) if positions[i] == 3) <= 5, "xi_mid_max"
    prob += lpSum(s[i] for i in range(n) if positions[i] == 4) >= 1, "xi_fwd_min"
    prob += lpSum(s[i] for i in range(n) if positions[i] == 4) <= 3, "xi_fwd_max"

    # ----------------------------------------------------------------
    # Captain constraints
    # ----------------------------------------------------------------
    for i in range(n):
        prob += c[i] <= s[i], f"captain_in_xi_{i}"
    prob += lpSum(c[i] for i in range(n)) == 1, "single_captain"

    # ----------------------------------------------------------------
    # Solve
    # ----------------------------------------------------------------
    solver = PULP_CBC_CMD(msg=False)
    prob.solve(solver)

    if prob.status != LpStatusOptimal:
        raise ValueError(
            f"LP squad optimizer infeasible / failed: status={prob.status} "
            f"(typical cause: too few available players or budget too tight)"
        )

    # Extract solution. PuLP variable values can be floats very close to
    # 0/1 due to solver tolerances — use 0.5 as the threshold.
    squad_ids = [player_ids[i] for i in range(n) if (x[i].value() or 0) > 0.5]
    xi_ids = [player_ids[i] for i in range(n) if (s[i].value() or 0) > 0.5]
    captain_id = next(
        player_ids[i] for i in range(n) if (c[i].value() or 0) > 0.5
    )

    # Vice-captain = highest horizon_total in XI ≠ captain.
    pid_to_h = {int(r.player_id): float(r.horizon_total) for _, r in df.iterrows()}
    xi_sorted = sorted(xi_ids, key=lambda p: pid_to_h.get(int(p), 0.0), reverse=True)
    vice_id = next(p for p in xi_sorted if p != captain_id)

    # Bank = budget − total cost (rounded to 2dp).
    pid_to_price = {int(r.player_id): float(r.price) for _, r in df.iterrows()}
    bank = round(budget - sum(pid_to_price[int(p)] for p in squad_ids), 2)

    return Squad(
        player_ids=tuple(int(p) for p in squad_ids),
        starting_xi=tuple(int(p) for p in xi_ids),
        captain_id=int(captain_id),
        vice_captain_id=int(vice_id),
        bank=max(0.0, bank),  # guard against tiny negative due to FP noise
        free_transfers=_DEFAULT_FREE_TRANSFERS,
    )


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _ceiling_column(df: pd.DataFrame, target_gw: int | None) -> list[float]:
    """Choose the per-target-GW ceiling column for the captaincy bonus.

    Falls back to ``horizon_total`` when no ``ceiling_gw{N}`` column
    exists, or when ``target_gw`` is None and we just need *some*
    ceiling-shaped bonus to differentiate captains.
    """
    if target_gw is not None:
        col = f"ceiling_gw{target_gw}"
        if col in df.columns:
            return df[col].astype(float).tolist()
    ceiling_cols = sorted(c for c in df.columns if c.startswith("ceiling_gw"))
    if ceiling_cols:
        return df[ceiling_cols[0]].astype(float).tolist()
    # Last-ditch fallback: use horizon_total as a stand-in ceiling.
    return df.horizon_total.astype(float).tolist()


__all__ = ["optimize_from_scratch"]
