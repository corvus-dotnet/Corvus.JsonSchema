"""RFC 6902 JSON Patch and RFC 7396 JSON Merge Patch over parsed JSON values.

Phase 0 implements the Merge Patch pair (apply + diff). The RFC 6902 operation patch (apply + diff) lands
in Phase 1 alongside the byte-native read-modify-write path.
"""

from __future__ import annotations

import copy

from .core import eq


class JsonPatchError(ValueError):
    """Raised when a patch cannot be applied (a failed ``test``, a missing path, a bad operation)."""


def apply_merge_patch(target: object, merge_patch: object) -> object:
    """Apply an RFC 7396 merge patch to ``target``, returning the merged value."""
    if not isinstance(merge_patch, dict):
        return copy.deepcopy(merge_patch)

    result: dict[str, object] = dict(target) if isinstance(target, dict) else {}
    for key, value in merge_patch.items():
        if value is None:
            result.pop(key, None)
        else:
            result[key] = apply_merge_patch(result.get(key), value)
    return result


def create_merge_patch(source: object, target: object) -> object:
    """Compute the RFC 7396 merge patch that transforms ``source`` into ``target``."""
    if not isinstance(source, dict) or not isinstance(target, dict):
        return copy.deepcopy(target)

    patch: dict[str, object] = {}
    for key in source:
        if key not in target:
            patch[key] = None
    for key, value in target.items():
        if key not in source:
            patch[key] = copy.deepcopy(value)
        elif not eq(source[key], value):
            patch[key] = create_merge_patch(source[key], value)
    return patch


def apply_patch(document: object, patch: object) -> object:
    """Apply an RFC 6902 JSON Patch to ``document`` (Phase 1)."""
    del document, patch
    raise NotImplementedError("apply_patch (RFC 6902) is Phase 1")


def create_patch(source: object, target: object) -> object:
    """Compute the RFC 6902 JSON Patch that transforms ``source`` into ``target`` (Phase 1)."""
    del source, target
    raise NotImplementedError("create_patch (RFC 6902 diff) is Phase 1")