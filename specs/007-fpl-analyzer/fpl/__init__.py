"""FPL Ultimate Analyzer package.

Public surface (per ``contracts/package_api.md``):

    from fpl import run_analysis        # used by fpl_gui.py
    from fpl.cli import main            # used by fpl_main.py

Everything else is internal and may be refactored without notice.
"""

from fpl._version import __version__
from fpl.run import run_analysis

__all__ = ["__version__", "run_analysis"]
