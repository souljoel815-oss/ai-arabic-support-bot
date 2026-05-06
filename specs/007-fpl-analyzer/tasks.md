---

description: "Task list for FPL Ultimate Analyzer (007-fpl-analyzer)"
---

# Tasks: FPL Ultimate Analyzer

**Input**: Design documents from `/specs/007-fpl-analyzer/`
**Prerequisites**: spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: IN SCOPE. The constitution's Principle III (Test-First Discipline, NON-NEGOTIABLE) and `plan.md`'s Constitution Check require Red → Green for every module. Tests precede implementation; the test task and its paired implementation task share the same `[USx]` label and are sequenced test-first within each phase.

**Organization**: Tasks are grouped by user story (P1–P5 per spec.md). Each user-story phase is independently testable and demonstrably valuable on its own per Constitution Principle V.

## Format: `- [ ] [TaskID] [P?] [Story?] Description with file path`

- `[P]` — Different file, no dependency on an incomplete task in this phase. Safe to run in parallel.
- `[USx]` — Belongs to user story `x` (US1 = P1, US2 = P2, etc.).
- File paths are project-relative (relative to repo root).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialise the project skeleton inside `specs/007-fpl-analyzer/` so subsequent phases have somewhere to import from.

- [X] T001 Create the package + tests directory skeleton at `specs/007-fpl-analyzer/`: create empty `fpl/__init__.py`, `fpl/models/__init__.py`, `fpl/optimizer/__init__.py`, `tests/__init__.py`, `tests/conftest.py`, `tests/contract/__init__.py`, `tests/integration/__init__.py`, `tests/unit/__init__.py`, `tests/fixtures/fpl/.gitkeep`, `tests/fixtures/understat/.gitkeep`
- [X] T002 Add `specs/007-fpl-analyzer/requirements-dev.txt` listing test dependencies: `pytest>=8`, `responses>=0.25`, `freezegun>=1.5`, `pytest-cov>=5`, `streamlit>=1.28` (for the P5 AppTest harness)
- [X] T003 [P] Add `specs/007-fpl-analyzer/pytest.ini` with `[pytest] testpaths = tests`, `addopts = -q --strict-markers`, and markers `contract`, `integration`, `unit`
- [X] T004 [P] Add `specs/007-fpl-analyzer/fpl/_version.py` with `__version__ = "0.1.0"` and re-export from `fpl/__init__.py`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement the shared modules every user story depends on: error classes, the typed dataclass layer, the FPL API client, the disk cache, the Run Status / Diagnostics surface, and the feature engineering pipeline.

**⚠️ CRITICAL**: No user-story phase can begin until this phase is complete. Within this phase, every implementation task is preceded by its test task (Red → Green per Constitution Principle III).

### Test fixtures (parallel, no internal deps)

- [X] T005 [P] Create representative bootstrap-static fixture at `specs/007-fpl-analyzer/tests/fixtures/fpl/bootstrap_static.json` — 4 events spanning a current/finished/upcoming mix, 3 teams, 8 elements covering all 4 positions, with `history_past`, `chance_of_playing_next_round`, `expected_goals`, `expected_assists`, `selected_by_percent` populated
- [X] T006 [P] Create fixtures fixture at `specs/007-fpl-analyzer/tests/fixtures/fpl/fixtures.json` — 6 fixtures across 2 GWs, including one DGW for one team and one blank for another
- [X] T007 [P] Create picks fixture at `specs/007-fpl-analyzer/tests/fixtures/fpl/picks_team_12345_gw30.json` — 15 picks (2 GK / 5 DEF / 5 MID / 3 FWD), captain + vice flagged, `entry_history` with bank/value/total_transfers
- [X] T008 [P] Create Understat fixture at `specs/007-fpl-analyzer/tests/fixtures/understat/epl_2025.html` — minimal HTML containing a `<script>` block with `playersData` JSON for the 8 players in `bootstrap_static.json` (xG, xA, time)

### Errors module (no test needed — pure exception classes)

