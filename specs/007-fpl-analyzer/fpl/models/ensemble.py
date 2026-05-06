"""Three-head ensemble predictor (T027, FR-007 / FR-008 / FR-009).

Heads (research.md § 4):

* **Mean** — equal-weighted average of XGBoost + LightGBM + CatBoost
  regressors. Drives transfer/captain decisions, multi-week scoring, and
  the from-scratch optimiser objective.
* **Quantile** (LightGBM, ``objective="quantile"``, ``alpha=0.9``) —
  per-(player, GW) ceiling estimate required by FR-008 and consumed by
  the Triple Captain heuristic (FR-024).
* **Poisson** (LightGBM, ``objective="poisson"``) — goal/assist rate;
  feeds the Free Hit swing in chip strategy (FR-026) and the
  differentials ranking tiebreaker.

The heavy ML libraries are imported inside :py:meth:`train` so module
import stays cheap — useful for the contract tests that only need the
class definition.
"""

from __future__ import annotations

from typing import Any

import numpy as np
import pandas as pd


class Ensemble:
    """Three-head GBM ensemble.

    The constructor takes only hyperparameters; ``train(X, y)`` does the
    actual fits. Tests pass small ``n_estimators`` to keep the suite
    fast; production callers (``fpl.run``) use the research-spec defaults.
    """

    def __init__(
        self,
        *,
        n_estimators: int = 400,
        learning_rate: float = 0.05,
        max_depth: int = 6,
        quantile_alpha: float = 0.9,
        random_state: int = 42,
    ) -> None:
        self.n_estimators = n_estimators
        self.learning_rate = learning_rate
        self.max_depth = max_depth
        self.quantile_alpha = quantile_alpha
        self.random_state = random_state

        self._mean_xgb: Any = None
        self._mean_lgbm: Any = None
        self._mean_catboost: Any = None
        self._quantile: Any = None
        self._poisson: Any = None
        self._feature_names: list[str] = []

    # ------------------------------------------------------------------
    # Training
    # ------------------------------------------------------------------

    def train(self, X: pd.DataFrame, y: Any) -> "Ensemble":
        """Fit the three heads. Returns self for fluent chaining."""
        from catboost import CatBoostRegressor
        from lightgbm import LGBMRegressor
        from xgboost import XGBRegressor

        self._feature_names = list(X.columns)
        y_arr = np.asarray(y, dtype=float)

        self._mean_xgb = XGBRegressor(
            n_estimators=self.n_estimators,
            learning_rate=self.learning_rate,
            max_depth=self.max_depth,
            random_state=self.random_state,
            verbosity=0,
        ).fit(X, y_arr)
        self._mean_lgbm = LGBMRegressor(
            n_estimators=self.n_estimators,
            learning_rate=self.learning_rate,
            max_depth=self.max_depth,
            random_state=self.random_state,
            verbosity=-1,
        ).fit(X, y_arr)
        self._mean_catboost = CatBoostRegressor(
            iterations=self.n_estimators,
            learning_rate=self.learning_rate,
            depth=self.max_depth,
            random_seed=self.random_state,
            verbose=0,
        ).fit(X, y_arr)

        self._quantile = LGBMRegressor(
            objective="quantile",
            alpha=self.quantile_alpha,
            n_estimators=self.n_estimators,
            learning_rate=self.learning_rate,
            max_depth=self.max_depth,
            random_state=self.random_state,
            verbosity=-1,
        ).fit(X, y_arr)

        # Poisson head requires non-negative targets.
        y_pos = np.maximum(y_arr, 0.0)
        self._poisson = LGBMRegressor(
            objective="poisson",
            n_estimators=self.n_estimators,
            learning_rate=self.learning_rate,
            max_depth=self.max_depth,
            random_state=self.random_state,
            verbosity=-1,
        ).fit(X, y_pos)

        return self

    # ------------------------------------------------------------------
    # Prediction heads
    # ------------------------------------------------------------------

    def predict_mean(self, X: pd.DataFrame) -> np.ndarray:
        """Equal-weighted mean of the three regressors (FR-007)."""
        self._require_trained()
        preds = (
            np.asarray(self._mean_xgb.predict(X), dtype=float)
            + np.asarray(self._mean_lgbm.predict(X), dtype=float)
            + np.asarray(self._mean_catboost.predict(X), dtype=float)
        ) / 3.0
        return preds

    def predict_ceiling(self, X: pd.DataFrame) -> np.ndarray:
        """Upper-quantile prediction (FR-008, ``alpha`` from constructor)."""
        self._require_trained()
        return np.asarray(self._quantile.predict(X), dtype=float)

    def predict_goal_rate(self, X: pd.DataFrame) -> np.ndarray:
        """Poisson rate, clipped to non-negative."""
        self._require_trained()
        return np.maximum(np.asarray(self._poisson.predict(X), dtype=float), 0.0)

    # ------------------------------------------------------------------
    # Diagnostics
    # ------------------------------------------------------------------

    def top_features(self, k: int = 10) -> list[tuple[str, float]]:
        """Top ``k`` features by mean-of-three-head importance."""
        self._require_trained()
        imp_xgb = np.asarray(self._mean_xgb.feature_importances_, dtype=float)
        imp_lgbm = np.asarray(self._mean_lgbm.feature_importances_, dtype=float)
        imp_cat = np.asarray(self._mean_catboost.feature_importances_, dtype=float)
        # Each library uses a different importance scale; normalise each
        # to sum to 1 before averaging so they contribute equally.
        def _norm(arr: np.ndarray) -> np.ndarray:
            total = arr.sum()
            return arr / total if total > 0 else arr
        avg = (_norm(imp_xgb) + _norm(imp_lgbm) + _norm(imp_cat)) / 3.0
        ranked = sorted(zip(self._feature_names, avg), key=lambda x: -x[1])
        return [(name, float(value)) for name, value in ranked[:k]]

    def weights_used(self) -> dict[str, float]:
        """Equal-weighting between the three mean-head regressors."""
        return {"xgb": 1.0 / 3.0, "lgbm": 1.0 / 3.0, "catboost": 1.0 / 3.0}

    # ------------------------------------------------------------------
    # Internals
    # ------------------------------------------------------------------

    def _require_trained(self) -> None:
        if self._mean_xgb is None:
            raise RuntimeError("Ensemble has not been trained — call train(X, y) first")


__all__ = ["Ensemble"]
