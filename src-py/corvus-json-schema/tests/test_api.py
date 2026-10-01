from __future__ import annotations

import copy
import importlib.util
import json
import random
import re
from pathlib import Path
from types import ModuleType
from typing import Any

import pytest

import corvus_json_schema as cjs
from corvus_json_schema.pattern import compile_pattern


def import_module(source: str, tmp_path: Path, name: str = "generated") -> ModuleType:
    path = tmp_path / f"{name}.py"
    path.write_text(source, encoding="utf-8")
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def test_compile_accepts_json_text_and_parsed_schemas() -> None:
    for v in (cjs.compile('{"type":"string","minLength":2}'), cjs.compile({"type": "string", "minLength": 2})):
        assert v("ab") is True
        assert v("a") is False
        assert v(12) is False


def test_generate_module_emits_a_standalone_module_equivalent_to_compile(tmp_path: Path) -> None:
    schema = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "id": {"type": "integer", "minimum": 1},
            "tags": {"type": "array", "items": {"type": "string"}, "uniqueItems": True},
        },
        "required": ["id"],
        "unevaluatedProperties": False,
    }
    mod = import_module(cjs.generate_module(schema), tmp_path)
    direct = cjs.compile(schema)
    for c in [
        {"id": 1},
        {"id": 1, "tags": ["a", "b"]},
        {"id": 1, "tags": ["a", "a"]},
        {"id": 0},
        {"id": 1, "extra": True},
        {},
        "x",
    ]:
        assert mod.validate(c) == direct(c), c


def test_generate_module_keeps_the_dynamic_scope(tmp_path: Path) -> None:
    schema = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/tree",
        "$dynamicAnchor": "node",
        "type": "object",
        "properties": {"data": True, "children": {"type": "array", "items": {"$dynamicRef": "#node"}}},
    }
    strict = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/strict-tree",
        "$dynamicAnchor": "node",
        "$ref": "tree",
        "unevaluatedProperties": False,
    }

    def resolve_document(uri: str) -> Any:
        return schema if uri == "https://example.com/tree" else None

    mod = import_module(cjs.generate_module(strict, resolve_document=resolve_document), tmp_path)
    direct = cjs.compile(strict, resolve_document=resolve_document)
    ok = {"children": [{"data": 1, "children": []}]}
    bad = {"children": [{"daat": 1}]}
    assert direct(ok) is True
    assert direct(bad) is False
    assert mod.validate(ok) is True
    assert mod.validate(bad) is False


def test_custom_formats_are_asserted_when_format_assertion_is_on() -> None:
    v = cjs.compile(
        {"type": "string", "format": "even-length"},
        assert_format=True,
        formats={"even-length": lambda s: len(s) % 2 == 0},
    )
    assert v("ab") is True
    assert v("abc") is False
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.generate_module({"format": "even-length"}, assert_format=True, formats={"even-length": lambda s: True})


def test_format_is_an_annotation_by_default_and_asserted_on_request() -> None:
    assert cjs.compile({"format": "ipv4"})("not an address") is True
    assert cjs.compile({"format": "ipv4"}, assert_format=True)("not an address") is False
    assert cjs.compile({"format": "ipv4"}, assert_format=True)("10.0.0.1") is True


def test_entry_point_evaluates_from_a_subschema() -> None:
    schema = {"$defs": {"positive": {"type": "number", "exclusiveMinimum": 0}}, "type": "string"}
    v = cjs.compile(schema, entry_point="#/$defs/positive")
    assert v(3) is True
    assert v(-3) is False
    assert v("s") is False


def test_default_dialect_applies_to_schemas_without_schema() -> None:
    schema = {"items": [{"type": "string"}], "additionalItems": False}
    assert cjs.compile(schema, default_dialect=cjs.Dialect.DRAFT7)(["a", "b"]) is False
    # In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies.
    assert cjs.compile(schema)(["a", "b"]) is True


def test_unresolvable_references_fail_compilation() -> None:
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.compile({"$ref": "https://example.com/missing.json"})


