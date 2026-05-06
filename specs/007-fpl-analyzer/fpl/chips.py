"""Chip-strategy timing recommendations (T043, US4, research.md § 9).

Public surface::

    triple_captain(*, pred_df, squad, target_gw, horizon)        -> ChipRecommendation
    bench_boost(*,  pred_df, squad, target_gw, horizon)          -> ChipRecommendation
    free_hit(*,     pred_df, current_squad, from_scratch_squad,
                    target_gw, horizon, swing_threshold=5.0)     -> ChipRecommendation
    wildcard(*,     pred_df, current_squad, from_scratch_squad,
                    target_gw, horizon, threshold=8.0)           -> ChipRecommendation
    compute_chip_plan(*, pred_df, current_squad,
                      from_scratch_squad, target_gw, horizon)    -> ChipPlan

Heuristics (FR-024 / 025 / 026):

* **TC** — argmax over (player ∈ squad, gw ∈ horizon) of
  ``ceiling_gw{gw}``. Source field is the ceiling head, never the mean
  (FR-024).
* **BB** — argmax over gw of ``sum(predicted_gw{gw} for player in bench)``
  where ``bench = squad.player_ids \\ squad.starting_xi``.
* **FH** — argmax over gw of
  ``sum(from_scratch_xi predicted_gw{gw}) − sum(current_xi predicted_gw{gw})``.
  Returns "no recommendation" if no GW exceeds ``swing_threshold``.
* **WC** — argmax over gw of cumulative swing from gw onwards across
  the rest of the horizon. Returns "no recommendation" if no GW exceeds
  ``threshold``.

Each function honours the FR-023 invariant: ``gw is None ⟺
supporting_metric is None``. The returned :class:`ChipRecommendation`
always carries a non-empty ``rationale`` string suitable for the CLI
``=== CHIP PLAN ===`` block and the Streamlit chip cards.
"""

from __future__ import annotations

import pandas as pd

from fpl.types import ChipPlan, ChipRecommendation, Squad


_NO_REC_RATIONALE_FALLBACK = "No positive case found in the planning horizon."


# ---------------------------------------------------------------------------
# Triple Captain (FR-024)
# ---------------------------------------------------------------------------


def triple_captain(
    *,
    pred_df: pd.DataFrame,
    squad: Squad,
    target_gw: int,
    horizon: int,
) -> ChipRecommendation:
    """argmax (player ∈ squad, gw ∈ horizon) of ``ceiling_gw{gw}`` (FR-024)."""
    candidates = pred_df[pred_df.player_id.isin(list(squad.player_ids))]
    if candidates.empty:
        return _no_rec("tc", "No candidates in pool.")

    best_gw: int | None = None
    best_player_id: int | None = None
    best_ceiling = -float("inf")

    for offset in range(horizon):
        gw = target_gw + offset
        col = f"ceiling_gw{gw}"
        if col not in candidates.columns:
            continue
        idx = candidates[col].idxmax()
        if pd.isna(idx):
            continue
        ceiling = float(candidates.loc[idx, col])
        if ceiling > best_ceiling:
            best_ceiling = ceiling
            best_gw = gw
            best_player_id = int(candidates.loc[idx, "player_id"])

    if best_gw is None or best_ceiling <= 0:
        return _no_rec("tc", _NO_REC_RATIONALE_FALLBACK)

    pname = (
        candidates[candidates.player_id == best_player_id].iloc[0].player_name
        if best_player_id is not None
        else "?"
    )
    return ChipRecommendation(
        chip="tc",
        gw=best_gw,
        supporting_metric=round(best_ceiling, 4),
        rationale=f"GW{best_gw}: {pname} ceiling = {best_ceiling:.2f}",
    )


# ---------------------------------------------------------------------------
# Bench Boost (FR-025)
# ---------------------------------------------------------------------------


def bench_boost(
    *,
    pred_df: pd.DataFrame,
    squad: Squad,
    target_gw: int,
    horizon: int,
) -> ChipRecommendation:
    """argmax over gw of the sum of bench predictions for that GW (FR-025)."""
    bench_ids = [p for p in squad.player_ids if p not in set(squad.starting_xi)]
    bench_df = pred_df[pred_df.player_id.isin(bench_ids)]
    if bench_df.empty or len(bench_ids) < 4:
        return _no_rec("bb", "Insufficient bench data.")

    best_gw: int | None = None
    best_score = -float("inf")
    for offset in range(horizon):
        gw = target_gw + offset
        col = f"predicted_gw{gw}"
        if col not in bench_df.columns:
            continue
        score = float(bench_df[col].sum())
        if score > best_score:
            best_score = score
            best_gw = gw

    if best_gw is None or best_score <= 0:
        return _no_rec("bb", "Bench projection too low to recommend.")

    return ChipRecommendation(
        chip="bb",
        gw=best_gw,
        supporting_metric=round(best_score, 4),
        rationale=f"GW{best_gw}: bench projection = {best_score:.2f}",
    )


# ---------------------------------------------------------------------------
# Free Hit (FR-026)
# ---------------------------------------------------------------------------


