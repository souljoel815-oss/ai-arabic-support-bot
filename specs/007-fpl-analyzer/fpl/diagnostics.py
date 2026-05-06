"""Run Status / Diagnostics surface (T018, FR-038).

Three responsibilities:

* :func:`build_run_status` — assemble a :class:`fpl.types.RunStatus` while
  enforcing the source-presence matrix from ``contracts/diagnostics.md``
  (``fpl_bootstrap`` and ``fpl_fixtures`` always; ``fpl_picks`` iff a
  ``team_id`` was supplied; ``understat`` iff Understat was attempted).
* :func:`render_cli_block` / :func:`parse_status_block` — serialise to
  and from the canonical ``=== RUN STATUS ===`` text block emitted by
  the CLI. The pair round-trips structurally over the surfaced fields
  (``inputs`` is intentionally not in the CLI surface — only the JSON
  on-disk schema carries it).
* :func:`write_json_run` — write the v1 on-disk JSON record (under
  ``<cache-dir>/runs/``) that backs the web-UI panel and the contract
  test for the JSON schema.

Streamlit rendering helpers live in :mod:`fpl.ui` (T048); this module
stays render-engine-agnostic so its test surface is plain Python.
"""

from __future__ import annotations

import json
import re
from collections.abc import Iterable
from datetime import datetime
from pathlib import Path
from typing import Any

from fpl.types import (
    CacheStatus,
    DataSourceStatus,
    ModelDiagnostics,
    RunStatus,
)

_HEADER = "=== RUN STATUS ==="
_REQUIRED_SOURCES: frozenset[str] = frozenset({"fpl_bootstrap", "fpl_fixtures"})
_KV_RE = re.compile(r"^([\w.]+)=(.*)$")


# ---------------------------------------------------------------------------
# Builder
# ---------------------------------------------------------------------------


def build_run_status(
    *,
    started_at: datetime,
    elapsed_total_ms: int,
    package_version: str,
    inputs: dict[str, Any],
    sources: Iterable[DataSourceStatus],
    cache: CacheStatus,
    model: ModelDiagnostics,
    warnings: Iterable[str] = (),
) -> RunStatus:
    """Validate the source-presence matrix, then construct a RunStatus.

    Source-presence matrix (FR-038, contracts/diagnostics.md):

    * ``fpl_bootstrap``  — always present
    * ``fpl_fixtures``   — always present
    * ``fpl_picks``      — present iff ``inputs['team_id']`` is not None
    * ``understat``      — present iff Understat was attempted (caller's
      responsibility; we don't infer this — we only enforce it iff the
      caller supplies the source)

    Raises ``ValueError`` on any matrix violation so the CLI / package
    layer fails loudly before the result reaches the user.
    """
    src_list = list(sources)
    src_names = {s.name for s in src_list}

    missing = _REQUIRED_SOURCES - src_names
    if missing:
        raise ValueError(
            f"RunStatus missing required sources: {sorted(missing)}"
        )

    team_id = inputs.get("team_id")
    has_picks = "fpl_picks" in src_names
    if (team_id is not None) and not has_picks:
        raise ValueError(
            f"team_id={team_id} supplied but no 'fpl_picks' source recorded"
        )
    if (team_id is None) and has_picks:
        raise ValueError(
            "'fpl_picks' source recorded but team_id is None — invariant violated"
        )

    return RunStatus(
        started_at=started_at,
        elapsed_total_ms=elapsed_total_ms,
        package_version=package_version,
        inputs=dict(inputs),
        sources=tuple(src_list),
        cache=cache,
        model=model,
        warnings=tuple(warnings),
    )


# ---------------------------------------------------------------------------
# CLI block render / parse
# ---------------------------------------------------------------------------


def render_cli_block(rs: RunStatus) -> str:
    """Render ``rs`` as the ``=== RUN STATUS ===`` block.

    Output ends with a single trailing ``\\n`` (the contract's "trailing
    blank line" is the boundary between this block and the next CLI
    block — see ``contracts/cli.md``).

    Note on ``model.top_features``: the contract sample shows just the
    feature names. We emit ``name:importance`` pairs to enable lossless
    round-tripping in :func:`parse_status_block` while still being
    grep-friendly. ``contracts/diagnostics.md`` is updated in lock-step
    when this contract test pins the schema.
    """
    lines: list[str] = [_HEADER]
    lines.append(f"started_at={_iso(rs.started_at)}")
    lines.append(f"elapsed_total_ms={rs.elapsed_total_ms}")
    lines.append(f"package_version={rs.package_version}")
    lines.append(f"cache.hit={'true' if rs.cache.hit else 'false'}")
    lines.append(f"cache.key={rs.cache.key}")
    lines.append(f"cache.age_seconds={rs.cache.age_seconds}")
    for s in rs.sources:
        lines.append(f"source.{s.name}.status={s.status}")
        lines.append(f"source.{s.name}.detail={s.detail}")
        lines.append(f"source.{s.name}.elapsed_ms={s.elapsed_ms}")
    lines.append(f"model.baseline_mae={rs.model.baseline_mae}")
    lines.append(
        "model.weights="
        + json.dumps(rs.model.weights_used, separators=(",", ":"), sort_keys=True)
    )
    top_str = ",".join(f"{name}:{imp}" for name, imp in rs.model.top_features)
    lines.append(f"model.top_features={top_str}")
    for i, w in enumerate(rs.warnings):
        lines.append(f"warning.{i}={w}")
    return "\n".join(lines) + "\n"


