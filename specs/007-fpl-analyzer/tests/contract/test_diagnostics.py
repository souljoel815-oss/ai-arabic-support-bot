"""Diagnostics contract tests (T017) — locks ``contracts/diagnostics.md``.

Covers:

* :func:`render_cli_block` produces the documented key order and surface.
* :func:`parse_status_block` round-trips back to a structurally-equal
  ``RunStatus`` (over the surfaced fields — ``inputs`` is intentionally
  not in the CLI block per the contract).
* :func:`write_json_run` writes the v1 on-disk JSON schema.
* :func:`build_run_status` enforces the required-source presence matrix
  across the 4 input combinations (with/without ``team_id`` ×
  with/without Understat).
"""

from __future__ import annotations

import dataclasses
import json
from datetime import datetime, timezone

import pytest

from fpl.diagnostics import (
    build_run_status,
    parse_status_block,
    render_cli_block,
    write_json_run,
)
from fpl.types import (
    CacheStatus,
    DataSourceStatus,
    ModelDiagnostics,
    RunStatus,
)


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------


def _ds(
    name: str,
    *,
    status: str = "ok",
    detail: str = "",
    elapsed_ms: int = 100,
) -> DataSourceStatus:
    return DataSourceStatus(name=name, status=status, detail=detail, elapsed_ms=elapsed_ms)  # type: ignore[arg-type]


def _make_run_status(*, with_team_id: bool = True, with_understat: bool = True) -> RunStatus:
    sources: list[DataSourceStatus] = [
        _ds("fpl_bootstrap", elapsed_ms=412),
        _ds("fpl_fixtures", elapsed_ms=187),
    ]
    if with_team_id:
        sources.append(_ds("fpl_picks", elapsed_ms=298))
    if with_understat:
        sources.append(
            _ds(
                "understat",
                status="unavailable",
                detail="python_3_14_no_aiohttp_wheel",
                elapsed_ms=0,
            )
        )

    return build_run_status(
        started_at=datetime(2026, 5, 6, 19, 22, 1, tzinfo=timezone.utc),
        elapsed_total_ms=4287,
        package_version="0.1.0",
        inputs={
            "target_gw": 31,
            "horizon": 3,
            "budget": 100.0,
            "team_id": 12345 if with_team_id else None,
            "no_understat": not with_understat,
        },
        sources=sources,
        cache=CacheStatus(hit=False, key="ab12cd34ef", age_seconds=0),
        model=ModelDiagnostics(
            baseline_mae=1.83,
            top_features=(
                ("form", 0.31),
                ("fixture_difficulty_5", 0.18),
                ("minutes_last5", 0.14),
                ("xG_last5", 0.11),
                ("price", 0.09),
            ),
            weights_used={"xgb": 0.33, "lgbm": 0.33, "catboost": 0.33},
        ),
        warnings=("Player 348 (Smith) flagged 50% chance — excluded from XI",),
    )


# ---------------------------------------------------------------------------
# render_cli_block
# ---------------------------------------------------------------------------


class TestRenderCliBlock:
    def test_header_and_trailing_newline(self):
        rs = _make_run_status()
        text = render_cli_block(rs)
        assert text.startswith("=== RUN STATUS ===\n")
        assert text.endswith("\n")

    def test_required_keys_present(self):
        rs = _make_run_status()
        text = render_cli_block(rs)
        for key in [
            "started_at=",
            "elapsed_total_ms=",
            "package_version=",
            "cache.hit=",
            "cache.key=",
            "cache.age_seconds=",
            "model.baseline_mae=",
            "model.weights=",
            "model.top_features=",
        ]:
            assert key in text, f"missing key {key!r}"

    def test_warnings_indexed_from_zero(self):
        rs = _make_run_status()
        text = render_cli_block(rs)
        # Exactly one warning in the fixture.
        assert "warning.0=" in text
        assert "warning.1=" not in text

    def test_source_keys_emitted_per_source(self):
        rs = _make_run_status()
        text = render_cli_block(rs)
        for src in ("fpl_bootstrap", "fpl_fixtures", "fpl_picks", "understat"):
            for field in ("status", "detail", "elapsed_ms"):
                assert f"source.{src}.{field}=" in text


# ---------------------------------------------------------------------------
# parse_status_block — round-trip
# ---------------------------------------------------------------------------


class TestParseStatusBlockRoundTrip:
    @pytest.mark.parametrize(
        "with_team_id, with_understat",
        [(True, True), (True, False), (False, True), (False, False)],
    )
    def test_round_trip_preserves_surfaced_fields(self, with_team_id, with_understat):
        rs1 = _make_run_status(with_team_id=with_team_id, with_understat=with_understat)
        text = render_cli_block(rs1)
        rs2 = parse_status_block(text)
        # `inputs` is intentionally not in the CLI surface — only the JSON
        # on-disk schema carries it. Compare ignoring inputs.
        expected = dataclasses.replace(rs1, inputs={})
        assert rs2 == expected

    def test_rejects_missing_header(self):
        rs = _make_run_status()
        text = render_cli_block(rs)
        # Drop the header line.
        truncated = "\n".join(text.splitlines()[1:])
        with pytest.raises(ValueError, match=r"=== RUN STATUS ==="):
            parse_status_block(truncated)


