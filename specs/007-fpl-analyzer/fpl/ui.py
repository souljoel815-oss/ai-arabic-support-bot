"""Streamlit rendering helpers (T048, FR-031, FR-038).

Two layers:

* **Pure helpers** — :func:`format_money`, :func:`decorate_squad_table`,
  :func:`build_chip_card_text`. No Streamlit dependency at runtime;
  testable without an app context.
* **Streamlit renderers** — :func:`render_chip_cards` (typed
  :class:`fpl.types.ChipPlan`) and :func:`render_run_status_panel`
  (typed :class:`fpl.types.RunStatus`). They import
  :mod:`streamlit` lazily so the pure helpers above stay importable in
  CLI-only environments.

The page entrypoint (``fpl_gui.py``) keeps its inline ``_render_*``
helpers for the legacy dict-shaped inputs (recommendation card,
multi-week plan table, outlook tables) and delegates to this module
for the typed surfaces and for the new Run Status panel that wasn't
in V11.
"""

from __future__ import annotations

from typing import Any

import pandas as pd

from fpl.types import ChipPlan, ChipRecommendation, RunStatus


# ---------------------------------------------------------------------------
# Pure helpers
# ---------------------------------------------------------------------------


def format_money(value: Any) -> str:
    """Render a £m value as ``£X.Xm`` (one decimal, FPL convention)."""
    return f"£{float(value):.1f}m"


def decorate_squad_table(
    df: pd.DataFrame,
    captain_name: str | None = None,
    vice_name: str | None = None,
) -> pd.DataFrame:
    """Project + rename a squad/predictions DataFrame for display.

    Selects the friendly subset of columns and renames them. When a
    captain (and optionally vice) name is supplied, prepends a ``Role``
    column tagging Captain / Vice / blank.
    """
    cols = [
        c
        for c in (
            "player_name",
            "team_name",
            "position",
            "price",
            "predicted",
            "horizon_total",
            "selected_by_percent",
            "fixture_ticker_5",
        )
        if c in df.columns
    ]
    view = df[cols].copy()
    rename_map = {
        "player_name": "Player",
        "team_name": "Team",
        "position": "Pos",
        "price": "Price",
        "predicted": "GW Pred",
        "horizon_total": "Weighted Total",
        "selected_by_percent": "Own %",
        "fixture_ticker_5": "Fixture Ticker",
    }
    view = view.rename(columns=rename_map)
    if captain_name is not None and "Player" in view.columns:
        roles = [
            "Captain"
            if p == captain_name
            else ("Vice" if p == vice_name else "")
            for p in view["Player"]
        ]
        view.insert(0, "Role", roles)
    return view


def build_chip_card_text(rec: ChipRecommendation) -> str:
    """Compose the body text of a chip card from a :class:`ChipRecommendation`.

    Used by :func:`render_chip_cards` and unit-tested in isolation so
    we can assert on the exact format without spinning up Streamlit.
    """
    if rec.gw is None:
        return f"No recommendation available\n\n{rec.rationale}"
    metric_str = (
        f"{rec.supporting_metric:.2f}" if rec.supporting_metric is not None else "?"
    )
    return f"GW{rec.gw}\n\nMetric: {metric_str}\n\n{rec.rationale}"


# ---------------------------------------------------------------------------
# Streamlit renderers (Streamlit imported lazily)
# ---------------------------------------------------------------------------


_CHIP_LABELS: dict[str, str] = {
    "triple_captain": "Triple Captain",
    "bench_boost": "Bench Boost",
    "free_hit": "Free Hit",
    "wildcard": "Wildcard",
}


def render_chip_cards(
    plan: ChipPlan,
    *,
    show_tc: bool = True,
    show_bb: bool = True,
    show_fh: bool = True,
    show_wc: bool = True,
) -> None:
    """Render up to 4 chip cards via Streamlit columns (FR-031)."""
    import streamlit as st

    enabled = [
        (attr, _CHIP_LABELS[attr])
        for attr, show in (
            ("triple_captain", show_tc),
            ("bench_boost", show_bb),
            ("free_hit", show_fh),
            ("wildcard", show_wc),
        )
        if show
    ]
    if not enabled:
        return
    cols = st.columns(len(enabled))
    for col, (attr, label) in zip(cols, enabled):
        rec = getattr(plan, attr)
        col.info(f"**{label}**\n\n{build_chip_card_text(rec)}")


def render_run_status_panel(rs: RunStatus, *, container: Any | None = None) -> None:
    """Render the unified Run Status / Diagnostics panel (FR-038).

    Shown in both the CLI (via ``fpl.diagnostics.render_cli_block``) and
    the web UI (here). When ``container`` is supplied (e.g., a Streamlit
    ``st.expander``), all output goes inside it; otherwise it renders at
    the top level.
    """
    import streamlit as st

    target = container if container is not None else st

    target.subheader("Run Status / Diagnostics")
    cols = target.columns(4)
    cols[0].metric("Elapsed", f"{rs.elapsed_total_ms} ms")
    cols[1].metric("Cache", "HIT" if rs.cache.hit else "MISS")
    cols[2].metric("Baseline MAE", f"{rs.model.baseline_mae:.2f}")
    cols[3].metric("Started", rs.started_at.strftime("%H:%M:%S"))

    if rs.sources:
        src_data = [
            {
                "Source": s.name,
                "Status": s.status,
                "Detail": s.detail,
                "Elapsed (ms)": s.elapsed_ms,
            }
            for s in rs.sources
        ]
        target.dataframe(
            pd.DataFrame(src_data), use_container_width=True, hide_index=True
        )

    if rs.model.top_features:
        with target.expander("Model diagnostics"):
            st.dataframe(
                pd.DataFrame(rs.model.top_features, columns=["Feature", "Importance"]),
                use_container_width=True,
                hide_index=True,
            )
            st.json(rs.model.weights_used)

    for w in rs.warnings:
        target.warning(w)


__all__ = [
    "build_chip_card_text",
    "decorate_squad_table",
    "format_money",
    "render_chip_cards",
    "render_run_status_panel",
]
