"""Temporal conversions over the ``whenever`` library (the Python peer of the TypeScript ``Temporal`` model).

The branded ``date`` / ``date-time`` / ``time`` / ``duration`` string formats gain a ``to_temporal`` accessor
that parses into the matching offset/precision-preserving ``whenever`` value (the analog of the C# NodaTime
conversions). Validation is separate (see :mod:`corvus_json_runtime.formats`); these are parse helpers.
"""

from __future__ import annotations

from whenever import Date, Instant, Time, TimeDelta


def to_instant(value: str) -> Instant:
    """Parse an RFC 3339 ``date-time`` into an absolute ``Instant``."""
    return Instant.parse_iso(value)


def to_plain_date(value: str) -> Date:
    """Parse an RFC 3339 ``date`` into a ``whenever.Date``."""
    return Date.parse_iso(value)


def to_plain_time(value: str) -> Time:
    """Parse an RFC 3339 ``time`` into a ``whenever.Time``."""
    return Time.parse_iso(value)


def to_duration(value: str) -> TimeDelta:
    """Parse an ISO 8601 ``duration`` into a ``whenever.TimeDelta``."""
    return TimeDelta.parse_iso(value)