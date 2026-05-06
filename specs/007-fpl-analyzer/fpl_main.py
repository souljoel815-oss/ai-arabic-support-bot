#!/usr/bin/env python3
"""FPL Ultimate Analyzer V11 — entry script (delegates to the `fpl` package).

Usage:
    python3 fpl_main.py --gw 31
    python3 fpl_main.py --gw 31 --team-id 12345 --horizon 4
    python3 fpl_main.py --backtest 8,10,15,18,20,25,28,30
    python3 fpl_main.py --clear-cache

Same flags as previous versions plus --no-multi-week to skip the V11 multi-week plan.
"""
from fpl.cli import main


if __name__ == '__main__':
    main()
