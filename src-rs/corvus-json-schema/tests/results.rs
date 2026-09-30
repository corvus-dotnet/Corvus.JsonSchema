//! Results collection: the expectations of the C# evaluator's ResultsTests and ResultPathTests, plus worked examples
//! traced through the C# collecting path (row order, locations, messages, levels). A port of the TypeScript
//! `test/results.test.mjs`.

use corvus_json_schema::{
    CompileOptions, JsonSchemaResultsCollector, ResultsLevel, Validator, collect_annotations, compile, compile_with,
};
use serde_json::{Value, json};

fn dump(v: &Validator, instance: &Value, level: ResultsLevel) -> String {
    let mut c = JsonSchemaResultsCollector::new(level);
    v.evaluate(instance, &mut c).unwrap();
    c.results()
        .iter()
        .map(|r| {
            format!(
                "{}|{}|{}|{}|{}",
                if r.is_match { "match" } else { "fail" },
                r.schema_evaluation_location,
                r.evaluation_location,
                r.document_evaluation_location,
                r.message
            )
        })
        .collect::<Vec<_>>()
        .join("\n")
}

fn person() -> Value {
    json!({
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "title": "Person",
        "properties": {
            "name": { "type": "string", "minLength": 1, "description": "The name" },
            "age": { "type": "integer", "minimum": 0 }
        },
        "required": ["name"],
        "additionalProperties": false
    })
}

fn refs() -> Value {
    json!({
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$defs": {
            "fooId": { "type": "integer", "minimum": 0 },
            "holder": { "type": "object", "properties": { "fooId": { "$ref": "#/$defs/fooId" } } },
            "viaRef": { "$ref": "#/$defs/fooId" }
        }
    })
}

fn example() -> Value {
    json!({
        "type": "object",
        "properties": { "a": { "type": "string" } },
        "required": ["b"],
        "anyOf": [{ "required": ["a"] }, { "minProperties": 5 }]
    })
}

fn entry(schema: &Value, entry_point: &str) -> Validator {
    compile_with(schema, &CompileOptions { entry_point: Some(entry_point.into()), ..CompileOptions::default() })
        .unwrap()
}

const LEVELS: [ResultsLevel; 3] = [ResultsLevel::Basic, ResultsLevel::Detailed, ResultsLevel::Verbose];

#[test]
fn flag_and_collecting_evaluation_agree_at_every_level() {
    let v = compile(&person()).unwrap();
    for (instance, expected) in [
        (json!({ "name": "a", "age": 3 }), true),
        (json!({ "name": "", "age": 3 }), false),
        (json!({ "age": 3 }), false),
        (json!({ "name": "a", "extra": 1 }), false),
        (json!({ "name": "a", "age": -1 }), false),
        (json!([]), false),
    ] {
        assert_eq!(v.is_valid(&instance), expected);
        for level in LEVELS {
            assert_eq!(v.evaluate(&instance, &mut JsonSchemaResultsCollector::new(level)).unwrap(), expected);
        }
    }
}

#[test]
fn basic_results_report_failing_keywords_with_locations() {
    let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
    assert!(!compile(&person()).unwrap().evaluate(&json!({ "name": "", "age": -1 }), &mut c).unwrap());
    let failures: Vec<_> = c.results().iter().filter(|r| !r.is_match).collect();
    assert!(
        failures
            .iter()
            .any(|f| f.evaluation_location.ends_with("/minLength") && f.document_evaluation_location == "/name")
    );
    assert!(
        failures
            .iter()
            .any(|f| f.evaluation_location.ends_with("/minimum") && f.document_evaluation_location == "/age")
    );
    assert!(failures.iter().any(|f| f.schema_evaluation_location == "/properties/name"));
    assert!(c.results().iter().all(|r| r.message.is_empty()));
}

#[test]
fn verbose_annotations_are_produced() {
    let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Verbose);
    assert!(compile(&person()).unwrap().evaluate(&json!({ "name": "a" }), &mut c).unwrap());
    let annotations = collect_annotations(&c);
    assert_eq!(annotations[""]["title"]["#"], json!("Person"));
    assert_eq!(annotations[""]["title"].len(), 1);
    assert_eq!(annotations["/name"]["description"]["#/properties/name"], json!("The name"));
}

#[test]
fn verbose_output_follows_the_csharp_row_order() {
    assert_eq!(
        dump(&compile(&person()).unwrap(), &json!({ "name": "a" }), ResultsLevel::Verbose),
        [
            "match|/properties/name|/properties/name|/name|The value was expected to match the subschema.",
            "match|/properties/name|/properties/name/description|/name|\"The name\"",
            "match|/properties/name/minLength|/properties/name/minLength|/name|Expected the length of the value to be greater than or equal to '1'",
            "match|/properties/name/type|/properties/name/type|/name|The value was expected to be of type 'string'",
            "match||||The value was expected to match the subschema.",
            "match||/title||\"Person\"",
            "match|/required|/required|/name|Required property present 'name'",
            "match|/type|/type||The value was expected to be of type 'object'",
        ]
        .join("\n")
    );
}

