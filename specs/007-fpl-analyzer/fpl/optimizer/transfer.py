"""Single-week transfer recommendation (T028, US1, research.md § 7).

Public surface:

    recommend_single_week_transfer(
        pred_df, current_squad, free_transfers, *, top_n_per_position=50,
    ) -> tuple[Recommendation, list[Recommendation]]

Returns the best ``Recommendation`` plus up to 5 ranked alternatives
(FR-013). Scoring: ``net_gain = horizon_total_in − horizon_total_out −
hit_cost`` where ``hit_cost = max(0, k − free_transfers) × 4`` for a
``k``-transfer move.

Constraint enforcement:

* Available filter (FR-010): only players with ``available=True`` enter
  the candidate pool.
* Budget: ``current_squad.bank + price_out − price_in ≥ 0``.
* Max-3-per-club: enforced at the swap site (we recompute club counts
  after the proposed swap).
* Position parity: a ``k``-transfer swap is k same-position swaps —
  preserves the squad's 2/5/5/3 split and the XI formation.

If no candidate produces ``gain > 0``, the recommendation is HOLD with
an explanatory note (FR-014). MVP scope covers HOLD + 1-transfer;
2-transfer / 3-transfer / 4-transfer enumeration is reserved for a
future iteration (keeps US-1 demonstrable end-to-end without delaying
on combinatorial pruning).
"""

from __future__ import annotations

import pandas as pd

from fpl.types import Recommendation, Squad, TransferLeg


def recommend_single_week_transfer(
    *,
    pred_df: pd.DataFrame,
    current_squad: Squad,
    free_transfers: int,
    top_n_per_position: int = 50,
) -> tuple[Recommendation, list[Recommendation]]:
    """Pick the best single-week move; return it plus up to 5 alternatives."""
    pred_by_id = {int(row.player_id): row for _, row in pred_df.iterrows()}
    current_ids: frozenset[int] = frozenset(int(p) for p in current_squad.player_ids)

    # FR-010: available filter; FR-027 differentials use the same pool downstream.
    available_df = pred_df[pred_df.available.astype(bool)]

    # Pre-filter top-N per position from the available pool, excluding our own players.
    candidates_by_pos: dict[int, pd.DataFrame] = {}
    for pos in (1, 2, 3, 4):
        pool = available_df[
            (available_df.position_id == pos)
            & (~available_df.player_id.isin(list(current_ids)))
        ]
        candidates_by_pos[pos] = pool.nlargest(top_n_per_position, "horizon_total")

    base_club_counts = _club_counts(current_squad.player_ids, pred_by_id)

    candidates: list[Recommendation] = []

    # ---- 1-transfer enumeration ------------------------------------------------

    for out_id in current_squad.player_ids:
        out_p = pred_by_id[out_id]
        pos = int(out_p.position_id)
        for _, in_p in candidates_by_pos[pos].iterrows():
            in_id = int(in_p.player_id)
            new_bank = float(current_squad.bank) + float(out_p.price) - float(in_p.price)
            if new_bank < 0:
                continue
            if not _club_swap_ok(base_club_counts, int(out_p.team_id), int(in_p.team_id)):
                continue

            gross_gain = float(in_p.horizon_total) - float(out_p.horizon_total)
            hit_cost = _hit_cost(transfer_count=1, free_transfers=free_transfers)
            net_gain = gross_gain - hit_cost

            new_fts = max(0, int(free_transfers) - 1)
            new_squad = _build_squad_with_one_swap(
                current_squad, out_id, in_id, new_bank, new_fts
            )
            candidates.append(
                Recommendation(
                    kind="1-transfer",
                    transfers=(
                        TransferLeg(
                            out_player_id=int(out_id),
                            in_player_id=int(in_id),
                            delta=round(gross_gain, 4),
                        ),
                    ),
                    hit_cost=hit_cost,
                    gain=round(net_gain, 4),
                    new_bank=round(new_bank, 4),
                    new_squad=new_squad,
                    captain_id=new_squad.captain_id,
                    vice_captain_id=new_squad.vice_captain_id,
                )
            )

    candidates.sort(key=lambda r: r.gain, reverse=True)

    # ---- Pick primary + up to 5 alternatives ----------------------------------

    if not candidates or candidates[0].gain <= 0:
        primary = _make_hold(current_squad, note=(
            "No positive-gain transfer found in the available candidate pool."
        ))
        # Alternatives — keep up to 5 considered moves so the GUI can show them
        # under "Other single-week options considered" (FR-013).
        alternatives = candidates[:5]
        return primary, alternatives

    primary = candidates[0]
    alternatives = candidates[1:6]
    return primary, alternatives


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _club_counts(player_ids: tuple[int, ...], pred_by_id: dict) -> dict[int, int]:
    counts: dict[int, int] = {}
    for pid in player_ids:
        team = int(pred_by_id[int(pid)].team_id)
        counts[team] = counts.get(team, 0) + 1
    return counts


def _club_swap_ok(base: dict[int, int], out_team: int, in_team: int) -> bool:
    """Return True iff swapping out_team for in_team keeps every club ≤ 3."""
    if out_team == in_team:
        return True
    return base.get(in_team, 0) + 1 <= 3


def _hit_cost(*, transfer_count: int, free_transfers: int) -> int:
    """Multiples of 4 per transfer beyond free_transfers (FR-012)."""
    return max(0, transfer_count - int(free_transfers)) * 4


def _make_hold(squad: Squad, *, note: str | None = None) -> Recommendation:
    return Recommendation(
        kind="hold",
        transfers=(),
        hit_cost=0,
        gain=0,
        new_bank=squad.bank,
        new_squad=squad,
        captain_id=squad.captain_id,
        vice_captain_id=squad.vice_captain_id,
        note=note,
    )


def _build_squad_with_one_swap(
    squad: Squad,
    out_id: int,
    in_id: int,
    new_bank: float,
    new_fts: int,
) -> Squad:
    """Replace out_id with in_id throughout the Squad, preserving formation.

    Single-position swaps preserve the 2/5/5/3 split and the XI
    formation. If the captain or vice was the player being transferred
    out, the new player inherits the role.
    """
    new_player_ids = tuple(
        in_id if int(pid) == int(out_id) else int(pid) for pid in squad.player_ids
    )
    new_xi = tuple(
        in_id if int(pid) == int(out_id) else int(pid) for pid in squad.starting_xi
    )

    new_captain = in_id if squad.captain_id == out_id else squad.captain_id
    new_vice = in_id if squad.vice_captain_id == out_id else squad.vice_captain_id
    if new_captain == new_vice:
        # Pick the highest-position-id XI member as the new vice.
        for pid in new_xi:
            if pid != new_captain:
                new_vice = pid
                break

    return Squad(
        player_ids=new_player_ids,
        starting_xi=new_xi,
        captain_id=new_captain,
        vice_captain_id=new_vice,
        bank=new_bank,
        free_transfers=new_fts,
        chips_used=squad.chips_used,
        chips_available=squad.chips_available,
        active_chip=squad.active_chip,
    )


__all__ = ["recommend_single_week_transfer"]