- [X] T009 [P] Implement `specs/007-fpl-analyzer/fpl/errors.py` defining `UnknownTeamId`, `FPLApiError`, `CacheWriteError` exceptions per `contracts/package_api.md` § Errors

### Typed dataclass layer (test-first)

- [X] T010 [P] Write `specs/007-fpl-analyzer/tests/unit/test_squad_invariants.py` — Red — assert `Squad.__post_init__` rejects: wrong total count, wrong position split, >3 per club, invalid XI formation, captain == vice (FR-019, SC-006, SC-007). Use small synthetic `player_ids` lists.
- [X] T011 Implement `specs/007-fpl-analyzer/fpl/types.py` with `TransferLeg`, `Squad`, `Recommendation`, `MultiWeekStep`, `MultiWeekPlan`, `ChipRecommendation`, `ChipPlan`, `CachedAnalysisResult` dataclasses + `__post_init__` invariant checks per `data-model.md` §§ 4-7, 9. Passes T010 Green.

### FPL API client (test-first)

- [X] T012 [P] Write `specs/007-fpl-analyzer/tests/contract/test_fpl_api.py` — Red — locks `contracts/fpl_api.md`: assert that `fpl.api.fetch_bootstrap_static`, `fetch_fixtures`, `fetch_team_picks(team_id, gw)`, `fetch_understat_optional(season)` exist; the User-Agent header matches `fpl-analyzer/<version>`; only the listed endpoints are called; field subset consumed matches the contract; cold-run total request count ≤ 4 with all four sources or ≤ 2 without picks/Understat (FR-037)
- [X] T013 [P] Write `specs/007-fpl-analyzer/tests/unit/test_api_retry.py` — Red — using `responses`, assert exponential backoff on 429/5xx with `total=3, backoff_factor=1.0`; final `FPLApiError` after exhaustion; non-retried 4xx (other than 429) raises immediately; 404 on the picks endpoint raises `UnknownTeamId`
- [X] T014 Implement `specs/007-fpl-analyzer/fpl/api.py`: `requests.Session` + `urllib3.Retry` adapter, descriptive User-Agent header sourced from `fpl._version.__version__`, 100 ms inter-request gap, 10 s connect / 20 s read timeouts, four bulk fetchers (`fetch_bootstrap_static`, `fetch_fixtures`, `fetch_team_picks`, `fetch_understat_optional`). Passes T012 + T013 Green.

### Disk cache (test-first)

- [X] T015 [P] Write `specs/007-fpl-analyzer/tests/unit/test_cache.py` — Red — using `freezegun` and `tmp_path`: assert TTL expiry at 1 h, restart-survival within TTL (re-instantiate cache and re-read), SHA-1 cache-key derivation from canonical input tuple, `clear_cache()` removes the directory and recreates skeleton, `cache.lock` blocks a second writer attempting to write the same key concurrently
- [X] T016 Implement `specs/007-fpl-analyzer/fpl/cache.py`: resolve cache root via `platformdirs.user_cache_dir("fpl-analyzer")` (overridable via `cache_dir` arg used in tests); read/write Parquet (data), joblib (models), JSON (runs); SHA-1 key from canonical-JSON of `(target_gw, horizon, budget, team_id, no_understat, package_version)`; advisory `cache.lock` file. Passes T015 Green.

### Run Status / Diagnostics (test-first)

- [X] T017 [P] Write `specs/007-fpl-analyzer/tests/contract/test_diagnostics.py` — Red — locks `contracts/diagnostics.md`: assert `render_cli_block(rs)` produces the exact key order documented; `parse_status_block(text)` round-trips back to a structurally-equal `RunStatus`; on-disk JSON conforms to the schema in the contract; required-source presence matrix holds across the 4 input combinations (with/without team_id × with/without Understat)
- [X] T018 Implement `specs/007-fpl-analyzer/fpl/diagnostics.py`: `DataSourceStatus`, `CacheStatus`, `ModelDiagnostics`, `RunStatus` dataclasses; `render_cli_block(rs) -> str`; `parse_status_block(text) -> RunStatus`; `write_json_run(rs, path)`. Passes T017 Green.

### Feature engineering (test-first)

