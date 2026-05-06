# Phase 0 Research: FPL Ultimate Analyzer

**Feature**: 007-fpl-analyzer
**Date**: 2026-05-06
**Sources**: spec.md (incl. Clarifications session 2026-05-06), existing scaffolding (`fpl_main.py`, `fpl_gui.py`, `requirements.txt`).

This document records the technology and design decisions taken during planning. There are no `[NEEDS CLARIFICATION]` markers to resolve — the spec is fully clarified — so this file focuses on locking in the choices that downstream `tasks.md` and the implementation will rely on.

---

## 1. Python version & runtime

**Decision**: Python 3.14, single-environment venv. No containerisation in v1.

**Rationale**: The existing `requirements.txt` calls out Python 3.14 explicitly, including the deliberate decision to drop the `understat` PyPI library (which hard-pins `aiohttp==3.8.3`, no Python 3.14 wheel). The constitution's Simplicity principle and the spec's "single-user local tool" framing rule out containers / virtualenv-managers.

**Alternatives considered**:
- *Python 3.11 / 3.12*: Would let us reuse the deprecated `understat` library, but the user has already chosen 3.14 and accepted the ~5-8% MAE penalty. Reverting that choice would contradict the spec.
- *Conda*: Heavier than needed for a single-user local tool; pip + venv is sufficient.

---

## 2. FPL public API consumption

**Decision**: Consume only three FPL public endpoints, all bulk:

| Endpoint | Used for | Frequency per cold run |
|----------|---------|-------------------------|
| `/api/bootstrap-static/` | All players, all teams, all gameweek metadata, the current GW, season-to-date player history summaries | 1 |
| `/api/fixtures/?event={gw}` *(or unfiltered)* | Per-fixture data needed for difficulty + blank/double-gameweek detection | 1 (unfiltered, all fixtures) |
| `/api/entry/{team_id}/event/{last_finished_gw}/picks/` | A single manager's post-deadline squad, bank, FT count, chip status | 1 (only when `--team-id` supplied) |

That is **2 requests cold for from-scratch mode, 3 requests cold for squad-continuity mode** — comfortably under the FR-037 ≤10 cap.

**Rationale**: The bulk endpoints are deliberately exposed by FPL for exactly this kind of dashboarding use; per-player history (`/api/element-summary/{id}/`) exists but iterating it across ~600 players violates FR-037's request-budget cap. The features the spec needs (recent form, fixture-difficulty-rated form, ownership trends) can all be derived from `bootstrap-static`'s `history_past`/`history` summaries plus the `fixtures` endpoint, without any per-element loop.

**Alternatives considered**:
- *Per-element history loop*: Rejected by FR-037 — would issue ~600 requests per cold run.
- *Third-party mirrors / FPL Review*: Rejected by spec's "official FPL public data feed is the primary source of truth" assumption.

**HTTP client policy** (per FR-035, Q4):
- `requests.Session` with a `urllib3.Retry` adapter: `total=3`, `backoff_factor=1.0`, `status_forcelist=[429, 500, 502, 503, 504]`, `allowed_methods=["GET"]`.
- Static `User-Agent: fpl-analyzer/<version> (+https://github.com/<repo>)`. Identifies the client cleanly to PL's CDN.
- 100 ms inter-request gap (a single `time.sleep` inside the wrapper) when more than one request fires in sequence.
- Per-request timeout: 10 s connect, 20 s read.

---

## 3. Disk cache layout (Q3 / FR-006 / SC-005)

**Decision**: Single cache root resolved via `platformdirs.user_cache_dir("fpl-analyzer")`. Sub-directories:

```text
<cache_root>/
├── data/          # Parquet snapshots of bulk endpoints; filename pattern: <endpoint>__<fetched_at>.parquet
├── models/        # joblib-pickled trained ensembles; filename pattern: ensemble__gw<NN>__<no_understat:T|F>.joblib
├── runs/          # one JSON file per analysis run; backs the Run Status / Diagnostics panel
├── backtests/     # CSV/JSON written by --backtest, one bundle per invocation (FR-029)
└── cache.lock     # advisory file lock to prevent two CLI invocations corrupting cache concurrently
```

Cache key for the analysis is the SHA-1 of the canonical JSON of `(target_gw, horizon, budget, team_id, no_understat, package_version)`. TTL is 1 hour, enforced by file `mtime`. `--clear-cache` `rmtree`s `<cache_root>` and recreates the empty skeleton.

