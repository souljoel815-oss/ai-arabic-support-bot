"""Disk-resident analysis cache (T016, FR-006, SC-005, Q3 clarification).

Layout under ``platformdirs.user_cache_dir("fpl-analyzer")``:

* ``data/``       — Parquet snapshots of FPL bulk-endpoint responses,
  filename pattern ``<endpoint>__<key>.parquet``.
* ``models/``     — joblib-pickled trained ensembles, filename pattern
  ``ensemble__<key>.joblib``.
* ``runs/``       — per-run JSON artefact backing the Run Status / Diagnostics
  panel (also written by :func:`fpl.diagnostics.write_json_run`).
* ``backtests/``  — FR-029 detailed per-player results (CSV / JSON).
* ``cache.lock``  — advisory file lock to prevent two analyzer
  invocations from writing the same key concurrently.

The cache key is a 10-char prefix of SHA-1(canonical-JSON of the
analysis-affecting input tuple). Only the inputs that change the output
of a run go into the key — display-only toggles are intentionally
excluded (FR-033).

This module owns *paths* and *freshness checks*; actual (de)serialisation
of DataFrames / models lives in their consumers (``fpl.run``,
``fpl.diagnostics``, ``fpl.backtest``). That separation keeps this layer
trivially unit-testable without pandas / joblib pulled in at import time.
"""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import time
from contextlib import contextmanager
from pathlib import Path
from typing import Iterator

import platformdirs

from fpl._version import __version__
from fpl.errors import CacheWriteError


APP_NAME = "fpl-analyzer"
DEFAULT_TTL_SEC = 3600  # FR-006: ~1 hour
_KEY_PREFIX_LEN = 10
_SUBDIRS: tuple[str, ...] = ("data", "models", "runs", "backtests")
_LOCK_NAME = "cache.lock"


def default_cache_dir() -> Path:
    """OS-standard user cache directory for the analyzer."""
    return Path(platformdirs.user_cache_dir(APP_NAME))


def make_cache_key(
    *,
    target_gw: int,
    horizon: int,
    budget: float,
    team_id: int | None,
    no_understat: bool,
) -> str:
    """Stable SHA-1 prefix of the analysis-affecting inputs.

    Inputs are normalised to canonical JSON (sorted keys, no whitespace)
    so the key is identical across processes / OSes / locales. ``budget``
    is rounded to 4 decimal places to avoid float-formatting drift.
    """
    payload = {
        "target_gw": int(target_gw),
        "horizon": int(horizon),
        "budget": round(float(budget), 4),
        "team_id": int(team_id) if team_id is not None else None,
        "no_understat": bool(no_understat),
        "package_version": __version__,
    }
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":"))
    digest = hashlib.sha1(canonical.encode("utf-8")).hexdigest()
    return digest[:_KEY_PREFIX_LEN]


class DiskCache:
    """Path provider + freshness check + advisory lock for the cache.

    Tests pass a ``tmp_path`` to isolate each test; production callers
    rely on the default :func:`default_cache_dir`.
    """

    def __init__(
        self,
        root: Path | str | None = None,
        *,
        ttl_sec: int = DEFAULT_TTL_SEC,
    ) -> None:
        self.root = Path(root) if root is not None else default_cache_dir()
        self.ttl_sec = ttl_sec
        self._ensure_skeleton()

    # ------------------------------------------------------------------
    # Layout
    # ------------------------------------------------------------------

    def _ensure_skeleton(self) -> None:
        for sub in _SUBDIRS:
            (self.root / sub).mkdir(parents=True, exist_ok=True)

    @property
    def lock_path(self) -> Path:
        return self.root / _LOCK_NAME

    def data_path(
        self, name: str, key: str, *, suffix: str = ".parquet"
    ) -> Path:
        """Path for an FPL bulk-endpoint snapshot keyed by ``key``."""
        return self.root / "data" / f"{name}__{key}{suffix}"

    def model_path(self, key: str) -> Path:
        """Path for a joblib-pickled trained ensemble."""
        return self.root / "models" / f"ensemble__{key}.joblib"

    def run_path(self, key: str) -> Path:
        """Path for the per-run JSON artefact."""
        return self.root / "runs" / f"run__{key}.json"

    def backtest_dir(self) -> Path:
        """Directory for FR-029 backtest output artifacts."""
        return self.root / "backtests"

    # ------------------------------------------------------------------
    # Freshness
    # ------------------------------------------------------------------

    def is_fresh(self, path: Path) -> bool:
        """``True`` iff ``path`` exists and its mtime is within the TTL."""
        try:
            mtime = path.stat().st_mtime
        except FileNotFoundError:
            return False
        if not path.is_file():
            return False
        return (time.time() - mtime) < self.ttl_sec

    # ------------------------------------------------------------------
    # Cache management
    # ------------------------------------------------------------------

    def clear(self) -> None:
        """Remove the cache directory entirely and recreate the skeleton.

        Backs the ``--clear-cache`` CLI flag (FR-006).
        """
        if self.root.exists():
            shutil.rmtree(self.root)
        self._ensure_skeleton()

    @contextmanager
    def write_lock(self, *, timeout_sec: float = 30.0) -> Iterator[None]:
        """Acquire the advisory write lock for the duration of the block.

        Atomic create via ``O_EXCL`` ensures only one writer at a time
        can hold the lock. Raises :class:`CacheWriteError` if the lock
        is held longer than ``timeout_sec``.

        The lock is always released — even if the wrapped block raises.
        """
        deadline = time.monotonic() + timeout_sec
        while True:
            try:
                fd = os.open(self.lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
                os.close(fd)
                break
            except FileExistsError:
                if time.monotonic() >= deadline:
                    raise CacheWriteError(
                        f"Could not acquire cache lock at {self.lock_path} "
                        f"after {timeout_sec}s"
                    )
                time.sleep(0.05)
        try:
            yield
        finally:
            try:
                self.lock_path.unlink()
            except FileNotFoundError:
                pass


__all__ = [
    "APP_NAME",
    "DEFAULT_TTL_SEC",
    "DiskCache",
    "default_cache_dir",
    "make_cache_key",
]
