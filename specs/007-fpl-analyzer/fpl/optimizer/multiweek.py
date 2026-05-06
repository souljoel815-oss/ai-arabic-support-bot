"""Multi-week beam-search planner (T034, US2, research.md § 8).

Public surface::

    beam_search_multi_week(
        pred_df, starting_squad, *, target_gw, horizon,
        beam_width=16, branching_factor=8,
    ) -> MultiWeekPlan

State per beam slot::

    (squad, cumulative_score, cumulative_hits, steps)

…where ``squad`` carries the up-to-date ``bank`` and ``free_transfers``.

Each step explores up to ``branching_factor`` candidate transitions
(``hold`` + the top 1-transfer moves enumerated by
:func:`fpl.optimizer.transfer.recommend_single_week_transfer`). The
beam is pruned to ``beam_width`` after each step using the net score
``cumulative_score − cumulative_hits × 4`` as the ranking key.

Constraint enforcement (FR-016, FR-017, SC-007):

* Free-transfer carry: ``min(5, fts_before − transfers_used + 1)``.
* Bank ≥ 0 (already enforced by :class:`Recommendation` invariants).
* Hit costs ≥ 0 and multiples of 4 (already enforced by
  :class:`Recommendation`).

Output:

* ``best_path`` (the highest net-score state's path) becomes the main
  :class:`MultiWeekPlan`.
* Up to 3 next-best non-best states become its ``alternatives`` (FR-018).
* ``baseline_score`` is the hold-every-week predicted XI score; the
  ``net_gain_vs_baseline`` follows the relationship asserted by
  ``tests/unit/test_multiweek.py``.

MVP scope: ``branching_factor`` defaults to 8 (hold + 7 top moves).
2-transfer enumeration is reachable through the alternatives that
:func:`recommend_single_week_transfer` returns when implemented (US1
MVP only emits 1-transfer; deeper-arity moves arrive in a future
iteration).
"""

from __future__ import annotations

import dataclasses

import pandas as pd

from fpl.optimizer.transfer import recommend_single_week_transfer
from fpl.types import MultiWeekPlan, MultiWeekStep, Recommendation, Squad


_DEFAULT_BEAM_WIDTH = 16
_DEFAULT_BRANCHING_FACTOR = 8
_FT_CAP = 5


# ---------------------------------------------------------------------------
# Internal beam state
# ---------------------------------------------------------------------------


@dataclasses.dataclass(frozen=True)
class _BeamState:
    """One slot in the beam — a squad path through the horizon."""

    squad: Squad  # carries up-to-date bank + free_transfers
    cumulative_score: float
    cumulative_hits: int
    steps: tuple[MultiWeekStep, ...]

    def net_score(self) -> float:
        return self.cumulative_score - self.cumulative_hits * 4


# ---------------------------------------------------------------------------
# Public entry point
# ---------------------------------------------------------------------------


