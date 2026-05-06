"""CLI entry point (T030, FR-028, contracts/cli.md).

Public surface:

    fpl.cli.main(argv: list[str] | None = None) -> int

Returns a process exit code mapping per ``contracts/cli.md``:

* 0 — success
* 2 — invalid argument (argparse failure or out-of-range value)
* 3 — unrecoverable FPL public-data failure (``FPLApiError``)
* 4 — unrecoverable cache write failure (``CacheWriteError``)
* 5 — invalid ``--team-id`` (``UnknownTeamId``)

stdout block layout in analysis mode (per contract):

    === RUN STATUS ===
    ...

    === RECOMMENDATION ===
    ...

    === MULTI-WEEK PLAN ===   (omitted when --no-multi-week)
    ...

    === CHIP PLAN ===
    ...

    === DIFFERENTIALS ===
    ...

Backtest mode (FR-029) is a CLI placeholder until T051 lands.
"""

from __future__ import annotations

import argparse
import sys
from typing import IO, Any

from fpl.cache import DiskCache
from fpl.diagnostics import render_cli_block
from fpl.errors import CacheWriteError, FPLApiError, UnknownTeamId
from fpl.run import run_analysis


# ---------------------------------------------------------------------------
# Argument parser
# ---------------------------------------------------------------------------


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="fpl_main.py",
        description=(
            "FPL Ultimate Analyzer — weekly transfer/captain decisions, "
            "multi-week planning, chip strategy."
        ),
    )
    # Sentinels (default=None) where we need to distinguish "user-supplied"
    # from "default" — for the --backtest mutual-exclusivity rules.
    parser.add_argument("--gw", type=int, default=None,
                         help="Target gameweek (1..38; omit or 0 for auto-detect)")
    parser.add_argument("--horizon", type=int, default=None,
                         help="Planning horizon (1..5, default 3)")
    parser.add_argument("--team-id", type=int, default=None,
                         help="FPL Team ID — enables squad-continuity mode")
    parser.add_argument("--budget", type=float, default=100.0,
                         help="From-scratch budget in £m (80..110, default 100)")
    parser.add_argument("--no-understat", action="store_true",
                         help="Skip xG/xA enrichment from Understat")
    parser.add_argument("--no-multi-week", action="store_true",
                         help="Skip the beam-search multi-week plan")
    parser.add_argument("--backtest", type=str, default=None,
                         help="Comma-separated past GWs to evaluate (FR-029)")
    parser.add_argument("--clear-cache", action="store_true",
                         help="Delete the cache directory and exit")
    parser.add_argument("--cache-dir", type=str, default=None,
                         help="Override the OS-standard cache directory")
    return parser


# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    """CLI entry point. ``fpl_main.py`` calls ``sys.exit(main())``."""
    parser = _build_parser()
    try:
        args = parser.parse_args(argv)
    except SystemExit as exc:
        # argparse exits 0 on --help; propagate that. Any other code (2 for
        # parse failures) → return 2 so the caller doesn't kill the process.
        code = exc.code if exc.code is not None else 0
        if code == 0:
            raise
        return 2

    # ---- Range validation (boundary check, FR-028) -----------------------
    if args.gw is not None and not (0 <= args.gw <= 38):
        print(f"--gw must be 0..38 (0 = auto-detect), got {args.gw}",
              file=sys.stderr)
        return 2
    if args.horizon is not None and not (1 <= args.horizon <= 5):
        print(f"--horizon must be 1..5, got {args.horizon}", file=sys.stderr)
        return 2
    if not (80.0 <= args.budget <= 110.0):
        print(f"--budget must be 80.0..110.0, got {args.budget}",
              file=sys.stderr)
        return 2

    # ---- Mutually-exclusive flags (contracts/cli.md) ---------------------
    if args.backtest is not None:
        if args.team_id is not None:
            print("--backtest is mutually exclusive with --team-id",
                  file=sys.stderr)
            return 2
        if args.horizon is not None:
            print("--backtest is mutually exclusive with --horizon",
                  file=sys.stderr)
            return 2
        if args.no_multi_week:
            print("--backtest is mutually exclusive with --no-multi-week",
                  file=sys.stderr)
            return 2

    # ---- --clear-cache short-circuit -------------------------------------
    if args.clear_cache:
        try:
            DiskCache(args.cache_dir).clear()
        except CacheWriteError as exc:
            print(f"Cache clear failed: {exc}", file=sys.stderr)
            return 4
        print("Cache cleared.")
        return 0

    # ---- --backtest stub (T051 lands the real impl) ----------------------
    if args.backtest is not None:
        print("Backtest mode is not yet implemented (T051 / Phase 8).",
              file=sys.stderr)
        return 2

    # ---- Run the analysis ------------------------------------------------
    horizon = args.horizon if args.horizon is not None else 3
    target_gw = args.gw if (args.gw is not None and args.gw > 0) else None

    try:
        result = run_analysis(
            target_gw=target_gw,
            horizon=horizon,
            budget=args.budget,
            team_id=args.team_id,
            no_understat=args.no_understat,
            no_multi_week=args.no_multi_week,
            cache_dir=args.cache_dir,
        )
    except UnknownTeamId as exc:
        print(f"[fpl] Unknown FPL team_id: {exc.team_id}", file=sys.stderr)
        return 5
    except FPLApiError as exc:
        print(f"[fpl] FPL API error: {exc}", file=sys.stderr)
        return 3
    except CacheWriteError as exc:
        print(f"[fpl] Cache write error: {exc}", file=sys.stderr)
        return 4
    except ValueError as exc:
        print(f"[fpl] Invalid argument: {exc}", file=sys.stderr)
        return 2

    # ---- Render the documented stdout blocks -----------------------------
    out = sys.stdout
    out.write(render_cli_block(result.run_status))
    out.write("\n")

    out.write("=== RECOMMENDATION ===\n")
    _render_recommendation(out, result)
    out.write("\n")

    if not args.no_multi_week:
        out.write("=== MULTI-WEEK PLAN ===\n")
        _render_multi_week(out, result)
        out.write("\n")

    out.write("=== CHIP PLAN ===\n")
    _render_chip_plan(out, result)
    out.write("\n")

    out.write("=== DIFFERENTIALS ===\n")
    _render_differentials(out, result)
    out.write("\n")

    return 0