- [X] T019 [P] Write `specs/007-fpl-analyzer/tests/unit/test_features.py` — Red — assert `build_feature_matrix(players_df, fixtures_df, target_gw, horizon, understat_df=None) -> pd.DataFrame` produces one row per (player, target_gw); columns match research.md § 5; rolling windows respect target deadline (no leakage — pin `kickoff_time`, query a past GW, assert no future fixture leaks in)
- [X] T020 Implement `specs/007-fpl-analyzer/fpl/features.py` per research.md § 5. Passes T019 Green.

### Foundation export

- [X] T021 Update `specs/007-fpl-analyzer/fpl/__init__.py` to re-export `__version__` and a placeholder `run_analysis` raising `NotImplementedError("US1 not yet implemented")` — lets contract tests for `package_api.md` import the symbol even before US1 is built

**Checkpoint**: Foundation ready. User Stories 1-5 may now begin in any order (or in parallel by separate developers).

---

## Phase 3: User Story 1 — Weekly transfer & captain decision (Priority: P1) 🎯 MVP

**Goal**: Given an FPL Team ID and a target gameweek, return a single recommendation card (HOLD or N-transfer + captain) with net gain, hit cost, and bank-after — accessible from the CLI.

**Independent Test**: `python fpl_main.py --team-id 12345 --gw 30 --no-multi-week` prints `=== RUN STATUS ===` + `=== RECOMMENDATION ===` + `=== CHIP PLAN ===` + `=== DIFFERENTIALS ===` blocks. The recommendation block contains `kind`, `gain`, `hit_cost`, `captain`, `vice_captain`, and (if a transfer) `transfers`. All four US-1 acceptance scenarios pass.

### Tests for User Story 1

- [X] T022 [P] [US1] Write `specs/007-fpl-analyzer/tests/contract/test_cli.py` — Red — locks `contracts/cli.md`: assert each documented flag is parsed; mutually-exclusive combinations exit 2; exit codes 0/2/3/4/5 match; in analysis mode the stdout block order is RUN STATUS → RECOMMENDATION → MULTI-WEEK PLAN → CHIP PLAN → DIFFERENTIALS; `--no-multi-week` omits the MULTI-WEEK PLAN block
- [X] T023 [P] [US1] Write `specs/007-fpl-analyzer/tests/contract/test_package_api.py` — Red — locks `contracts/package_api.md`: assert the `fpl.run_analysis(...)` keyword-only signature; assert the returned mapping exposes both the typed-field accessors AND every legacy key consumed by `fpl_gui.py` (`'mode'`, `'gw'`, `'pred_df'`, `'differential_df'`, `'multi_gw_outlook'`, `'squad'`, `'current_squad_plan'`, `'multi_week_plan'`, `'chip_plan'`, `'top_features'`, `'baseline_mae'`, `'model_weights'`, `'primary_view'`)
- [X] T024 [P] [US1] Write `specs/007-fpl-analyzer/tests/integration/test_p1_weekly_rec.py` — Red — covers all 4 US-1 acceptance scenarios using the fixtures from T005-T008: (1) HOLD or transfer with gain/hit/captain; (2) HOLD when no positive-gain transfer exists, with note; (3) net-of-hit gain shown when 0 FTs; (4) invalid Team ID → `UnknownTeamId` from package, exit 5 from CLI, with fallback-to-from-scratch suggestion message
- [X] T025 [P] [US1] Write `specs/007-fpl-analyzer/tests/unit/test_models.py` — Red — assert: ensemble mean-head output has shape `(n_players, horizon)`; quantile head ≥ mean head element-wise (FR-008 ceiling monotonicity); Poisson head non-negative; baseline-vs-ensemble MAE on the fixture set is reasonable

### Implementation for User Story 1