def parse_status_block(text: str) -> RunStatus:
    """Inverse of :func:`render_cli_block`.

    The CLI block does not carry ``inputs``; the returned ``RunStatus``
    therefore has ``inputs={}``. This is by design — the CLI surface is
    a diagnostic, not an audit log; the audit log is the JSON on-disk
    artifact written by :func:`write_json_run`, which DOES carry inputs.
    """
    lines = [line for line in text.splitlines() if line.strip() != ""]
    if not lines or lines[0] != _HEADER:
        raise ValueError(
            f"Run status block must start with {_HEADER!r}, got {lines[0]!r if lines else '<empty>'}"
        )

    kv: dict[str, str] = {}
    for line in lines[1:]:
        m = _KV_RE.match(line)
        if not m:
            raise ValueError(f"Unparseable line in RUN STATUS block: {line!r}")
        kv[m.group(1)] = m.group(2)

    started_at = datetime.fromisoformat(kv["started_at"].replace("Z", "+00:00"))
    cache = CacheStatus(
        hit=(kv["cache.hit"] == "true"),
        key=kv["cache.key"],
        age_seconds=int(kv["cache.age_seconds"]),
    )

    # Sources: discover names by scanning keys with prefix "source." and
    # preserving first-seen order (matches the render order, which is
    # insertion order of the rs.sources tuple).
    seen: list[str] = []
    seen_set: set[str] = set()
    for k in kv:
        if k.startswith("source."):
            parts = k.split(".", 2)
            if len(parts) == 3 and parts[1] not in seen_set:
                seen.append(parts[1])
                seen_set.add(parts[1])
    sources = tuple(
        DataSourceStatus(
            name=name,                                            # type: ignore[arg-type]
            status=kv[f"source.{name}.status"],                   # type: ignore[arg-type]
            detail=kv[f"source.{name}.detail"],
            elapsed_ms=int(kv[f"source.{name}.elapsed_ms"]),
        )
        for name in seen
    )

    weights = json.loads(kv["model.weights"])
    top_features_raw = kv["model.top_features"]
    top_features = (
        tuple(_parse_feature_pair(p) for p in top_features_raw.split(","))
        if top_features_raw
        else ()
    )
    model = ModelDiagnostics(
        baseline_mae=float(kv["model.baseline_mae"]),
        top_features=top_features,
        weights_used=weights,
    )

    warnings = tuple(
        kv[k]
        for k in sorted(
            (k for k in kv if k.startswith("warning.")),
            key=lambda k: int(k.split(".", 1)[1]),
        )
    )

    return RunStatus(
        started_at=started_at,
        elapsed_total_ms=int(kv["elapsed_total_ms"]),
        package_version=kv["package_version"],
        inputs={},
        sources=sources,
        cache=cache,
        model=model,
        warnings=warnings,
    )


def _parse_feature_pair(pair: str) -> tuple[str, float]:
    name, imp = pair.rsplit(":", 1)
    return (name, float(imp))


# ---------------------------------------------------------------------------
# JSON on-disk record (v1)
# ---------------------------------------------------------------------------


def write_json_run(rs: RunStatus, path: Path) -> None:
    """Serialise ``rs`` to the v1 JSON schema documented in
    ``contracts/diagnostics.md``. Creates parent directories as needed.
    """
    payload = {
        "version": 1,
        "started_at": _iso(rs.started_at),
        "elapsed_total_ms": rs.elapsed_total_ms,
        "package_version": rs.package_version,
        "inputs": dict(rs.inputs),
        "cache": {
            "hit": rs.cache.hit,
            "key": rs.cache.key,
            "age_seconds": rs.cache.age_seconds,
        },
        "sources": [
            {
                "name": s.name,
                "status": s.status,
                "detail": s.detail,
                "elapsed_ms": s.elapsed_ms,
            }
            for s in rs.sources
        ],
        "model": {
            "baseline_mae": rs.model.baseline_mae,
            "weights_used": dict(rs.model.weights_used),
            "top_features": [list(t) for t in rs.model.top_features],
        },
        "warnings": list(rs.warnings),
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _iso(dt: datetime) -> str:
    """ISO-8601 with ``Z`` for UTC (matches the contract sample)."""
    s = dt.isoformat(timespec="seconds")
    if s.endswith("+00:00"):
        return s.replace("+00:00", "Z")
    return s


__all__ = [
    "build_run_status",
    "render_cli_block",
    "parse_status_block",
    "write_json_run",
]
