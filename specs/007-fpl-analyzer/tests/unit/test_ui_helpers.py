"""UI helper unit tests (T046).

Covers the pure helpers in :mod:`fpl.ui` — the ones that don't need a
Streamlit runtime to test. The Streamlit-bound rendering functions are
exercised by ``tests/integration/test_p5_web_ui_smoke.py``.
"""

from __future__ import annotations

import pandas as pd
import pytest

from fpl.types import ChipRecommendation
from fpl.ui import (
    build_chip_card_text,
    decorate_squad_table,
    format_money,
)


pytestmark = pytest.mark.unit


# ---------------------------------------------------------------------------
# format_money
# ---------------------------------------------------------------------------


class TestFormatMoney:
    @pytest.mark.parametrize(
        "value, expected",
        [
            (100.0, "£100.0m"),
            (95.5, "£95.5m"),
            (0, "£0.0m"),
            (4.25, "£4.3m"),  # rounded to 1 decimal
            (12, "£12.0m"),
        ],
    )
    def test_format(self, value, expected):
        assert format_money(value) == expected


# ---------------------------------------------------------------------------
# decorate_squad_table
# ---------------------------------------------------------------------------


def _sample_squad_df() -> pd.DataFrame:
    return pd.DataFrame(
        [
            {
                "player_name": "Alvarez",
                "team_name": "Alpha FC",
                "position": "GK",
                "price": 5.0,
                "predicted": 4.2,
                "horizon_total": 12.5,
                "selected_by_percent": 12.0,
            },
            {
                "player_name": "Bishop",
                "team_name": "Delta Town",
                "position": "FWD",
                "price": 8.0,
                "predicted": 6.5,
                "horizon_total": 18.7,
                "selected_by_percent": 16.5,
            },
        ]
    )


class TestDecorateSquadTable:
    def test_renames_columns_to_friendly_labels(self):
        df = _sample_squad_df()
        view = decorate_squad_table(df)
        # Original columns are renamed.
        assert "Player" in view.columns
        assert "Team" in view.columns
        assert "Pos" in view.columns
        assert "Price" in view.columns
        assert "GW Pred" in view.columns
        assert "Weighted Total" in view.columns
        assert "Own %" in view.columns
        # Originals should not be present.
        assert "player_name" not in view.columns
        assert "team_name" not in view.columns

    def test_role_column_marks_captain_and_vice(self):
        df = _sample_squad_df()
        view = decorate_squad_table(df, captain_name="Alvarez", vice_name="Bishop")
        assert "Role" in view.columns
        roles = view["Role"].tolist()
        assert roles == ["Captain", "Vice"]

    def test_no_role_column_when_captain_not_supplied(self):
        df = _sample_squad_df()
        view = decorate_squad_table(df)
        assert "Role" not in view.columns

    def test_passes_through_only_known_columns(self):
        df = _sample_squad_df()
        df["unknown_col"] = "extra"
        view = decorate_squad_table(df)
        # Unknown columns are dropped from the friendly view.
        assert "unknown_col" not in view.columns


# ---------------------------------------------------------------------------
# build_chip_card_text
# ---------------------------------------------------------------------------


class TestBuildChipCardText:
    def test_no_recommendation_returns_explanatory_text(self):
        rec = ChipRecommendation(
            chip="tc",
            gw=None,
            supporting_metric=None,
            rationale="No positive case found in the planning horizon.",
        )
        text = build_chip_card_text(rec)
        assert "No recommendation" in text
        assert "No positive case" in text

    def test_recommendation_includes_gw_and_rationale(self):
        rec = ChipRecommendation(
            chip="tc",
            gw=31,
            supporting_metric=15.5,
            rationale="GW31: Adekanmi ceiling = 15.50",
        )
        text = build_chip_card_text(rec)
        assert "GW31" in text
        assert "Adekanmi" in text
