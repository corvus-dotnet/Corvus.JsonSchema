"""Helpers called by generated validators.

Standalone modules emitted by ``generate_module()`` import this module, so its names are public API.

Instances are the values ``json.loads`` produces: ``dict``, ``list``, ``str``, ``int``, ``float``, ``bool`` and
``None``. JSON equality differs from Python's in one place: ``True == 1`` and ``False == 0`` in Python, but a boolean
is never equal to a number in JSON. Numbers compare by value (``1 == 1.0``), as JSON Schema requires.
"""

from __future__ import annotations

import base64
import binascii
import json
import re
from collections.abc import Iterable, Sequence
from typing import Any

from .formats import FORMAT_VALIDATORS, NUMERIC_FORMAT_VALIDATORS, legacy_hostname
from .options import SchemaCompilationError, SchemaEvaluationDepthError
from .pattern import Matcher, compile_pattern

__all__ = [
    "FORMAT_VALIDATORS",
    "INF",
    "NUMERIC_FORMAT_VALIDATORS",
    "SchemaEvaluationDepthError",
    "always",
    "content",
    "depth_exceeded",
    "equal",
    "has",
    "includes",
    "key",
    "key_set",
    "legacy_hostname",
    "multiple_of",
    "never",
    "pattern",
    "unique",
]

INF = float("inf")


def always(_: Any) -> bool:
    """The check of a ``true`` schema (in dispatch tables)."""
    return True


def never(_: Any) -> bool:
    """The check of a ``false`` schema (in dispatch tables)."""
    return False


def pattern(source: str) -> Matcher:
    """The search function of an ECMA-262 pattern (truthy on a match)."""
    matcher = compile_pattern(source)
    if matcher is None:
        raise SchemaCompilationError(f"Invalid regular expression '{source}'.")
    return matcher


def equal(a: Any, b: Any) -> bool:
    """JSON equality: numbers by value, objects by their property sets, arrays element-wise."""
    ta = type(a)
    tb = type(b)
    if ta is dict:
        if tb is not dict or len(a) != len(b):
            return False
        for k, v in a.items():
            if k not in b or not equal(v, b[k]):
                return False
        return True
    if ta is list:
        if tb is not list or len(a) != len(b):
            return False
        for x, y in zip(a, b, strict=False):
            if not equal(x, y):
                return False
        return True
    if ta is bool or tb is bool:
        return a is b
    if tb is dict or tb is list:
        return False
    return bool(a == b)


def includes(values: Sequence[Any], x: Any) -> bool:
    """True when some element of ``values`` is JSON-equal to ``x``."""
    for v in values:
        if equal(v, x):
            return True
    return False


class _Tag:
    __slots__ = ("name",)

    def __init__(self, name: str) -> None:
        self.name = name

    def __repr__(self) -> str:
        return self.name


_BOOL = _Tag("bool")
_OBJECT = _Tag("object")
_ARRAY = _Tag("array")


def key(v: Any) -> Any:
    """A hashable key that agrees with JSON equality: equal values have equal keys, and unequal values unequal keys."""
    t = type(v)
    if t is str:
        return v
    if t is bool:
        return (_BOOL, v)
    if t is dict:
        return (_OBJECT, frozenset([(k, key(x)) for k, x in v.items()]))
    if t is list:
        return (_ARRAY, tuple([key(x) for x in v]))
    return v


def key_set(values: Iterable[Any]) -> frozenset[Any]:
    """The keys of JSON values, for membership tests with ``has``."""
    return frozenset(key(v) for v in values)


def has(keys: frozenset[Any], x: Any) -> bool:
    """True when ``x`` is JSON-equal to a value whose key is in ``keys`` (see ``key_set``)."""
    return key(x) in keys


def unique(a: list[Any]) -> bool:
    """``uniqueItems``: strings, numbers and null through a set (they hash as themselves, and Python's equality is
    JSON's for them), anything else through keys that agree with JSON equality."""
    n = len(a)
    if n < 2:
        return True
    for v in a:
        t = type(v)
        if t is not str and t is not int and t is not float and v is not None:
            return len({key(v) for v in a}) == n
    return len(set(a)) == n


_FLOAT_RE = re.compile(r"^(-?)(\d+)(?:\.(\d+))?(?:e([+-]?\d+))?$")


def _to_decimal(x: int | float) -> tuple[int, int]:
    """The decimal form of a number as (mantissa, exponent): exact for integers, and the shortest round-trip form of a
    float, which is what the JSON text said for any value ``json.loads`` produced."""
    if type(x) is int:
        return x, 0
    m = _FLOAT_RE.match(repr(x))
    assert m is not None
    frac = m.group(3) or ""
    return int(m.group(1) + m.group(2) + frac), int(m.group(4) or "0") - len(frac)


def multiple_of(x: int | float, d: int | float) -> bool:
    """Exact ``multipleOf`` over the decimal forms of both numbers."""
    if type(x) is float and (x != x or x in (float("inf"), float("-inf"))):
        return False
    am, ae = _to_decimal(x)
    bm, be = _to_decimal(d)
    if bm == 0:
        return False
    # x / d is an integer iff am * 10^(ae - be) is divisible by bm.
    shift = ae - be
    num: int = am * 10**shift if shift >= 0 else am
    den: int = bm if shift >= 0 else bm * 10**-shift
    return num % den == 0


_BASE64_RE = re.compile(r"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?\Z")


def _reject_constant(name: str) -> Any:
    raise ValueError(name)


def _is_json(s: str) -> bool:
    try:
        json.loads(s, parse_constant=_reject_constant)
        return True
    except ValueError:
        return False


def content(s: str, kind: int) -> bool:
    """Draft 7 content assertion: 1 = base64, 2 = application/json, 3 = both."""
    if kind == 1:
        return _BASE64_RE.match(s) is not None
    if kind == 2:
        return _is_json(s)
    if kind == 3:
        if _BASE64_RE.match(s) is None:
            return False
        try:
            decoded = base64.b64decode(s).decode("utf-8", errors="replace")
        except (binascii.Error, ValueError):
            return False
        return _is_json(decoded)
    return True


def depth_exceeded() -> None:
    """Called by generated code when in-place recursion exceeds the configured depth."""
    raise SchemaEvaluationDepthError()
