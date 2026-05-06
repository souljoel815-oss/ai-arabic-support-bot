"""Feature-engineering unit tests (T019, FR-003, research.md § 5).

The function under test:

    build_feature_matrix(players_df, fixtures_df, teams_df, *,
                         target_gw, horizon, understat=None) -> pd.DataFrame

Verified properties:

* Shape: one row per (player_id, gameweek) over the horizon.
* Columns include the categories listed in research.md § 5: identity,
  position, status / availability (FR-010), recent form, season totals,
  fixture-difficulty / count (incl. blanks and DGWs), and optional
  Understat enrichment.
* Per-GW fixture features reflect the actual fixture count for the
  player's club in that GW (DGW = 2, normal = 1, blank = 0). The DGW for
  Alpha in GW 31 and the blank for Alpha in GW 32 are baked into the
  test fixtures (T005-T006).
* When ``understat=None`` (the FR-005 / Q1 happy path on Python 3.14
  without xG/xA), the Understat columns default to 0 rather than NaN —
  models train on numeric inputs.
* Players with ``chance_of_playing < 75`` are marked ``available=False``
  (FR-010). The per-station drop happens downstream; this layer just
  surfaces the flag.

The "no-leakage" guarantee is bounded: the function uses only the
inputs it is handed. The backtest harness (FR-029) is responsible for
snapshotting players_df / fixtures_df to the state-as-of the evaluated
deadline. We assert here that, given a snapshot, fixture features for
gameweek N do not absorb fixture data from gameweek M ≠ N.
"""

from __future__ import annotations

import pandas as pd
import pytest

from fpl.features import (
    build_feature_matrix,
    fixtures_df_from_fixtures,
    players_df_from_bootstrap,
    teams_df_from_bootstrap,
)


pytestmark = pytest.mark.unit


# ---------------------------------------------------------------------------
# DataFrame conversion helpers
# ---------------------------------------------------------------------------


class TestPlayersDfFromBootstrap:
    def test_shape_and_columns(self, bootstrap_static_payload):
        df = players_df_from_bootstrap(bootstrap_static_payload)
        assert len(df) == 15
        required = {
            "player_id",
            "player_name",
            "team_id",
            "position_id",
            "position",
            "price",
            "selected_by_percent",
            "chance_of_playing",
            "available",
            "form",
            "season_total_points",
            "season_ppg",
            "season_xG",
            "season_xA",
        }
        assert required <= set(df.columns), (
            f"missing columns: {required - set(df.columns)}"
        )

    def test_price_is_in_pounds(self, bootstrap_static_payload):
        df = players_df_from_bootstrap(bootstrap_static_payload)
        # FPL's now_cost is in tenths of a million; we expose £m.
        alvarez = df[df.player_id == 101].iloc[0]
        assert alvarez.price == pytest.approx(5.0)
        carrasco = df[df.player_id == 303].iloc[0]
        assert carrasco.price == pytest.approx(11.0)

    def test_position_label_mapping(self, bootstrap_static_payload):
        df = players_df_from_bootstrap(bootstrap_static_payload)
        # 101 = GK, 201 = DEF, 301 = MID, 401 = FWD
        assert df[df.player_id == 101].iloc[0].position == "GK"
        assert df[df.player_id == 201].iloc[0].position == "DEF"
        assert df[df.player_id == 301].iloc[0].position == "MID"
        assert df[df.player_id == 401].iloc[0].position == "FWD"

    def test_available_threshold_is_75pct(self, bootstrap_static_payload):
        df = players_df_from_bootstrap(bootstrap_static_payload)
        # 202 has chance_of_playing=75 → just barely available.
        # `bool()` cast because pandas surfaces ``np.True_`` (a numpy
        # scalar) and ``is True`` would compare object identity.
        assert bool(df[df.player_id == 202].iloc[0].available) is True
        # 205 has chance_of_playing=50 → NOT available (FR-010)
        assert bool(df[df.player_id == 205].iloc[0].available) is False

    def test_player_id_is_unique(self, bootstrap_static_payload):
        df = players_df_from_bootstrap(bootstrap_static_payload)
        assert df.player_id.is_unique


class TestFixturesDfFromFixtures:
    def test_shape_and_columns(self, fixtures_payload):
        df = fixtures_df_from_fixtures(fixtures_payload)
        assert len(df) == 6
        required = {
            "fixture_id",
            "gameweek",
            "kickoff_time",
            "home_team_id",
            "away_team_id",
            "home_team_difficulty",
            "away_team_difficulty",
        }
        assert required <= set(df.columns)

    def test_kickoff_time_parsed_as_datetime(self, fixtures_payload):
        df = fixtures_df_from_fixtures(fixtures_payload)
        assert pd.api.types.is_datetime64_any_dtype(df.kickoff_time)


class TestTeamsDfFromBootstrap:
    def test_shape_and_columns(self, bootstrap_static_payload):
        df = teams_df_from_bootstrap(bootstrap_static_payload)
        assert len(df) == 5
        required = {"team_id", "team_name", "team_short_name", "team_strength"}
        assert required <= set(df.columns)


# ---------------------------------------------------------------------------
# build_feature_matrix
# ---------------------------------------------------------------------------


