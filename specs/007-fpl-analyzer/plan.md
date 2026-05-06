# Implementation Plan: FPL Ultimate Analyzer

**Branch**: `007-fpl-analyzer` | **Date**: 2026-05-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/007-fpl-analyzer/spec.md`

## Summary

Build the missing `fpl/` Python package that the existing `fpl_main.py` (CLI shim) and `fpl_gui.py` (Streamlit shim) already import. The package ingests the public FPL feed (post-deadline only — no FPL login, per Q1), engineers per-player features, scores players using a small ensemble (XGBoost + LightGBM + CatBoost mean predictor, plus a quantile head and a Poisson goal-rate head for ceiling/captain), and drives three optimizers: PuLP linear-programming for from-scratch 15-man squad construction, ranked enumeration for single-week transfer recommendations, and beam search across the planning horizon for the multi-week plan. A unified Run Status / Diagnostics panel is rendered in both CLI and web UI per Q5. The disk-resident analysis cache lives in the OS-standard user cache directory (Q3), survives process restarts within a 1-hour TTL, and also holds backtest output artifacts (Q2). Networking is polite-client (User-Agent + retry/backoff) and structurally bulk-endpoint-only (≤10 outbound requests per cold run, per Q4 / FR-037).

## Technical Context

**Language/Version**: Python 3.14 (matches the existing `requirements.txt` comment about Python 3.14 wheel availability and the deliberate Understat drop)
**Primary Dependencies**:
- Numerics & data: `numpy`, `pandas`, `scipy`
- ML: `scikit-learn`, `xgboost`, `lightgbm`, `catboost`
- Linear programming: `PuLP`
- HTTP: `requests` (with `urllib3` retry adapter)
- Web UI: `streamlit`, `plotly`
- Cross-platform paths: `platformdirs` (OS-standard user cache dir for Q3)
- Fuzzy team-name matching: `rapidfuzz`
- Optional xG/xA enrichment: best-effort use of the public Understat HTML/JSON via `requests` (the deprecated `understat` PyPI package is intentionally avoided per existing `requirements.txt` notes)

**Storage**: Disk cache only — no database. Cache layout under `platformdirs.user_cache_dir("fpl-analyzer")`:
- `data/` — Parquet snapshots of FPL bulk-endpoint responses, keyed by endpoint and fetch timestamp
- `models/` — `joblib`-pickled trained ensembles and feature transformers, keyed by `(target_gw, no_understat)`
- `runs/` — per-run JSON artefact (the data backing the Run Status / Diagnostics panel)
- `backtests/` — per-player CSV/JSON files written by `--backtest` (FR-029)
- 1-hour TTL enforced via cache-key timestamp; manual `--clear-cache` purges the directory

**Testing**: `pytest` for unit + integration; `responses` for mocking the FPL public API in contract / integration tests; `freezegun` for time-pinned cache-TTL tests. Coverage gating not part of this plan; Constitution Principle III still applies (Red → Green for any test in scope).

**Target Platform**: Cross-platform desktop / laptop — Windows 10+, macOS 13+, Linux (Ubuntu 22.04+). Python 3.14 single environment, no containerisation in v1.

**Project Type**: Single Python project — a domain library (`fpl/`) plus two thin entry points (CLI `fpl_main.py` and Streamlit `fpl_gui.py`). Both entry points already exist as shims in `specs/007-fpl-analyzer/`; the package they import is what this plan builds.

**Performance Goals**:
- Cold-run end-to-end ≤ 120 s (SC-001 first run; SC-002 from-scratch first run)
- Cached-run end-to-end ≤ 5 s (SC-001 cached run)
- Cache hit rate ≥ 95% within the TTL window (SC-005)
- Cold-run outbound HTTP request count ≤ 10 (FR-037)

**Constraints**:
- Public, post-deadline FPL data only — no FPL credentials, cookies, or login (FR-004 post-Q1)
- Polite-client networking: descriptive User-Agent, modest pacing, exponential backoff on 429 / 5xx (FR-035)
- Bulk endpoints required wherever they exist; per-player loops forbidden against the FPL API (FR-037)
- Backtests must be free of look-ahead bias (FR-029, "Stale public data" edge case)
- Single-user local tool — no multi-tenancy, no auth, no remote hosting (Out of Scope)

**Scale/Scope**:
- Domain: ~600 active Premier League players × 38 gameweeks per season; ~10 fixtures per gameweek
- Codebase: target ≤ ~7,000 LOC for the `fpl/` package; ≤ ~2,500 LOC for tests at MVP
- Cache footprint: < 200 MB per fresh season (Parquet snapshots + a small fleet of pickled models)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The constitution defines five principles (`v1.0.0`). Each is evaluated against this plan below.

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Spec-First Development** (NON-NEGOTIABLE) | PASS | `spec.md` authored 2026-05-06, `/speckit-clarify` completed (5 questions integrated under `## Clarifications → Session 2026-05-06`), no `[NEEDS CLARIFICATION]` markers remain. |
| **II. Plan Before Code** | PASS (in progress) | This document is the plan. The only pre-existing code (`fpl_main.py`, `fpl_gui.py`) is two thin entry-shims that import a package not yet present; production code is gated on this plan being approved. |
| **III. Test-First Discipline** (NON-NEGOTIABLE) | PASS (committed) | The plan commits to Red → Green for every module: contract tests for the CLI argument schema, the `fpl.run_analysis` public function, the FPL API consumption surface, and the Run Status / Diagnostics output schema; integration tests for each user story; unit tests for optimizers, caching, and feature engineering. Tests precede implementation in `tasks.md` (downstream output of `/speckit-tasks`). |
| **IV. Simplicity & YAGNI** | PASS with two justified deviations (see Complexity Tracking) | The plan rejects: a model registry, a config-DSL, a plugin system for optimizers, and any pre-deadline-auth scaffolding. Two non-trivial choices (a small model ensemble and a beam-search planner) are retained because the spec's Success Criteria require capabilities a single-point GBM and greedy planner cannot deliver. |
| **V. Incremental, Independently Testable Delivery** | PASS | `spec.md` already prioritises five stories P1–P5 with independent acceptance scenarios. The package is decomposed so that completing P1 alone yields a working "weekly recommendation" CLI without touching the multi-week planner, the LP optimiser, the chip module, or the web UI. `tasks.md` (downstream) will preserve this story-grouped ordering. |