#[test]
fn matching_keywords_carry_their_message_in_verbose_output() {
    let v = compile(&json!({
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": ["integer", "array"],
        "uniqueItems": true,
        "properties": { "n": { "type": "integer" } }
    }))
    .unwrap();
    let rows = |instance: Value| {
        let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Verbose);
        let valid = v.evaluate(&instance, &mut c).unwrap();
        (valid, c.take_results())
    };
    let (valid, r) = rows(json!([1, 2]));
    assert!(valid);
    assert!(r.iter().any(|x| x.is_match
        && x.evaluation_location == "/type"
        && x.message == "The value was expected to be of type '[\"array\", \"integer\"]'"));
    assert!(r.iter().any(|x| x.is_match && x.evaluation_location == "/uniqueItems" && !x.message.is_empty()));
    let (valid, r) = rows(json!([1, 1]));
    assert!(!valid);
    assert!(r.iter().any(|x| !x.is_match && x.evaluation_location == "/uniqueItems" && !x.message.is_empty()));
    let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Verbose);
    assert!(compile(&json!({ "type": "integer" })).unwrap().evaluate(&json!(3), &mut c).unwrap());
    assert!(c.results().iter().any(|x| x.is_match
        && x.evaluation_location == "/type"
        && x.message == "The value was expected to be of type 'integer'"));
}

#[test]
fn an_entry_point_reports_its_own_schema_location() {
    assert_eq!(
        dump(&entry(&refs(), "#/$defs/fooId"), &json!("notAnInteger"), ResultsLevel::Detailed),
        [
            "fail|/$defs/fooId|||The value was expected to match the subschema.",
            "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'",
        ]
        .join("\n")
    );
}

#[test]
fn a_pure_ref_property_is_elided_with_ref_in_the_evaluation_path() {
    assert_eq!(
        dump(&entry(&refs(), "#/$defs/holder"), &json!({ "fooId": "notAnInteger" }), ResultsLevel::Detailed),
        [
            "fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the subschema.",
            "fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of type 'integer'",
            "fail|/$defs/holder|||The value was expected to match the subschema.",
        ]
        .join("\n")
    );
}

#[test]
fn a_pure_ref_root_reports_against_its_target() {
    assert_eq!(
        dump(&entry(&refs(), "#/$defs/viaRef"), &json!("notAnInteger"), ResultsLevel::Detailed),
        [
            "fail|/$defs/fooId|||The value was expected to match the subschema.",
            "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'",
        ]
        .join("\n")
    );
}

#[test]
fn a_required_failure_carries_the_property_name() {
    assert_eq!(
        dump(&compile(&json!({ "type": "object", "required": ["name"] })).unwrap(), &json!({}), ResultsLevel::Detailed),
        [
            "fail||||The value was expected to match the subschema.",
            "fail|/required|/required|/name|Required property not present 'name'",
        ]
        .join("\n")
    );
}

#[test]
fn detailed_output_keeps_failures_only_with_messages() {
    let expected = [
        "fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.",
        "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
        "fail||||The value was expected to match the subschema.",
        "fail|/required|/required|/b|Required property not present 'b'",
    ];
    let v = compile(&example()).unwrap();
    assert_eq!(dump(&v, &json!({ "a": 1 }), ResultsLevel::Detailed), expected.join("\n"));
    let basic: Vec<&str> = expected.iter().map(|l| &l[..=l.rfind('|').unwrap()]).collect();
    assert_eq!(dump(&v, &json!({ "a": 1 }), ResultsLevel::Basic), basic.join("\n"));
}

#[test]
fn verbose_output_reverses_a_contexts_own_rows_after_its_summary() {
    assert_eq!(
        dump(&compile(&example()).unwrap(), &json!({ "a": 1 }), ResultsLevel::Verbose),
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
        .join("\n")
    );
}

#[test]
fn not_subtrees_and_boolean_schemas() {
    assert_eq!(
        dump(&compile(&json!({ "not": { "type": "string" } })).unwrap(), &json!("x"), ResultsLevel::Detailed),
        [
            "fail||||The value was expected to match the subschema.",
            "fail|/not|/not||The value matched the subschema in a not composition, which means the evaluation was not a match.",
        ]
        .join("\n")
    );
    assert_eq!(
        dump(&compile(&json!(false)).unwrap(), &json!(1), ResultsLevel::Detailed),
        ["fail||||The value was expected to match the subschema.", "fail||||"].join("\n")
    );
}

