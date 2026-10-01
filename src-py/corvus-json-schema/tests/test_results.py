"""Results collection: the expectations of the C# evaluator's ResultsTests and ResultPathTests, plus worked examples
traced through the C# collecting path (row order, locations, messages, levels)."""

from __future__ import annotations

import importlib
import os
from typing import Any

# CORVUS_IMPL=corvus_json_schema_rs runs these against the Rust-backed package, which has the same API.
cjs: Any = importlib.import_module(os.environ.get("CORVUS_IMPL", "corvus_json_schema"))
JsonSchemaResultsCollector = cjs.JsonSchemaResultsCollector
ResultsLevel = cjs.ResultsLevel


def dump(validator: Any, instance: Any, level: Any) -> str:
    collector = JsonSchemaResultsCollector.create(level)
    validator.evaluate(instance, collector)
    return "\n".join(
        f"{'match' if r.is_match else 'fail'}|{r.schema_evaluation_location}|{r.evaluation_location}|"
        f"{r.document_evaluation_location}|{r.message}"
        for r in collector.results
    )


PERSON = {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "title": "Person",
    "properties": {
        "name": {"type": "string", "minLength": 1, "description": "The name"},
        "age": {"type": "integer", "minimum": 0},
    },
    "required": ["name"],
    "additionalProperties": False,
}


def test_flag_and_collecting_evaluation_agree_at_every_level() -> None:
    v = cjs.compile(PERSON)
    for instance, expected in [
        ({"name": "a", "age": 3}, True),
        ({"name": "", "age": 3}, False),
        ({"age": 3}, False),
        ({"name": "a", "extra": 1}, False),
        ({"name": "a", "age": -1}, False),
        ([], False),
    ]:
        assert v(instance) == expected
        for level in (ResultsLevel.BASIC, ResultsLevel.DETAILED, ResultsLevel.VERBOSE):
            assert v.evaluate(instance, JsonSchemaResultsCollector.create(level)) == expected


def test_basic_results_report_failing_keywords_with_locations() -> None:
    collector = JsonSchemaResultsCollector.create(ResultsLevel.BASIC)
    assert cjs.compile(PERSON).evaluate({"name": "", "age": -1}, collector) is False
    failures = [r for r in collector.results if not r.is_match]
    assert any(
        f.evaluation_location.endswith("/minLength") and f.document_evaluation_location == "/name" for f in failures
    )
    assert any(
        f.evaluation_location.endswith("/minimum") and f.document_evaluation_location == "/age" for f in failures
    )
    assert any(f.schema_evaluation_location == "/properties/name" for f in failures)
    assert all(r.message == "" for r in collector.results)


def test_verbose_annotations_are_produced() -> None:
    collector = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE)
    assert cjs.compile(PERSON).evaluate({"name": "a"}, collector) is True
    annotations = cjs.collect_annotations(collector)
    assert annotations[""]["title"] == {"#": "Person"}
    assert annotations["/name"]["description"] == {"#/properties/name": "The name"}


def test_verbose_output_follows_the_csharp_row_order() -> None:
    assert dump(cjs.compile(PERSON), {"name": "a"}, ResultsLevel.VERBOSE) == "\n".join(
        [
            "match|/properties/name|/properties/name|/name|The value was expected to match the subschema.",
            'match|/properties/name|/properties/name/description|/name|"The name"',
            "match|/properties/name/minLength|/properties/name/minLength|/name|Expected the length of the value to be greater than or equal to '1'",
            "match|/properties/name/type|/properties/name/type|/name|The value was expected to be of type 'string'",
            "match||||The value was expected to match the subschema.",
            'match||/title||"Person"',
            "match|/required|/required|/name|Required property present 'name'",
            "match|/type|/type||The value was expected to be of type 'object'",
        ]
    )


def test_matching_keywords_carry_their_message_in_verbose_output() -> None:
    schema = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": ["integer", "array"],
        "uniqueItems": True,
        "properties": {"n": {"type": "integer"}},
    }

    def rows(instance: Any) -> tuple[bool, list[Any]]:
        c = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE)
        valid = cjs.compile(schema).evaluate(instance, c)
        return valid, c.results

    valid, results = rows([1, 2])
    assert valid
    assert any(
        x.is_match
        and x.evaluation_location == "/type"
        and x.message == 'The value was expected to be of type \'["array", "integer"]\''
        for x in results
    )
    assert any(x.is_match and x.evaluation_location == "/uniqueItems" and x.message for x in results)
    valid, results = rows([1, 1])
    assert not valid
    assert any(not x.is_match and x.evaluation_location == "/uniqueItems" and x.message for x in results)
    c = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE)
    assert cjs.compile({"type": "integer"}).evaluate(3, c)
    assert any(
        x.is_match
        and x.evaluation_location == "/type"
        and x.message == "The value was expected to be of type 'integer'"
        for x in c.results
    )