def beam_search_multi_week(
    *,
    pred_df: pd.DataFrame,
    starting_squad: Squad,
    target_gw: int,
    horizon: int,
    beam_width: int = _DEFAULT_BEAM_WIDTH,
    branching_factor: int = _DEFAULT_BRANCHING_FACTOR,
) -> MultiWeekPlan:
    """Run beam search; return the best :class:`MultiWeekPlan`."""
    if not (1 <= int(horizon) <= 5):
        raise ValueError(f"horizon must be 1..5, got {horizon}")

    initial = _BeamState(
        squad=starting_squad,
        cumulative_score=0.0,
        cumulative_hits=0,
        steps=(),
    )
    beam: list[_BeamState] = [initial]

    for offset in range(int(horizon)):
        gw = target_gw + offset
        new_beam: list[_BeamState] = []
        for state in beam:
            for rec, week_score in _enumerate_transitions(
                pred_df=pred_df,
                current_squad=state.squad,
                gw=gw,
                branching_factor=branching_factor,
            ):
                fts_after = min(
                    _FT_CAP,
                    max(0, state.squad.free_transfers - len(rec.transfers) + 1),
                )
                bank_after = float(rec.new_bank)

                step = MultiWeekStep(
                    gw=gw,
                    action=rec,
                    week_predicted_score=round(float(week_score), 4),
                    bank_after=round(bank_after, 4),
                    fts_after=fts_after,
                )

                # Carry bank + FTs into the new squad's state for the next step.
                next_squad = dataclasses.replace(
                    rec.new_squad,
                    bank=bank_after,
                    free_transfers=fts_after,
                )

                new_beam.append(
                    _BeamState(
                        squad=next_squad,
                        cumulative_score=state.cumulative_score + float(week_score),
                        cumulative_hits=state.cumulative_hits + rec.hit_cost // 4,
                        steps=state.steps + (step,),
                    )
                )

        # Prune to the top beam_width by net cumulative score.
        new_beam.sort(key=_BeamState.net_score, reverse=True)
        beam = new_beam[:beam_width]

    if not beam:
        raise ValueError("Beam search produced no candidate paths")

    # Final ranking + hold-baseline.
    beam.sort(key=_BeamState.net_score, reverse=True)
    best = beam[0]
    baseline = _hold_baseline(starting_squad, pred_df, target_gw, int(horizon))

    main = MultiWeekPlan(
        steps=best.steps,
        cumulative_hits=best.cumulative_hits,
        total_score=round(best.cumulative_score, 4),
        baseline_score=round(baseline, 4),
        net_gain_vs_baseline=round(best.net_score() - baseline, 4),
        alternatives=(),
    )

    alternatives: list[MultiWeekPlan] = []
    for alt in beam[1:4]:
        alternatives.append(
            MultiWeekPlan(
                steps=alt.steps,
                cumulative_hits=alt.cumulative_hits,
                total_score=round(alt.cumulative_score, 4),
                baseline_score=round(baseline, 4),
                net_gain_vs_baseline=round(alt.net_score() - baseline, 4),
                alternatives=(),
            )
        )

    return dataclasses.replace(main, alternatives=tuple(alternatives))


# ---------------------------------------------------------------------------
# Per-state transition enumeration
# ---------------------------------------------------------------------------


def _enumerate_transitions(
    *,
    pred_df: pd.DataFrame,
    current_squad: Squad,
    gw: int,
    branching_factor: int,
) -> list[tuple[Recommendation, float]]:
    """Hold + top 1-transfer candidates with each one's per-week XI score."""
    transitions: list[tuple[Recommendation, float]] = []

    # 1. Hold — always a valid transition.
    transitions.append(
        (_hold_recommendation(current_squad), _xi_predicted_score(current_squad, pred_df, gw))
    )

    # 2. 1-transfer candidates — reuse the single-week enumerator.
    primary, alts = recommend_single_week_transfer(
        pred_df=pred_df,
        current_squad=current_squad,
        free_transfers=int(current_squad.free_transfers),
    )

    seen: set[tuple] = set()
    candidates: list[Recommendation] = []
    for rec in [primary, *alts]:
        if rec.kind == "hold":
            continue
        key = tuple((t.out_player_id, t.in_player_id) for t in rec.transfers)
        if key in seen:
            continue
        seen.add(key)
        candidates.append(rec)

    # branching_factor includes the hold slot.
    for rec in candidates[: max(0, branching_factor - 1)]:
        score = _xi_predicted_score(rec.new_squad, pred_df, gw)
        transitions.append((rec, score))

    return transitions


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _hold_recommendation(squad: Squad) -> Recommendation:
    return Recommendation(
        kind="hold",
        transfers=(),
        hit_cost=0,
        gain=0,
        new_bank=squad.bank,
        new_squad=squad,
        captain_id=squad.captain_id,
        vice_captain_id=squad.vice_captain_id,
        note=None,
    )


def _xi_predicted_score(squad: Squad, pred_df: pd.DataFrame, gw: int) -> float:
    """Sum the starting-XI's ``predicted_gw{gw}`` from ``pred_df``."""
    pred_col = f"predicted_gw{gw}"
    if pred_col not in pred_df.columns:
        return 0.0
    xi_set = set(int(p) for p in squad.starting_xi)
    matching = pred_df[pred_df.player_id.isin(list(xi_set))]
    return float(matching[pred_col].sum())


def _hold_baseline(
    squad: Squad, pred_df: pd.DataFrame, target_gw: int, horizon: int
) -> float:
    """Hold-every-week baseline = sum of starting XI's predictions across horizon."""
    return sum(
        _xi_predicted_score(squad, pred_df, target_gw + offset)
        for offset in range(horizon)
    )


__all__ = ["beam_search_multi_week"]