def test_invalid_patterns_fail_compilation() -> None:
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.compile({"pattern": "^(abc"})


def test_in_place_recursion_beyond_max_depth_raises() -> None:
    v = cjs.compile({"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "$ref": "#/$defs/loop"}, max_depth=16)
    with pytest.raises(cjs.SchemaEvaluationDepthError):
        v(1)
    # The depth count is reset for the next evaluation.
    with pytest.raises(cjs.SchemaEvaluationDepthError):
        v(1)


def test_numbers_are_compared_exactly_for_multiple_of() -> None:
    v = cjs.compile({"multipleOf": 0.01})
    assert v(0.07) is True
    assert v(19.99) is True
    assert v(0.075) is False
    assert cjs.compile({"multipleOf": 0.0001})(0.0075) is True
    assert cjs.compile({"multipleOf": 3})(10**30) is False
    assert cjs.compile({"multipleOf": 3})(3 * 10**30) is True


def test_booleans_are_not_numbers() -> None:
    assert cjs.compile({"const": 1})(True) is False
    assert cjs.compile({"const": 1})(1.0) is True
    assert cjs.compile({"const": False})(0) is False
    assert cjs.compile({"enum": [0, 1]})(False) is False
    assert cjs.compile({"enum": [0, 1, 2, 3, 4, 5, 6, 7]})(True) is False
    assert cjs.compile({"enum": [0, 1, 2, 3, 4, 5, 6, 7]})(7.0) is True
    assert cjs.compile({"enum": [[1], {"a": True}]})([True]) is False
    assert cjs.compile({"enum": [[1], {"a": True}]})({"a": True}) is True
    assert cjs.compile({"uniqueItems": True})([1, True]) is True
    assert cjs.compile({"uniqueItems": True})([1, 1.0]) is False
    assert cjs.compile({"uniqueItems": True})([[1], [True]]) is True
    assert cjs.compile({"uniqueItems": True})([{"a": 1}, {"a": 1.0}]) is False
    assert cjs.compile({"type": "integer"})(True) is False
    assert cjs.compile({"type": "number", "minimum": 0})(True) is False


def test_integers_are_exact() -> None:
    v = cjs.compile({"type": "integer", "maximum": 2**64})
    assert v(2**64) is True
    assert v(2**64 + 1) is False
    assert cjs.compile({"type": "integer"})(1.0) is True
    assert cjs.compile({"$schema": "http://json-schema.org/draft-04/schema#", "type": "integer"})(1.0) is False


def test_unusual_property_names_are_looked_up_as_keys() -> None:
    v = cjs.compile(
        {
            "required": ["constructor", "toString"],
            "properties": {"__proto__": {"type": "string"}, "a'b\"c": {"type": "string"}},
        }
    )
    assert v({}) is False
    assert v({"constructor": 1, "toString": 2}) is True
    assert v({"constructor": 1, "toString": 2, "__proto__": 3}) is False
    assert v({"constructor": 1, "toString": 2, "__proto__": "x"}) is True
    assert v({"constructor": 1, "toString": 2, "a'b\"c": 1}) is False


def test_structurally_identical_subschemas_share_one_generated_function(monkeypatch: pytest.MonkeyPatch) -> None:
    # Without inlining (which would put each copy in its caller), the three copies are one function.
    monkeypatch.setattr("corvus_json_schema.codegen.INLINE_CHILDREN", False)
    leaf = {"type": "object", "properties": {"a": {"type": "string"}, "b": {"type": "integer"}}, "required": ["a"]}
    v = cjs.compile({"type": "object", "properties": {"x": leaf, "y": copy.deepcopy(leaf), "z": copy.deepcopy(leaf)}})
    assert len(re.findall(r"^def ", v.source, re.M)) == 2


def test_pattern_fast_paths_agree_with_the_regular_expression() -> None:
    patterns = [
        "^[1-5](?:[0-9]{2}|XX)$",
        "^[a-zA-Z0-9._-]+$",
        "^\\d{4}-\\d{2}-\\d{2}$",
        "^[A-Z]",
        "^[a-z]{2,3}$",
        "^(ab|cd)",
        "^[a-c]{2}[0-9]*$",
        "^x-",
        "^\\w+$",
        "^(a|b)c{2}$",
        "^(abc|de)$",
        "^abc$",
        "b-c",
        "^ab",
    ]
    alphabet = ["a", "b", "c", "d", "e", "x", "X", "A", "Z", "-", ".", "_", "0", "1", "2", "5", "9", " ", "é", "😀"]
    rng = random.Random(12345)
    for p in patterns:
        matcher = compile_pattern(p)
        assert matcher is not None
        via_compile = cjs.compile({"pattern": p})
        for _ in range(3000):
            s = "".join(rng.choice(alphabet) for _ in range(rng.randrange(7)))
            assert via_compile(s) == (matcher(s) is not None), f"{p} on {s!r}"


@pytest.mark.parametrize(
    ("pattern", "text", "expected"),
    [
        ("^[a-z]+$", "abc\n", False),
        ("\\d", "\u0663", False),
        ("\\w", "é", False),
        ("^\\p{L}+$", "héllo", True),
        ("a.c", "a\nc", False),
        ("a.c", "a\u2028c", False),
        ("^\\s$", "\u00a0", True),
        ("^\\S$", "\u00a0", False),
        ("(?<year>\\d{4})-\\k<year>", "2020-2020", True),
        ("^\\d{3}\\-\\d{4}$", "555-1234", True),
        ("\\bfoo", "éfoo", True),
        ("[^]", "\n", True),
        ("^[]", "a", False),
        ("\\u{1F600}", "😀", True),
        ("\\uD83D\\uDE00", "😀", True),
        ("(?<=a+)b", "aab", True),
    ],
)
def test_patterns_have_ecma_semantics(pattern: str, text: str, expected: bool) -> None:
    assert cjs.compile({"pattern": pattern})(text) is expected


def test_standalone_modules_collect_the_same_results_as_compile(tmp_path: Path) -> None:
    schema = {"type": "object", "properties": {"a": {"type": "string", "title": "A"}}, "required": ["b"]}
    mod = import_module(cjs.generate_module(schema), tmp_path)
    direct = cjs.compile(schema)
    for level in (cjs.ResultsLevel.BASIC, cjs.ResultsLevel.DETAILED, cjs.ResultsLevel.VERBOSE):
        a = cjs.JsonSchemaResultsCollector.create(level)
        b = cjs.JsonSchemaResultsCollector.create(level)
        assert mod.evaluate({"a": 1}, a) == direct.evaluate({"a": 1}, b)
        assert a.results == b.results
    assert "def evaluate" not in cjs.generate_module(schema, collecting=False)


def test_one_schema_object_used_at_two_locations_keeps_both_locations() -> None:
    leaf = {"type": "string"}
    v = cjs.compile({"properties": {"a": leaf, "b": leaf}})
    assert v({"a": "x", "b": 1}) is False
    c = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.DETAILED)
    v.evaluate({"a": "x", "b": 1}, c)
    assert any(
        r.schema_evaluation_location == "/properties/b/type" and r.document_evaluation_location == "/b"
        for r in c.results
    )


def _root_function(source: str, name: str = "v0") -> str:
    m = re.search(rf"^def {name}\(x\):\n(?:    .*\n?)*", source, re.M)
    assert m is not None
    return m.group(0)


def test_all_of_and_ref_compositions_of_plain_objects_check_an_object_in_one_pass() -> None:
    base = {
        "type": "object",
        "properties": {"id": {"type": "integer"}, "tags": {"type": "array"}},
        "required": ["id"],
        "maxProperties": 4,
    }
    flat = {
        "$defs": {"base": base},
        "allOf": [
            {"$ref": "#/$defs/base"},
            {"properties": {"name": {"type": "string"}, "id": {"type": "integer"}}, "required": ["name"]},
        ],
        "properties": {"active": True},
        "minProperties": 2,
    }
    # The root function holds every branch's names: one section, no call per branch.
    root = _root_function(cjs.compile(flat).source)
    for name in ("id", "tags", "name"):
        assert repr(name) in root, name

    conflicting = {"allOf": [{"properties": {"id": {"type": "integer"}}}, {"properties": {"id": {"minimum": 1}}}]}
    non_object_branch = {
        "allOf": [
            {"properties": {"id": {"type": "integer"}}},
            {"minLength": 2, "properties": {"n": {"type": "string"}}},
        ]
    }
    for schema in (flat, conflicting, non_object_branch):
        v = cjs.compile(schema)
        for instance in [
            {"id": 1, "name": "a"},
            {"id": 1, "name": "a", "tags": [], "active": 1},
            {"id": 1, "name": "a", "tags": [], "active": 1, "other": 2},
            {"id": "x", "name": "a"},
            {"id": 1, "name": 2},
            {"id": 0},
            {"name": "a"},
            {"id": 1, "n": "a"},
            {"id": 1, "n": 1},
            {},
            "ab",
            "a",
            [],
            None,
        ]:
            collected = v.evaluate(instance, cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.BASIC))
            assert v(instance) == collected, f"{json.dumps(schema)} on {json.dumps(instance)}"
    assert cjs.compile(flat)({"id": 1, "name": "a", "tags": [], "active": 1, "other": 2}) is False
    assert cjs.compile(flat)({"id": 1}) is False
    assert cjs.compile(non_object_branch)("a") is False