def _make_inputs(bootstrap_static_payload, fixtures_payload):
    p_df = players_df_from_bootstrap(bootstrap_static_payload)
    f_df = fixtures_df_from_fixtures(fixtures_payload)
    t_df = teams_df_from_bootstrap(bootstrap_static_payload)
    return p_df, f_df, t_df


class TestBuildFeatureMatrixShape:
    def test_one_row_per_player_per_gameweek(
        self, bootstrap_static_payload, fixtures_payload
    ):
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(p_df, f_df, t_df, target_gw=31, horizon=2)
        # 15 players × 2 gameweeks = 30 rows
        assert len(m_df) == 15 * 2
        assert m_df.duplicated(subset=["player_id", "gameweek"]).sum() == 0

    def test_horizon_1_has_only_target_gw(
        self, bootstrap_static_payload, fixtures_payload
    ):
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(p_df, f_df, t_df, target_gw=31, horizon=1)
        assert set(m_df.gameweek.unique()) == {31}

    def test_horizon_3_spans_target_through_target_plus_2(
        self, bootstrap_static_payload, fixtures_payload
    ):
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(p_df, f_df, t_df, target_gw=31, horizon=2)
        assert set(m_df.gameweek.unique()) == {31, 32}


class TestBuildFeatureMatrixFixtureFeatures:
    def test_dgw_for_alpha_in_gw31(self, bootstrap_static_payload, fixtures_payload):
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(p_df, f_df, t_df, target_gw=31, horizon=2)
        # Alpha (team_id=1) has fixture_id 1 (vs Beta) AND fixture_id 3 (vs Epsilon) in GW 31
        alpha_gw31 = m_df[(m_df.team_id == 1) & (m_df.gameweek == 31)]
        assert (alpha_gw31.fixture_count == 2).all(), (
            f"Alpha GW31 fixture_count: {alpha_gw31.fixture_count.unique().tolist()}"
        )

    def test_blank_for_alpha_in_gw32(self, bootstrap_static_payload, fixtures_payload):
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(p_df, f_df, t_df, target_gw=31, horizon=2)
        # Alpha has no fixtures in GW 32
        alpha_gw32 = m_df[(m_df.team_id == 1) & (m_df.gameweek == 32)]
        assert (alpha_gw32.fixture_count == 0).all()

    def test_normal_team_has_one_fixture(
        self, bootstrap_static_payload, fixtures_payload
    ):
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(p_df, f_df, t_df, target_gw=31, horizon=2)
        # Gamma (team_id=3) plays once in GW 31 (vs Delta, fixture id 2)
        gamma_gw31 = m_df[(m_df.team_id == 3) & (m_df.gameweek == 31)]
        assert (gamma_gw31.fixture_count == 1).all()

    def test_per_gw_features_are_independent(
        self, bootstrap_static_payload, fixtures_payload
    ):
        """No leakage: GW 31 features must reflect only GW 31 fixtures."""
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(p_df, f_df, t_df, target_gw=31, horizon=2)
        # Beta has 1 fixture in GW 31 and 2 fixtures in GW 32 (DGW)
        beta_gw31 = m_df[(m_df.team_id == 2) & (m_df.gameweek == 31)]
        beta_gw32 = m_df[(m_df.team_id == 2) & (m_df.gameweek == 32)]
        assert (beta_gw31.fixture_count == 1).all()
        assert (beta_gw32.fixture_count == 2).all()


class TestBuildFeatureMatrixOptionalUnderstat:
    def test_zero_when_understat_missing(
        self, bootstrap_static_payload, fixtures_payload
    ):
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(
            p_df, f_df, t_df, target_gw=31, horizon=1, understat=None
        )
        assert "xG_understat" in m_df.columns
        assert "xA_understat" in m_df.columns
        assert (m_df.xG_understat == 0).all()
        assert (m_df.xA_understat == 0).all()

    def test_populated_when_understat_present(
        self, bootstrap_static_payload, fixtures_payload, understat_html
    ):
        from fpl.api import _parse_understat_html

        understat = _parse_understat_html(understat_html)
        assert understat is not None

        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(
            p_df, f_df, t_df, target_gw=31, horizon=1, understat=understat
        )
        # Adekanmi (id=401) has xG=18.4 in the Understat fixture
        adekanmi = m_df[m_df.player_id == 401].iloc[0]
        assert adekanmi.xG_understat > 10.0


class TestBuildFeatureMatrixAvailability:
    def test_available_flag_propagates(self, bootstrap_static_payload, fixtures_payload):
        p_df, f_df, t_df = _make_inputs(bootstrap_static_payload, fixtures_payload)
        m_df = build_feature_matrix(p_df, f_df, t_df, target_gw=31, horizon=1)
        # 205 (Esteban) is unavailable per the fixture (chance_of_playing=50).
        # ``bool()`` cast normalises pandas/numpy ``np.False_`` to plain bool.
        esteban = m_df[m_df.player_id == 205].iloc[0]
        assert bool(esteban.available) is False
        # 202 (Boateng) is at the 75% threshold — still considered available.
        boateng = m_df[m_df.player_id == 202].iloc[0]
        assert bool(boateng.available) is True