REFS = {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "$defs": {
        "fooId": {"type": "integer", "minimum": 0},
        "holder": {"type": "object", "properties": {"fooId": {"$ref": "#/$defs/fooId"}}},
        "viaRef": {"$ref": "#/$defs/fooId"},
    },
}


def test_an_entry_point_reports_its_own_schema_location() -> None:
    assert dump(cjs.compile(REFS, entry_point="#/$defs/fooId"), "notAnInteger", ResultsLevel.DETAILED) == "\n".join(
        [
            "fail|/$defs/fooId|||The value was expected to match the subschema.",
            "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'",
        ]
    )


def test_a_pure_ref_property_is_elided_with_ref_in_the_evaluation_path() -> None:
    assert dump(
        cjs.compile(REFS, entry_point="#/$defs/holder"), {"fooId": "notAnInteger"}, ResultsLevel.DETAILED
    ) == "\n".join(
        [
            "fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the subschema.",
            "fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of type 'integer'",
            "fail|/$defs/holder|||The value was expected to match the subschema.",
        ]
    )


def test_a_pure_ref_root_reports_against_its_target() -> None:
    assert dump(cjs.compile(REFS, entry_point="#/$defs/viaRef"), "notAnInteger", ResultsLevel.DETAILED) == "\n".join(
        [
            "fail|/$defs/fooId|||The value was expected to match the subschema.",
            "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'",
        ]
    )


def test_a_required_failure_carries_the_property_name() -> None:
    assert dump(cjs.compile({"type": "object", "required": ["name"]}), {}, ResultsLevel.DETAILED) == "\n".join(
        [
            "fail||||The value was expected to match the subschema.",
            "fail|/required|/required|/name|Required property not present 'name'",
        ]
    )


EXAMPLE = {
    "type": "object",
    "properties": {"a": {"type": "string"}},
    "required": ["b"],
    "anyOf": [{"required": ["a"]}, {"minProperties": 5}],
}


def test_detailed_output_keeps_failures_only_with_messages() -> None:
    expected = [
        "fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.",
        "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
        "fail||||The value was expected to match the subschema.",
        "fail|/required|/required|/b|Required property not present 'b'",
    ]
    assert dump(cjs.compile(EXAMPLE), {"a": 1}, ResultsLevel.DETAILED) == "\n".join(expected)
    assert dump(cjs.compile(EXAMPLE), {"a": 1}, ResultsLevel.BASIC) == "\n".join(
        line[: line.rfind("|") + 1] for line in expected
    )


def test_verbose_output_reverses_a_contexts_own_rows_after_its_summary() -> None:
    assert dump(cjs.compile(EXAMPLE), {"a": 1}, ResultsLevel.VERBOSE) == "\n".join(
        [
            "fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.",
            "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
            "match|/anyOf/0|/anyOf/0||The value was expected to match the subschema.",
            "match|/anyOf/0/required|/anyOf/0/required|/a|Required property present 'a'",
            "fail||||The value was expected to match the subschema.",
            "match|/anyOf|/anyOf||The value matched at least one subschema.",
            "fail|/required|/required|/b|Required property not present 'b'",
            "match|/type|/type||The value was expected to be of type 'object'",
        ]
    )


def test_not_subtrees_and_boolean_schemas() -> None:
    assert dump(cjs.compile({"not": {"type": "string"}}), "x", ResultsLevel.DETAILED) == "\n".join(
        [
            "fail||||The value was expected to match the subschema.",
            "fail|/not|/not||The value matched the subschema in a not composition, which means the evaluation was not a match.",
        ]
    )
    assert dump(cjs.compile(False), 1, ResultsLevel.DETAILED) == "\n".join(
        ["fail||||The value was expected to match the subschema.", "fail||||"]
    )


def test_a_valid_instance_at_detailed_level_yields_only_the_passing_root_row() -> None:
    assert dump(cjs.compile(PERSON), {"name": "a"}, ResultsLevel.DETAILED) == "match||||"


