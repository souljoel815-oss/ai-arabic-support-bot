"""Public exception classes for the ``fpl`` package.

Mapped to CLI exit codes by ``fpl.cli`` (see ``contracts/cli.md``):

* :class:`UnknownTeamId`     → exit 5
* :class:`FPLApiError`       → exit 3
* :class:`CacheWriteError`   → exit 4

Internal callers raise these at the boundary identified in
``plan.md`` § Constitution Check (the FPL API client and the
``run_analysis`` entry function). Internal modules should not catch and
re-raise them — let them propagate.
"""

from __future__ import annotations


class FPLAnalyzerError(Exception):
    """Base class for every public exception raised by ``fpl``.

    Provided so callers who want to catch any analyzer-originated failure
    in one ``except`` clause can do so without listing each subclass.
    Internal code should always raise one of the more specific subclasses.
    """


class UnknownTeamId(FPLAnalyzerError):
    """The FPL public API returned 404 for the supplied ``team_id``.

    Triggers the US-1 acceptance scenario 4 fallback (CLI exits 5; the
    package-level caller may choose to retry in from-scratch mode).
    """

    def __init__(self, team_id: int):
        self.team_id = team_id
        super().__init__(f"Unknown FPL team_id: {team_id}")


class FPLApiError(FPLAnalyzerError):
    """An FPL public-API request failed unrecoverably after retry/backoff.

    Raised once the polite-client retry policy in ``fpl.api`` has been
    exhausted (see FR-035). Carries the originating endpoint and HTTP
    status, when available, to make the CLI exit-3 message actionable.
    """

    def __init__(self, message: str, *, endpoint: str | None = None, status: int | None = None):
        self.endpoint = endpoint
        self.status = status
        super().__init__(message)


class CacheWriteError(FPLAnalyzerError):
    """Writing to the disk cache failed (full disk, permission, lock).

    Maps to CLI exit 4. Read failures are *not* errors — a missing or
    unreadable cache entry is treated as a miss and a fresh fetch is
    issued.
    """