- [X] T026 [P] [US1] Implement `specs/007-fpl-analyzer/fpl/models/baseline.py` — naive baseline (`predicted = season_PPG × form_modifier`) with `train(X, y)` and `predict(X) -> np.ndarray`. Used by T025 and SC-008 backtest comparison
- [X] T027 [US1] Implement `specs/007-fpl-analyzer/fpl/models/ensemble.py` with three trained heads per research.md § 4: mean = equal-weighted mean of `XGBRegressor + LGBMRegressor + CatBoostRegressor` (n_estimators=400, learning_rate=0.05, max_depth=6); quantile = `LGBMRegressor(objective="quantile", alpha=0.9)`; Poisson = `LGBMRegressor(objective="poisson")`. Public API: `Ensemble.train(X, y)`, `Ensemble.predict_mean(X)`, `Ensemble.predict_ceiling(X)`, `Ensemble.predict_goal_rate(X)`. Passes T025 Green.
- [X] T028 [US1] Implement `specs/007-fpl-analyzer/fpl/optimizer/transfer.py` per research.md § 7: enumerate hold / 1-transfer / 2-transfer; pre-filter candidate pool to top-50 per position; pre-filter 2-transfer combos by free-budget heuristic; score each candidate by net gain after hits; return a `Recommendation` plus up to 5 alternatives (FR-013)
- [X] T029 [US1] Implement `specs/007-fpl-analyzer/fpl/run.py`'s `run_analysis(...)` for the P1-only path: validate args; resolve cache; if hit, load cached result and rebuild RunStatus; if miss, fetch (api), build features (features), train/predict (models), run transfer-optimizer (optimizer.transfer), assemble RunStatus, persist to cache, return mapping. Multi-week / chips / from-scratch branches return `None`/placeholders; differentials filter on `selected_by_percent < 10`. Passes T023 + T024 Green for P1 scenarios.
- [X] T030 [US1] Implement `specs/007-fpl-analyzer/fpl/cli.py` `main(argv=None) -> int` per `contracts/cli.md`: argparse with all flags; mutually-exclusive validation; invokes `run_analysis` (or backtest path — placeholder until Phase 8); prints `=== RUN STATUS ===` and `=== RECOMMENDATION ===` and `=== CHIP PLAN ===` and `=== DIFFERENTIALS ===` blocks; maps exceptions to exit codes. Passes T022 Green.
- [X] T031 [US1] Wire `specs/007-fpl-analyzer/fpl/__init__.py` to export the real `run_analysis` from `fpl.run`. Verify `python fpl_main.py --team-id 12345 --gw 30 --no-multi-week` works end-to-end against the fixtures in T005-T008 (mocked via responses inside the integration test).

**Checkpoint**: P1 fully functional and demonstrable. MVP shippable.

---

## Phase 4: User Story 2 — Multi-week transfer plan (Priority: P2)

**Goal**: For a given Team ID and horizon (1-5), return a per-gameweek plan with action, transfers, hit cost, weekly predicted score, bank-after, and FTs-after, plus up to 3 alternative paths.

**Independent Test**: `python fpl_main.py --team-id 12345 --gw 30 --horizon 4` prints a `=== MULTI-WEEK PLAN ===` block with 4 rows; FT cap=5 honoured; bank/FT carry correctly between rows; the 3 US-2 acceptance scenarios pass.

### Tests for User Story 2

- [ ] T032 [P] [US2] Write `specs/007-fpl-analyzer/tests/unit/test_multiweek.py` — Red — assert beam-search invariants: every state's `fts_after ≤ 5` (FR-016, SC-007); `bank_after ≥ 0`; `hit_cost` is a multiple of 4; `len(alternatives) ≤ 3` (FR-018); `len(steps) == horizon`; consecutive steps' `gw` increments by 1
- [ ] T033 [P] [US2] Write `specs/007-fpl-analyzer/tests/integration/test_p2_multiweek_plan.py` — Red — covers all 3 US-2 acceptance scenarios: (1) horizon-4 with FT carry-over; (2) -4 hit week 2 chosen when cumulative net gain is positive; (3) DGW/blank reflected in weekly predictions (use the DGW row in the T006 fixture)

### Implementation for User Story 2

