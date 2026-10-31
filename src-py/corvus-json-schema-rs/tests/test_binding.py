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


def test_not_on_an_in_place_cycle_stops_at_max_depth() -> None:
    # The crate before 0.1.6 evaluated a not without the guard on in-place recursion, so the schemas whose not leads
    # back to the schema it is in overflowed the stack, which ends the process.
    looping = {"allOf": [{"$ref": "#/$defs/loop"}]}
    schemas: list[dict[str, object]] = [
        {"not": {"$ref": "#"}},
        {"not": {"not": {"$ref": "#"}}},
        {"type": "integer", "not": {"$ref": "#"}},
        {"allOf": [{"not": {"$ref": "#"}}]},
        {"$defs": {"a": {"not": {"$ref": "#/$defs/b"}}, "b": {"not": {"$ref": "#/$defs/a"}}}, "$ref": "#/$defs/a"},
        {"$defs": {"loop": looping}, "not": {"$ref": "#/$defs/loop"}},
        {"$defs": {"loop": looping}, "not": {"not": {"$ref": "#/$defs/loop"}}},
        {"$defs": {"loop": looping}, "properties": {"a": {"not": {"$ref": "#/$defs/loop"}}}},
        {"unevaluatedProperties": False, "not": {"$ref": "#"}},
    ]
    for s, schema in enumerate(schemas):
        v = cjs.compile(schema, max_depth=16)
        for instance in [1, "a", {"a": 1}, [1]]:
            # Only an object with the property reaches the loop of the eighth schema, and anything but an integer
            # fails the type of the third before its not is reached, when failing fast.
            if (s == 7 and not isinstance(instance, dict)) or (s == 2 and instance != 1):
                continue
            with pytest.raises(cjs.SchemaEvaluationDepthError):
                v(instance)
            with pytest.raises(cjs.SchemaEvaluationDepthError):
                v.is_valid(instance)
            with pytest.raises(cjs.SchemaEvaluationDepthError):
                v.is_valid_json(json.dumps(instance))
            for level in cjs.ResultsLevel:
                with pytest.raises(cjs.SchemaEvaluationDepthError):
                    v.evaluate(instance, cjs.JsonSchemaResultsCollector.create(level))


def test_a_separated_list_keeps_an_anchor_inside_it() -> None:
    # The crate before 0.1.7 dropped a "$" at the end of the repeated group of a separated list, and after its first
    # item, so the first two patterns were matched as ^x(,a)*$ and the third as ^(a,)*b$. The answers are V8's.
    v = cjs.compile({"pattern": "^x(,a$)*$"})
    assert v("x") is True
    assert v("x,a") is True
    assert v("x,a,a") is False
    assert v.is_valid_json(json.dumps("x,a,a")) is False
    v = cjs.compile({"pattern": "^x$(,a)*$"})
    assert v("x") is True
    assert v("x,a") is False
    v = cjs.compile({"pattern": "^(a,$)*b$"})
    assert v("b") is True
    assert v("a,b") is False
    # An escaped "$" is a literal character.
    v = cjs.compile({"pattern": "^x(,a\\$)*$"})
    assert v("x,a$,a$") is True
    assert v("x,a,a") is False


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


# The limits on a regular expression. A schema and an instance may both come from outside, so a match of a pattern
# has a limit of time (one second unless pattern_match_limit says otherwise), and a pattern has limits of size.
# Reaching either raises an exception of its own, and is never an answer.

NEVER_ANSWERS = r"^(?:(a+)\1*)+$"
NEVER_ANSWERED = "a" * 60 + "b"


def test_a_match_that_reaches_its_limit_raises() -> None:
    import time

    v = cjs.compile({"pattern": NEVER_ANSWERS}, pattern_match_limit=0.05)
    for check in (
        lambda: v(NEVER_ANSWERED),
        lambda: v.is_valid(NEVER_ANSWERED),
        lambda: v.is_valid_json(json.dumps(NEVER_ANSWERED)),
        lambda: v.evaluate(NEVER_ANSWERED),
        lambda: v.evaluate(NEVER_ANSWERED, cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.BASIC)),
    ):
        # perf_counter and not monotonic: on Windows monotonic moves in steps of some 15 ms, and a match stopped at
        # its limit of 50 ms by the crate's clock could read as 47 ms.
        start = time.perf_counter()
        with pytest.raises(cjs.PatternMatchLimitError) as raised:
            check()
        took = time.perf_counter() - start
        assert NEVER_ANSWERS in str(raised.value) and "50ms" in str(raised.value)
        assert 0.05 <= took < 20
    assert not issubclass(cjs.PatternMatchLimitError, cjs.SchemaEvaluationDepthError)
    assert v("aaaa") is True and v("b") is False


