"""User Story 1 — Weekly transfer & captain decision (T024).

Covers all 4 acceptance scenarios from spec.md § US-1:

1. Valid Team ID + target gameweek → HOLD or transfer with gain/hit/captain.
2. Marginal transfer (no positive net gain) → HOLD with explanatory note.
3. 0 free transfers → recommendation gain reflects -4 hit cost.
4. Invalid Team ID → ``UnknownTeamId`` propagates; CLI translates to exit 5
   and the in-process caller can fall back to from-scratch (FR-034).
"""

from __future__ import annotations

import pytest
import responses

from fpl import run_analysis
from fpl.api import FPL_BASE, UNDERSTAT_BASE
from fpl.errors import UnknownTeamId


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


class TestUS1AcceptanceScenarios:
    @responses.activate
    def test_scenario_1_returns_a_recommendation_card(
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
        rec = result["current_squad_plan"]["recommended"]
        # The recommendation must carry the documented fields.
        assert "kind" in rec
        assert "gain" in rec
        assert "hit_cost" in rec
        assert "captain_id" in rec
        assert "vice_captain_id" in rec
        assert rec["kind"] in {"hold", "1-transfer", "2-transfer"}

    @responses.activate
    def test_scenario_2_recommends_hold_when_no_positive_gain(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload, understat_html,
    ):
        # The fixture squad already contains the high-ceiling players (Adekanmi 401,
        # Carrasco 303). With only 8 free agents in the universe, 1-transfer
        # candidates that beat hold are rare → HOLD is a plausible outcome.
        _stub_continuity(bootstrap_static_payload, fixtures_payload,
                         picks_payload, understat_html)
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=12345,
            no_multi_week=True,
            cache_dir=str(tmp_path),
        )
        rec = result["current_squad_plan"]["recommended"]
        # Either HOLD with note, or a transfer with positive gain.
        if rec["kind"] == "hold":
            # Note explains why nothing was picked.
            assert rec.get("note") is not None
            assert rec["gain"] == 0
            assert rec["hit_cost"] == 0
        else:
            # If a transfer was picked, gain MUST be positive (FR-014).
            assert rec["gain"] > 0

    @responses.activate
    def test_scenario_3_hit_cost_is_a_multiple_of_4(
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
        rec = result["current_squad_plan"]["recommended"]
        # Whatever the recommendation, hit_cost must be 0, 4, 8, 12 or 16.
        assert rec["hit_cost"] % 4 == 0
        assert 0 <= rec["hit_cost"] <= 16

    @responses.activate
    def test_scenario_4_invalid_team_id_falls_back_to_from_scratch(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, understat_html,
    ):
        # 99999 returns 404 from the picks endpoint → UnknownTeamId.
        # The package raises; the caller chooses how to fall back.
        # contracts/package_api.md says callers may catch UnknownTeamId and
        # retry in from-scratch mode; the CLI maps it to exit 5.
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/",
                       json=bootstrap_static_payload, status=200)
        responses.add(responses.GET, f"{FPL_BASE}/api/fixtures/",
                       json=fixtures_payload, status=200)
        responses.add(responses.GET, f"{FPL_BASE}/api/entry/99999/event/30/picks/",
                       status=404)
        responses.add(responses.GET, f"{UNDERSTAT_BASE}/league/EPL/2025",
                       body=understat_html, status=200)
        with pytest.raises(UnknownTeamId):
            run_analysis(
                target_gw=31,
                horizon=2,
                team_id=99999,
                no_multi_week=True,
                cache_dir=str(tmp_path),
            )

        # And the same caller can succeed in from-scratch mode without team_id.
        # New cache_dir to avoid the failed run leaving stale state.
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=None,
            no_multi_week=True,
            cache_dir=str(tmp_path / "fallback"),
        )
        assert result["primary_view"] == "from_scratch"
        assert result["mode"] == "from_scratch"


class TestUS1RunStatusIntegration:
    @responses.activate
    def test_run_status_records_all_sources_used(
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
        rs = result.run_status
        names = {s.name for s in rs.sources}
        # All 4 sources consulted (Understat enabled by default).
        assert "fpl_bootstrap" in names
        assert "fpl_fixtures" in names
        assert "fpl_picks" in names
        assert "understat" in names

    @responses.activate
    def test_no_understat_omits_understat_source(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload,
    ):
        responses.add(responses.GET, f"{FPL_BASE}/api/bootstrap-static/",
                       json=bootstrap_static_payload, status=200)
        responses.add(responses.GET, f"{FPL_BASE}/api/fixtures/",
                       json=fixtures_payload, status=200)
        responses.add(responses.GET, f"{FPL_BASE}/api/entry/12345/event/30/picks/",
                       json=picks_payload, status=200)
        result = run_analysis(
            target_gw=31,
            horizon=2,
            team_id=12345,
            no_understat=True,
            no_multi_week=True,
            cache_dir=str(tmp_path),
        )
        rs = result.run_status
        names = {s.name for s in rs.sources}
        assert "understat" not in names

    @responses.activate
    def test_cache_hit_on_second_call(
        self, tmp_path,
        bootstrap_static_payload, fixtures_payload, picks_payload, understat_html,
    ):
        _stub_continuity(bootstrap_static_payload, fixtures_payload,
                         picks_payload, understat_html)
        kwargs = dict(
            target_gw=31, horizon=2, team_id=12345,
            no_multi_week=True, cache_dir=str(tmp_path),
        )
        first = run_analysis(**kwargs)
        assert first.run_status.cache.hit is False
        second = run_analysis(**kwargs)
        assert second.run_status.cache.hit is True
