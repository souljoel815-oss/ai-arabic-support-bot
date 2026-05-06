"""Feature engineering for the FPL analyzer (T020, FR-003, research.md § 5).

Three layers:

1. **Boundary helpers** :func:`players_df_from_bootstrap`,
   :func:`teams_df_from_bootstrap`, :func:`fixtures_df_from_fixtures` —
   convert the JSON shapes returned by :mod:`fpl.api` into tidy
   DataFrames. They live here (not in :mod:`fpl.api`) so the API layer
   stays pandas-free.

2. **Main feature matrix** :func:`build_feature_matrix` — given the
   three DataFrames plus a target gameweek and horizon, returns one row
   per ``(player_id, gameweek)`` covering the horizon. Each row carries
   the identity / availability / season-total / fixture-difficulty /
   optional Understat columns required by research.md § 5.

3. **Understat optional enrichment** — when an Understat dict is
   supplied, ``xG_understat`` / ``xA_understat`` columns are populated
   per matched player (matched by name, then by club). When it isn't,
   those columns default to 0 (FR-005 graceful degradation; SC-004
   penalty is paid at training time, not here).

No-leakage scope: this layer is purely a pure function of its inputs;
the backtest harness (FR-029, T051) is responsible for snapshotting
``players_df`` / ``fixtures_df`` to the state-as-of each evaluated
deadline. The unit tests assert that fixture features for GW N do not
absorb GW M ≠ N data, which is the property *this* function owns.
"""

from __future__ import annotations

from typing import Any

import pandas as pd

from rapidfuzz import fuzz, process  # used for understat name matching


# ---------------------------------------------------------------------------
# Boundary helpers
# ---------------------------------------------------------------------------


_POSITION_LABELS: dict[int, str] = {1: "GK", 2: "DEF", 3: "MID", 4: "FWD"}


def players_df_from_bootstrap(bootstrap: dict) -> pd.DataFrame:
    """Tidy DataFrame view over ``bootstrap['elements']``."""
    elements = bootstrap.get("elements", []) or []
    rows = []
    for el in elements:
        chance = el.get("chance_of_playing_next_round")
        chance_value = float(chance) if chance is not None else 100.0
        rows.append(
            {
                "player_id": int(el["id"]),
                "player_name": el.get("web_name", ""),
                "team_id": int(el["team"]),
                "position_id": int(el["element_type"]),
                "position": _POSITION_LABELS.get(int(el["element_type"]), "?"),
                "price": float(el["now_cost"]) / 10.0,
                "selected_by_percent": float(el.get("selected_by_percent", 0.0) or 0.0),
                "chance_of_playing": chance_value,
                # FR-010: chance_of_playing >= 75 is treated as available.
                "available": chance_value >= 75.0,
                "form": float(el.get("form", 0.0) or 0.0),
                "season_total_points": int(el.get("total_points", 0) or 0),
                "season_ppg": float(el.get("points_per_game", 0.0) or 0.0),
                "season_xG": float(el.get("expected_goals", 0.0) or 0.0),
                "season_xA": float(el.get("expected_assists", 0.0) or 0.0),
                "status": el.get("status", "a"),
            }
        )
    return pd.DataFrame(rows)


def teams_df_from_bootstrap(bootstrap: dict) -> pd.DataFrame:
    """Tidy DataFrame view over ``bootstrap['teams']``."""
    teams = bootstrap.get("teams", []) or []
    rows = [
        {
            "team_id": int(t["id"]),
            "team_name": t.get("name", ""),
            "team_short_name": t.get("short_name", ""),
            "team_strength": int(t.get("strength", 3)),
            "team_strength_home": int(t.get("strength_overall_home", 1000)),
            "team_strength_away": int(t.get("strength_overall_away", 1000)),
        }
        for t in teams
    ]
    return pd.DataFrame(rows)


def fixtures_df_from_fixtures(fixtures: list) -> pd.DataFrame:
    """Tidy DataFrame view over the ``/api/fixtures/`` payload."""
    rows = [
        {
            "fixture_id": int(f["id"]),
            "gameweek": int(f["event"]) if f.get("event") is not None else 0,
            "kickoff_time": f.get("kickoff_time"),
            "finished": bool(f.get("finished", False)),
            "home_team_id": int(f["team_h"]),
            "away_team_id": int(f["team_a"]),
            "home_team_difficulty": int(f.get("team_h_difficulty", 3)),
            "away_team_difficulty": int(f.get("team_a_difficulty", 3)),
        }
        for f in fixtures
    ]
    df = pd.DataFrame(rows)
    if not df.empty:
        df["kickoff_time"] = pd.to_datetime(df["kickoff_time"], utc=True, errors="coerce")
    return df


# ---------------------------------------------------------------------------
# Per-team per-GW fixture features
# ---------------------------------------------------------------------------


