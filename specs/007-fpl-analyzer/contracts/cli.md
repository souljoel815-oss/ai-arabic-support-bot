# Contract: CLI

**Module**: `fpl.cli` (entry: `fpl_main.py`)
**Stability**: Locked by `tests/contract/test_cli.py`. Any change here MUST be accompanied by a contract-test update and a `spec.md` change.

## Invocation

```text
python fpl_main.py [OPTIONS]
```

## Options

| Flag | Type | Default | Spec ref |
|------|------|---------|----------|
| `--gw INT` | int in `1..38`, or `0` | `0` (auto-detect next upcoming GW) | FR-028 |
| `--horizon INT` | int in `1..5` | `3` | FR-028, Assumptions |
| `--team-id INT` | int (FPL public team id) | none | FR-004, FR-028 |
| `--budget FLOAT` | float in `[80.0, 110.0]` | `100.0` | FR-019, FR-028, Assumptions |
| `--no-understat` | flag | off | FR-005, FR-028 |
| `--no-multi-week` | flag | off | FR-028 |
| `--backtest CSV` | comma-separated ints, each in `1..38`, all `< current_gw` | none | FR-029 |
| `--clear-cache` | flag | off | FR-006, FR-028 |
| `--cache-dir PATH` | absolute path | `platformdirs.user_cache_dir("fpl-analyzer")` | FR-006 |

Combinations rejected at the boundary (exit code 2):

- `--backtest` together with `--team-id`, `--horizon`, or `--no-multi-week` (backtest is a separate mode)
- `--gw 0` with `--backtest` (each backtest GW is supplied explicitly)
- A `--backtest` GW that is not strictly less than the current finished GW (no look-ahead, FR-029)

## stdout layout (analysis mode)

When NOT in `--backtest` or `--clear-cache` mode, stdout is structured into named blocks separated by blank lines. Each block starts with a header line `=== <BLOCK> ===`.

Required blocks, in order:

1. `=== RUN STATUS ===`
   Machine-parseable `key=value` lines (one per line) per `contracts/diagnostics.md`. **FR-038**.
2. `=== RECOMMENDATION ===`
   The single weekly recommendation card (P1). Includes `kind`, `gain`, `hit_cost`, `captain`, `vice_captain`, `transfers` (one line each).
3. `=== MULTI-WEEK PLAN ===` *(omitted if `--no-multi-week`)*
   One row per GW in the horizon, columns: `gw, action, transfers, hit, week_pred, bank_after, fts_after`. **FR-015**, **FR-016**.
4. `=== CHIP PLAN ===`
   One row per chip (TC, BB, FH, WC), columns: `chip, gw, supporting_metric, rationale` or `chip, NONE` for "no recommendation". **FR-023**.
5. `=== DIFFERENTIALS ===`
   Up to 10 rows from `pred_df` filtered by `selected_by_percent < 10` and ranked by `horizon_total`. **FR-027**.

Rendering: pandas `DataFrame.to_string(index=False)` (no leading row index) with at most 4 sig figs per float.

## stdout layout (backtest mode)

When `--backtest` is supplied:

1. `=== RUN STATUS ===` — same as above.
2. `=== BACKTEST SUMMARY ===` — one row per evaluated GW, plus an aggregate row at the bottom. Columns: `gw, mae, xi_pred, xi_actual, xi_delta, rec_vs_hold_delta`. **FR-029 (a)**.
3. `Wrote backtest detail to: <path>` — single line listing the CSV/JSON path inside `<cache-dir>/backtests/`. **FR-029 (b)**.

The detail file is CSV by default; if any cell would need quoting heavier than RFC 4180 plain comma escaping, the writer falls back to JSON Lines and renames the file accordingly.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `2` | Invalid argument (per the rejection list above) |
| `3` | Unrecoverable FPL public-data failure after retry/backoff (FR-035) |
| `4` | Unrecoverable cache write failure (disk full, permissions) |
| `5` | Invalid `--team-id` (404 / unknown), per US-1 acceptance scenario 4 |

## stderr

- All retry / backoff log lines (e.g., `[fpl] retry 1/3 on 503 after 1.0s`) go to stderr, never stdout.
- Non-fatal warnings (e.g., `[fpl] Understat unavailable on Python 3.14, proceeding without xG/xA`) go to stderr.
- The `=== RUN STATUS ===` block on stdout is the canonical machine-parseable summary; stderr is for humans tailing logs.
