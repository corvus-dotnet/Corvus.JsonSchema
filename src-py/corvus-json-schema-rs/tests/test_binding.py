"""The Rust-backed package: instances read in place, the conversions it falls back to, and the API it shares with the
pure-Python package."""

from __future__ import annotations

import enum
import json
from collections import OrderedDict

import pytest

import corvus_json_schema_rs as cjs

PERSON = {
    "type": "object",
    "properties": {"name": {"type": "string", "minLength": 1}, "age": {"type": "integer", "minimum": 0}},
    "required": ["name"],
    "additionalProperties": False,
}


def test_objects_are_read_in_place() -> None:
    v = cjs.compile(PERSON)
    assert v({"name": "a", "age": 3}) is True
    assert v({"name": "", "age": 3}) is False
    assert v({"age": 3}) is False
    assert v({"name": "a", "extra": 1}) is False
    assert v([]) is False


def test_json_text_is_parsed_in_rust() -> None:
    v = cjs.compile(PERSON)
    assert v.is_valid_json('{"name": "a"}') is True
    assert v.is_valid_json(b'{"name": ""}') is False
    with pytest.raises(ValueError, match="Invalid JSON"):
        v.is_valid_json("{")
    with pytest.raises(ValueError, match="Invalid JSON"):
        v.is_valid_json(b'{"name": "\xff"}')
    with pytest.raises(TypeError):
        v.is_valid_json(3)


def test_large_objects_look_names_up_by_hash() -> None:
    names = [f"p{i}" for i in range(40)]
    v = cjs.compile(
        {"type": "object", "required": ["p39"], "properties": {"p39": {"const": 1}}, "dependentRequired": {"p0": ["p1"]}}
    )
    instance = {n: 1 for n in names}
    assert v(instance) is True
    del instance["p1"]
    assert v(instance) is False
    assert v({n: 2 for n in names}) is False


@pytest.mark.parametrize(
    ("schema", "instance", "expected"),
    [
        # Tuples are arrays, through the conversion.
        ({"type": "array", "items": {"type": "integer"}}, (1, 2), True),
        ({"type": "array", "items": {"type": "integer"}}, (1, "x"), False),
        # Integers beyond 64 bits are the nearest double.
        ({"type": "integer", "minimum": 2**64}, 2**70, True),
        ({"maximum": 2**64}, 2**70, False),
        # Subclasses of dict, list, str and int are read as their bases.
        (PERSON, OrderedDict(name="a", age=1), True),
        ({"type": "string", "enum": ["a"]}, "a", True),
        # Lone surrogates (json.loads keeps them) are read through the conversion.
        ({"type": "string", "maxLength": 1}, "\ud800", True),
        ({"type": "object", "propertyNames": {"maxLength": 1}}, {"\ud800": 1}, True),
    ],
)
def test_instances_the_in_place_reader_falls_back_on(schema: object, instance: object, expected: bool) -> None:
    assert cjs.compile(schema)(instance) is expected


class Colour(str, enum.Enum):
    RED = "red"


class Size(enum.IntEnum):
    SMALL = 1


def test_enum_members_are_their_values() -> None:
    assert cjs.compile({"enum": ["red"]})(Colour.RED) is True
    assert cjs.compile({"type": "integer", "const": 1})(Size.SMALL) is True


def test_booleans_are_not_numbers() -> None:
    assert cjs.compile({"const": 1})(True) is False
    assert cjs.compile({"type": "integer"})(True) is False
    assert cjs.compile({"enum": [0, 1]})(False) is False


def test_values_that_are_not_json_raise_when_the_schema_reads_them() -> None:
    # A schema that never looks at the value (true, {}) accepts anything.
    assert cjs.compile(True)({1, 2}) is True
    v = cjs.compile({"type": ["object", "array", "number"]})
    with pytest.raises(TypeError, match="not JSON"):
        v({1, 2})
    with pytest.raises(TypeError, match="keys must be strings"):
        cjs.compile({"additionalProperties": {"type": "string"}})({1: "a"})
    with pytest.raises(ValueError, match="NaN"):
        v(float("nan"))


def test_deep_nesting_is_refused_rather_than_overflowing() -> None:
    deep: object = 1
    for _ in range(5000):
        deep = [deep]
    with pytest.raises(ValueError, match="nested too deeply"):
        cjs.compile({"items": {"$ref": "#"}})(deep)


def test_in_place_reading_agrees_with_parsed_text() -> None:
    schema = {
        "type": "object",
        "properties": {
            "a": {"type": "array", "items": {"type": "number"}, "uniqueItems": True},
            "b": {"oneOf": [{"type": "string", "pattern": "^x"}, {"type": "object", "required": ["k"]}]},
            "c": {"type": "object", "additionalProperties": {"type": "boolean"}},
        },
    }
    v = cjs.compile(schema)
    for instance in [
        {"a": [1, 2.5, 3]},
        {"a": [1, 1.0]},
        {"b": "xy"},
        {"b": "yy"},
        {"b": {"k": 1}},
        {"b": {}},
        {"c": {"x": True, "y": False}},
        {"c": {"x": 1}},
    ]:
        assert v(instance) == v.is_valid_json(json.dumps(instance)), instance


def test_excluded_class_with_a_member_outside_ascii() -> None:
    # The crate before 0.1.4 kept this pattern's excluded set as ASCII bits and read é as the bits of C and ).
    v = cjs.compile({"pattern": r"^(?=[^é]+$)(?=(.*\w)).+$"})
    assert v("C1") is True
    assert v(")a") is True
    assert v("é1") is False
    assert v.is_valid_json('"é1"') is False


def test_custom_formats_and_resolvers_call_back_into_python() -> None:
    v = cjs.compile({"type": "string", "format": "even"}, assert_format=True, formats={"even": lambda s: len(s) % 2 == 0})
    assert v("ab") is True
    assert v("abc") is False

    def resolve(uri: str) -> object:
        return {"type": "integer"} if uri == "https://example.com/int.json" else None

    v = cjs.compile({"$ref": "https://example.com/int.json"}, resolve_document=resolve)
    assert v(1) is True
    assert v("1") is False
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.compile({"$ref": "https://example.com/missing.json"}, resolve_document=resolve)


def test_in_place_recursion_beyond_max_depth_raises() -> None:
    v = cjs.compile({"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "$ref": "#/$defs/loop"}, max_depth=16)
    with pytest.raises(cjs.SchemaEvaluationDepthError):
        v(1)


def test_results_and_annotations() -> None:
    schema = {"title": "Person", **PERSON}
    v = cjs.compile(schema)
    c = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.VERBOSE)
    assert v.evaluate({"name": "a"}, c) is True
    assert c.level is cjs.ResultsLevel.VERBOSE
    assert cjs.collect_annotations(c)[""]["title"] == {"#": "Person"}
    assert [a.keyword for a in cjs.enumerate_annotations(c)] == ["title"]
    c = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.DETAILED)
    assert v.evaluate({}, c) is False
    assert any(r.message == "Required property not present 'name'" for r in c.results)
    assert cjs.schema_location_fragment("/properties/a b") == "#/properties/a%20b"
