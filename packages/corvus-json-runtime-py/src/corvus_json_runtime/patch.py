"""RFC 6902 JSON Patch and RFC 7396 JSON Merge Patch over parsed JSON values.

A faithful port of the TypeScript runtime's applyPatch / createPatch / applyMergePatch / createMergePatch.
Operations act on a deep copy, so the caller's input is never mutated.
"""

from __future__ import annotations

import copy
from collections.abc import Sequence

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


def _ptr_tokens(pointer: str) -> list[str]:
    if pointer == "":
        return []
    if not pointer.startswith("/"):
        raise JsonPatchError(f"invalid JSON Pointer: {pointer!r}")
    return [t.replace("~1", "/").replace("~0", "~") for t in pointer[1:].split("/")]


def _esc_ptr(key: str) -> str:
    return key.replace("~", "~0").replace("/", "~1")


def _arr_index(token: str, length: int, allow_dash: bool) -> int:
    if token == "-":
        return length if allow_dash else -1
    if not token or (len(token) > 1 and token[0] == "0") or not token.isdigit():
        return -1
    return int(token)


def _ptr_get(doc: object, tokens: Sequence[str]) -> object:
    cur = doc
    for token in tokens:
        if isinstance(cur, list):
            i = _arr_index(token, len(cur), False)
            if i < 0 or i >= len(cur):
                raise JsonPatchError(f"path not found: {token}")
            cur = cur[i]
        elif isinstance(cur, dict) and token in cur:
            cur = cur[token]
        else:
            raise JsonPatchError(f"path not found: {token}")
    return cur


def _ptr_add(doc: object, tokens: Sequence[str], value: object) -> object:
    if not tokens:
        return value
    last = tokens[-1]
    parent = _ptr_get(doc, tokens[:-1])
    if isinstance(parent, list):
        i = _arr_index(last, len(parent), True)
        if i < 0 or i > len(parent):
            raise JsonPatchError(f"invalid array index for add: {last}")
        parent.insert(i, value)
    elif isinstance(parent, dict):
        parent[last] = value
    else:
        raise JsonPatchError(f"cannot add at {last}: parent is not a container")
    return doc


def _ptr_remove(doc: object, tokens: Sequence[str]) -> object:
    if not tokens:
        raise JsonPatchError("cannot remove the whole document")
    last = tokens[-1]
    parent = _ptr_get(doc, tokens[:-1])
    if isinstance(parent, list):
        i = _arr_index(last, len(parent), False)
        if i < 0 or i >= len(parent):
            raise JsonPatchError(f"array index out of range for remove: {last}")
        del parent[i]
    elif isinstance(parent, dict) and last in parent:
        del parent[last]
    else:
        raise JsonPatchError(f"path not found for remove: {last}")
    return doc


def _ptr_replace(doc: object, tokens: Sequence[str], value: object) -> object:
    if not tokens:
        return value
    last = tokens[-1]
    parent = _ptr_get(doc, tokens[:-1])
    if isinstance(parent, list):
        i = _arr_index(last, len(parent), False)
        if i < 0 or i >= len(parent):
            raise JsonPatchError(f"array index out of range for replace: {last}")
        parent[i] = value
    elif isinstance(parent, dict) and last in parent:
        parent[last] = value
    else:
        raise JsonPatchError(f"path not found for replace: {last}")
    return doc


def apply_patch(document: object, patch: object) -> object:
    """Apply an RFC 6902 JSON Patch to ``document``, returning the patched value (the input is not mutated)."""
    if not isinstance(patch, list):
        raise JsonPatchError("a JSON Patch must be an array of operations")
    doc = copy.deepcopy(document)
    for op in patch:
        if not isinstance(op, dict):
            raise JsonPatchError("each patch operation must be an object")
        kind = op.get("op")
        path = op.get("path")
        if not isinstance(path, str):
            raise JsonPatchError(f"operation {kind!r} requires a string 'path'")
        tokens = _ptr_tokens(path)
        if kind == "add":
            if "value" not in op:
                raise JsonPatchError("'add' requires a 'value'")
            doc = _ptr_add(doc, tokens, copy.deepcopy(op["value"]))
        elif kind == "remove":
            doc = _ptr_remove(doc, tokens)
        elif kind == "replace":
            if "value" not in op:
                raise JsonPatchError("'replace' requires a 'value'")
            doc = _ptr_replace(doc, tokens, copy.deepcopy(op["value"]))
        elif kind == "test":
            if "value" not in op:
                raise JsonPatchError("'test' requires a 'value'")
            if not eq(_ptr_get(doc, tokens), op["value"]):
                raise JsonPatchError(f"test failed at {path}")
        elif kind == "move":
            frm = op.get("from")
            if not isinstance(frm, str):
                raise JsonPatchError("'move' requires a string 'from'")
            from_tokens = _ptr_tokens(frm)
            if len(tokens) > len(from_tokens) and all(t == tokens[i] for i, t in enumerate(from_tokens)):
                raise JsonPatchError("cannot move a location into one of its own children")
            moved = _ptr_get(doc, from_tokens)
            doc = _ptr_remove(doc, from_tokens)
            doc = _ptr_add(doc, tokens, moved)
        elif kind == "copy":
            frm = op.get("from")
            if not isinstance(frm, str):
                raise JsonPatchError("'copy' requires a string 'from'")
            doc = _ptr_add(doc, tokens, copy.deepcopy(_ptr_get(doc, _ptr_tokens(frm))))
        else:
            raise JsonPatchError(f"unknown patch op: {kind!r}")
    return doc


def _diff(source: object, target: object, pointer: str, out: list[object]) -> None:
    if eq(source, target):
        return
    if isinstance(source, list) and isinstance(target, list):
        if len(source) != len(target):
            out.append({"op": "replace", "path": pointer, "value": target})
            return
        for i in range(len(source)):
            _diff(source[i], target[i], f"{pointer}/{i}", out)
        return
    if isinstance(source, dict) and isinstance(target, dict):
        for key in source:
            member = f"{pointer}/{_esc_ptr(key)}"
            if key not in target:
                out.append({"op": "remove", "path": member})
            else:
                _diff(source[key], target[key], member, out)
        for key in target:
            if key not in source:
                out.append({"op": "add", "path": f"{pointer}/{_esc_ptr(key)}", "value": target[key]})
        return
    out.append({"op": "replace", "path": pointer, "value": target})


def create_patch(source: object, target: object) -> object:
    """Compute an RFC 6902 patch (add/remove/replace) that turns ``source`` into ``target``."""
    out: list[object] = []
    _diff(source, target, "", out)
    return out