def test_property_names_keeps_the_object_location_and_adds_a_failure_row_per_name() -> None:
    assert dump(cjs.compile({"propertyNames": {"maxLength": 2}}), {"abc": 1}, ResultsLevel.DETAILED) == "\n".join(
        [
            "fail|/propertyNames|/propertyNames||The value was expected to match the subschema.",
            "fail|/propertyNames/maxLength|/propertyNames/maxLength||Expected the length of the value to be less than or equal to '2'",
            "fail||||The value was expected to match the subschema.",
            "fail|/propertyNames|/propertyNames||The property name did not match the schema.",
        ]
    )


def test_draft4_exclusive_bounds_report_under_exclusive_maximum_with_the_maximum() -> None:
    v = cjs.compile({"$schema": "http://json-schema.org/draft-04/schema#", "maximum": 3, "exclusiveMaximum": True})
    assert dump(v, 3, ResultsLevel.DETAILED) == "\n".join(
        [
            "fail||||The value was expected to match the subschema.",
            "fail|/exclusiveMaximum|/exclusiveMaximum||The value was expected to be less than '3'",
        ]
    )


def test_failing_any_of_branches_are_discarded_and_unevaluated_properties_has_no_message() -> None:
    v = cjs.compile({"anyOf": [{"properties": {"a": True}}, {"required": ["zz"]}], "unevaluatedProperties": False})
    assert dump(v, {"a": 1, "b": 2}, ResultsLevel.DETAILED) == "\n".join(
        [
            "fail|/unevaluatedProperties|/unevaluatedProperties|/b|The value was expected to match the subschema.",
            "fail|/unevaluatedProperties|/unevaluatedProperties|/b|",
            "fail||||The value was expected to match the subschema.",
            "fail|/unevaluatedProperties|/unevaluatedProperties||",
        ]
    )


def test_a_collector_accumulates_across_evaluations() -> None:
    v = cjs.compile(PERSON)
    c = JsonSchemaResultsCollector.create(ResultsLevel.DETAILED)
    v.evaluate({"age": "x"}, c)
    first = c.result_count
    assert first > 0
    v.evaluate({"age": "x"}, c)
    assert c.result_count == 2 * first


def test_dependencies_reports_under_its_own_name_in_every_dialect() -> None:
    v = cjs.compile(
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "dependencies": {"a": ["b"], "c": {"required": ["d"]}},
        }
    )
    assert dump(v, {"a": 1, "c": 1}, ResultsLevel.DETAILED) == "\n".join(
        [
            "fail|/dependencies/c|/dependencies/c||The value was expected to match the subschema.",
            "fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present 'd'",
            "fail||||The value was expected to match the subschema.",
            "fail|/dependencies|/dependencies|/c|The value did match the schema applied because it contained the property 'c'",
            "fail|/dependencies|/dependencies|/b|Required property not present 'b'",
        ]
    )
    modern = cjs.compile({"dependentRequired": {"a": ["b"]}, "dependentSchemas": {"c": {"required": ["d"]}}})
    rows = dump(modern, {"a": 1, "c": 1}, ResultsLevel.DETAILED)
    assert "fail|/dependentRequired|/dependentRequired|/b|Required property not present 'b'" in rows
    assert "fail|/dependentSchemas/c|/dependentSchemas/c||" in rows


def test_a_statically_resolved_dynamic_ref_hop_is_named_dynamic_ref_in_the_evaluation_path() -> None:
    v = cjs.compile(
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "properties": {"p": {"$dynamicRef": "#/$defs/n"}},
            "$defs": {"n": {"type": "integer"}},
        }
    )
    assert dump(v, {"p": "x"}, ResultsLevel.DETAILED) == "\n".join(
        [
            "fail|/$defs/n|/properties/p/$dynamicRef|/p|The value was expected to match the subschema.",
            "fail|/$defs/n/type|/properties/p/$dynamicRef/type|/p|The value was expected to be of type 'integer'",
            "fail||||The value was expected to match the subschema.",
        ]
    )


def test_numbers_in_messages_are_written_as_json_numbers() -> None:
    v = cjs.compile({"minimum": 2.0, "maximum": 1e21, "multipleOf": 1.5e-7})
    rows = dump(v, 1, ResultsLevel.DETAILED)
    assert "greater than or equal to '2.0'" in rows
    assert "multiple of '1.5e-7'" in rows
    rows = dump(v, 1e22, ResultsLevel.DETAILED)
    assert "less than or equal to '1e+21'" in rows
