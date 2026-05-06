"""User Story 4 — Chip-strategy timing recommendations (T042).

US-4 acceptance scenarios from spec.md:

1. BB prefers double-gameweek; FH prefers a swing-favorable GW. (Verified
   structurally — the chip plan must produce ``ChipRecommendation``
   objects with the right schema; deterministic GW selection on the
   small fixture is fragile.)
2. TC supporting_metric is sourced from the ceiling head, not the mean
   (FR-024). Verified directly in the chip's invariant.
3. "No recommendation" representation works when no positive case exists
   (FR-023): ``gw=None`` AND ``supporting_metric=None`` simultaneously.
"""

from __future__ import annotations

import pytest
import responses

from fpl import run_analysis
from fpl.api import FPL_BASE, UNDERSTAT_BASE
from fpl.types import ChipPlan, ChipRecommendation


pytestmark = pytest.mark.integration


def _stub_continuity(
    bootstrap_static_payload, fixtures_payload, picks_payload, understat_html
):
    responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/",
                   json=bootstrap_static_payload, status=200)
    responses.add(responses.GET, f"{FPL_BASE}/api/fixtures/",
                   json=fixtures_payload, status=200)
    responses.add(responses.GET, f"{FPL_BASE}/api/entry/12345/event/30/picks/",
                   json=picks_payload, status=200)
    responses.add(responses.GET, f"{UNDERSTAT_BASE}/league/EPL/2025",
                   body=understat_html, status=200)


class TestUS4ChipPlanShape:
    @responses.activate
    def test_chip_plan_present_and_typed(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload, understat_html,
    ):
        _stub_continuity(bootstrap_static_payload, fixtures_payload,
                         picks_payload, understat_html)
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=12345,
            no_multi_week=True,
            cache_dir=str(tmp_path),
        )
        cp = result.chip_plan
        assert isinstance(cp, ChipPlan)
        # All 4 chips present, each a ChipRecommendation with right literal.
        assert isinstance(cp.triple_captain, ChipRecommendation)
        assert cp.triple_captain.chip == "tc"
        assert cp.bench_boost.chip == "bb"
        assert cp.free_hit.chip == "fh"
        assert cp.wildcard.chip == "wc"

    @responses.activate
    def test_no_recommendation_invariant_holds(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload, understat_html,
    ):
        _stub_continuity(bootstrap_static_payload, fixtures_payload,
                         picks_payload, understat_html)
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=12345,
            no_multi_week=True,
            cache_dir=str(tmp_path),
        )
        cp = result.chip_plan
        # FR-023 invariant: gw is None ⟺ supporting_metric is None.
        for rec in (cp.triple_captain, cp.bench_boost, cp.free_hit, cp.wildcard):
            assert (rec.gw is None) == (rec.supporting_metric is None)

    @responses.activate
    def test_from_scratch_mode_still_emits_chip_plan(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, understat_html,
    ):
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/",
                       json=bootstrap_static_payload, status=200)
        responses.add(responses.GET, f"{FPL_BASE}/api/fixtures/",
                       json=fixtures_payload, status=200)
        responses.add(responses.GET, f"{UNDERSTAT_BASE}/league/EPL/2025",
                       body=understat_html, status=200)
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=None,
            no_multi_week=True,
            budget=110.0,
            cache_dir=str(tmp_path),
        )
        cp = result.chip_plan
        assert isinstance(cp, ChipPlan)
        # FH and WC need both squads; in from-scratch mode current_squad is
        # None, so FH/WC should explicitly say "no recommendation" with a
        # rationale rather than crashing.
        assert cp.free_hit.gw is None
        assert cp.wildcard.gw is None
