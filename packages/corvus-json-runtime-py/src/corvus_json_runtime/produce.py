"""The immer-style ``produce`` and the byte-native read-modify-write helpers (Model C).

These are the differentiator path and land in Phase 6 (byte-native RMW). The signatures are fixed here so
the public surface is stable; the bodies raise ``NotImplementedError`` until then.
"""

from __future__ import annotations

from collections.abc import Callable, Sequence


def produce(source: bytes, recipe: Callable[[object], None]) -> bytes:
    """Record mutations on a typed draft of the parsed ``source`` and lower them to a byte patch (Phase 1)."""
    del source, recipe
    raise NotImplementedError("produce (Model C) is Phase 1")


def rmw_upsert(source: bytes, targets: Sequence[object]) -> bytes:
    """Splice changed members' value bytes into ``source``, copying the rest through verbatim (Phase 1)."""
    del source, targets
    raise NotImplementedError("rmw_upsert (Model C) is Phase 1")


def rmw_produce_full(
    source: bytes,
    targets: Sequence[object],
    removals: Sequence[bytes],
    array_edits: Sequence[object],
) -> bytes:
    """Produce a new document applying upserts, removals, and array edits over ``source`` bytes (Phase 1)."""
    del source, targets, removals, array_edits
    raise NotImplementedError("rmw_produce_full (Model C) is Phase 1")