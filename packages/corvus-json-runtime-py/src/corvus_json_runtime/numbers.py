"""Exact numeric primitives.

Numeric keywords validate on a number's exact decimal value, never a lossy binary float. Documents are
parsed with ``parse_float=Decimal`` (see :func:`corvus_json_runtime.core.decode_and_parse`), so a JSON
number arrives as an ``int`` or a :class:`~decimal.Decimal` and keeps every digit.
"""

from __future__ import annotations

from decimal import Decimal, InvalidOperation
from fractions import Fraction
from typing import TypeGuard


def _as_decimal(value: object) -> Decimal | None:
    """Coerce a JSON numeric value to an exact ``Decimal``, or ``None`` when it is not a number."""
    if isinstance(value, bool):
        return None
    if isinstance(value, Decimal):
        return value
    if isinstance(value, int):
        return Decimal(value)
    if isinstance(value, float):
        return Decimal(repr(value))
    if isinstance(value, str):
        try:
            return Decimal(value)
        except InvalidOperation:
            return None
    return None


def is_num(value: object) -> TypeGuard[int | float | Decimal]:
    """True when ``value`` is a JSON number (``int`` / ``float`` / ``Decimal``, excluding ``bool``).

    Typed as a ``TypeGuard`` so a generated ``is_num(value) and value < N`` bound check narrows ``value`` to a
    comparable numeric type. ``bool`` is excluded at runtime but is a subtype of ``int``, so the narrowed type
    is a harmless superset."""
    return isinstance(value, (int, float, Decimal)) and not isinstance(value, bool)


def is_int(value: object) -> bool:
    """True when ``value`` is an integer-valued JSON number (``1`` and ``1.0`` both qualify).

    A non-number (including a numeric-looking string) is never an integer, even though ``_as_decimal``
    would parse it — the string coercion exists only for schema operands, not for classifying instances.
    """
    if not is_num(value):
        return False
    parsed = _as_decimal(value)
    return parsed is not None and parsed == parsed.to_integral_value()


def cmp(left: object, right: object) -> int:
    """Exact three-way compare of two numeric values (``-1`` / ``0`` / ``1``)."""
    a = _as_decimal(left)
    b = _as_decimal(right)
    if a is None or b is None:
        raise ValueError("cmp requires numeric values")
    if a < b:
        return -1
    if a > b:
        return 1
    return 0


def num_eq(left: object, right: object) -> bool:
    """Mathematical equality of two numbers (``1`` == ``1.0``, exact beyond float precision)."""
    a = _as_decimal(left)
    b = _as_decimal(right)
    return a is not None and b is not None and a == b


def multiple_of(value: object, divisor: object) -> bool:
    """Exact ``multipleOf``: ``value / divisor`` is an integer.

    Uses ``Fraction`` (unbounded exact rationals) rather than ``Decimal`` ``%`` so the result is not
    limited by the current decimal context precision for large or high-precision numbers.
    """
    v = _as_decimal(value)
    d = _as_decimal(divisor)
    if v is None or d is None or d == 0:
        return False
    return (Fraction(v) % Fraction(d)) == 0


def exact_number(value: object) -> str:
    """The exact decimal digits of ``value`` as a string (the big-number seam)."""
    parsed = _as_decimal(value)
    if parsed is None:
        raise ValueError("exact_number requires a numeric value")
    return str(parsed)