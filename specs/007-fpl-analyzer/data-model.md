# Data Model: FPL Ultimate Analyzer

**Feature**: 007-fpl-analyzer
**Date**: 2026-05-06

This document gives concrete shapes to the entities listed in `spec.md` § Key Entities. All shapes are Python-side dataclass / pandas-DataFrame definitions used by the `fpl/` package; nothing in this layer is a database schema.

Conventions:
- Snake-case field names match the FPL API where possible to keep the boundary thin.
- "DataFrame" tables list `column → dtype` and are pandas-side.
- Validation rules cite the spec FR they enforce.

---

## 1. `Player`

**One row per Premier League player available for FPL selection.** Sourced from the `bootstrap-static` endpoint, enriched with engineered features and per-GW predictions.

DataFrame `players_df`:

| Column | dtype | Source / notes |
|--------|-------|----------------|
| `player_id` | `int64` | FPL `id`. Primary key. |
| `player_name` | `string` | `web_name`. |
| `team_id` | `int64` | Club the player currently belongs to. |
| `team_name` | `string` | Joined from `teams`. |
| `position_id` | `int8` | 1=GK, 2=DEF, 3=MID, 4=FWD. |
| `position` | `string` | "GK"/"DEF"/"MID"/"FWD". |
| `price` | `float64` | `now_cost / 10` (£m). |
| `selected_by_percent` | `float64` | Ownership %. |
| `chance_of_playing` | `float64` | 0..100; 100 if FPL field is null. |
| `available` | `bool` | `chance_of_playing >= 75`. **FR-010**: rows with `available == False` are excluded from any recommendation or starting XI. |
| `total_points_so_far` | `int64` | Season-to-date. |
| `form` | `float64` | FPL field. |
| `expected_goals_so_far` | `float64` | xG so far (Understat or 0). |
| `expected_assists_so_far` | `float64` | xA so far (Understat or 0). |
| `predicted_gw{N}` | `float64` | Per-GW expected-points predictions for GWs in horizon. **FR-007**. |
| `ceiling_gw{N}` | `float64` | Per-GW q=0.9 quantile prediction. **FR-008**. |
| `goals_rate_gw{N}` | `float64` | Per-GW Poisson-head goal rate. Used by FR-026 (FH swing). |
| `horizon_total` | `float64` | Sum of `predicted_gw{N}` across the planning horizon. |

**Invariants**:
- `(player_id)` is unique.
- `position_id ∈ {1, 2, 3, 4}`.
- `price > 0`.
- For every column `predicted_gw{N}`, the corresponding `ceiling_gw{N} ≥ predicted_gw{N}` (quantile monotonicity, asserted in `tests/unit/test_models.py`).

---

## 2. `Fixture`

**One row per scheduled match.** Used to build per-player per-gameweek fixture features and to detect blanks/doubles.

DataFrame `fixtures_df`:

| Column | dtype | Source / notes |
|--------|-------|----------------|
| `fixture_id` | `int64` | FPL `id`. |
| `gameweek` | `int8` | `event` field. |
| `home_team_id` | `int64` | |
| `away_team_id` | `int64` | |
| `kickoff_time` | `datetime64[ns, UTC]` | Used for **no-look-ahead** filtering in backtests (FR-029). |
| `finished` | `bool` | |
| `home_team_difficulty` | `int8` | 1..5. |
| `away_team_difficulty` | `int8` | 1..5. |

**Derived per (team, gameweek)**:
- `fixture_count(team, gw)` — 0 = blank, 1 = normal, ≥2 = double.

---

## 3. `Gameweek`

**Scheduling window during which fixtures are played and FPL transfers are locked.** One row per gameweek for the current season.

DataFrame `gameweeks_df`:

| Column | dtype | Source / notes |
|--------|-------|----------------|
| `gw` | `int8` | 1..38. Primary key. |
| `deadline_time` | `datetime64[ns, UTC]` | Cutoff for transfers. |
| `is_current` | `bool` | The next not-yet-finished GW. |
| `is_finished` | `bool` | All fixtures in the GW are `finished`. |
| `is_blank_for_team` | `dict[int, bool]` | Cached map team_id → blank flag (built from `fixtures_df`). |
| `is_double_for_team` | `dict[int, bool]` | Same, double flag. |