# ---------------------------------------------------------------------------
# Source-presence matrix (FR-038, contracts/diagnostics.md)
# ---------------------------------------------------------------------------


def _common_kwargs(team_id: int | None, no_understat: bool) -> dict:
    return dict(
        started_at=datetime(2026, 5, 6, tzinfo=timezone.utc),
        elapsed_total_ms=0,
        package_version="0.1.0",
        inputs={"team_id": team_id, "no_understat": no_understat},
        cache=CacheStatus(hit=False, key="x", age_seconds=0),
        model=ModelDiagnostics(baseline_mae=0.0, top_features=(), weights_used={}),
    )


class TestSourcePresenceMatrix:
    def test_required_sources_must_be_present(self):
        with pytest.raises(ValueError, match=r"required sources"):
            build_run_status(
                **_common_kwargs(team_id=None, no_understat=True),
                sources=[_ds("fpl_fixtures")],  # missing fpl_bootstrap
            )

    def test_picks_required_when_team_id_supplied(self):
        with pytest.raises(ValueError, match=r"fpl_picks"):
            build_run_status(
                **_common_kwargs(team_id=12345, no_understat=True),
                sources=[_ds("fpl_bootstrap"), _ds("fpl_fixtures")],
            )

    def test_picks_forbidden_when_team_id_absent(self):
        with pytest.raises(ValueError, match=r"fpl_picks"):
            build_run_status(
                **_common_kwargs(team_id=None, no_understat=True),
                sources=[_ds("fpl_bootstrap"), _ds("fpl_fixtures"), _ds("fpl_picks")],
            )

    def test_all_four_combos_build_successfully(self):
        # Case 1: from-scratch + Understat
        build_run_status(
            **_common_kwargs(team_id=None, no_understat=False),
            sources=[_ds("fpl_bootstrap"), _ds("fpl_fixtures"), _ds("understat", status="ok")],
        )
        # Case 2: from-scratch + no Understat
        build_run_status(
            **_common_kwargs(team_id=None, no_understat=True),
            sources=[_ds("fpl_bootstrap"), _ds("fpl_fixtures")],
        )
        # Case 3: continuity + Understat
        build_run_status(
            **_common_kwargs(team_id=12345, no_understat=False),
            sources=[
                _ds("fpl_bootstrap"),
                _ds("fpl_fixtures"),
                _ds("fpl_picks"),
                _ds("understat", status="ok"),
            ],
        )
        # Case 4: continuity + no Understat
        build_run_status(
            **_common_kwargs(team_id=12345, no_understat=True),
            sources=[_ds("fpl_bootstrap"), _ds("fpl_fixtures"), _ds("fpl_picks")],
        )


# ---------------------------------------------------------------------------
# JSON on-disk schema (v1)
# ---------------------------------------------------------------------------


class TestWriteJsonRun:
    def test_v1_schema_top_level(self, tmp_path):
        rs = _make_run_status()
        path = tmp_path / "run_001.json"
        write_json_run(rs, path)
        data = json.loads(path.read_text(encoding="utf-8"))

        assert data["version"] == 1
        for key in ("started_at", "elapsed_total_ms", "package_version", "inputs",
                     "cache", "sources", "model", "warnings"):
            assert key in data, f"missing top-level key {key!r}"

    def test_sources_have_required_fields(self, tmp_path):
        rs = _make_run_status()
        path = tmp_path / "run_002.json"
        write_json_run(rs, path)
        data = json.loads(path.read_text(encoding="utf-8"))

        assert isinstance(data["sources"], list)
        for s in data["sources"]:
            assert {"name", "status", "detail", "elapsed_ms"} <= set(s.keys())

    def test_model_top_features_pairs(self, tmp_path):
        rs = _make_run_status()
        path = tmp_path / "run_003.json"
        write_json_run(rs, path)
        data = json.loads(path.read_text(encoding="utf-8"))

        # top_features is a list of [name, importance] pairs.
        for item in data["model"]["top_features"]:
            assert isinstance(item, list)
            assert len(item) == 2
            assert isinstance(item[0], str)
            assert isinstance(item[1], (int, float))

    def test_inputs_round_trip_via_json(self, tmp_path):
        rs = _make_run_status()
        path = tmp_path / "run_004.json"
        write_json_run(rs, path)
        data = json.loads(path.read_text(encoding="utf-8"))

        # The JSON-on-disk surface IS expected to carry inputs (unlike the
        # CLI block). Verifies asymmetry between the two surfaces.
        assert data["inputs"]["team_id"] == 12345
        assert data["inputs"]["target_gw"] == 31

    def test_creates_parent_directory(self, tmp_path):
        rs = _make_run_status()
        path = tmp_path / "nested" / "deeper" / "run.json"
        write_json_run(rs, path)
        assert path.exists()