# ---------------------------------------------------------------------------
# Block renderers
# ---------------------------------------------------------------------------


def _render_recommendation(out: IO[str], result: Any) -> None:
    if result.get("primary_view") == "from_scratch":
        sq = result.get("squad") or {}
        out.write("Mode: from-scratch (FR-021 fallback — no Team ID supplied)\n")
        out.write(f"XI predicted (target GW): {float(sq.get('xi_pred_gw1', 0.0)):.2f}\n")
        out.write(f"Captain: player_id={sq.get('captain', '?')}\n")
        out.write(f"Vice-captain: player_id={sq.get('vice_captain', '?')}\n")
        return

    plan = result.get("current_squad_plan") or {}
    rec = plan.get("recommended") or {}
    out.write(f"Kind: {rec.get('kind', '?')}\n")
    out.write(f"Gain: {float(rec.get('gain', 0.0)):.2f}\n")
    out.write(f"Hit cost: {int(rec.get('hit_cost', 0))}\n")
    out.write(f"New bank: £{float(rec.get('new_bank', 0.0)):.2f}m\n")
    cap_name = plan.get("captain_name", "")
    vice_name = plan.get("vice_captain_name", "")
    out.write(
        f"Captain: {cap_name} (id={rec.get('captain_id', '?')})\n"
    )
    out.write(
        f"Vice-captain: {vice_name} (id={rec.get('vice_captain_id', '?')})\n"
    )
    for t in rec.get("transfers") or []:
        out.write(
            f"  Transfer: out={t['out_player_id']} -> in={t['in_player_id']} "
            f"(delta {float(t['delta']):+.2f})\n"
        )
    if rec.get("note"):
        out.write(f"Note: {rec['note']}\n")


def _render_multi_week(out: IO[str], result: Any) -> None:
    plan = result.get("multi_week_plan")
    if plan is None:
        out.write(
            "(no multi-week plan — supply --team-id to enable continuity mode)\n"
        )
        return
    out.write(
        f"Total: {plan.total_score:.2f}  Baseline (hold): {plan.baseline_score:.2f}  "
        f"Net gain: {plan.net_gain_vs_baseline:+.2f}  "
        f"Cumulative hits: {plan.cumulative_hits}\n"
    )
    out.write(
        f"{'GW':<5}{'Action':<14}{'Hit':<5}{'Wk pred':<10}"
        f"{'Bank after':<13}{'FTs after':<10}\n"
    )
    for step in plan.steps:
        bank_str = f"£{step.bank_after:.2f}m"
        out.write(
            f"{step.gw:<5}"
            f"{step.action.kind:<14}"
            f"{step.action.hit_cost:<5}"
            f"{step.week_predicted_score:<10.2f}"
            f"{bank_str:<13}"
            f"{step.fts_after:<10}\n"
        )
    if plan.alternatives:
        out.write(f"\nAlternative paths considered: {len(plan.alternatives)}\n")
        for i, alt in enumerate(plan.alternatives, 1):
            out.write(
                f"  {i}. total={alt.total_score:.2f}  hits={alt.cumulative_hits}  "
                f"net gain={alt.net_gain_vs_baseline:+.2f}\n"
            )


def _render_chip_plan(out: IO[str], result: Any) -> None:
    cp = result.get("chip_plan")
    if cp is None:
        out.write("(no chip plan)\n")
        return
    for attr, label in (
        ("triple_captain", "TC"),
        ("bench_boost", "BB"),
        ("free_hit", "FH"),
        ("wildcard", "WC"),
    ):
        rec = getattr(cp, attr)
        if rec.gw is None:
            out.write(f"{label}: NONE — {rec.rationale}\n")
        else:
            out.write(
                f"{label}: GW{rec.gw} (metric: {float(rec.supporting_metric):.2f}) — "
                f"{rec.rationale}\n"
            )


def _render_differentials(out: IO[str], result: Any) -> None:
    df = result.get("differential_df")
    if df is None or len(df) == 0:
        out.write("(no low-ownership candidates in pool)\n")
        return
    for _, row in df.iterrows():
        out.write(
            f"  {row.player_name} ({row.position}, "
            f"£{float(row.price):.1f}m, own {float(row.selected_by_percent):.1f}%) "
            f"horizon: {float(row.horizon_total):.2f}\n"
        )


__all__ = ["main"]