def _team_gw_features(fixtures_df: pd.DataFrame, gameweek: int) -> pd.DataFrame:
    """Return one row per team with fixture features for a single gameweek.

    Aggregates DGW (fixture_count >= 2) by averaging opponent strength
    and counting fixtures. Blank teams (count = 0) come back via an
    outer merge in :func:`build_feature_matrix`.
    """
    gw_fixtures = fixtures_df[fixtures_df.gameweek == gameweek]
    if gw_fixtures.empty:
        return pd.DataFrame(
            columns=[
                "team_id",
                "fixture_count",
                "opponent_strength_avg",
                "is_home_any",
            ]
        )

    long_rows = []
    for _, row in gw_fixtures.iterrows():
        long_rows.append(
            {
                "team_id": row.home_team_id,
                "opponent_id": row.away_team_id,
                "opponent_difficulty": row.home_team_difficulty,
                "is_home": True,
            }
        )
        long_rows.append(
            {
                "team_id": row.away_team_id,
                "opponent_id": row.home_team_id,
                "opponent_difficulty": row.away_team_difficulty,
                "is_home": False,
            }
        )
    long_df = pd.DataFrame(long_rows)

    grouped = long_df.groupby("team_id").agg(
        fixture_count=("opponent_id", "size"),
        opponent_strength_avg=("opponent_difficulty", "mean"),
        is_home_any=("is_home", "any"),
    ).reset_index()
    return grouped


# ---------------------------------------------------------------------------
# Main feature matrix
# ---------------------------------------------------------------------------


_FEATURE_COLUMNS: tuple[str, ...] = (
    "player_id",
    "player_name",
    "team_id",
    "team_strength",
    "position_id",
    "position",
    "gameweek",
    "price",
    "selected_by_percent",
    "chance_of_playing",
    "available",
    "form",
    "season_total_points",
    "season_ppg",
    "season_xG",
    "season_xA",
    "fixture_count",
    "opponent_strength_avg",
    "is_home_any",
    "xG_understat",
    "xA_understat",
)


def build_feature_matrix(
    players_df: pd.DataFrame,
    fixtures_df: pd.DataFrame,
    teams_df: pd.DataFrame,
    *,
    target_gw: int,
    horizon: int = 3,
    understat: dict[str, Any] | None = None,
) -> pd.DataFrame:
    """Build the long-format per-(player, gameweek) feature matrix.

    Output one row per ``(player_id, gameweek)`` for ``gameweek`` in
    ``[target_gw, target_gw + horizon)``. Columns are listed in
    ``_FEATURE_COLUMNS`` (one per category from research.md § 5).
    """
    if horizon < 1:
        raise ValueError(f"horizon must be >= 1, got {horizon}")

    teams_join = teams_df[["team_id", "team_strength"]]

    base = players_df.merge(teams_join, on="team_id", how="left")

    # One (player, gameweek) row per horizon step.
    rows = []
    for offset in range(horizon):
        gw = target_gw + offset
        gw_features = _team_gw_features(fixtures_df, gw)
        merged = base.merge(gw_features, on="team_id", how="left")
        merged["gameweek"] = gw
        # Blank teams for this GW have NaN fixture features → fill with 0.
        merged["fixture_count"] = merged["fixture_count"].fillna(0).astype(int)
        merged["opponent_strength_avg"] = merged["opponent_strength_avg"].fillna(0.0)
        merged["is_home_any"] = merged["is_home_any"].fillna(False).astype(bool)
        rows.append(merged)

    out = pd.concat(rows, ignore_index=True)

    # Optional Understat enrichment (FR-005).
    if understat is None:
        out["xG_understat"] = 0.0
        out["xA_understat"] = 0.0
    else:
        out = _attach_understat(out, understat)

    # Pin column order so downstream code can rely on it.
    return out[list(_FEATURE_COLUMNS)].copy()


# ---------------------------------------------------------------------------
# Understat name matching (FR-005)
# ---------------------------------------------------------------------------


def _attach_understat(
    feature_df: pd.DataFrame, understat: dict[str, Any]
) -> pd.DataFrame:
    """Attach Understat ``xG`` / ``xA`` season totals via fuzzy name match.

    Falls back to 0 for any unmatched player so models train on numeric
    inputs only. The Run Status / Diagnostics panel (FR-038) records
    Understat presence; per-player match diagnostics are out of scope
    for v1.
    """
    players = understat.get("players", []) or []
    if not players:
        feature_df = feature_df.copy()
        feature_df["xG_understat"] = 0.0
        feature_df["xA_understat"] = 0.0
        return feature_df

    name_to_player: dict[str, dict] = {p["player_name"]: p for p in players}
    candidate_names = list(name_to_player.keys())

    xg_map: dict[int, float] = {}
    xa_map: dict[int, float] = {}
    seen_player_ids: set[int] = set()
    for _, row in feature_df.drop_duplicates("player_id").iterrows():
        if row.player_id in seen_player_ids:
            continue
        seen_player_ids.add(int(row.player_id))
        match = process.extractOne(
            row.player_name,
            candidate_names,
            scorer=fuzz.WRatio,
            score_cutoff=85,
        )
        if match is None:
            xg_map[int(row.player_id)] = 0.0
            xa_map[int(row.player_id)] = 0.0
            continue
        matched_name = match[0]
        u = name_to_player[matched_name]
        try:
            xg_map[int(row.player_id)] = float(u.get("xG", 0.0))
            xa_map[int(row.player_id)] = float(u.get("xA", 0.0))
        except (TypeError, ValueError):
            xg_map[int(row.player_id)] = 0.0
            xa_map[int(row.player_id)] = 0.0

    out = feature_df.copy()
    out["xG_understat"] = out.player_id.map(xg_map).fillna(0.0).astype(float)
    out["xA_understat"] = out.player_id.map(xa_map).fillna(0.0).astype(float)
    return out


__all__ = [
    "build_feature_matrix",
    "fixtures_df_from_fixtures",
    "players_df_from_bootstrap",
    "teams_df_from_bootstrap",
]
