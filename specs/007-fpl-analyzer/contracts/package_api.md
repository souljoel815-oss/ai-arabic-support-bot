# Contract: `fpl` Python Package

**Stability**: Locked by `tests/contract/test_package_api.py`. The two existing entry-shims (`fpl_main.py`, `fpl_gui.py`) MUST continue to work without modification — they import this package's surface.

## Public surface

```python
from fpl import run_analysis            # Used by fpl_gui.py
from fpl.cli import main                # Used by fpl_main.py
```

Nothing else is part of the public API. Internal modules (`fpl.api`, `fpl.cache`, `fpl.models`, `fpl.optimizer.*`, `fpl.chips`, `fpl.diagnostics`, `fpl.backtest`, `fpl.ui`, `fpl.features`, `fpl.run`) are explicitly **not** stable; they may be refactored without notice.

## `fpl.run_analysis(...)`

```python
def run_analysis(
    *,
    target_gw: int | None = None,        # None ⇒ auto-detect next upcoming GW
    horizon: int = 3,                    # 1..5
    budget: float = 100.0,               # £m, 80..110
    team_id: int | None = None,          # squad-continuity mode when set
    no_understat: bool = False,
    no_multi_week: bool = False,
    cache_dir: str | os.PathLike | None = None,  # default: platformdirs.user_cache_dir
) -> Mapping[str, object]:
```

Return value is a mapping conforming to `data-model.md § 9` (`CachedAnalysisResult`). The legacy keys consumed by the existing `fpl_gui.py` MUST all be present and functional:

- `'mode'` → `"from_scratch"` | `"analysis"` *(legacy alias for "continuity")*
- `'gw'`, `'horizon'`, `'budget'`, `'team_id'`
- `'pred_df'`, `'differential_df'`, `'multi_gw_outlook'`
- `'squad'` *(from-scratch)*: `{'xi': [...], 'bench': [...], 'captain': int, 'vice_captain': int, 'xi_pred_gw1': float, 'xi_pred_score': float}`
- `'current_squad_plan'` *(continuity)*: dict with `bank, free_transfers, captain_name, captain_team, vice_captain_name, current_score_gw1, current_score_horizon, recommended, alternatives, xi_df, bench_df, outlook_df`
- `'multi_week_plan'`
- `'chip_plan'`
- `'top_features'`, `'baseline_mae'`, `'model_weights'`
- `'primary_view'` → `"continuity"` | `"from_scratch"`

The same return value also exposes structured-typed accessors (used by tests and downstream tools): `result.run_status`, `result.chip_plan` (typed `ChipPlan`), `result.multi_week_plan` (typed `MultiWeekPlan` or `None`), etc.

### Errors

| Condition | Exception |
|-----------|-----------|
| Invalid argument (out-of-range, mutually exclusive) | `ValueError` |
| Unknown `team_id` (FPL 404) | `fpl.errors.UnknownTeamId` |
| Unrecoverable FPL API error after retry | `fpl.errors.FPLApiError` |
| Unrecoverable cache write failure | `fpl.errors.CacheWriteError` |

The CLI translates each to the corresponding exit code per `contracts/cli.md`.

## `fpl.cli.main(argv: list[str] | None = None) -> int`

Side-effect-free wrapper around `argparse` + `run_analysis`. Returns the exit code. `fpl_main.py` calls `sys.exit(main())`.

## Stability guarantee

- Adding new keys to the returned mapping is non-breaking.
- Removing or renaming any key listed above is breaking and requires a `spec.md` amendment + a contract-test update.
- Adding new keyword-only parameters with defaults is non-breaking.
- Adding positional parameters or removing existing parameters is breaking.