**Quality & Workflow Constraints check**:
- Constitution Check Gate — this section. ✅
- Cross-Artifact Consistency — `/speckit-analyze` will run after `tasks.md`. ✅ (downstream)
- Clarifications Are Authoritative — every clarification from Session 2026-05-06 is encoded in `spec.md` and referenced from the plan. ✅
- Boundary Validation Only — input validation will live at three boundaries: (a) the CLI argument parser, (b) the FPL API client (response schema), and (c) the `fpl.run_analysis` public function. Internal modules trust their callers. ✅

**Gate decision**: PASS. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/007-fpl-analyzer/
├── spec.md                # /speckit-specify (committed)
├── plan.md                # this file (/speckit-plan)
├── research.md            # Phase 0 (/speckit-plan)
├── data-model.md          # Phase 1 (/speckit-plan)
├── quickstart.md          # Phase 1 (/speckit-plan)
├── contracts/             # Phase 1 (/speckit-plan)
│   ├── cli.md             # CLI argument schema + exit codes + stdout layout
│   ├── package_api.md     # Stable surface of the `fpl` package
│   ├── fpl_api.md         # Which FPL endpoints are consumed and the field subset relied upon
│   └── diagnostics.md     # Run Status / Diagnostics panel schema (machine-parseable per FR-038)
├── checklists/
│   └── requirements.md    # spec quality checklist (already passing)
└── tasks.md               # /speckit-tasks (downstream — NOT created here)
```

### Source Code (this feature)

```text
specs/007-fpl-analyzer/
├── fpl_main.py            # existing CLI entry-shim (`from fpl.cli import main`)
├── fpl_gui.py             # existing Streamlit entry-shim (`from fpl import run_analysis`)
├── requirements.txt       # existing dependencies pin
├── fpl/                   # NEW — the package this plan builds
│   ├── __init__.py        # exports run_analysis
│   ├── cli.py             # argparse + main()
│   ├── run.py             # run_analysis orchestrator (the function fpl_gui.py imports)
│   ├── api.py             # FPL public API client: bulk endpoints, User-Agent, retry/backoff
│   ├── cache.py           # disk cache (Parquet for data, joblib for models, JSON for runs)
│   ├── features.py        # per-player feature engineering for ML
│   ├── models/
│   │   ├── __init__.py
│   │   ├── ensemble.py    # mean (XGB + LGBM + CatBoost) + quantile (q=0.9) + Poisson heads
│   │   └── baseline.py    # naive baseline used for SC-008 comparison and tests
│   ├── optimizer/
│   │   ├── __init__.py
│   │   ├── squad.py       # PuLP LP for from-scratch 15-man squad (P3)
│   │   ├── transfer.py    # ranked enumeration for single-week transfer rec (P1)
│   │   └── multiweek.py   # beam search across the horizon (P2)
│   ├── chips.py           # TC / BB / FH / WC recommendation logic (P4)
│   ├── diagnostics.py     # Run Status / Diagnostics builder + renderer (FR-038)
│   ├── backtest.py        # backtest mode: per-GW table on stdout + CSV/JSON in cache dir (FR-029)
│   └── ui.py              # Streamlit rendering helpers (the page itself stays in fpl_gui.py)
└── tests/
    ├── conftest.py        # fixtures: fake FPL bulk responses, frozen-time cache tests
    ├── contract/
    │   ├── test_cli.py            # CLI args, exit codes, stdout shape — guards contracts/cli.md
    │   ├── test_package_api.py    # `fpl.run_analysis` signature + return shape
    │   ├── test_fpl_api.py        # consumed FPL endpoint subset + field schema
    │   └── test_diagnostics.py    # machine-parseable run-status output
    ├── integration/
    │   ├── test_p1_weekly_rec.py        # User Story 1 acceptance scenarios
    │   ├── test_p2_multiweek_plan.py    # User Story 2 acceptance scenarios
    │   ├── test_p3_from_scratch.py      # User Story 3 acceptance scenarios
    │   ├── test_p4_chip_strategy.py     # User Story 4 acceptance scenarios
    │   └── test_p5_web_ui_smoke.py      # User Story 5 — Streamlit smoke / page-render
    └── unit/
        ├── test_cache.py            # TTL, hit/miss, restart-survival
        ├── test_api_retry.py        # 429 / 5xx exponential backoff
        ├── test_squad_optimizer.py  # FPL composition rules enforced (FR-019)
        ├── test_multiweek.py        # FT cap, bank carry-over, hit costs (FR-016, FR-017, SC-007)
        ├── test_features.py         # feature shape, no leakage
        └── test_models.py           # ensemble outputs + quantile monotonicity
