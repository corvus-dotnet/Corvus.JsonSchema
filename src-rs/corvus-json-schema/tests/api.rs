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

/// A `not` whose subschema leads back to the schema it is in recurses in place like any other applicator. It stops
/// at the maximum depth: the validation is an error, and `is_valid` reports the instance as invalid. (Evaluating
/// `not` went around the depth guard, so the first of these schemas overflowed the stack, and for the others the
/// `not` turned the abandoned evaluation's false into true.)
#[test]
fn not_on_an_in_place_cycle_stops_at_max_depth() {
    let looping = json!({ "allOf": [{ "$ref": "#/$defs/loop" }] });
    let schemas = [
        json!({ "not": { "$ref": "#" } }),
        json!({ "not": { "not": { "$ref": "#" } } }),
        json!({ "type": "integer", "not": { "$ref": "#" } }),
        json!({ "allOf": [{ "not": { "$ref": "#" } }] }),
        json!({
            "$defs": { "a": { "not": { "$ref": "#/$defs/b" } }, "b": { "not": { "$ref": "#/$defs/a" } } },
            "$ref": "#/$defs/a"
        }),
        json!({ "$defs": { "loop": looping }, "not": { "$ref": "#/$defs/loop" } }),
        json!({ "$defs": { "loop": looping }, "not": { "not": { "$ref": "#/$defs/loop" } } }),
        json!({ "$defs": { "loop": looping }, "properties": { "a": { "not": { "$ref": "#/$defs/loop" } } } }),
        json!({ "unevaluatedProperties": false, "not": { "$ref": "#" } }),
    ];
    let instances = [json!(1), json!("a"), json!({ "a": 1 }), json!([1])];
    for (s, schema) in schemas.iter().enumerate() {
        let v = compile_with(schema, &CompileOptions { max_depth: 16, ..CompileOptions::default() }).unwrap();
        for instance in &instances {
            // Only an object with the property reaches the loop of the eighth schema, and anything but an integer
            // fails the type of the third before its not is reached, when failing fast.
            if (s == 7 && !instance.is_object()) || (s == 2 && !instance.is_i64()) {
                continue;
            }
            let text = instance.to_string();
            assert!(!v.is_valid(instance), "{schema}: is_valid reported {instance} as valid");
            assert!(v.validate(instance).is_err(), "{schema}: validate({instance}) is not an error");
            assert!(
                matches!(v.validate_json(&text), Err(corvus_json_schema::JsonValidationError::DepthExceeded(_))),
                "{schema}: validate_json({instance}) is not a depth error"
            );
            for level in [ResultsLevel::Basic, ResultsLevel::Detailed, ResultsLevel::Verbose] {
                assert!(
                    v.evaluate(instance, &mut JsonSchemaResultsCollector::new(level)).is_err(),
                    "{schema}: evaluate({instance}) is not an error"
                );
            }
        }
    }
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

#[test]
fn discriminators_key_null_and_numbers_by_value() {
    let branch = |kind: Value, extra: &str| json!({ "type": "object", "properties": { "kind": { "const": kind }, extra: { "type": "string" } }, "required": ["kind", extra] });
    let schema = json!({ "oneOf": [branch(json!(null), "a"), branch(json!(1), "b"), branch(json!(1.0), "c"), branch(json!("x"), "d")] });
    let v = compile(&schema).unwrap();
    for instance in [
        json!({ "kind": null, "a": "s" }),
        json!({ "kind": null, "b": "s" }),
        json!({ "kind": 1, "b": "s" }),
        json!({ "kind": 1, "b": "s", "c": "t" }),
        json!({ "kind": 1.0, "c": "t" }),
        json!({ "kind": "x", "d": "s" }),
        json!({ "kind": "y", "d": "s" }),
        json!({ "d": "s" }),
    ] {
        let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
        let collected = v.evaluate(&instance, &mut c).unwrap();
        assert_eq!(v.is_valid(&instance), collected, "{instance}");
    }
    // Both numeric branches match `1` when it has both properties: oneOf fails.
    assert!(!v.is_valid(&json!({ "kind": 1, "b": "s", "c": "t" })));
}

#[test]
fn arrays_of_simple_arrays_match_the_general_path() {
    let position = json!({ "type": "array", "minItems": 2, "maxItems": 3, "items": { "type": "number" } });
    for schema in [
        json!({ "type": "array", "items": position }),
        json!({ "type": "array", "items": { "type": ["array", "string"], "minItems": 2, "items": { "type": "integer" } } }),
        json!({ "type": "array", "items": { "type": "object", "minItems": 2 } }),
        json!({ "type": "array", "items": { "type": "array", "items": { "type": "array", "items": { "type": "number" } } } }),
    ] {
        let v = compile(&schema).unwrap();
        for instance in [
            json!([]),
            json!([[1, 2]]),
            json!([[1, 2], [3, 4, 5]]),
            json!([[1]]),
            json!([[1, 2, 3, 4]]),
            json!([[1, "a"]]),
            json!([[1.5, 2]]),
            json!([["x", "y"]]),
            json!(["s", [1, 2]]),
            json!([{}, [1, 2]]),
            json!([[[1, 2]], [[3]]]),
            json!([[[1, "a"]]]),
        ] {
            let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
            let collected = v.evaluate(&instance, &mut c).unwrap();
            assert_eq!(v.is_valid(&instance), collected, "{schema} on {instance}");
        }
    }
}

#[test]
fn fused_not_required_and_absent_pattern_conditions_match_the_general_path() {
    let extensions = json!({ "patternProperties": { "^x-": true } });
    for schema in [
        // `not: {required}` alongside unevaluatedProperties (the OpenAPI example object).
        json!({
            "type": "object",
            "properties": { "value": true, "externalValue": { "type": "string" }, "summary": { "type": "string" } },
            "not": { "required": ["value", "externalValue"] },
            "$ref": "#/$defs/ext",
            "unevaluatedProperties": false,
            "$defs": { "ext": extensions }
        }),
        // An `if` deciding on no name matching a pattern (the OpenAPI responses object).
        json!({
            "type": "object",
            "properties": { "default": { "type": "integer" } },
            "patternProperties": { "^[1-5](?:[0-9]{2}|XX)$": { "type": "integer" } },
            "$ref": "#/$defs/ext",
            "unevaluatedProperties": false,
            "if": { "patternProperties": { "^[1-5](?:[0-9]{2}|XX)$": false } },
            "then": { "required": ["default"] },
            "$defs": { "ext": extensions }
        }),
        // Both, gated by another condition, with a name the pattern also matches.
        json!({
            "type": "object",
            "properties": { "kind": true, "a": true, "b": true, "x-a": true },
            "allOf": [{ "$ref": "#/$defs/ext" }, { "properties": { "c": true } }],
            "if": { "properties": { "kind": { "const": "k" } }, "required": ["kind"] },
            "then": {
                "not": { "required": ["a", "b"] },
                "if": { "patternProperties": { "^x-": false } },
                "then": { "required": ["c"] },
                "else": { "properties": { "d": true } }
            },
            "unevaluatedProperties": false,
            "$defs": { "ext": extensions }
        }),
    ] {
        let v = compile(&schema).unwrap();
        for instance in [
            json!({}),
            json!({ "value": 1 }),
            json!({ "value": 1, "externalValue": "u" }),
            json!({ "externalValue": 2 }),
            json!({ "x-y": 1, "summary": "s" }),
            json!({ "other": 1 }),
            json!({ "default": 1 }),
            json!({ "200": 1 }),
            json!({ "2XX": 1, "x-a": 1 }),
            json!({ "600": 1 }),
            json!({ "default": "a", "404": 1 }),
            json!({ "kind": "k" }),
            json!({ "kind": "k", "c": 1 }),
            json!({ "kind": "k", "a": 1, "b": 1, "c": 1 }),
            json!({ "kind": "k", "a": 1, "c": 1 }),
            json!({ "kind": "k", "x-a": 1, "d": 1 }),
            json!({ "kind": "k", "x-z": 1, "d": 1 }),
            json!({ "kind": "k", "d": 1, "c": 1 }),
            json!({ "kind": "j", "a": 1, "b": 1 }),
        ] {
            let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
            let collected = v.evaluate(&instance, &mut c).unwrap();
            assert_eq!(v.is_valid(&instance), collected, "{schema} on {instance}");
        }
    }
}

#[test]
fn flat_fused_objects_match_the_general_path() {
    let shared =
        json!({ "properties": { "a": { "type": "string" }, "b": true }, "required": ["a"], "maxProperties": 3 });
    for schema in [
        json!({ "allOf": [{ "$ref": "#/$defs/s" }], "properties": { "c": { "type": "integer" } }, "$defs": { "s": shared } }),
        json!({
            "allOf": [{ "$ref": "#/$defs/s" }, { "properties": { "a": { "type": "string" } }, "required": ["c"] }],
            "properties": { "c": { "type": "integer" } },
            "minProperties": 2,
            "$defs": { "s": shared }
        }),
        // The same name with different schemas stays a fused plan.
        json!({ "allOf": [{ "$ref": "#/$defs/s" }], "properties": { "a": { "minLength": 2 } }, "$defs": { "s": shared } }),
    ] {
        let v = compile(&schema).unwrap();
        for instance in [
            json!({}),
            json!({ "a": "x" }),
            json!({ "a": "xy", "c": 1 }),
            json!({ "a": 1, "c": 1 }),
            json!({ "a": "x", "c": "1" }),
            json!({ "a": "x", "b": null, "c": 1 }),
            json!({ "a": "x", "b": 1, "c": 1, "d": 1 }),
            json!({ "c": 1 }),
            json!([]),
        ] {
            let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
            let collected = v.evaluate(&instance, &mut c).unwrap();
            assert_eq!(v.is_valid(&instance), collected, "{schema} on {instance}");
        }
    }
}

#[test]
fn fused_objects_below_a_dynamic_reference_match_the_general_path() {
    // The items' $dynamicRef resolves (through the scope) to "strict", whose allOf contributor is in its own resource.
    let schema = json!({
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/root",
        "$ref": "strict",
        "$defs": {
            "strict": {
                "$id": "https://example.com/strict",
                "$dynamicAnchor": "node",
                "type": "object",
                "properties": { "data": true, "y": true, "children": { "type": "array", "items": { "$ref": "tree#/$defs/kids" } } },
                "allOf": [{ "$ref": "#/$defs/extra" }],
                "unevaluatedProperties": false,
                "$defs": { "extra": { "properties": { "x": { "type": "integer" } } } }
            },
            "tree": {
                "$id": "https://example.com/tree",
                "$dynamicAnchor": "node",
                "type": "object",
                "$defs": { "kids": { "$dynamicRef": "#node" } }
            }
        }
    });
    let v = compile(&schema).unwrap();
    for instance in [
        json!({}),
        json!({ "data": 1, "x": 2 }),
        json!({ "x": "a" }),
        json!({ "y": 1, "children": [{ "y": 1 }] }),
        json!({ "children": [{ "z": 1 }] }),
        json!({ "children": [{ "x": 1, "children": [{ "y": 2, "data": 3 }] }] }),
        json!({ "children": [{ "children": [{ "x": "no" }] }] }),
        json!({ "z": 1 }),
    ] {
        let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
        let collected = v.evaluate(&instance, &mut c).unwrap();
        assert_eq!(v.is_valid(&instance), collected, "{instance}");
    }
    assert!(v.is_valid(&json!({ "children": [{ "x": 1, "children": [{ "y": 2 }] }] })));
    assert!(!v.is_valid(&json!({ "children": [{ "z": 1 }] })));
}

/// Small objects are decided by looking the few declared and required names up in them; large ones by visiting their
/// properties. Both agree, for `serde_json::Value` and `JsonDocument` instances, top-level and nested.
#[test]
fn few_names_are_looked_up_in_small_and_large_objects() {
    let schema = json!({
        "properties": { "a": { "type": "string" }, "n": { "properties": { "x": { "type": "integer" } } } },
        "required": ["b"]
    });
    let v = compile(&schema).unwrap();
    let padding = |count: usize| (0..count).map(|i| format!(r#","p{i}": {i}"#)).collect::<String>();
    for pad in [0, 3, 40] {
        let p = padding(pad);
        for (text, expected) in [
            (format!(r#"{{"b": 1, "a": "x"{p}}}"#), true),
            (format!(r#"{{"a": "x"{p}}}"#), false),
            (format!(r#"{{"a": 1, "b": 1{p}}}"#), false),
            (format!(r#"{{"b": null{p}, "n": {{"x": 2{p}}}}}"#), true),
            (format!(r#"{{"b": null{p}, "n": {{"x": "2"{p}}}}}"#), false),
        ] {
            let value: Value = serde_json::from_str(&text).unwrap();
            assert_eq!(v.is_valid(&value), expected, "{text}");
            let document = corvus_json_schema::JsonDocument::parse(&text).unwrap();
            assert_eq!(v.validate_instance(document.root()).unwrap(), expected, "{text} (document)");
        }
    }
}

/// JSON text is validated in place; invalid JSON and runaway recursion are told apart.
#[test]
fn validates_json_text() {
    let v = compile(&json!({ "type": "array", "items": { "type": "integer" } })).unwrap();
    assert_eq!(v.validate_json("[1, 2, 3]"), Ok(true));
    assert_eq!(v.validate_json("[1, \"2\"]"), Ok(false));
    let Err(corvus_json_schema::JsonValidationError::InvalidJson(e)) = v.validate_json("[1, 2") else {
        panic!("expected invalid JSON")
    };
    assert_eq!(e.offset(), 5);
    // A small depth, as in in_place_recursion_beyond_max_depth_is_an_error: a debug build's frames at the default
    // depth overflow a test thread's stack.
    let options = CompileOptions { max_depth: 16, ..CompileOptions::default() };
    let looping =
        compile_with(&json!({ "$defs": { "a": { "$ref": "#/$defs/a" } }, "$ref": "#/$defs/a" }), &options).unwrap();
    assert!(matches!(looping.validate_json("1"), Err(corvus_json_schema::JsonValidationError::DepthExceeded(_))));
    let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Detailed);
    assert_eq!(v.evaluate_json("[\"x\"]", &mut c), Ok(false));
    assert!(c.results().iter().any(|r| !r.is_match && r.document_evaluation_location == "/0"));
}

/// A format callback that validates JSON text itself, during a validation of JSON text on the same thread, gets
/// buffers of its own.
#[test]
fn validating_json_from_a_format_callback_works() {
    let inner = compile(&json!({ "type": "object", "required": ["a"] })).unwrap();
    let mut options = CompileOptions { assert_format: Some(true), ..CompileOptions::default() };
    options.formats.insert("embedded-json".into(), Arc::new(move |s: &str| inner.validate_json(s).unwrap_or(false)));
    let v = compile_with(&json!({ "type": "array", "items": { "format": "embedded-json" } }), &options).unwrap();
    assert_eq!(v.validate_json(r#"["{\"a\": 1}", "{\"a\": 2}"]"#), Ok(true));
    assert_eq!(v.validate_json(r#"["{\"a\": 1}", "{\"b\": 2}"]"#), Ok(false));
    assert_eq!(v.validate_json(r#"["{\"a\": 1}", "not json"]"#), Ok(false));
}

/// A number in a schema and the same number in an instance are the same double, whichever parser read each: the
/// schema through serde_json (here from serde_json's own output, as a caller passing schema text does), the instance
/// through JsonDocument. Without serde_json's float_roundtrip feature, serde_json read `9.727837981879871e+26`, its own
/// output, one unit in the last place off, so an `exclusiveMaximum` equal to the instance passed.
#[test]
fn schema_and_instance_numbers_parse_alike() {
    for (keyword, literal) in [
        ("exclusiveMaximum", "972783798187987123879878123.18878137"),
        ("exclusiveMinimum", "-972783798187987123879878123.18878137"),
    ] {
        let number: f64 = serde_json::from_str(literal).unwrap();
        let text = serde_json::to_string(&number).unwrap();
        let schema: Value = serde_json::from_str(&format!(r#"{{"{keyword}": {text}}}"#)).unwrap();
        let v = compile(&schema).unwrap();
        assert_eq!(v.validate_json(&text), Ok(false), "{keyword} {text} (JSON text)");
        let instance: Value = serde_json::from_str(&text).unwrap();
        assert!(!v.is_valid(&instance), "{keyword} {text} (Value)");
    }
}
