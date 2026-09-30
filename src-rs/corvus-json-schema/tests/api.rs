//! Public API behaviour, ported from the TypeScript `test/api.test.mjs`.

use std::sync::Arc;

use corvus_json_schema::{CompileOptions, Dialect, JsonSchemaResultsCollector, ResultsLevel, compile, compile_with};
use serde_json::{Value, json};

#[test]
fn keeps_the_dynamic_scope() {
    let tree = json!({
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/tree",
        "$dynamicAnchor": "node",
        "type": "object",
        "properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } }
    });
    let strict = json!({
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/strict-tree",
        "$dynamicAnchor": "node",
        "$ref": "tree",
        "unevaluatedProperties": false
    });
    let options = CompileOptions {
        resolve_document: Some(Arc::new(move |uri: &str| (uri == "https://example.com/tree").then(|| tree.clone()))),
        ..CompileOptions::default()
    };
    let v = compile_with(&strict, &options).unwrap();
    assert!(v.is_valid(&json!({ "children": [{ "data": 1, "children": [] }] })));
    assert!(!v.is_valid(&json!({ "children": [{ "daat": 1 }] })));
}

#[test]
fn custom_formats_are_asserted_when_format_assertion_is_on() {
    let mut options = CompileOptions { assert_format: Some(true), ..CompileOptions::default() };
    options.formats.insert("even-length".into(), Arc::new(|s: &str| s.chars().count() % 2 == 0));
    let v = compile_with(&json!({ "type": "string", "format": "even-length" }), &options).unwrap();
    assert!(v.is_valid(&json!("ab")));
    assert!(!v.is_valid(&json!("abc")));
}

#[test]
fn format_is_an_annotation_by_default_and_asserted_on_request() {
    let schema = json!({ "format": "ipv4" });
    let asserted = CompileOptions { assert_format: Some(true), ..CompileOptions::default() };
    assert!(compile(&schema).unwrap().is_valid(&json!("not an address")));
    assert!(!compile_with(&schema, &asserted).unwrap().is_valid(&json!("not an address")));
    assert!(compile_with(&schema, &asserted).unwrap().is_valid(&json!("10.0.0.1")));
}

#[test]
fn entry_point_evaluates_from_a_subschema() {
    let schema = json!({ "$defs": { "positive": { "type": "number", "exclusiveMinimum": 0 } }, "type": "string" });
    let options = CompileOptions { entry_point: Some("#/$defs/positive".into()), ..CompileOptions::default() };
    let v = compile_with(&schema, &options).unwrap();
    assert!(v.is_valid(&json!(3)));
    assert!(!v.is_valid(&json!(-3)));
    assert!(!v.is_valid(&json!("s")));
}

#[test]
fn default_dialect_applies_to_schemas_without_schema() {
    let schema = json!({ "items": [{ "type": "string" }], "additionalItems": false });
    let draft7 = CompileOptions { default_dialect: Dialect::Draft7, ..CompileOptions::default() };
    assert!(!compile_with(&schema, &draft7).unwrap().is_valid(&json!(["a", "b"])));
    // In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies.
    assert!(compile(&schema).unwrap().is_valid(&json!(["a", "b"])));
}

#[test]
fn unresolvable_references_fail_compilation() {
    assert!(compile(&json!({ "$ref": "https://example.com/missing.json" })).is_err());
}

#[test]
fn in_place_recursion_beyond_max_depth_is_an_error() {
    let schema = json!({ "$defs": { "loop": { "allOf": [{ "$ref": "#/$defs/loop" }] } }, "$ref": "#/$defs/loop" });
    let v = compile_with(&schema, &CompileOptions { max_depth: 16, ..CompileOptions::default() }).unwrap();
    assert!(v.validate(&json!(1)).is_err());
    assert!(!v.is_valid(&json!(1)));
    assert!(v.evaluate(&json!(1), &mut JsonSchemaResultsCollector::new(ResultsLevel::Detailed)).is_err());
}

#[test]
fn numbers_are_compared_exactly_for_multiple_of() {
    let v = compile(&json!({ "multipleOf": 0.01 })).unwrap();
    assert!(v.is_valid(&json!(0.07)));
    assert!(v.is_valid(&json!(19.99)));
    assert!(!v.is_valid(&json!(0.075)));
    assert!(compile(&json!({ "multipleOf": 0.0001 })).unwrap().is_valid(&json!(0.0075)));
}

#[test]
fn one_schema_object_used_at_two_locations_keeps_both_locations() {
    let leaf = json!({ "type": "string" });
    let v = compile(&json!({ "properties": { "a": leaf, "b": leaf } })).unwrap();
    assert!(!v.is_valid(&json!({ "a": "x", "b": 1 })));
    let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Detailed);
    v.evaluate(&json!({ "a": "x", "b": 1 }), &mut c).unwrap();
    assert!(
        c.results()
            .iter()
            .any(|r| r.schema_evaluation_location == "/properties/b/type" && r.document_evaluation_location == "/b")
    );
}

#[test]
fn validators_are_shareable_between_threads() {
    let v = compile(&json!({ "type": "array", "items": { "type": "integer", "minimum": 0 } })).unwrap();
    let handles: Vec<_> = (0..4)
        .map(|i| {
            let v = v.clone();
            std::thread::spawn(move || v.is_valid(&Value::Array(vec![json!(i); 100])))
        })
        .collect();
    for h in handles {
        assert!(h.join().unwrap());
    }
}

#[test]
fn discriminated_one_of_and_any_of_agree_with_exhaustive_evaluation() {
    let shape = |kind: &str, extra: &str| json!({ "type": "object", "properties": { "kind": { "const": kind }, extra: { "type": "number" } }, "required": ["kind", extra] });
    let branches =
        json!([shape("circle", "r"), shape("square", "side"), { "properties": { "kind": { "enum": [1, true] } } }]);
    for keyword in ["oneOf", "anyOf"] {
        let v = compile(&json!({ keyword: branches })).unwrap();
        for instance in [
            json!({ "kind": "circle", "r": 1 }),
            json!({ "kind": "circle", "side": 1 }),
            json!({ "kind": "square", "side": 1 }),
            json!({ "kind": "triangle" }),
            json!({ "kind": 1.0 }),
            json!({ "kind": true }),
            json!({ "kind": false }),
            json!({}),
            json!("circle"),
        ] {
            let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
            let collected = v.evaluate(&instance, &mut c).unwrap();
            assert_eq!(v.is_valid(&instance), collected, "{keyword} on {instance}");
        }
    }
}
