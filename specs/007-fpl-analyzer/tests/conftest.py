"""Shared pytest fixtures for the FPL Ultimate Analyzer test suite.

Pulls in the small, checked-in JSON / HTML fixtures under ``tests/fixtures/``
so contract and integration tests never hit the live FPL API.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

FIXTURES_DIR = Path(__file__).parent / "fixtures"
FPL_FIXTURES_DIR = FIXTURES_DIR / "fpl"
UNDERSTAT_FIXTURES_DIR = FIXTURES_DIR / "understat"


def _load_json(name: str) -> dict:
    return json.loads((FPL_FIXTURES_DIR / name).read_text(encoding="utf-8"))


@pytest.fixture
def bootstrap_static_payload() -> dict:
    """Representative ``/api/bootstrap-static/`` response."""
    return _load_json("bootstrap_static.json")


@pytest.fixture
def fixtures_payload() -> list:
    """Representative ``/api/fixtures/`` response (a list)."""
    return _load_json("fixtures.json")


@pytest.fixture
def picks_payload() -> dict:
    """Representative ``/api/entry/12345/event/30/picks/`` response."""
    return _load_json("picks_team_12345_gw30.json")


@pytest.fixture
def understat_html() -> str:
    """Minimal Understat EPL season page with embedded ``playersData``."""
    return (UNDERSTAT_FIXTURES_DIR / "epl_2025.html").read_text(encoding="utf-8")
