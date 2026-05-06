"""Naive baseline predictor (T026).

Used by:

* ``tests/unit/test_models.py`` as a sanity check.
* ``fpl.backtest`` (T051) as the SC-008 comparison reference — "follow
  the analyzer's recommendation outperforms a hold-every-week baseline
  by at least 1 point per gameweek on average."

The baseline is intentionally simple: a 50/50 blend of a player's
season-to-date PPG and FPL's ``form`` field, falling back to the global
mean if those columns aren't on the feature matrix.
"""

from __future__ import annotations

from typing import Any

import numpy as np
import pandas as pd


class NaiveBaseline:
    """A 2-line predictor used purely as a comparison anchor."""

    def __init__(self) -> None:
        self._mean: float = 0.0
        self._has_ppg: bool = False
        self._has_form: bool = False

    def train(self, X: pd.DataFrame, y: Any) -> "NaiveBaseline":
        y_arr = np.asarray(y, dtype=float)
        self._mean = float(y_arr.mean()) if y_arr.size else 0.0
        cols = set(X.columns) if isinstance(X, pd.DataFrame) else set()
        self._has_ppg = "season_ppg" in cols
        self._has_form = "form" in cols
        return self

    def predict(self, X: pd.DataFrame) -> np.ndarray:
        n = len(X)
        if self._has_ppg and self._has_form:
            ppg = X["season_ppg"].to_numpy(dtype=float)
            form = X["form"].to_numpy(dtype=float)
            return 0.5 * ppg + 0.5 * form
        if self._has_ppg:
            return X["season_ppg"].to_numpy(dtype=float)
        return np.full(n, self._mean, dtype=float)


__all__ = ["NaiveBaseline"]