def test_the_default_match_limit_is_one_second_and_can_be_turned_off() -> None:
    import time

    assert cjs.CompileOptions().pattern_match_limit == 1.0
    v = cjs.compile({"patternProperties": {NEVER_ANSWERS: {"type": "integer"}}})
    start = time.perf_counter()
    with pytest.raises(cjs.PatternMatchLimitError, match="1s"):
        v({NEVER_ANSWERED: 1})
    assert 1.0 <= time.perf_counter() - start < 21
    # Off: the match runs to its end. Fourteen characters take a moment, and the answer is no.
    for off in (None, 0, 0.0, -1):
        v = cjs.compile({"pattern": NEVER_ANSWERS}, pattern_match_limit=off)
        assert v("a" * 14 + "b") is False
    v = cjs.compile({"pattern": NEVER_ANSWERS}, cjs.CompileOptions(pattern_match_limit=None))
    assert v("a" * 14 + "b") is False
    with pytest.raises(ValueError):
        cjs.compile({"pattern": "a"}, pattern_match_limit=float("nan"))


def test_a_match_that_reaches_its_memory_limit_raises() -> None:
    v = cjs.compile({"pattern": "(?:a?){2147483647}"})
    with pytest.raises(cjs.PatternMatchLimitError, match="1,048,576 backtracking entries"):
        v("")


def test_a_pattern_over_a_limit_of_size_is_not_an_invalid_pattern() -> None:
    def nest(depth: int) -> str:
        return "(?:" * depth + "a" + ")" * depth

    for at, over, reason in (
        ("a" * 16384, "a" * 16385, "the pattern has a size of more than 1048576 bytes when it is read"),
        (nest(999), nest(1000), "the groups of the pattern nest more than 1000 deep"),
    ):
        cjs.compile({"pattern": at})
        for schema in ({"pattern": over}, {"patternProperties": {over: True}}):
            with pytest.raises(cjs.PatternTooLargeError) as raised:
                cjs.compile(schema)
            message = str(raised.value)
            assert message.endswith(f"is too large to read as an ECMA-262 regular expression: {reason}.")
            assert "invalid" not in message.lower() and len(message) < 400
    with pytest.raises(cjs.PatternTooLargeError, match="longer than 1048576 bytes"):
        cjs.compile({"pattern": "a" * (2**20 + 1)})
    # It is a compilation error, so code that catches those still catches it.
    assert issubclass(cjs.PatternTooLargeError, cjs.SchemaCompilationError)
    with pytest.raises(cjs.SchemaCompilationError, match="^Invalid regular expression") as raised:
        cjs.compile({"pattern": "a**"})
    assert not isinstance(raised.value, cjs.PatternTooLargeError)


def test_patterns_are_read_in_the_edition_and_unicode_version_asked_for() -> None:
    # 2020-12 reads the grammar of ECMAScript 2020, which has no modifier groups; draft 7 reads today's.
    modifier = {"$schema": "https://json-schema.org/draft/2020-12/schema", "pattern": "^(?i:a)$"}
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.compile(modifier)
    assert cjs.compile(modifier, regex_edition=cjs.RegexEdition.CURRENT).is_valid("A")
    assert cjs.compile(modifier, cjs.CompileOptions(regex_edition=cjs.RegexEdition.CURRENT)).is_valid("A")
    draft7 = {"$schema": "http://json-schema.org/draft-07/schema#", "pattern": "^(?i:a)$"}
    assert cjs.compile(draft7).is_valid("A")
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.compile(draft7, regex_edition="es2020")
    # Sidetic is a script from Unicode 17: naming it is a regular expression under 17, not under 13.
    regex = {"$schema": "http://json-schema.org/draft-07/schema#", "format": "regex"}
    for version, valid in [("default", True), (cjs.RegexUnicodeVersion.V17, True), (cjs.RegexUnicodeVersion.V13, False)]:
        v = cjs.compile(regex, assert_format=True, regex_unicode_version=version)
        assert v.is_valid("\\p{Script=Sidetic}") is valid, version
    with pytest.raises(ValueError, match="regex_edition"):
        cjs.compile({}, regex_edition="es2019")
    with pytest.raises(ValueError, match="regex_unicode_version"):
        cjs.compile({}, regex_unicode_version="16")