- [ ] T034 [US2] Implement `specs/007-fpl-analyzer/fpl/optimizer/multiweek.py` per research.md § 8: beam search with width=16, branching factor 12 actions/step (hold + top-N 1-transfer + top-M 2-transfer), state = `(squad_player_ids, bank, free_transfers, cumulative_score, cumulative_hits, path)`. Returns `MultiWeekPlan` with up to 3 alternatives. Passes T032 Green.
- [ ] T035 [US2] Update `specs/007-fpl-analyzer/fpl/run.py` to invoke `optimizer.multiweek.beam_search(...)` when `team_id is not None` and `not no_multi_week`; populate `result['multi_week_plan']` and the typed `MultiWeekPlan` field. Passes T033 Green.
- [ ] T036 [US2] Update `specs/007-fpl-analyzer/fpl/cli.py` to render the `=== MULTI-WEEK PLAN ===` block per `contracts/cli.md` when the plan is present. Re-run T022 — the new block becomes part of the locked stdout layout.

**Checkpoint**: P1 + P2 both work, independently testable.

---

## Phase 5: User Story 3 — From-scratch squad / Wildcard preview (Priority: P3)

**Goal**: With a budget (default £100m) and target gameweek, construct a complete legal 15-man squad satisfying all FPL rules; expose it as a Wildcard preview alongside continuity-mode results.

**Independent Test**: `python fpl_main.py --gw 1 --budget 100 --no-multi-week` (no team-id) prints a from-scratch squad with exactly 15 players (2/5/5/3 by position), ≤3 per club, ≤£100m, plus a starting XI and captain. The 3 US-3 acceptance scenarios pass.

### Tests for User Story 3

- [ ] T037 [P] [US3] Write `specs/007-fpl-analyzer/tests/unit/test_squad_optimizer.py` — Red — assert `optimize_from_scratch(pred_df, budget=100.0) -> Squad` returns a `Squad` whose every invariant in T011 holds, plus: total `price ≤ budget`, exactly 15 distinct players, exactly 2 GK / 5 DEF / 5 MID / 3 FWD, ≤3 per club (FR-019, SC-006); the chosen XI is a valid formation; the captain is in XI
- [ ] T038 [P] [US3] Write `specs/007-fpl-analyzer/tests/integration/test_p3_from_scratch.py` — Red — covers all 3 US-3 acceptance scenarios: (1) £100m + current GW → legal squad; (2) returned result includes both 15-man and starting XI with captain/vice; (3) when team_id IS supplied, the same `from_scratch` view is included as a Wildcard preview without overwriting continuity-mode fields

### Implementation for User Story 3

- [ ] T039 [US3] Implement `specs/007-fpl-analyzer/fpl/optimizer/squad.py` per research.md § 6: PuLP MILP with binary variables `x_i, s_i, c_i`; FPL composition + budget + per-club + XI formation + captain constraints; objective `sum(horizon_total * s) + sum(ceiling * c)`. Public API: `optimize_from_scratch(pred_df, budget=100.0) -> Squad`. Passes T037 Green.
- [ ] T040 [US3] Update `specs/007-fpl-analyzer/fpl/run.py`: when `team_id is None`, drive the from-scratch path; when `team_id is not None`, additionally compute the from-scratch Wildcard-preview squad and include it under `result['from_scratch']` (legacy key) without disturbing continuity-mode fields. Passes T038 Green.

**Checkpoint**: P1 + P2 + P3 all work independently. The CLI now serves from-scratch use without a Team ID.

---

## Phase 6: User Story 4 — Chip-strategy timing recommendations (Priority: P4)

**Goal**: For each of TC, BB, FH, WC, return a target gameweek + supporting metric, or "no recommendation" if no positive case exists in the horizon.

**Independent Test**: `python fpl_main.py --team-id 12345 --gw 30 --horizon 4` prints a `=== CHIP PLAN ===` block with one row per chip; BB prefers DGWs (per the T006 fixture), FH prefers swing-favorable GWs, TC by ceiling, "no recommendation" when nothing positive.

### Tests for User Story 4