def test_unevaluated_properties_under_conditionals_is_decided_without_run_time_tracking() -> None:
    parameter = {
        "type": "object",
        "properties": {
            "name": {"type": "string"},
            "in": {"enum": ["query", "path", "header"]},
            "schema": True,
            "content": True,
        },
        "required": ["name", "in"],
        "if": {"properties": {"in": {"const": "query"}}, "required": ["in"]},
        "then": {"properties": {"allowEmptyValue": {"type": "boolean"}}},
        "dependentSchemas": {
            "schema": {
                "properties": {"style": {"type": "string"}},
                "allOf": [
                    {
                        "if": {"properties": {"style": {"const": "form"}}, "required": ["style"]},
                        "then": {"properties": {"explode": {"type": "boolean"}}},
                        "else": {"properties": {"explode": {"const": False}}},
                    }
                ],
            }
        },
        "unevaluatedProperties": False,
    }
    patterns = {
        "properties": {"kind": True},
        "if": {"properties": {"kind": {"const": "ext"}}},
        "then": {"patternProperties": {"^x-": {"type": "string"}}},
        "else": {"if": {"required": ["legacy"]}, "then": {"properties": {"legacy": True, "old": {"type": "integer"}}}},
        "unevaluatedProperties": {"type": "number"},
    }
    alternatives = {
        "properties": {"a": True},
        "anyOf": [{"properties": {"b": True}}, {"required": ["c"]}],
        "unevaluatedProperties": False,
    }
    for schema in (parameter, patterns):
        assert "= set()" not in cjs.compile(schema).source, json.dumps(schema)
    for schema in (parameter, patterns, alternatives):
        v = cjs.compile(schema)
        for instance in [
            {"name": "q", "in": "query"},
            {"name": "q", "in": "query", "allowEmptyValue": True},
            {"name": "q", "in": "path", "allowEmptyValue": True},
            {"name": "q", "in": "query", "schema": {}, "style": "form", "explode": True},
            {"name": "q", "in": "query", "schema": {}, "style": "simple", "explode": True},
            {"name": "q", "in": "query", "schema": {}, "style": "simple", "explode": False},
            {"name": "q", "in": "query", "style": "form", "explode": True},
            {"name": "q", "in": "query", "schema": {}, "other": 1},
            {"kind": "ext", "x-a": "y"},
            {"kind": "ext", "x-a": 1},
            {"kind": "other", "x-a": "y"},
            {"kind": "other", "x-a": 2},
            {"legacy": 1, "old": 2},
            {"legacy": 1, "old": "two"},
            {"old": 2},
            {"a": 1, "b": 2},
            {"a": 1, "c": 2},
            {"a": 1, "b": 2, "c": 3},
            {},
            [],
            "x",
        ]:
            collected = v.evaluate(instance, cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.BASIC))
            assert v(instance) == collected, f"{json.dumps(schema)} on {json.dumps(instance)}"


