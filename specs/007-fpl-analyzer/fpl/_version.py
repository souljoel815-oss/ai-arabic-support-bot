"""Single source of truth for the package version.

Kept in its own module so ``fpl/__init__.py`` doesn't need to parse package
metadata at import time and so the ``User-Agent`` string assembled in
``fpl/api.py`` (T014) can ``from fpl._version import __version__`` without
pulling in the rest of the package.
"""

__version__ = "0.1.0"