#[test]
fn a_valid_instance_at_detailed_level_yields_only_the_passing_root_row() {
    assert_eq!(dump(&compile(&person()).unwrap(), &json!({ "name": "a" }), ResultsLevel::Detailed), "match||||");
}

#[test]
fn property_names_keeps_the_object_location_and_adds_a_failure_row_per_name() {
    assert_eq!(
        dump(&compile(&json!({ "propertyNames": { "maxLength": 2 } })).unwrap(), &json!({ "abc": 1 }), ResultsLevel::Detailed),
        [
            "fail|/propertyNames|/propertyNames||The value was expected to match the subschema.",
            "fail|/propertyNames/maxLength|/propertyNames/maxLength||Expected the length of the value to be less than or equal to '2'",
            "fail||||The value was expected to match the subschema.",
            "fail|/propertyNames|/propertyNames||The property name did not match the schema.",
        ]
        .join("\n")
    );
}

#[test]
fn draft4_exclusive_bounds_report_under_exclusive_maximum_with_the_maximum() {
    let v = compile(
        &json!({ "$schema": "http://json-schema.org/draft-04/schema#", "maximum": 3, "exclusiveMaximum": true }),
    )
    .unwrap();
    assert_eq!(
        dump(&v, &json!(3), ResultsLevel::Detailed),
        [
            "fail||||The value was expected to match the subschema.",
            "fail|/exclusiveMaximum|/exclusiveMaximum||The value was expected to be less than '3'",
        ]
        .join("\n")
    );
}

#[test]
fn failing_any_of_branches_are_discarded_and_unevaluated_properties_has_no_message() {
    let v = compile(
        &json!({ "anyOf": [{ "properties": { "a": true } }, { "required": ["zz"] }], "unevaluatedProperties": false }),
    )
    .unwrap();
    assert_eq!(
        dump(&v, &json!({ "a": 1, "b": 2 }), ResultsLevel::Detailed),
        [
            "fail|/unevaluatedProperties|/unevaluatedProperties|/b|The value was expected to match the subschema.",
            "fail|/unevaluatedProperties|/unevaluatedProperties|/b|",
            "fail||||The value was expected to match the subschema.",
            "fail|/unevaluatedProperties|/unevaluatedProperties||",
        ]
        .join("\n")
    );
}

#[test]
fn a_collector_accumulates_across_evaluations() {
    let v = compile(&person()).unwrap();
    let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Detailed);
    v.evaluate(&json!({ "age": "x" }), &mut c).unwrap();
    let first = c.results().len();
    assert!(first > 0);
    v.evaluate(&json!({ "age": "x" }), &mut c).unwrap();
    assert_eq!(c.results().len(), 2 * first);
}

#[test]
fn dependencies_reports_under_its_own_name_in_every_dialect() {
    let v = compile(&json!({
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "dependencies": { "a": ["b"], "c": { "required": ["d"] } }
    }))
    .unwrap();
    assert_eq!(
        dump(&v, &json!({ "a": 1, "c": 1 }), ResultsLevel::Detailed),
        [
            "fail|/dependencies/c|/dependencies/c||The value was expected to match the subschema.",
            "fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present 'd'",
            "fail||||The value was expected to match the subschema.",
            "fail|/dependencies|/dependencies|/c|The value did match the schema applied because it contained the property 'c'",
            "fail|/dependencies|/dependencies|/b|Required property not present 'b'",
        ]
        .join("\n")
    );
    let modern =
        compile(&json!({ "dependentRequired": { "a": ["b"] }, "dependentSchemas": { "c": { "required": ["d"] } } }))
            .unwrap();
    let rows = dump(&modern, &json!({ "a": 1, "c": 1 }), ResultsLevel::Detailed);
    assert!(rows.contains("fail|/dependentRequired|/dependentRequired|/b|Required property not present 'b'"));
    assert!(rows.contains("fail|/dependentSchemas/c|/dependentSchemas/c||"));
}

#[test]
fn a_statically_resolved_dynamic_ref_hop_is_named_dynamic_ref_in_the_evaluation_path() {
    let v = compile(&json!({
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "properties": { "p": { "$dynamicRef": "#/$defs/n" } },
        "$defs": { "n": { "type": "integer" } }
    }))
    .unwrap();
    assert_eq!(
        dump(&v, &json!({ "p": "x" }), ResultsLevel::Detailed),
        [
            "fail|/$defs/n|/properties/p/$dynamicRef|/p|The value was expected to match the subschema.",
            "fail|/$defs/n/type|/properties/p/$dynamicRef/type|/p|The value was expected to be of type 'integer'",
            "fail||||The value was expected to match the subschema.",
        ]
        .join("\n")
    );
}
