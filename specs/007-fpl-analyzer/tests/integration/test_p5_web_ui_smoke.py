"""User Story 5 — Web UI smoke test (T047).

The full Streamlit ``AppTest`` harness is heavyweight and depends on
several optional Streamlit testing internals. For this MVP smoke test
we verify:

* :mod:`fpl.ui` exposes every helper the GUI delegates to (FR-031,
  FR-038).
* :mod:`fpl_gui` (the page entrypoint) loads without import error and
  has the required top-level shape — ``main`` callable + ``cached_run_analysis``
  wrapper.
* The cached wrapper preserves the FR-033 invariant by NOT keying on
  display toggles (its parameter list is a strict subset of the analysis
  inputs).

A full ``AppTest``-driven render-and-click test is left as a future
enhancement — the constraint is real-Streamlit rendering, which the
contract test layer doesn't strictly need.
"""

from __future__ import annotations

import importlib
import importlib.util
import inspect
import sys
from pathlib import Path

import pytest


pytestmark = pytest.mark.integration


# Make the feature directory importable so `import fpl_gui` resolves.
FEATURE_DIR = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(FEATURE_DIR))


# ---------------------------------------------------------------------------
# fpl.ui surface (FR-031, FR-038)
# ---------------------------------------------------------------------------


class TestUiModuleSurface:
    def test_imports_cleanly(self):
        # Just import — verifies fpl.ui doesn't have import-time side effects
        # that break in a non-Streamlit environment.
        importlib.import_module("fpl.ui")

    @pytest.mark.parametrize(
        "name",
        [
            "format_money",
            "decorate_squad_table",
            "build_chip_card_text",
            "render_chip_cards",
            "render_run_status_panel",
        ],
    )
    def test_required_helper_exposed(self, name):
        mod = importlib.import_module("fpl.ui")
        assert hasattr(mod, name), f"fpl.ui missing required helper {name!r}"
        assert callable(getattr(mod, name))


# ---------------------------------------------------------------------------
# fpl_gui module surface
# ---------------------------------------------------------------------------


class TestFplGuiModule:
    def test_imports_cleanly(self):
        # Importing fpl_gui executes Streamlit calls (set_page_config etc.)
        # at module top level. We guard against import errors only — full
        # rendering happens via the AppTest harness in a future iteration.
        spec = importlib.util.spec_from_file_location(
            "_fpl_gui_smoke", FEATURE_DIR / "fpl_gui.py"
        )
        assert spec is not None
        # Attempt to load the module. The Streamlit globals (st.*) are
        # available; the page won't actually render without an app server.
        try:
            mod = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(mod)  # type: ignore[union-attr]
        except Exception as exc:  # pragma: no cover — diagnostic surface
            pytest.fail(f"fpl_gui.py failed to import: {exc!r}")

        assert hasattr(mod, "main"), "fpl_gui.main missing"
        assert hasattr(mod, "cached_run_analysis"), (
            "fpl_gui.cached_run_analysis missing"
        )


# ---------------------------------------------------------------------------
# FR-033 — display toggles must not invalidate the cached analysis
# ---------------------------------------------------------------------------


class TestCacheKeyDoesNotIncludeDisplayToggles:
    """``cached_run_analysis``'s parameters set the cache key. Display-only
    toggles (hide differentials, hide a chip card, etc.) must NOT appear
    in that signature — otherwise toggling one would re-run the entire
    analysis (FR-033 violation).
    """

    def test_signature_only_carries_analysis_inputs(self):
        spec = importlib.util.spec_from_file_location(
            "_fpl_gui_for_sig", FEATURE_DIR / "fpl_gui.py"
        )
        mod = importlib.util.module_from_spec(spec)  # type: ignore[arg-type]
        spec.loader.exec_module(mod)  # type: ignore[union-attr]

        sig = inspect.signature(mod.cached_run_analysis)
        # Allowed: only the 5 input keys that DO affect the analysis.
        allowed = {"target_gw", "budget", "horizon", "team_id", "no_understat"}
        actual = set(sig.parameters.keys())

        forbidden_toggle_names = {
            "show_differentials",
            "show_tc",
            "show_bb",
            "show_fh",
            "show_wc",
            "show_multi_week",
            "show_from_scratch",
        }
        violations = actual & forbidden_toggle_names
        assert not violations, (
            f"FR-033 violation: cached_run_analysis signature includes "
            f"display-only toggle(s): {sorted(violations)}"
        )
        # Strict mode (informational): all params must be in `allowed`.
        unexpected = actual - allowed
        assert not unexpected, (
            f"cached_run_analysis signature has unexpected params: {sorted(unexpected)}"
        )