def free_hit(
    *,
    pred_df: pd.DataFrame,
    current_squad: Squad | None,
    from_scratch_squad: Squad | None,
    target_gw: int,
    horizon: int,
    swing_threshold: float = 5.0,
) -> ChipRecommendation:
    """argmax over gw of ``from_scratch_xi(gw) − own_xi(gw)`` (FR-026)."""
    if current_squad is None or from_scratch_squad is None:
        return _no_rec(
            "fh", "Free Hit needs both the current and from-scratch squads."
        )

    best_gw: int | None = None
    best_swing = -float("inf")
    for offset in range(horizon):
        gw = target_gw + offset
        col = f"predicted_gw{gw}"
        if col not in pred_df.columns:
            continue
        own_score = _xi_score(pred_df, current_squad, col)
        new_score = _xi_score(pred_df, from_scratch_squad, col)
        swing = new_score - own_score
        if swing > best_swing:
            best_swing = swing
            best_gw = gw

    if best_gw is None or best_swing < swing_threshold:
        return _no_rec(
            "fh",
            (
                "No gameweek shows a from-scratch swing large enough to justify "
                f"Free Hit (best={best_swing:.2f} < threshold={swing_threshold:.2f})."
            ),
        )

    return ChipRecommendation(
        chip="fh",
        gw=best_gw,
        supporting_metric=round(best_swing, 4),
        rationale=f"GW{best_gw}: from-scratch swing = +{best_swing:.2f} pts",
    )


# ---------------------------------------------------------------------------
# Wildcard
# ---------------------------------------------------------------------------


def wildcard(
    *,
    pred_df: pd.DataFrame,
    current_squad: Squad | None,
    from_scratch_squad: Squad | None,
    target_gw: int,
    horizon: int,
    threshold: float = 8.0,
) -> ChipRecommendation:
    """argmax over start-gw of cumulative swing from that GW onwards."""
    if current_squad is None or from_scratch_squad is None:
        return _no_rec(
            "wc", "Wildcard needs both the current and from-scratch squads."
        )

    best_gw: int | None = None
    best_gain = -float("inf")
    for start_offset in range(horizon):
        gw_at = target_gw + start_offset
        gain = 0.0
        for k in range(start_offset, horizon):
            gw = target_gw + k
            col = f"predicted_gw{gw}"
            if col not in pred_df.columns:
                continue
            gain += _xi_score(pred_df, from_scratch_squad, col) - _xi_score(
                pred_df, current_squad, col
            )
        if gain > best_gain:
            best_gain = gain
            best_gw = gw_at

    if best_gw is None or best_gain < threshold:
        return _no_rec(
            "wc",
            (
                "Wildcard not justified — best cumulative rebuild gain "
                f"{best_gain:.2f} < threshold {threshold:.2f}."
            ),
        )

    return ChipRecommendation(
        chip="wc",
        gw=best_gw,
        supporting_metric=round(best_gain, 4),
        rationale=f"GW{best_gw}: cumulative rebuild gain = +{best_gain:.2f} pts",
    )


# ---------------------------------------------------------------------------
# Composer
# ---------------------------------------------------------------------------


def compute_chip_plan(
    *,
    pred_df: pd.DataFrame,
    current_squad: Squad | None,
    from_scratch_squad: Squad | None,
    target_gw: int,
    horizon: int,
) -> ChipPlan:
    """Compose all four chip recommendations into a :class:`ChipPlan`."""
    basis_squad = current_squad if current_squad is not None else from_scratch_squad
    if basis_squad is None:
        return ChipPlan(
            triple_captain=_no_rec("tc", "No squad supplied."),
            bench_boost=_no_rec("bb", "No squad supplied."),
            free_hit=_no_rec("fh", "No squad supplied."),
            wildcard=_no_rec("wc", "No squad supplied."),
        )

    return ChipPlan(
        triple_captain=triple_captain(
            pred_df=pred_df, squad=basis_squad, target_gw=target_gw, horizon=horizon
        ),
        bench_boost=bench_boost(
            pred_df=pred_df, squad=basis_squad, target_gw=target_gw, horizon=horizon
        ),
        free_hit=free_hit(
            pred_df=pred_df,
            current_squad=current_squad,
            from_scratch_squad=from_scratch_squad,
            target_gw=target_gw,
            horizon=horizon,
        ),
        wildcard=wildcard(
            pred_df=pred_df,
            current_squad=current_squad,
            from_scratch_squad=from_scratch_squad,
            target_gw=target_gw,
            horizon=horizon,
        ),
    )


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _no_rec(chip: str, rationale: str) -> ChipRecommendation:
    return ChipRecommendation(
        chip=chip,  # type: ignore[arg-type]
        gw=None,
        supporting_metric=None,
        rationale=rationale,
    )


def _xi_score(pred_df: pd.DataFrame, squad: Squad, col: str) -> float:
    """Sum starting-XI ``col`` values from ``pred_df``."""
    xi_set = set(int(p) for p in squad.starting_xi)
    matching = pred_df[pred_df.player_id.isin(list(xi_set))]
    return float(matching[col].sum())


__all__ = [
    "bench_boost",
    "compute_chip_plan",
    "free_hit",
    "triple_captain",
    "wildcard",
]