def test_many_declared_properties_dispatch_through_a_table() -> None:
    names = [f"p{i}" for i in range(20)]
    schema = {
        "type": "object",
        "properties": {
            n: {"type": "integer"} if i % 2 else {"type": "string", "minLength": 1} for i, n in enumerate(names)
        },
        "patternProperties": {"^x-": {"type": "boolean"}},
        "additionalProperties": False,
    }
    v = cjs.compile(schema)
    assert ".get(k)" in v.source
    assert v({"p0": "a", "p1": 1, "x-y": True}) is True
    assert v({"p0": "", "p1": 1}) is False
    assert v({"p1": "a"}) is False
    assert v({"x-y": 1}) is False
    assert v({"q": 1}) is False


def test_validators_with_module_state_are_safe_across_threads() -> None:
    import threading

    tree = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/tree",
        "$dynamicAnchor": "node",
        "type": "object",
        "properties": {"data": True, "children": {"type": "array", "items": {"$dynamicRef": "#node"}}},
    }
    strict = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/strict-tree",
        "$dynamicAnchor": "node",
        "$ref": "tree",
        "unevaluatedProperties": False,
    }
    documents = {"https://example.com/tree": tree, "https://example.com/strict-tree": strict}
    # The entry resource does not define the anchor, so the reference stays dynamic (a scope is kept at run time).
    root = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/root",
        "$ref": "strict-tree",
    }
    v = cjs.compile(root, resolve_document=documents.get)
    assert "LOCK" in v.source and "DS.append" in v.source
    ok = {"children": [{"data": 1, "children": [{"data": 2, "children": []}] * 20}] * 20}
    bad = {"children": [{"data": 1, "children": [{"daat": 2}]}]}
    errors: list[str] = []

    def work() -> None:
        try:
            for _ in range(200):
                if v(ok) is not True or v(bad) is not False:
                    errors.append("wrong result")
        except Exception as e:  # the failure is the finding
            errors.append(repr(e))

    threads = [threading.Thread(target=work) for _ in range(8)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    assert errors == []


def test_a_custom_format_can_reenter_the_validator() -> None:
    holder: dict[str, cjs.Validator] = {}

    def nested(s: str) -> bool:
        # Validates a JSON document embedded in the string with the same validator.
        return holder["v"](json.loads(s)) if s.startswith("{") else True

    schema = {
        "$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}},
        "type": "object",
        "properties": {"inner": {"type": "string", "format": "nested"}, "x": {"$ref": "#/$defs/loop"}},
    }
    v = cjs.compile(schema, assert_format=True, formats={"nested": nested}, max_depth=8)
    holder["v"] = v
    assert "LOCK" in v.source
    assert v({"inner": json.dumps({"inner": "plain"})}) is True
    with pytest.raises(cjs.SchemaEvaluationDepthError):
        v({"x": 1})
    assert v({"inner": "plain"}) is True


def test_scratch_names_never_shadow_module_constants() -> None:
    # The contains count is a local of the function that also reads the enum's constant: their names must differ.
    schema = {
        "prefixItems": [{"type": "string", "pattern": "^a.*b"}],
        "contains": {"type": "string"},
        "enum": [["ab"], ["axb", 1]],
    }
    v = cjs.compile(schema)
    assert v(["ab"]) is True
    assert v(["axb", 1]) is True
    assert v(["zz"]) is False
    assert v(["ab", "c"]) is False