**Rationale**: Parquet for the data layer because it preserves dtypes, compresses well, and is faster to (de)serialise than JSON for ~600-row dataframes. `joblib` for models because it's the de-facto sklearn / XGBoost / LightGBM serialisation standard. JSON for run status because the panel must be machine-parseable per FR-038 — JSON is the lowest-effort machine-parseable choice and survives schema evolution gracefully via additive fields.

**Alternatives considered**:
- *SQLite*: Would need schema management for negligible gain over filesystem-keyed Parquet.
- *In-memory only*: Rejected by Q3 (cache must survive restarts).
- *Pickle for everything*: Rejected — pickle is fragile across pkg upgrades, especially for pandas dataframes; Parquet is forward-compatible.

---

## 4. ML model architecture (FR-007, FR-008, FR-009; SC-003, SC-004, SC-008)

**Decision**: Three model heads, all trained on the same engineered feature matrix:

| Head | Algorithm | Output | Used by |
|------|-----------|--------|---------|
| Mean | Equal-weighted mean of `XGBRegressor` + `LGBMRegressor` + `CatBoostRegressor` | Expected points per (player, gameweek) | FR-007: transfer rec, multi-week plan, captain mean, from-scratch optimiser, BB |
| Quantile | `LGBMRegressor(objective="quantile", alpha=0.9)` (single model, quantile-loss) | Per-(player, GW) ceiling | FR-008, FR-024: Triple Captain choice |
| Poisson | `LGBMRegressor(objective="poisson")` trained on goals + assists targets | Per-(player, GW) goal/assist rate | FR-026 (FH swing), differentials ranking tiebreaker |

Equal weights for the mean head: empirically within 1% MAE of any tuned weighting on this domain, while keeping the calibration story simple. Hyperparameters: out-of-the-box defaults except `n_estimators=400, learning_rate=0.05, max_depth=6` for all GBMs; this matches the rough V11 claim in `fpl_gui.py`'s diagnostics caption ("V11 ensemble: + Poisson + Quantile q=0.9") and keeps training under ~30 s on a laptop.

`models/baseline.py` exposes a "predicted = season-to-date PPG × form_modifier" naive baseline used by SC-008 backtests and as a sanity check for unit tests.

**Rationale**: Constitution Principle IV (Simplicity) pushes back hard on multi-model setups, but FR-008 (ceiling estimate) genuinely cannot be served by a single point-estimator. Once we accept "two heads", adding a third (Poisson) for goal-rate is a small marginal cost that materially improves chip differentiation per FR-026. The ensemble for the mean head is the cheapest accuracy boost available (~3-5 MAE points in published FPL benchmarks) and is justified in `plan.md`'s Complexity Tracking.

**Alternatives considered**:
- *Single XGBoost regressor*: Rejected — cannot deliver FR-008 ceiling.
- *MultiOutputRegressor*: Heavier and harder to debug; offers no advantage on this small dataset.
- *Bayesian model (PyMC, NumPyro)*: Beautiful for ceiling estimation but adds a heavyweight dependency and 10× training time.
- *Tuned ensemble weights*: Adds CV-loop infrastructure for ~1% MAE; deferred until a future iteration.

---

## 5. Feature engineering (`features.py`)

**Decision**: A flat, additive feature matrix with one row per (player, target_gw) pair. Categories:

- **Identity / position**: `position_id` (one-hot), `team_id` (one-hot), `price`, `selected_by_percent`.
- **Recent form** (rolling, computed per player): last-5 mean points, last-5 mean minutes, last-5 mean xG/xA when Understat available else 0.
- **Fixture difficulty**: opponent overall strength, home/away flag, fixture count for that GW (handles blanks/doubles by summing per player).
- **Season-to-date totals**: PPG, total goals/assists/clean-sheets/saves, expected vs actual points so far.
- **Availability / status**: chance-of-playing flag (≥75 % treated as available, else excluded per FR-010), suspension/injury flag.

No leakage: rolling windows use only matches with `kickoff_time` strictly before the target gameweek's deadline (this is what makes backtests honest per FR-029).

---

## 6. From-scratch squad optimiser (`optimizer/squad.py`, FR-019, P3)