```

**Structure Decision**: Single self-contained Python project rooted at `specs/007-fpl-analyzer/`. The two existing entry-shims (`fpl_main.py`, `fpl_gui.py`) are kept as-is — they already match a clean entry-point pattern (`from fpl.cli import main` and `from fpl import run_analysis`) and renaming them would only invalidate the user's documented invocation examples. The `fpl/` package and `tests/` tree are new. No code lives outside this directory; the repo-root `src/` and `tests/` directories that show up as untracked in `git status` belong to a different, unrelated feature and are out of scope.

## Complexity Tracking

The following two non-trivial choices warrant explicit YAGNI justification (Constitution Principle IV).

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **Small ML ensemble** (XGBoost + LightGBM + CatBoost mean head, plus a quantile q=0.9 head and a Poisson goal-rate head) instead of a single GBM | The spec mandates **two distinct prediction outputs**: an expected-points score for transfer/captain decisions (FR-007), AND a per-gameweek "ceiling" estimate explicitly required for Triple Captain (FR-008, FR-024). A single-point regression cannot produce a calibrated upper-quantile by construction; we either fit a quantile head separately or run a second pass. The ensemble for the mean head is then a small accuracy boost (~3-5 points of MAE, materially helping SC-003) at the cost of one extra dependency. The Poisson head specifically targets goal-rate features that otherwise hide behind composite "predicted points" — needed for chip differentiation (TC vs BB vs FH). | **Single XGBoost regressor**: rejected because it cannot produce a per-player ceiling for FR-008 / FR-024 without bolt-ons. **Single LightGBM with quantile loss**: rejected because the same model trained on quantile loss tends to underperform on the mean target by 5-10% in this domain — using two heads (one mean, one quantile) is both simpler to reason about and more accurate. **Linear regression baseline**: kept as `models/baseline.py` for tests and SC-008 comparison, not as the production path. |
| **Beam search** for the multi-week plan (P2) instead of greedy week-by-week or full enumeration | A user story acceptance scenario explicitly demands looking past locally-best moves: "Given a multi-transfer move in week 2 unlocks a stronger sequence, the recommended path may take a -4 hit in week 2 if the cumulative net gain is positive" (US-2 scenario 2). Greedy fails this scenario by construction. Full enumeration over a 5-GW horizon × ~600 players × multi-transfer combinations is computationally infeasible (>10^15 states). Beam search with a fixed beam width (e.g., 16) and bounded transfer arity per step (≤2 in most weeks, ≤4 on a Wildcard week) hits the SC-001 ≤120 s budget while still catching multi-week trades. | **Greedy single-week-at-a-time**: rejected because it fails US-2 scenario 2 by construction (ignores future state). **Full enumeration**: rejected because the state space is intractable (FT carry-over + bank carry-over + ≥2-transfer combinations × 5 GWs >> tractable). **Mixed-integer programming (MIP) over the horizon**: the right tool theoretically, but adds a heavyweight solver dependency and far more constraint-modelling code than beam search; a hand-tuned beam width is simpler, debuggable, and meets the spec's Success Criteria. |

The remainder of the design intentionally keeps things simple:
- No plugin / strategy DSL — optimisers are plain Python modules.
- No model registry — pickled models live in the same disk cache, keyed by `(target_gw, no_understat)`.
- No config file — CLI flags + sensible defaults, mirrored as Streamlit sidebar widgets.
- No pre-deadline-auth scaffolding — disallowed by Q1 / FR-004.
- No internal validation re-runs — input is validated at the three boundaries identified in Constitution Check, then trusted.
