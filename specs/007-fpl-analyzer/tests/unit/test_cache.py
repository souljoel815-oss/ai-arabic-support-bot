"""Disk cache unit tests (T015) — FR-006, SC-005, Q3 clarification.

Covers:

* Layout: skeleton subdirectories created under the root.
* :func:`make_cache_key` is deterministic; varying any input
  produces a distinct key; ``team_id=None`` is distinct from any int.
* TTL: a missing file is not fresh; a recent file is fresh; a forged-old
  file is not fresh.
* Restart survival: re-instantiating ``DiskCache`` against the same
  root preserves files within TTL.
* ``clear()`` removes the directory and recreates the skeleton.
* Advisory write lock acquires + releases, and raises
  :class:`CacheWriteError` after ``timeout_sec`` if held.
"""

from __future__ import annotations

import os
import time

import pytest

from fpl.cache import (
    APP_NAME,
    DEFAULT_TTL_SEC,
    DiskCache,
    default_cache_dir,
    make_cache_key,
)
from fpl.errors import CacheWriteError


pytestmark = pytest.mark.unit


# ---------------------------------------------------------------------------
# make_cache_key
# ---------------------------------------------------------------------------


class TestMakeCacheKey:
    def test_deterministic_for_same_inputs(self):
        k1 = make_cache_key(
            target_gw=31, horizon=3, budget=100.0, team_id=12345, no_understat=False
        )
        k2 = make_cache_key(
            target_gw=31, horizon=3, budget=100.0, team_id=12345, no_understat=False
        )
        assert k1 == k2
        assert isinstance(k1, str)
        assert len(k1) >= 8  # truncated SHA-1 hex

    @pytest.mark.parametrize(
        "field, value",
        [
            ("target_gw", 30),
            ("horizon", 2),
            ("budget", 95.0),
            ("team_id", 99999),
            ("no_understat", True),
        ],
    )
    def test_distinct_for_each_input_field(self, field, value):
        base = dict(
            target_gw=31, horizon=3, budget=100.0, team_id=12345, no_understat=False
        )
        k_base = make_cache_key(**base)
        modified = {**base, field: value}
        assert make_cache_key(**modified) != k_base, (
            f"cache key did not change when {field}={value}"
        )

    def test_team_id_none_is_distinct_from_int(self):
        k_with = make_cache_key(
            target_gw=31, horizon=3, budget=100.0, team_id=12345, no_understat=False
        )
        k_without = make_cache_key(
            target_gw=31, horizon=3, budget=100.0, team_id=None, no_understat=False
        )
        assert k_with != k_without


# ---------------------------------------------------------------------------
# Layout
# ---------------------------------------------------------------------------


class TestDiskCacheLayout:
    def test_skeleton_dirs_created(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        for sub in ("data", "models", "runs", "backtests"):
            assert (cache.root / sub).is_dir(), f"missing skeleton dir {sub!r}"

    def test_path_helpers_under_root(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        key = "abc1234567"
        assert cache.data_path("bootstrap", key).parent == cache.root / "data"
        assert cache.model_path(key).parent == cache.root / "models"
        assert cache.run_path(key).parent == cache.root / "runs"
        assert cache.backtest_dir() == cache.root / "backtests"

    def test_lock_path_at_root(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        assert cache.lock_path.parent == cache.root


# ---------------------------------------------------------------------------
# TTL freshness
# ---------------------------------------------------------------------------


class TestTtl:
    def test_missing_file_is_not_fresh(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        assert cache.is_fresh(cache.data_path("nope", "x")) is False

    def test_recent_file_is_fresh(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        path = cache.data_path("bootstrap", "x")
        path.write_text("{}", encoding="utf-8")
        assert cache.is_fresh(path) is True

    def test_old_file_beyond_ttl_is_not_fresh(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root", ttl_sec=2)
        path = cache.data_path("bootstrap", "x")
        path.write_text("{}", encoding="utf-8")
        # Forge an mtime older than the 2-second TTL.
        old = time.time() - 100
        os.utime(path, (old, old))
        assert cache.is_fresh(path) is False

    def test_default_ttl_is_one_hour(self):
        # FR-006 / Q3 clarification.
        assert DEFAULT_TTL_SEC == 3600


# ---------------------------------------------------------------------------
# Restart survival (Q3 clarification)
# ---------------------------------------------------------------------------


class TestRestartSurvival:
    def test_files_visible_after_reinitialisation(self, tmp_path):
        root = tmp_path / "cache_root"
        cache1 = DiskCache(root)
        path = cache1.data_path("bootstrap", "abc1234567")
        path.write_text('{"hello": "world"}', encoding="utf-8")

        # Simulate process restart — old reference goes away.
        del cache1
        cache2 = DiskCache(root)

        assert cache2.is_fresh(path)
        assert path.read_text(encoding="utf-8") == '{"hello": "world"}'


# ---------------------------------------------------------------------------
# clear()
# ---------------------------------------------------------------------------


class TestClear:
    def test_clear_removes_files_and_recreates_skeleton(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        (cache.root / "data" / "junk.parquet").write_text("garbage", encoding="utf-8")
        (cache.root / "models" / "junk.joblib").write_text("g", encoding="utf-8")
        (cache.root / "runs" / "junk.json").write_text("g", encoding="utf-8")

        cache.clear()

        for sub in ("data", "models", "runs", "backtests"):
            d = cache.root / sub
            assert d.is_dir(), f"skeleton dir {sub!r} missing after clear"
            assert list(d.iterdir()) == [], f"dir {sub!r} not empty after clear"

    def test_clear_removes_lock_file_too(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        cache.lock_path.touch()
        cache.clear()
        assert not cache.lock_path.exists()


# ---------------------------------------------------------------------------
# Advisory write lock
# ---------------------------------------------------------------------------


class TestWriteLock:
    def test_acquires_and_releases(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        with cache.write_lock(timeout_sec=1.0):
            assert cache.lock_path.exists()
        # Lock file removed on exit.
        assert not cache.lock_path.exists()

    def test_already_held_lock_raises_after_timeout(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        # Manually create the lock to simulate a different writer holding it.
        cache.lock_path.touch()
        with pytest.raises(CacheWriteError, match=r"lock"):
            with cache.write_lock(timeout_sec=0.05):
                pass

    def test_releases_lock_on_exception(self, tmp_path):
        cache = DiskCache(tmp_path / "cache_root")
        with pytest.raises(RuntimeError):
            with cache.write_lock(timeout_sec=1.0):
                assert cache.lock_path.exists()
                raise RuntimeError("simulated write failure")
        # Lock should still be released even though the inner block raised.
        assert not cache.lock_path.exists()


# ---------------------------------------------------------------------------
# default cache dir uses platformdirs / APP_NAME
# ---------------------------------------------------------------------------


def test_default_cache_dir_uses_app_name():
    p = default_cache_dir()
    assert APP_NAME in str(p), (
        f"default cache dir {p!s} should contain {APP_NAME!r}"
    )