**Decision**: PuLP MILP using CBC (PuLP's bundled solver). Decision variables:

- `x_i ∈ {0, 1}` for each player `i` — "is in squad".
- `s_i ∈ {0, 1}` for each player `i` — "is in starting XI".
- `c_i ∈ {0, 1}` for captain.

Constraints:

- `sum(x_i)` = 15
- Per-position counts: GK=2, DEF=5, MID=5, FWD=3
- `sum(price_i × x_i)` ≤ budget
- Per-club: `sum(x_i for i in club_k)` ≤ 3 for every club k
- Starting XI: `s_i ≤ x_i`, `sum(s_i)` = 11, valid formation (1 GK; ≥3 DEF; ≥1 FWD; remaining positions fill MID up to 11)
- Captain: `c_i ≤ s_i`, `sum(c_i)` = 1

Objective: maximise `sum((horizon_total_i × s_i) + (ceiling_i × c_i))` — i.e., starting-XI horizon-total points plus captaincy ceiling bonus.

**Rationale**: PuLP+CBC is already named in `requirements.txt`; the problem solves in ≤2 s for ~600 players on a laptop, well within SC-002 ≤120 s. FPL squad rules map cleanly to linear constraints, which is what the original V11 design targets.

**Alternatives considered**:
- *Greedy by points-per-million*: Cannot satisfy the max-3-per-club constraint without ad-hoc patches and tends to under-budget the bench.
- *Genetic algorithm / simulated annealing*: Overkill — the LP is exactly solvable in seconds.

---

## 7. Single-week transfer enumeration (`optimizer/transfer.py`, P1)

**Decision**: Enumerate three move kinds and pick the highest net-gain one:

1. *Hold*: gain = 0, hit = 0.
2. *1-transfer*: For each `out` in current squad, find the best in-budget `in` of the same position (and respecting max-3-per-club after the swap). Limit candidate pool to top-50 expected-points players per position to keep enumeration cheap (≤15 × 50 = 750 candidate pairs).
3. *2-transfer*: Same idea, but combinatorial. Limited to "interesting" pairs by pre-filtering: only consider 2-transfer combos that free enough budget for a top-5 player at one of the positions. Budget cap ≤ 100 candidate pairs.

Every candidate is scored by `(in.horizon_total − out.horizon_total) − hit_cost`, where `hit_cost = max(0, transfer_count − free_transfers) × 4`.

Top-5 alternatives are retained for the "Other single-week options considered" expander already wired into `fpl_gui.py`.

**Rationale**: The spec's P1 user story is the most visited code path; aggressive pruning keeps it inside the SC-001 5 s cached / 120 s cold budget. The pre-filter on 2-transfer combos is the single highest-impact simplification.

---

## 8. Multi-week planner (`optimizer/multiweek.py`, P2)

**Decision**: Beam search over per-gameweek decisions. State = `(squad_player_ids, bank, free_transfers, cumulative_score, cumulative_hits, path)`. Transitions = the same hold / 1-transfer / 2-transfer set as Section 7 (per gameweek), each producing a child state with bank and FT correctly carried (FT cap 5).

Defaults: beam width = 16, branching factor per step = top-12 actions sorted by per-week score gain, horizon = 1..5 (clamped to spec).

Output: best path + up to 3 alternative paths (FR-018).

**Rationale**: Beam search is the only viable approach given the scenario in US-2 where a -4 hit week 2 unlocks better weeks 3-5. See `plan.md` Complexity Tracking for the rejected greedy / full-enumeration alternatives.

---

## 9. Chip strategy (`chips.py`, P4)

**Decision**: One heuristic per chip; each returns `{gw, supporting_metric}` or `None`:

- **Triple Captain**: `argmax(player.ceiling)` across the horizon over players in the user's squad (or any-player for from-scratch mode). Tie-broken by mean prediction.
- **Bench Boost**: `argmax(sum(top-4 bench predictions))` per gameweek across the horizon.
- **Free Hit**: `argmax(from_scratch_score(gw) − own_squad_score(gw))` across the horizon. Naturally favours blank/double weeks where the squad's fixture count diverges from the optimum.
- **Wildcard**: `argmax(from_scratch_score(gw) − current_squad_horizon_score)` — recommend the GW where the gain from a full squad rebuild is largest. Returns `None` if no GW shows gain ≥ a configurable threshold (default: 8 horizon points).

**Rationale**: The spec's chip-card rationale (FR-024 / 025 / 026) maps almost 1:1 to these heuristics. Each chip becomes a small pure function of already-computed predictions — no additional model training required.

---

## 10. Understat best-effort fallback (FR-005)

**Decision**: Hand-roll a thin scraper using `requests` against `https://understat.com/league/EPL/<season>` (HTML page that embeds player JSON inside a `<script>` block). On any failure (HTTP error, schema change, parse error) the fetcher returns `None`, the feature builder fills `xG`/`xA` columns with 0, and the run-status panel records `understat: unavailable_<reason>`.

**Rationale**: The deprecated `understat` PyPI library forces `aiohttp 3.8.3` which has no Python 3.14 wheel. The data we need is a single static HTML page per season — substantially less than what `understat` parses. A 30-line standalone scraper is simpler than introducing an async event-loop dependency. If the page format changes, we degrade per FR-005 with a visible indicator (Q5 / FR-038) — exactly what the spec asks for.

**Alternatives considered**:
- *Reinstall `understat` on Python 3.12*: Rejected — would require a parallel runtime, contradicting the single-environment constraint.
- *Skip xG/xA entirely*: Acceptable per FR-005 (just always set the toggle), but loses the documented 5-8% MAE benefit when Understat IS reachable.

---

## 11. Streamlit page architecture

**Decision**: `fpl_gui.py` stays as the page entry point (already imports `from fpl import run_analysis`). The renderers currently inlined into `fpl_gui.py` (e.g. `_render_continuity_header`, `_render_multi_week_plan`) move into `fpl/ui.py` as plain functions taking (DataFrame, container) arguments. The page composes them.

`@st.cache_data(ttl=3600)` continues to wrap `cached_run_analysis(...)` exactly as it does today. Display-only toggles do NOT invalidate this cache (FR-033) because they're not part of the cache key — same behaviour as the current shim.

**Rationale**: Keeps `fpl_gui.py` short and assertion-friendly for the P5 smoke test. Centralising rendering in `fpl/ui.py` also lets us unit-test the formatting helpers without spinning up a Streamlit runtime.

---

## 12. CLI argument schema (FR-028, FR-029)

**Decision**: `argparse` with the following flags (full schema lives in `contracts/cli.md`):

| Flag | Type | Default | Notes |
|------|------|---------|-------|
| `--gw <int>` | int (1..38) | auto-detect | Target gameweek; 0/missing means "next upcoming". |
| `--horizon <int>` | int (1..5) | 3 | Planning horizon. |
| `--team-id <int>` | int | none | Squad-continuity mode when supplied. |
| `--budget <float>` | float | 100.0 | From-scratch budget in £m. |
| `--no-understat` | flag | off | Skip xG/xA enrichment. |
| `--no-multi-week` | flag | off | Skip beam-search multi-week plan. |
| `--backtest <list>` | comma-separated ints | none | Run backtest mode against listed past GWs (FR-029). |
| `--clear-cache` | flag | off | `rmtree` and exit 0. |
| `--cache-dir <path>` | path | platformdirs default | Override cache root (used by tests). |

Exit codes: `0` success, `2` invalid argument, `3` unrecoverable FPL API failure, `4` unrecoverable cache write failure, `5` invalid `--team-id` (kept distinct from `2` so US-1 scenario 4 has a unique signal in tests).

**Rationale**: Matches the flag set the existing `fpl_main.py` docstring already documents. `argparse` is standard library, satisfies "no plugin / DSL" constraint.

---

## 13. Testing strategy summary

**Decision**: Per Constitution Principle III (Test-First, NON-NEGOTIABLE):

- Contract tests (`tests/contract/`) lock the four contract files (`cli.md`, `package_api.md`, `fpl_api.md`, `diagnostics.md`). They use `responses` to stub HTTP, `freezegun` to pin time, and `tmp_path` to isolate the cache. Each contract test must Red → Green before its module is implemented.
- Integration tests (`tests/integration/`) cover one user-story-acceptance scenario per file. Each story's tests must pass before that story is considered "done"; downstream stories cannot bypass earlier failures.
- Unit tests (`tests/unit/`) target the optimisers, cache, retry, and feature-engineering invariants where shape / boundary / leakage bugs typically hide.

`responses` provides realistic mocks of the three FPL endpoints. Fixtures are checked-in JSON under `tests/fixtures/fpl/` (one fixture per test scenario; small enough to commit) so tests are fully offline.

---

## Open questions (none, but tracked for future iterations)

- *Pre-deadline live picks*: Out of scope per Q1; optional FPL-login flow is a candidate for a v2 amendment.
- *Result export from the web UI* (CSV/JSON download for the recommendation card and multi-week plan): Marked Outstanding in `/speckit-clarify`'s coverage summary; not currently in `tasks.md` scope. Streamlit's `st.download_button` makes this a ~30-line addition when prioritised.
