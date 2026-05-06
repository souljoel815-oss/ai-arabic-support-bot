"""Typed data containers shared across the ``fpl`` package (T011).

Per ``data-model.md`` §§ 4-7 + § 9 with two intentional adjustments
recorded by `/speckit-analyze` (Session 2026-05-06):

* Finding **I1** — `CachedAnalysisResult.mode` uses the legacy literal
  ``"from_scratch" | "analysis"`` (matching ``fpl_gui.py``'s existing
  check ``result.get('mode') != 'analysis'``) and a separate
  ``primary_view`` field carries the ``"from_scratch" | "continuity"``
  view selection.
* Composition checks (2/5/5/3 split + max-3-per-club + XI formation)
  live in :func:`validate_squad_composition` rather than
  ``Squad.__post_init__`` because they need external position/club
  lookup tables. The factories in ``fpl.api`` (picks loader) and
  ``fpl.optimizer.squad`` (LP) call them at the boundary.

This module deliberately avoids importing pandas at module load — the
``pred_df`` / ``differential_df`` fields are typed as :data:`typing.Any`
so unit tests for these dataclasses remain pandas-free.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Literal, Mapping

# ---------------------------------------------------------------------------
# Literal aliases — kept short so type hints elsewhere stay readable.
# ---------------------------------------------------------------------------

ChipName = Literal["tc", "bb", "fh", "wc"]
RecommendationKind = Literal[
    "hold", "1-transfer", "2-transfer", "3-transfer", "4-transfer"
]
ModeLabel = Literal["from_scratch", "analysis"]   # legacy fpl_gui.py value
PrimaryView = Literal["from_scratch", "continuity"]
SourceName = Literal["fpl_bootstrap", "fpl_fixtures", "fpl_picks", "understat"]
SourceStatus = Literal[
    "ok", "ok_retried", "skipped_by_toggle", "unavailable", "failed"
]

_VALID_CHIPS: frozenset[str] = frozenset({"tc", "bb", "fh", "wc"})


# ---------------------------------------------------------------------------
# Squad / Recommendation / Multi-week / Chips
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class TransferLeg:
    out_player_id: int
    in_player_id: int
    delta: float


@dataclass(frozen=True)
class Squad:
    """A 15-player FPL squad with chosen XI and captaincy.

    The composition rules (2/5/5/3 by position, ≤3 per club, valid XI
    formation) require a position/club lookup; see
    :func:`validate_squad_composition`. The invariants checked here are
    the ones that need no external context.
    """

    player_ids: tuple[int, ...]
    starting_xi: tuple[int, ...]
    captain_id: int
    vice_captain_id: int
    bank: float
    free_transfers: int
    chips_used: frozenset[str] = field(default_factory=frozenset)
    chips_available: frozenset[str] = field(
        default_factory=lambda: frozenset({"tc", "bb", "fh", "wc"})
    )
    active_chip: str | None = None

    def __post_init__(self) -> None:
        if len(self.player_ids) != 15:
            raise ValueError(
                f"Squad must contain exactly 15 players, got {len(self.player_ids)}"
            )
        if len(set(self.player_ids)) != 15:
            raise ValueError("Squad player_ids must be unique")
        if len(self.starting_xi) != 11:
            raise ValueError(
                f"Starting XI must contain exactly 11 players, got {len(self.starting_xi)}"
            )
        if len(set(self.starting_xi)) != 11:
            raise ValueError("Starting XI players must be unique")
        if not set(self.starting_xi).issubset(set(self.player_ids)):
            raise ValueError("Starting XI players must all be in the 15-man squad")
        if self.captain_id not in set(self.starting_xi):
            raise ValueError(
                f"Captain {self.captain_id} must be in starting XI"
            )
        if self.vice_captain_id not in set(self.starting_xi):
            raise ValueError(
                f"Vice-captain {self.vice_captain_id} must be in starting XI"
            )
        if self.captain_id == self.vice_captain_id:
            raise ValueError("Captain and vice-captain must be different players")
        if self.bank < 0:
            raise ValueError(f"Bank balance must be >= 0, got {self.bank}")
        if not (0 <= self.free_transfers <= 5):
            raise ValueError(
                f"Free transfers must be in [0, 5], got {self.free_transfers}"
            )
        if self.active_chip is not None and self.active_chip not in _VALID_CHIPS:
            raise ValueError(f"Unknown active chip: {self.active_chip!r}")


def validate_squad_composition(
    squad: Squad,
    *,
    position_by_player_id: Mapping[int, int],
    club_by_player_id: Mapping[int, int],
) -> None:
    """Position-split + per-club cap + XI formation checks (FR-019, SC-006).

    Raises :class:`ValueError` on the first violation found, with a
    message specific enough that contract / unit tests can pattern-match
    on it (see ``tests/unit/test_squad_invariants.py``).
    """

    # Position split — exactly 2 GK / 5 DEF / 5 MID / 3 FWD.
    counts: dict[int, int] = {1: 0, 2: 0, 3: 0, 4: 0}
    for pid in squad.player_ids:
        if pid not in position_by_player_id:
            raise ValueError(f"Missing position for player_id {pid}")
        counts[position_by_player_id[pid]] += 1
    if counts != {1: 2, 2: 5, 3: 5, 4: 3}:
        raise ValueError(
            f"Squad position split must be 2/5/5/3 (GK/DEF/MID/FWD), got {counts}"
        )

    # Per-club cap — at most 3.
    club_counts: dict[int, int] = {}
    for pid in squad.player_ids:
        if pid not in club_by_player_id:
            raise ValueError(f"Missing club for player_id {pid}")
        club_counts[club_by_player_id[pid]] = (
            club_counts.get(club_by_player_id[pid], 0) + 1
        )
    over = {club: n for club, n in club_counts.items() if n > 3}
    if over:
        raise ValueError(
            f"Squad has more than 3 players in clubs: {over}"
        )

    # Starting XI formation — 1 GK; 3..5 DEF; 1..3 FWD; remainder MIDs.
    xi_positions = [position_by_player_id[pid] for pid in squad.starting_xi]
    n_gk = xi_positions.count(1)
    n_def = xi_positions.count(2)
    n_mid = xi_positions.count(3)
    n_fwd = xi_positions.count(4)
    if n_gk != 1:
        raise ValueError(f"Starting XI must have exactly 1 GK, got {n_gk}")
    if not (3 <= n_def <= 5):
        raise ValueError(f"Starting XI must have 3..5 DEF, got {n_def}")
    if not (1 <= n_fwd <= 3):
        raise ValueError(f"Starting XI must have 1..3 FWD, got {n_fwd}")
    if n_gk + n_def + n_mid + n_fwd != 11:
        raise ValueError(
            f"Starting XI must total 11 players, got {n_gk + n_def + n_mid + n_fwd}"
        )


@dataclass(frozen=True)
class Recommendation:
    kind: RecommendationKind
    transfers: tuple[TransferLeg, ...]
    hit_cost: int
    gain: float
    new_bank: float
    new_squad: Squad
    captain_id: int
    vice_captain_id: int
    note: str | None = None

    def __post_init__(self) -> None:
        if self.kind == "hold":
            if len(self.transfers) != 0:
                raise ValueError("hold recommendation must have 0 transfers")
            if self.hit_cost != 0:
                raise ValueError("hold recommendation must have hit_cost == 0")
            if self.gain != 0:
                raise ValueError("hold recommendation must have gain == 0")
        else:
            n = int(self.kind.split("-", 1)[0])
            if len(self.transfers) != n:
                raise ValueError(
                    f"{self.kind} must have {n} transfers, got {len(self.transfers)}"
                )
        if self.hit_cost < 0 or self.hit_cost % 4 != 0:
            raise ValueError(
                f"hit_cost must be a non-negative multiple of 4, got {self.hit_cost}"
            )
        if self.new_bank < 0:
            raise ValueError(f"new_bank must be >= 0, got {self.new_bank}")


@dataclass(frozen=True)
class MultiWeekStep:
    gw: int
    action: Recommendation
    week_predicted_score: float
    bank_after: float
    fts_after: int

    def __post_init__(self) -> None:
        if not (0 <= self.fts_after <= 5):
            raise ValueError(
                f"fts_after must be in [0, 5], got {self.fts_after}"
            )
        if self.bank_after < 0:
            raise ValueError(f"bank_after must be >= 0, got {self.bank_after}")


@dataclass(frozen=True)
class MultiWeekPlan:
    steps: tuple[MultiWeekStep, ...]
    cumulative_hits: int
    total_score: float
    baseline_score: float
    net_gain_vs_baseline: float
    alternatives: tuple["MultiWeekPlan", ...] = ()

    def __post_init__(self) -> None:
        if not (1 <= len(self.steps) <= 5):
            raise ValueError(
                f"Multi-week plan horizon must be 1..5, got {len(self.steps)}"
            )
        for prev, cur in zip(self.steps, self.steps[1:]):
            if cur.gw != prev.gw + 1:
                raise ValueError(
                    f"Steps must be sequential by gw; saw {prev.gw} -> {cur.gw}"
                )
        if len(self.alternatives) > 3:
            raise ValueError(
                f"At most 3 alternatives allowed, got {len(self.alternatives)}"
            )
        # Alternatives must not nest — keeps the data flat for tests / UI.
        for alt in self.alternatives:
            if alt.alternatives:
                raise ValueError(
                    "Alternatives must not contain further alternatives"
                )
        if self.cumulative_hits < 0:
            raise ValueError(f"cumulative_hits must be >= 0, got {self.cumulative_hits}")


@dataclass(frozen=True)
class ChipRecommendation:
    chip: ChipName
    gw: int | None
    supporting_metric: float | None
    rationale: str

    def __post_init__(self) -> None:
        if self.chip not in _VALID_CHIPS:
            raise ValueError(f"Unknown chip: {self.chip!r}")
        # FR-023: gw is None ⟺ supporting_metric is None.
        if (self.gw is None) != (self.supporting_metric is None):
            raise ValueError(
                "ChipRecommendation: gw and supporting_metric must both be None "
                f"or both be set; got gw={self.gw}, supporting_metric={self.supporting_metric}"
            )


@dataclass(frozen=True)
class ChipPlan:
    triple_captain: ChipRecommendation
    bench_boost: ChipRecommendation
    free_hit: ChipRecommendation
    wildcard: ChipRecommendation

    def __post_init__(self) -> None:
        if self.triple_captain.chip != "tc":
            raise ValueError("triple_captain must use chip='tc'")
        if self.bench_boost.chip != "bb":
            raise ValueError("bench_boost must use chip='bb'")
        if self.free_hit.chip != "fh":
            raise ValueError("free_hit must use chip='fh'")
        if self.wildcard.chip != "wc":
            raise ValueError("wildcard must use chip='wc'")


# ---------------------------------------------------------------------------
# Diagnostics — RunStatus and friends. Kept here so CachedAnalysisResult can
# embed RunStatus without import cycles. ``fpl.diagnostics`` imports them
# back for rendering / parsing.
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class DataSourceStatus:
    name: SourceName
    status: SourceStatus
    detail: str
    elapsed_ms: int

    def __post_init__(self) -> None:
        if self.elapsed_ms < 0:
            raise ValueError(f"elapsed_ms must be >= 0, got {self.elapsed_ms}")


@dataclass(frozen=True)
class CacheStatus:
    hit: bool
    key: str
    age_seconds: int

    def __post_init__(self) -> None:
        if self.age_seconds < 0:
            raise ValueError(f"age_seconds must be >= 0, got {self.age_seconds}")
        if not self.hit and self.age_seconds != 0:
            raise ValueError(
                "On cache miss, age_seconds must be 0; "
                f"got hit={self.hit}, age_seconds={self.age_seconds}"
            )


@dataclass(frozen=True)
class ModelDiagnostics:
    baseline_mae: float
    top_features: tuple[tuple[str, float], ...]
    weights_used: dict[str, float]


@dataclass(frozen=True)
class RunStatus:
    started_at: datetime
    elapsed_total_ms: int
    package_version: str
    inputs: dict[str, Any]
    sources: tuple[DataSourceStatus, ...]
    cache: CacheStatus
    model: ModelDiagnostics
    warnings: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        if self.elapsed_total_ms < 0:
            raise ValueError(
                f"elapsed_total_ms must be >= 0, got {self.elapsed_total_ms}"
            )
        # Required source-presence matrix is enforced at the call site
        # (build_run_status in fpl.diagnostics) since it depends on which
        # inputs were supplied. Tests in tests/contract/test_diagnostics.py
        # cover all 4 input combinations.


# ---------------------------------------------------------------------------
# Top-level analysis result.
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class CachedAnalysisResult:
    """Returned by ``fpl.run_analysis``.

    Two ``mode`` distinctions per analyze finding I1:

    * :attr:`mode` — legacy literal consumed by ``fpl_gui.py``: either
      ``"from_scratch"`` or ``"analysis"``. ``"analysis"`` here means
      "the run produced a real result"; the GUI's check
      ``result.get('mode') != 'analysis'`` short-circuits when the run
      itself failed.
    * :attr:`primary_view` — the actual mode of the run:
      ``"from_scratch"`` or ``"continuity"`` (FR-021 / FR-022).
    """

    mode: ModeLabel
    primary_view: PrimaryView
    target_gw: int
    horizon: int
    budget: float
    team_id: int | None
    pred_df: Any                            # pd.DataFrame in practice
    current_squad: Squad | None
    current_squad_plan: dict | None         # legacy dict shape consumed by fpl_gui.py
    multi_week_plan: MultiWeekPlan | None
    from_scratch: dict | None               # legacy dict shape consumed by fpl_gui.py
    differential_df: Any | None             # pd.DataFrame | None in practice
    chip_plan: ChipPlan
    run_status: RunStatus

    def __post_init__(self) -> None:
        if not (1 <= self.horizon <= 5):
            raise ValueError(f"horizon must be 1..5, got {self.horizon}")
        if self.budget <= 0:
            raise ValueError(f"budget must be > 0, got {self.budget}")
        if self.target_gw < 1:
            raise ValueError(f"target_gw must be >= 1, got {self.target_gw}")
        if self.team_id is not None and self.team_id <= 0:
            raise ValueError(f"team_id must be > 0 if supplied, got {self.team_id}")
        if self.primary_view == "continuity" and self.team_id is None:
            raise ValueError(
                "primary_view='continuity' requires team_id to be supplied"
            )
        if self.primary_view == "from_scratch" and self.current_squad is not None:
            # We're allowed to embed a from_scratch result inside a continuity
            # run (Wildcard preview) but not the other way round.
            raise ValueError(
                "primary_view='from_scratch' must not carry current_squad"
            )


__all__ = [
    "ChipName",
    "ChipPlan",
    "ChipRecommendation",
    "CachedAnalysisResult",
    "CacheStatus",
    "DataSourceStatus",
    "ModelDiagnostics",
    "ModeLabel",
    "MultiWeekPlan",
    "MultiWeekStep",
    "PrimaryView",
    "Recommendation",
    "RecommendationKind",
    "RunStatus",
    "SourceName",
    "SourceStatus",
    "Squad",
    "TransferLeg",
    "validate_squad_composition",
]
