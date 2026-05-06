# Contract: Run Status / Diagnostics Panel

**Source FR**: FR-038. Backed by data-model.md § 8 (`RunStatus`).
**Stability**: Locked by `tests/contract/test_diagnostics.py`. The CLI rendering format and the underlying JSON schema are both covered.

## Channels

The same `RunStatus` instance must be rendered in three places per FR-038:

1. **CLI stdout** — under the `=== RUN STATUS ===` block; machine-parseable `key=value` lines.
2. **Web UI** — as a Streamlit panel (`st.dataframe` for sources, `st.metric` for elapsed/cache/MAE).
3. **Cache** — the same `RunStatus` is also serialised as JSON to `<cache-dir>/runs/<run_id>.json` for offline replay.

## CLI rendering (stdout)

```text
=== RUN STATUS ===
started_at=2026-05-06T19:22:01Z
elapsed_total_ms=4287
package_version=0.1.0
cache.hit=false
cache.key=ab12cd34ef
cache.age_seconds=0
source.fpl_bootstrap.status=ok
source.fpl_bootstrap.detail=
source.fpl_bootstrap.elapsed_ms=412
source.fpl_fixtures.status=ok
source.fpl_fixtures.detail=
source.fpl_fixtures.elapsed_ms=187
source.fpl_picks.status=ok
source.fpl_picks.detail=
source.fpl_picks.elapsed_ms=298
source.understat.status=unavailable
source.understat.detail=python_3_14_no_aiohttp_wheel
source.understat.elapsed_ms=0
model.baseline_mae=1.83
model.weights={"xgb":0.33,"lgbm":0.33,"catboost":0.33}
model.top_features=form:0.31,fixture_difficulty_5:0.18,minutes_last5:0.14,xG_last5:0.11,price:0.09
warning.0=Player 348 (Smith) flagged 50% chance — excluded from XI
```

Rules:

- Keys are lowercase, dot-separated paths.
- Values are scalars (`int`, `float`, `bool`, `string`) or compact JSON for objects/arrays.
- The block is bounded by a leading `=== RUN STATUS ===` line and a trailing blank line.
- The order of keys is fixed (matches the dataclass field order in data-model.md).
- Tests use a small parser (`fpl.diagnostics.parse_status_block`) to round-trip the stdout block into a `RunStatus` and compare structurally.
- `model.top_features` uses `name:importance` pairs (comma-separated) so the round-trip in `parse_status_block` is lossless. The JSON-on-disk schema (below) carries the same pairs as `[name, importance]` arrays.
- `inputs` are intentionally **not** in the CLI block — only the JSON-on-disk record carries them. The CLI surface is a diagnostic, not an audit log.

## Web UI rendering

Streamlit panel layout (in `fpl/ui.py`, called from `fpl_gui.py`):

- A 4-column header row: `Elapsed`, `Cache`, `Baseline MAE`, `Run Started`.
- A `st.dataframe` for sources with columns: `name, status, detail, elapsed_ms`. Status cells use Streamlit's per-cell colour styling: `ok` = green, `ok_retried` / `skipped_by_toggle` = amber, `unavailable` / `failed` = red.
- A `st.expander` "Model diagnostics" wrapping `top_features` and `weights_used` (matches what `fpl_gui.py` currently expects under the `'top_features' / 'baseline_mae' / 'model_weights'` legacy keys).
- A `st.warning` per non-empty entry in `warnings`.

## JSON-on-disk schema

```json
{
  "version": 1,
  "started_at": "2026-05-06T19:22:01Z",
  "elapsed_total_ms": 4287,
  "package_version": "0.1.0",
  "inputs": {"target_gw": 31, "horizon": 3, "budget": 100.0, "team_id": 12345, "no_understat": false},
  "cache": {"hit": false, "key": "ab12cd34ef", "age_seconds": 0},
  "sources": [
    {"name": "fpl_bootstrap", "status": "ok", "detail": "", "elapsed_ms": 412},
    {"name": "fpl_fixtures",  "status": "ok", "detail": "", "elapsed_ms": 187},
    {"name": "fpl_picks",     "status": "ok", "detail": "", "elapsed_ms": 298},
    {"name": "understat",     "status": "unavailable", "detail": "python_3_14_no_aiohttp_wheel", "elapsed_ms": 0}
  ],
  "model": {
    "baseline_mae": 1.83,
    "weights_used": {"xgb": 0.33, "lgbm": 0.33, "catboost": 0.33},
    "top_features": [["form", 0.31], ["fixture_difficulty_5", 0.18], ["minutes_last5", 0.14]]
  },
  "warnings": ["Player 348 (Smith) flagged 50% chance — excluded from XI"]
}
```

Schema versioning: `version` is incremented when a field is removed or its type changes; additive fields preserve `version` and are tolerated by older readers (forward-compat).

## Required source presence

| Source | Always present? |
|--------|-----------------|
| `fpl_bootstrap` | Yes |
| `fpl_fixtures` | Yes |
| `fpl_picks` | Iff `team_id` was supplied |
| `understat` | Iff Understat was attempted (i.e., `--no-understat` was off) |

`tests/contract/test_diagnostics.py` asserts this presence matrix across the four input combinations.
