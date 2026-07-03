"""Unit tests for the model runtime core (numbers, deep-equality, decode, temporal, merge patch)."""

from __future__ import annotations

import unittest
from decimal import Decimal

from corvus_json_runtime import (
    apply_merge_patch,
    build,
    canonicalize,
    cmp,
    create_merge_patch,
    decode_and_parse,
    eq,
    exact_number,
    fmt,
    is_int,
    multiple_of,
    to_instant,
    to_plain_date,
)


class NumbersTests(unittest.TestCase):
    def test_decimal_parse_is_exact(self) -> None:
        value = decode_and_parse(b'{"n": 0.1}')
        assert isinstance(value, dict)
        self.assertEqual(value["n"], Decimal("0.1"))

    def test_multiple_of_is_exact_beyond_float(self) -> None:
        self.assertTrue(multiple_of(Decimal("0.3"), Decimal("0.1")))
        self.assertFalse(multiple_of(Decimal("0.35"), Decimal("0.1")))

    def test_is_int_accepts_integer_valued(self) -> None:
        self.assertTrue(is_int(Decimal("1.0")))
        self.assertTrue(is_int(2))
        self.assertFalse(is_int(Decimal("1.5")))
        self.assertFalse(is_int(True))

    def test_cmp_large_integers(self) -> None:
        # Exact string-constructed operands: the compare must be exact beyond the decimal context precision.
        self.assertEqual(
            cmp(Decimal("100000000000000000000000000000001"), Decimal("100000000000000000000000000000000")),
            1,
        )

    def test_multiple_of_large(self) -> None:
        self.assertTrue(multiple_of(Decimal("1e30"), Decimal("0.1")))

    def test_exact_number(self) -> None:
        self.assertEqual(exact_number(Decimal("1.100")), "1.100")


class EqualityTests(unittest.TestCase):
    def test_numeric_value_equality(self) -> None:
        self.assertTrue(eq(1, Decimal("1.0")))
        self.assertFalse(eq(1, "1"))

    def test_bool_is_not_int(self) -> None:
        self.assertFalse(eq(True, 1))

    def test_deep_object_equality(self) -> None:
        self.assertTrue(eq({"a": [1, 2]}, {"a": [Decimal("1"), 2]}))


class SerialiseTests(unittest.TestCase):
    def test_build_preserves_decimal_digits(self) -> None:
        self.assertEqual(build({"n": Decimal("0.10")}), b'{"n":0.10}')

    def test_canonicalize_sorts_keys(self) -> None:
        self.assertEqual(canonicalize({"b": 1, "a": 2}), b'{"a":2,"b":1}')


class FormatTests(unittest.TestCase):
    def test_uuid(self) -> None:
        self.assertTrue(fmt("uuid", "12345678-1234-1234-1234-123456789abc"))
        self.assertFalse(fmt("uuid", "nope"))

    def test_non_string_passes(self) -> None:
        self.assertTrue(fmt("uuid", 42))

    def test_unknown_format_is_annotation_only(self) -> None:
        self.assertTrue(fmt("not-a-real-format", "anything"))


class TemporalTests(unittest.TestCase):
    def test_date_and_instant_parse(self) -> None:
        self.assertEqual(to_plain_date("2020-01-02").year, 2020)
        self.assertIsNotNone(to_instant("2020-01-02T03:04:05Z"))


class MergePatchTests(unittest.TestCase):
    def test_apply_and_diff_round_trip(self) -> None:
        source: dict[str, object] = {"a": 1, "b": {"c": 2}, "d": 3}
        target: dict[str, object] = {"a": 1, "b": {"c": 4}, "e": 5}
        patch = create_merge_patch(source, target)
        self.assertEqual(apply_merge_patch(source, patch), target)


if __name__ == "__main__":
    unittest.main()