---

## 4. `Squad`

**A 15-player selection for a single FPL manager.** Built from the `entry/{team_id}/event/{last_finished_gw}/picks/` endpoint or constructed from scratch by the LP optimiser.

Dataclass:

```python
@dataclass(frozen=True)
class Squad:
    player_ids: tuple[int, ...]              # 15 elements; ordered (GKs, DEFs, MIDs, FWDs)
    starting_xi: tuple[int, ...]             # 11 elements; subset of player_ids
    captain_id: int                          # in starting_xi
    vice_captain_id: int                     # in starting_xi, != captain_id
    bank: float                              # £m
    free_transfers: int                      # 0..5 (cap from FR-016)
    chips_used: frozenset[str]               # {"tc", "bb", "fh", "wc1", "wc2"}
    chips_available: frozenset[str]          # complement of chips_used (and not the active chip)
    active_chip: str | None                  # one of "tc"/"bb"/"fh"/"wc" or None
```

**Invariants** (asserted at construction in `Squad.__post_init__`):

- `len(player_ids) == 15`, all distinct.
- Position counts: exactly 2 GK, 5 DEF, 5 MID, 3 FWD (**FR-019**, **SC-006**).
- Max 3 players from any single club (**FR-019**, **SC-006**).
- `len(starting_xi) == 11`, subset of `player_ids`, valid formation: 1 GK; 3 ≤ DEF ≤ 5; 1 ≤ FWD ≤ 3; the remainder MIDs.
- `captain_id ∈ starting_xi`, `vice_captain_id ∈ starting_xi`, `captain_id != vice_captain_id`.
- `bank ≥ 0`.
- `0 ≤ free_transfers ≤ 5` (**FR-016**, **SC-007**).

---

## 5. `Recommendation`

**A single proposed action for a single gameweek.** Returned by `optimizer/transfer.py` for P1 and embedded inside multi-week plan steps for P2.

Dataclass:

```python
@dataclass(frozen=True)
class TransferLeg:
    out_player_id: int
    in_player_id: int
    delta: float                             # in.horizon_total - out.horizon_total

@dataclass(frozen=True)
class Recommendation:
    kind: Literal["hold", "1-transfer", "2-transfer", "3-transfer", "4-transfer"]
    transfers: tuple[TransferLeg, ...]       # empty for "hold"
    hit_cost: int                            # multiple of 4; 0 if covered by FTs
    gain: float                              # net of hit_cost
    new_bank: float
    new_squad: Squad
    captain_id: int
    vice_captain_id: int
    note: str | None = None                  # e.g., "no positive-gain transfer found"
```

**Invariants**:

- `len(transfers) == int(kind.split("-")[0])` for transfer kinds, `0` for hold.
- `hit_cost == max(0, len(transfers) - free_transfers_in) * 4` (**FR-012**, **SC-007**).
- `kind == "hold" ⇒ gain == 0 and hit_cost == 0` (**FR-014**).
- `new_bank ≥ 0` (**SC-007**).

---

## 6. `MultiWeekPlan`

**An ordered sequence of per-gameweek decisions across the horizon.**

Dataclass:

```python
@dataclass(frozen=True)
class MultiWeekStep:
    gw: int
    action: Recommendation                   # the per-week move
    week_predicted_score: float              # XI prediction for that GW
    bank_after: float
    fts_after: int                           # capped at 5 (FR-016)

@dataclass(frozen=True)
class MultiWeekPlan:
    steps: tuple[MultiWeekStep, ...]
    cumulative_hits: int
    total_score: float
    baseline_score: float                    # hold-every-week baseline (FR-017)
    net_gain_vs_baseline: float
    alternatives: tuple["MultiWeekPlan", ...] # up to 3 (FR-018), each with its own steps; alternatives have empty `alternatives`
```

**Invariants**:

- `len(steps) == horizon` (1..5).
- `steps[i].gw == steps[i-1].gw + 1`.
- `steps[i].fts_after ≤ 5` (**FR-016**, **SC-007**).
- `len(alternatives) ≤ 3` (**FR-018**).
- All non-root plans have empty `alternatives` (no recursion).

---

## 7. `ChipPlan`

**Per-chip recommendation for the four FPL chips.**

Dataclass:

```python
@dataclass(frozen=True)
class ChipRecommendation:
    chip: Literal["tc", "bb", "fh", "wc"]
    gw: int | None                           # None ⇒ no positive case in horizon (FR-023)
    supporting_metric: float | None          # ceiling for TC, bench points for BB, swing for FH/WC
    rationale: str                           # short human-readable explanation

@dataclass(frozen=True)
class ChipPlan:
    triple_captain: ChipRecommendation
    bench_boost: ChipRecommendation
    free_hit: ChipRecommendation
    wildcard: ChipRecommendation
```

**Invariants**:

- `gw is None ⟺ supporting_metric is None`. The "no recommendation" case is uniformly representable per **FR-023**.
- TC's `supporting_metric` MUST be sourced from the ceiling head, not the mean head (**FR-024**).

---

## 8. `RunStatus` (backs the Run Status / Diagnostics panel)

**Every analysis run produces one of these.** The CLI prints it as a tabular section; the web UI renders it as a panel; both are required by **FR-038** to be machine-parseable.

Dataclass:

```python
@dataclass(frozen=True)
class DataSourceStatus:
    name: Literal["fpl_bootstrap", "fpl_fixtures", "fpl_picks", "understat"]
    status: Literal["ok", "ok_retried", "skipped_by_toggle", "unavailable", "failed"]
    detail: str                              # e.g., "retried 1× on 503", "Python 3.14 incompatible"
    elapsed_ms: int

@dataclass(frozen=True)
class CacheStatus:
    hit: bool
    key: str                                 # the SHA-1 of the canonical input tuple
    age_seconds: int                         # 0 on miss

@dataclass(frozen=True)
class ModelDiagnostics:
    baseline_mae: float                      # last known held-out MAE for the mean head
    top_features: tuple[tuple[str, float], ...]  # (feature_name, importance), top 10
    weights_used: dict[str, float]           # {xgb: 0.33, lgbm: 0.33, catboost: 0.33}

@dataclass(frozen=True)
class RunStatus:
    started_at: datetime                     # UTC
    elapsed_total_ms: int
    package_version: str
    inputs: dict[str, object]                # canonical input tuple, for traceability
    sources: tuple[DataSourceStatus, ...]    # at least fpl_bootstrap and fpl_fixtures; understat optional
    cache: CacheStatus
    model: ModelDiagnostics
    warnings: tuple[str, ...]                # non-fatal warnings (FR-038 (d))
```

**Invariants**:

- Required source names: `fpl_bootstrap`, `fpl_fixtures` always present. `fpl_picks` present iff `team_id` was supplied. `understat` present whenever it was attempted, regardless of outcome.
- `model.baseline_mae` is the SC-003 / SC-004 indicator surfaced via FR-009.

---

## 9. `CachedAnalysisResult` (top-level run output)

**Returned by `fpl.run_analysis(...)`.** This is what `fpl_gui.py`'s `cached_run_analysis(...)` shim is wrapping.

Dataclass:

```python
@dataclass(frozen=True)
class CachedAnalysisResult:
    mode: Literal["from_scratch", "continuity"]
    target_gw: int
    horizon: int
    budget: float
    team_id: int | None

    # Predictions
    pred_df: pd.DataFrame                    # `Player`-shape

    # Mode-specific outputs
    current_squad: Squad | None              # filled in continuity mode; None for from_scratch
    current_squad_plan: dict | None          # legacy dict shape preserved for fpl_gui.py
    multi_week_plan: MultiWeekPlan | None
    from_scratch: dict | None                # squad + xi + bench (mode-agnostic, used as Wildcard preview)
    differential_df: pd.DataFrame | None     # FR-027
    chip_plan: ChipPlan
    run_status: RunStatus
```

**Compatibility note**: `fpl_gui.py` already accesses fields like `result['mode']`, `result['xi_df']`, `result['squad']`, `result['differential_df']`, `result['chip_plan']`, `result['multi_week_plan']`, `result['top_features']`, `result['baseline_mae']`, `result['model_weights']`. To keep the shim untouched, `run_analysis` returns a `dict`-compatible mapping (e.g., a `TypedDict` or a `Mapping` proxy over the dataclass) that exposes both the typed fields above and the legacy keys the GUI expects. Contract tests in `tests/contract/test_package_api.py` lock both views.