- [ ] T041 [P] [US4] Write `specs/007-fpl-analyzer/tests/unit/test_chips.py` — Red — assert each of the 4 heuristic functions (`triple_captain`, `bench_boost`, `free_hit`, `wildcard`) returns a `ChipRecommendation` matching research.md § 9; FR-024 (TC uses ceiling, not mean); FR-025 (BB by 4-bench projection); FR-026 (FH by from-scratch vs own-squad swing); each returns `gw=None, supporting_metric=None` when no positive case exists in the horizon (FR-023)
- [ ] T042 [P] [US4] Write `specs/007-fpl-analyzer/tests/integration/test_p4_chip_strategy.py` — Red — covers all 3 US-4 acceptance scenarios using the T006 DGW fixture: BB prefers DGW; TC chooses the player whose ceiling is highest in any GW; "no recommendation" when no GW improves on hold for any chip

### Implementation for User Story 4

- [ ] T043 [US4] Implement `specs/007-fpl-analyzer/fpl/chips.py` with `triple_captain(pred_df, squad)`, `bench_boost(pred_df, squad)`, `free_hit(pred_df, current_squad, from_scratch_squad_per_gw)`, `wildcard(pred_df, current_squad)` returning typed `ChipRecommendation` per data-model.md § 7. Compose them into `compute_chip_plan(...)` returning a `ChipPlan`. Passes T041 Green.
- [ ] T044 [US4] Update `specs/007-fpl-analyzer/fpl/run.py` to compute `chip_plan` via `chips.compute_chip_plan(...)` for both modes; write to `result['chip_plan']` and the typed `ChipPlan` accessor. Passes T042 Green.
- [ ] T045 [US4] Update `specs/007-fpl-analyzer/fpl/cli.py` to render the `=== CHIP PLAN ===` block per `contracts/cli.md`. Re-run T022.

**Checkpoint**: P1-P4 all work independently. CLI now provides full strategic guidance.

---

## Phase 7: User Story 5 — Visual analysis via the web UI (Priority: P5)

**Goal**: The Streamlit page `fpl_gui.py` renders the recommendation card, XI/bench tables, multi-week plan table, predicted-points heatmap, chip cards, differentials panel, and Run Status / Diagnostics panel — all reflecting the typed outputs of `run_analysis`.

**Independent Test**: `streamlit run fpl_gui.py`, sidebar inputs Team ID + horizon, click Run Analysis. All FR-031 panels are visible; the diagnostics panel matches the CLI block; toggling display options does not retrigger the analysis (FR-033).

### Tests for User Story 5

- [ ] T046 [P] [US5] Write `specs/007-fpl-analyzer/tests/unit/test_ui_helpers.py` — Red — covers the pure formatting helpers in `fpl/ui.py` (`format_money`, `decorate_squad_table`, `build_status_panel_view`) without any Streamlit runtime, using small synthetic DataFrames
- [ ] T047 [P] [US5] Write `specs/007-fpl-analyzer/tests/integration/test_p5_web_ui_smoke.py` — Red — uses Streamlit's `AppTest` to render `specs/007-fpl-analyzer/fpl_gui.py` against a stubbed `run_analysis` returning a fixed `CachedAnalysisResult`; asserts every FR-031 panel is present (recommendation card, XI table, bench table, multi-week plan table, heatmap, 4 chip cards, differentials, run-status panel); asserts that flipping a display toggle does not retrigger the underlying `cached_run_analysis` call (FR-033)

### Implementation for User Story 5

- [ ] T048 [US5] Implement `specs/007-fpl-analyzer/fpl/ui.py` by extracting and porting the existing private `_format_money`, `_decorate_squad_table`, `_render_continuity_header`, `_render_recommendation_card`, `_render_alternatives`, `_render_squad_tables`, `_render_outlook`, `_render_chip_cards`, `_render_multi_week_plan`, `_render_from_scratch_view` helpers from `fpl_gui.py` into pure functions taking `(typed_view, container)` arguments. Add a new `render_run_status_panel(rs, container)` for the Run Status / Diagnostics surface (FR-038). Passes T046 Green.
- [ ] T049 [US5] Refactor `specs/007-fpl-analyzer/fpl_gui.py` to delegate rendering to `fpl/ui.py` functions. Preserve the existing `@st.cache_data(ttl=3600)` wrapper around `cached_run_analysis(...)` (FR-033). Add the run-status panel render at the bottom of the page (FR-031, FR-038). Passes T047 Green.

