# Quickstart: FPL Ultimate Analyzer

**Feature**: 007-fpl-analyzer
**Audience**: A developer who wants to run the analyzer locally for the first time, or verify a fresh checkout.

> **Status**: This quickstart describes the *intended* commands once the `fpl/` package is implemented. Today (post-`/speckit-plan`) the package does not yet exist; running `python fpl_main.py` will raise `ModuleNotFoundError: No module named 'fpl'` until tasks under `/speckit-tasks` + `/speckit-implement` complete. This document is the contract that those tasks will deliver against.

---

## Prerequisites

- Python **3.14** (the one wheel constraint that drives the `requirements.txt` story; see research.md § 1).
- A POSIX shell or Windows PowerShell.
- Network access to `fantasy.premierleague.com`. Optional: `understat.com` for xG/xA enrichment.

## 1. Install

```bash
cd specs/007-fpl-analyzer
python -m venv .venv
.venv/Scripts/Activate.ps1     # Windows PowerShell
# or:  source .venv/bin/activate     # macOS / Linux
pip install -r requirements.txt
```

## 2. CLI: get a recommendation for your team this week (User Story 1)

```bash
python fpl_main.py --team-id 12345 --gw 0 --horizon 3
```

What you should see:

- A `=== RUN STATUS ===` block first — confirms which data sources fired, cache hit/miss, baseline MAE.
- A `=== RECOMMENDATION ===` card: the single weekly action (HOLD or transfer in/out), the captain pick, the projected gain, the hit cost.
- A `=== MULTI-WEEK PLAN ===` table with one row per gameweek across the horizon.
- A `=== CHIP PLAN ===` block with one row per chip.
- A `=== DIFFERENTIALS ===` short table.

First run typically completes in **~90 s** (cold cache; FPL API + model training). Subsequent runs within the hour complete in **<5 s** (cache hit; SC-001).

## 3. CLI: from-scratch squad (User Story 3)

```bash
python fpl_main.py --gw 1 --budget 100 --no-multi-week
```

Returns the optimal 15-man squad within £100m, satisfying every FPL rule (2 GK / 5 DEF / 5 MID / 3 FWD, ≤3 per club). The starting XI is highlighted; captain/vice are marked.

## 4. CLI: backtest mode (FR-029)

```bash
python fpl_main.py --backtest 8,10,15,18,20,25,28,30
```

Stdout: a `=== BACKTEST SUMMARY ===` table with per-GW MAE, recommended-XI vs actual delta, and recommendation-vs-hold delta. Stdout also lists the path of a per-player CSV/JSON in `<cache-dir>/backtests/` for offline analysis.

## 5. Streamlit web UI (User Story 5)

```bash
streamlit run fpl_gui.py
```

Open the printed URL (default `http://localhost:8501`). Enter your FPL Team ID and horizon in the sidebar, click **Run Analysis**. The page renders, in order:

- Sidebar — inputs.
- Top — squad-continuity warning banner (FR-032).
- Recommendation card.
- Starting XI / Bench tables.
- Multi-week plan table.
- Predicted-points heatmap across the horizon.
- Chip-strategy cards (TC / BB / FH / WC).
- Differentials panel.
- Run Status / Diagnostics panel (FR-038) at the bottom — sources, cache, baseline MAE, warnings.

Toggling display options (e.g., hide differentials, hide a chip card) does **not** trigger a recompute (FR-033).

## 6. Cache control

The cache lives at:

- **Windows**: `%LOCALAPPDATA%\fpl-analyzer\Cache\`
- **macOS**: `~/Library/Caches/fpl-analyzer/`
- **Linux**: `~/.cache/fpl-analyzer/`

To clear it manually:

```bash
python fpl_main.py --clear-cache
```

Or override the location for a specific run (used by tests):

```bash
python fpl_main.py --team-id 12345 --cache-dir ./tmp-cache
```

## 7. Running the test suite (contributors)

```bash
cd specs/007-fpl-analyzer
pip install pytest responses freezegun
pytest tests/                       # full suite
pytest tests/contract/              # contract layer (CLI, package API, FPL API, diagnostics)
pytest tests/integration/test_p1_weekly_rec.py    # one user story
pytest tests/unit/test_squad_optimizer.py         # one module
```

All tests are fully offline (FPL HTTP responses are stubbed via `responses`, time is pinned via `freezegun`).

---

## Acceptance verification (mapping to spec)

| Spec item | How to verify from this quickstart |
|-----------|-------------------------------------|
| US-1 (weekly rec) | Step 2 — recommendation card present, transfers/captain printed. |
| US-2 (multi-week plan) | Step 2 — multi-week table prints `horizon` rows; alternatives in `--no-multi-week=off` (default) only. |
| US-3 (from-scratch) | Step 3 — squad printed, FPL constraints visibly satisfied. |
| US-4 (chips) | Step 2 / Step 5 — chip-plan block / chip cards. |
| US-5 (web UI) | Step 5. |
| FR-029 backtest | Step 4. |
| FR-033 cache stability under display toggles | Step 5; toggle a display option after first Run, no spinner. |
| FR-038 diagnostics panel | Step 2 / Step 5 — `=== RUN STATUS ===` block / bottom panel of the web UI. |
| SC-001 latency budgets | Time Step 2 cold (≤120 s) and again warm (<5 s). |
| SC-006 squad-rule satisfaction | Step 3, then count GK/DEF/MID/FWD and per-club. |
