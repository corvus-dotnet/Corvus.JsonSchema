"""Core JSON primitives shared by the generated validators and the read/write helpers."""

from __future__ import annotations

import json
from decimal import Decimal
from typing import TypeGuard

import regex

from .numbers import is_num, num_eq

# Compiled-pattern cache: the `pattern` keyword and `regex` format compile once at module scope. The `regex`
# module (not stdlib `re`) is used because JSON Schema patterns are ECMA-262, which includes `\p{...}` Unicode
# property escapes that stdlib `re` rejects; `regex` supports them.
_RE_CACHE: dict[str, regex.Pattern[str]] = {}


def decode_and_parse(data: bytes | str) -> object:
    """Decode UTF-8 bytes / a JSON string into a Python value, with numbers preserved as ``Decimal``."""
    text = data.decode("utf-8") if isinstance(data, (bytes, bytearray)) else data
    result: object = json.loads(text, parse_float=Decimal)
    return result


def is_obj(value: object) -> TypeGuard[dict[str, object]]:
    """True when ``value`` is a JSON object (a ``dict``); narrows the type for the generated validators."""
    return isinstance(value, dict)


def is_arr(value: object) -> TypeGuard[list[object]]:
    """True when ``value`` is a JSON array (a ``list``); narrows the type for the generated validators."""
    return isinstance(value, list)


def eq(left: object, right: object) -> bool:
    """JSON deep equality, comparing numbers by mathematical value (``1`` == ``1.0``)."""
    if is_num(left) and is_num(right):
        return num_eq(left, right)
    if isinstance(left, bool) or isinstance(right, bool):
        return left is right
    if isinstance(left, dict) and isinstance(right, dict):
        if left.keys() != right.keys():
            return False
        return all(eq(left[key], right[key]) for key in left)
    if isinstance(left, list) and isinstance(right, list):
        return len(left) == len(right) and all(eq(a, b) for a, b in zip(left, right))
    if isinstance(left, str) and isinstance(right, str):
        return left == right
    if left is None or right is None:
        return left is right
    return False


def ptr(document: object, pointer: str) -> object:
    """Resolve an RFC 6901 JSON Pointer against ``document`` (raises ``KeyError`` when absent)."""
    if pointer == "":
        return document
    current: object = document
    for raw in pointer.lstrip("/").split("/"):
        token = raw.replace("~1", "/").replace("~0", "~")
        if isinstance(current, dict):
            current = current[token]
        elif isinstance(current, list):
            current = current[int(token)]
        else:
            raise KeyError(pointer)
    return current


_NEVER = regex.compile(r"(?!)")  # matches nothing — the fallback for a pattern that will not compile


def re_test(pattern: str, value: object) -> bool:
    """True when ``value`` is a string that ``pattern`` matches (unanchored).

    ``pattern`` is already Python ``regex``-module syntax: the generator transpiles the schema's ECMA-262
    pattern at build time (the C# ``EcmaRegexPythonTranslator``), exactly as the C# engine transpiles to .NET.
    A pattern that still fails to compile becomes the never-matching pattern rather than crashing the validator."""
    if not isinstance(value, str):
        return False
    compiled = _RE_CACHE.get(pattern)
    if compiled is None:
        try:
            compiled = regex.compile(pattern)
        except regex.error:
            compiled = _NEVER
        _RE_CACHE[pattern] = compiled
    return compiled.search(value) is not None


def _dump(value: object, sort_keys: bool) -> str:
    """Serialise a JSON value to text, emitting ``Decimal`` as a bare number (no precision loss)."""
    if value is None:
        return "null"
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, Decimal):
        return str(value)
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        return repr(value)
    if isinstance(value, str):
        return json.dumps(value)
    if isinstance(value, (list, tuple)):
        return "[" + ",".join(_dump(item, sort_keys) for item in value) + "]"
    if isinstance(value, dict):
        items = sorted(value.items(), key=lambda kv: kv[0]) if sort_keys else list(value.items())
        return "{" + ",".join(json.dumps(key) + ":" + _dump(val, sort_keys) for key, val in items) + "}"
    raise TypeError(f"cannot serialise value of type {type(value).__name__}")


def build(value: object) -> bytes:
    """Serialise a full value to compact UTF-8 JSON bytes at the native floor (caller key order)."""
    return _dump(value, sort_keys=False).encode("utf-8")


def canonicalize(value: object) -> bytes:
    """Serialise a value to canonical UTF-8 JSON bytes (RFC 8785-style recursive key sort)."""
    return _dump(value, sort_keys=True).encode("utf-8")