**Checkpoint**: All five user stories work independently. The web UI provides visual access to everything the CLI offers.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Backtest mode (FR-029) and quality polish that touches multiple stories.

- [ ] T050 [P] Write `specs/007-fpl-analyzer/tests/integration/test_backtest.py` — Red — covers FR-029: `--backtest 8,10,15` prints a `=== BACKTEST SUMMARY ===` table with one row per evaluated GW + an aggregate row, plus a "Wrote backtest detail to:" line; the per-player CSV/JSON exists at the printed path under the cache dir; no look-ahead bias (rolling features at evaluation only use data with `kickoff_time < deadline_time of evaluated GW`); SC-008 baseline-comparison runs end-to-end
- [ ] T051 Implement `specs/007-fpl-analyzer/fpl/backtest.py` per FR-029 + `contracts/cli.md` § stdout layout (backtest mode): `run_backtest(gws: list[int], cache_dir) -> dict` returning summary rows; writes detail file under `<cache-dir>/backtests/`; CLI mode wires `--backtest` to call this. Passes T050 Green.
- [ ] T052 [P] Update `specs/007-fpl-analyzer/requirements.txt` to add `platformdirs>=4` (currently absent) and remove the trailing comment-only block referencing the dropped `understat` package — the in-repo Understat scraper makes that comment stale
- [ ] T053 Run the quickstart scenarios end-to-end (steps 2, 3, 4, 5 of `specs/007-fpl-analyzer/quickstart.md`) and record observed cold/cached latencies in a comment block at the bottom of `quickstart.md`. Validate SC-001 (≤120 s cold, ≤5 s cached) and SC-002 are met on a typical laptop.
- [ ] T054 [P] Run `/speckit-analyze` to verify cross-artifact consistency between spec.md, plan.md, tasks.md, data-model.md, contracts/, and research.md. Resolve any flagged inconsistencies before proceeding to `/speckit-implement`.
- [ ] T055 Final review: ensure all FRs (FR-001 through FR-038) and SCs (SC-001 through SC-009) have at least one test or acceptance gate covering them. Cross-link from a brief audit comment at the end of `tasks.md` (this file).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)** — no dependencies; can start immediately.
- **Phase 2 (Foundational)** — depends on Phase 1; **BLOCKS** all user-story phases.
- **Phase 3 (US1) — MVP** — depends on Phase 2; independently shippable.
- **Phase 4 (US2)** — depends on Phase 2 (and benefits from US1 for shared `run.py` orchestrator scaffolding, but US2's beam-search module is independent).
- **Phase 5 (US3)** — depends on Phase 2; can run in parallel with Phases 4 / 6.
- **Phase 6 (US4)** — depends on Phase 2; can run in parallel with Phases 4 / 5.
- **Phase 7 (US5)** — depends on Phase 3 minimum (the orchestrator must already produce a real `CachedAnalysisResult`); benefits from Phases 4-6 to render their outputs.
- **Phase 8 (Polish)** — depends on all desired user-story phases.

### Within each user story

- Test tasks precede their paired implementation task (Constitution Principle III, Test-First).
- Models / data shapes precede services that consume them.
- Services precede CLI / UI integration.
- Each story checkpoint represents a complete, demonstrable slice — STOP and validate before moving on.

### Parallel Opportunities

| Where | Tasks |
|-------|-------|
| Phase 1 setup | T003, T004 (different files, no internal deps) |
| Phase 2 fixtures | T005, T006, T007, T008 (different files) |
| Phase 2 errors / types tests | T009, T010 (different files) |
| Phase 2 API tests | T012, T013 (different files) |
| Phase 2 cache + diagnostics + features tests | T015, T017, T019 (different files) |
| US1 tests | T022, T023, T024, T025 (different files) |
| US1 baseline / ensemble | T026 can start in parallel with the test tasks above |
| US2 tests | T032, T033 (different files) |
| US3 tests | T037, T038 (different files) |
| US4 tests | T041, T042 (different files) |
| US5 tests | T046, T047 (different files) |
| Polish | T050 + T052 + T054 (different files / different commands) |

### Parallel Example: User Story 1 (kickoff)

```bash
# In one wave, write all four US1 test files in parallel:
Task: "Write specs/007-fpl-analyzer/tests/contract/test_cli.py — Red"
Task: "Write specs/007-fpl-analyzer/tests/contract/test_package_api.py — Red"
Task: "Write specs/007-fpl-analyzer/tests/integration/test_p1_weekly_rec.py — Red"
Task: "Write specs/007-fpl-analyzer/tests/unit/test_models.py — Red"

# Once T026 (baseline) lands, T027 (ensemble) can start in parallel with T028 (transfer optimizer):
Task: "Implement fpl/models/ensemble.py"
Task: "Implement fpl/optimizer/transfer.py"
```

---

## Implementation Strategy

### MVP First — User Story 1 only

1. Phase 1 (Setup) → 4 tasks → ~30 min.
2. Phase 2 (Foundational) → 17 tasks → ~1-2 days.
3. Phase 3 (US1) → 10 tasks → ~1 day.
4. **STOP and VALIDATE**: run `python fpl_main.py --team-id 12345 --gw 30 --no-multi-week`. Demo to user. **MVP shipped.**

### Incremental Delivery

1. MVP via Phase 3 → demo.
2. Add Phase 4 (US2) → multi-week plan visible in CLI → demo.
3. Add Phase 5 (US3) → from-scratch squad / Wildcard preview → demo.
4. Add Phase 6 (US4) → chip cards in CLI → demo.
5. Add Phase 7 (US5) → web UI → demo.
6. Add Phase 8 (Polish) → backtest mode + final audit.

### Parallel Team Strategy

After Phase 2 is complete:

- Developer A: Phase 3 (US1) — the gating story; the orchestrator scaffold lands first.
- Developer B: Phase 5 (US3) — squad LP optimiser is fully self-contained.
- Developer C: Phase 6 (US4) — chip heuristics are fully self-contained.
- Developer D: Phase 4 (US2) — beam-search planner can be built against the orchestrator's wiring once Phase 3 lands T029.
- Phase 7 (US5) starts when Phase 3 completes; benefits as 4/5/6 land.

---

## Summary

- **Total tasks**: 55 (T001-T055).
- **Per phase**:
  - Setup (Phase 1): 4
  - Foundational (Phase 2): 17
  - US1 — Weekly recommendation (Phase 3, P1, **MVP**): 10
  - US2 — Multi-week plan (Phase 4, P2): 5
  - US3 — From-scratch squad (Phase 5, P3): 4
  - US4 — Chip strategy (Phase 6, P4): 5
  - US5 — Web UI (Phase 7, P5): 4
  - Polish (Phase 8): 6
- **Parallel opportunities**: 23 tasks marked `[P]`.
- **Test-First coverage**: every implementation task in Phases 2-7 has a paired Red test task (T010↔T011, T012/T013↔T014, T015↔T016, T017↔T018, T019↔T020, T022/T023/T024/T025↔T027/T028/T029/T030, T032/T033↔T034/T035/T036, T037/T038↔T039/T040, T041/T042↔T043/T044/T045, T046/T047↔T048/T049, T050↔T051).
- **MVP scope**: Phases 1 + 2 + 3 (T001-T031). Delivers User Story 1 standalone.

---

## Notes

- `[P]` tasks = different files, no dependencies on incomplete tasks in the same phase.
- `[USx]` label maps tasks to specific user stories for traceability.
- Each user story is independently completable and testable per Constitution Principle V.
- Verify tests fail (Red) before implementing (Green) per Constitution Principle III.
- Commit at each phase checkpoint at minimum; smaller logical groups are encouraged.
- Stop at any checkpoint to validate the story independently and demo if appropriate.
- Avoid: vague tasks, same-file conflicts in `[P]` groups, cross-story dependencies that break independence.
