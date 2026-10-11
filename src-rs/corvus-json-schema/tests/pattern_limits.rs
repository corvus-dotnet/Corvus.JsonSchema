//! The limits on a regular expression (the tests of the Go module's `pattern_match_limit_test.go`,
//! `pattern_size_test.go` and `pattern_bounds_test.go`).
//!
//! A schema and an instance may both come from outside, so neither may be able to make a validator hang or hold
//! memory out of proportion to its input. A match of a `pattern` or `patternProperties` regular expression has a
//! limit of time (one second unless the options say otherwise) and, with it, of the memory of its backtracking. A
//! match that reaches it is an error, never "valid" and never "not valid". A pattern has limits of size, and one
//! over them is refused when the schema is compiled, with an error that is not the one of a pattern that is not
//! valid.
//!
//! No test here depends on a quiet machine: the matches that are to reach a limit would run for longer than anyone
//! waits, and the upper bounds on time are generous.

use std::time::{Duration, Instant};

use corvus_json_schema::{
    CompileOptions, DEFAULT_PATTERN_MATCH_LIMIT, JsonSchemaResultsCollector, JsonValidationError, ResultsLevel,
    SchemaCompilationErrorKind, SchemaEvaluationDepthError, SchemaEvaluationError, Validator, compile, compile_with,
};
use serde_json::{Value, json};

/// A time no test should come near.
const GENEROUS: Duration = Duration::from_secs(20);

/// A pattern whose match on [`never_answered`] takes longer than any test waits: a group read back inside a
/// repetition, which nothing but the limit ends.
const NEVER_ANSWERS: &str = r"^(?:(a+)\1*)+$";

fn never_answered() -> String {
    "a".repeat(60) + "b"
}

fn with_limit(limit: Option<Duration>) -> CompileOptions {
    CompileOptions { pattern_match_limit: limit, ..CompileOptions::default() }
}

/// Every place a schema can hold a pattern, each with an instance that gives the pattern the text.
fn limit_schemas(pattern: &str, text: &str) -> Vec<(&'static str, Value, Value)> {
    vec![
        ("pattern", json!({ "pattern": pattern }), json!(text)),
        (
            "pattern in a property",
            json!({ "type": "object", "properties": { "s": { "type": "string", "pattern": pattern } } }),
            json!({ "s": text }),
        ),
        ("pattern in items", json!({ "items": { "pattern": pattern } }), json!([text])),
        ("pattern under not", json!({ "not": { "pattern": pattern } }), json!(text)),
        ("pattern under anyOf", json!({ "anyOf": [{ "pattern": pattern }, { "type": "string" }] }), json!(text)),
        ("patternProperties", json!({ "patternProperties": { pattern: { "type": "integer" } } }), json!({ text: 1 })),
        (
            "patternProperties with properties",
            json!({ "properties": { "id": true }, "patternProperties": { pattern: true }, "additionalProperties": false }),
            json!({ "id": 1, text: 1 }),
        ),
        (
            "patternProperties and unevaluatedProperties",
            json!({ "patternProperties": { pattern: true }, "unevaluatedProperties": false }),
            json!({ text: 1 }),
        ),
        (
            "patternProperties under a condition",
            json!({ "if": { "patternProperties": { pattern: { "const": 1 } } }, "then": { "minProperties": 1 } }),
            json!({ text: 1 }),
        ),
        ("propertyNames", json!({ "propertyNames": { "pattern": pattern } }), json!({ text: 1 })),
    ]
}

/// Checks the error of a match that reached its limit of time: it names the pattern and the limit, and it is no
/// other error.
fn expect_match_limit(what: &str, result: Result<bool, SchemaEvaluationError>, limit: Duration) {
    match result {
        Err(SchemaEvaluationError::PatternMatchLimit(e)) => {
            assert_eq!((e.pattern(), e.limit(), e.is_memory()), (NEVER_ANSWERS, limit, false), "{what}");
            let message = e.to_string();
            assert!(message.contains(&format!("{limit:?}")) && message.contains(NEVER_ANSWERS), "{what}: {message}");
        }
        other => panic!("{what}: {other:?}, want the error of a match that reached its limit"),
    }
}

#[test]
fn a_match_that_reaches_its_limit_is_an_error() {
    let limit = Duration::from_millis(50);
    let text = never_answered();
    for (name, schema, instance) in limit_schemas(NEVER_ANSWERS, &text) {
        let v = compile_with(&schema, &with_limit(Some(limit))).unwrap_or_else(|e| panic!("{name}: {e}"));
        let json = instance.to_string();
        let timed = |what: &str, run: &dyn Fn() -> Result<bool, SchemaEvaluationError>| {
            let start = Instant::now();
            let result = run();
            let took = start.elapsed();
            expect_match_limit(&format!("{name}, {what}"), result, limit);
            assert!(took >= limit && took < limit + GENEROUS, "{name}, {what}: abandoned after {took:?}");
        };
        timed("try_validate", &|| v.try_validate(&instance));
        timed("try_validate_instance", &|| v.try_validate_instance(&instance));
        timed("try_validate_json", &|| v.try_validate_json(&json));
        for level in [ResultsLevel::Basic, ResultsLevel::Detailed, ResultsLevel::Verbose] {
            timed("try_evaluate", &|| v.try_evaluate(&instance, &mut JsonSchemaResultsCollector::new(level)));
            timed("try_evaluate_json", &|| v.try_evaluate_json(&json, &mut JsonSchemaResultsCollector::new(level)));
        }
        // The functions with no way to say which limit it was: false from the one that returns a bool, and the one
        // error their type has from the others. Never an answer.
        assert!(!v.is_valid(&instance), "{name}: is_valid is true for a match that reached its limit");
        assert_eq!(v.validate(&instance), Err(SchemaEvaluationDepthError), "{name}");
        assert_eq!(v.validate_instance(&instance), Err(SchemaEvaluationDepthError), "{name}");
        assert_eq!(
            v.validate_json(&json),
            Err(JsonValidationError::DepthExceeded(SchemaEvaluationDepthError)),
            "{name}"
        );
        let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
        assert_eq!(v.evaluate(&instance, &mut c), Err(SchemaEvaluationDepthError), "{name}");
        assert_eq!(v.evaluate_json(&json, &mut c), Err(JsonValidationError::DepthExceeded(SchemaEvaluationDepthError)));
    }
}

/// The functions whose error type was there before the limit of a match have one error for an evaluation that was
/// abandoned. Its message said the schema recursed beyond the maximum depth, which is not so when a match reached
/// its limit. It says the validation was abandoned at a limit, names both, and points to the `try_` functions.
/// The `try_` functions keep the message of each.
#[test]
fn the_error_of_the_functions_that_were_there_does_not_say_depth_alone() {
    let text = never_answered();
    let (_, schema, instance) = limit_schemas(NEVER_ANSWERS, &text).remove(0);
    let v = compile_with(&schema, &with_limit(Some(Duration::from_millis(50)))).unwrap();
    let json = instance.to_string();
    let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
    let messages = [
        v.validate(&instance).unwrap_err().to_string(),
        v.validate_instance(&instance).unwrap_err().to_string(),
        v.validate_json(&json).unwrap_err().to_string(),
        v.evaluate(&instance, &mut c).unwrap_err().to_string(),
        v.evaluate_instance(&instance, &mut c).unwrap_err().to_string(),
        v.evaluate_json(&json, &mut c).unwrap_err().to_string(),
    ];
    for message in &messages {
        assert_eq!(
            message,
            "The validation was abandoned at a limit: the schema recursed in place beyond the maximum depth, or a \
             match of a regular expression reached its limit. The try_ functions say which."
        );
    }
    // The functions that say which keep a message for each.
    let limit = v.try_validate(&instance).unwrap_err().to_string();
    assert!(limit.starts_with("A match of the regular expression ") && !limit.contains("depth"), "{limit}");
    let looping =
        compile(&json!({"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "$ref": "#/$defs/loop"})).unwrap();
    let depth = looping.try_validate(&json!(1)).unwrap_err();
    assert!(matches!(depth, SchemaEvaluationError::DepthExceeded(_)));
    assert_eq!(depth.to_string(), "The schema recursed in place beyond the maximum depth.");
    assert_eq!(looping.validate(&json!(1)).unwrap_err().to_string(), messages[0]);
}

#[test]
fn the_default_match_limit_is_one_second() {
    assert_eq!(DEFAULT_PATTERN_MATCH_LIMIT, Duration::from_secs(1));
    assert_eq!(CompileOptions::default().pattern_match_limit, Some(Duration::from_secs(1)));
    let v = compile(&json!({ "pattern": NEVER_ANSWERS })).unwrap();
    let start = Instant::now();
    let result = v.try_validate(&json!(never_answered()));
    let took = start.elapsed();
    expect_match_limit("the default limit", result, Duration::from_secs(1));
    assert!(took >= Duration::from_secs(1) && took < Duration::from_secs(1) + GENEROUS, "abandoned after {took:?}");
}

/// The limit is on each match, not on the validation: forty strings that each take a part of it are answered.
#[test]
fn the_match_limit_is_for_each_match() {
    let items: Vec<String> = (0..40).map(|_| "a".repeat(12) + "b").collect();
    let v = compile_with(&json!({ "items": { "not": { "pattern": NEVER_ANSWERS } } }), &with_limit(Some(GENEROUS)))
        .unwrap();
    assert_eq!(v.try_validate(&json!(items)), Ok(true));
}

#[test]
fn the_match_limit_can_be_turned_off() {
    let schema = json!({ "pattern": NEVER_ANSWERS });
    let instance = json!("a".repeat(12) + "b");
    let micro = Duration::from_micros(1);
    let result = compile_with(&schema, &with_limit(Some(micro))).unwrap().try_validate(&instance);
    expect_match_limit("a limit of a microsecond", result, micro);
    for off in [None, Some(Duration::ZERO)] {
        let v = compile_with(&schema, &with_limit(off)).unwrap();
        assert_eq!(v.try_validate(&instance), Ok(false), "with the limit {off:?}");
        assert_eq!(v.try_validate(&json!("aaaa")), Ok(true), "with the limit {off:?}, a string that matches");
        assert!(!v.is_valid(&instance) && v.is_valid(&json!("aaaa")));
    }
}

#[test]
fn a_match_that_reaches_its_memory_limit_is_an_error() {
    let hour = Duration::from_secs(3600);
    let v = compile_with(&json!({ "pattern": "(?:a?){2147483647}" }), &with_limit(Some(hour))).unwrap();
    let start = Instant::now();
    let result = v.try_validate(&json!(""));
    let took = start.elapsed();
    match result {
        Err(SchemaEvaluationError::PatternMatchLimit(e)) => {
            assert!(e.is_memory() && e.limit() == hour && e.pattern() == "(?:a?){2147483647}");
            assert!(e.to_string().contains("1,048,576 backtracking entries"), "{e}");
        }
        other => panic!("{other:?}, want the error of a match that reached its limit of memory"),
    }
    assert!(took < GENEROUS, "abandoned after {took:?}");
    // With no limit of time there is none of memory either, and the match is the engine's to finish: this one
    // gives up at once, because the text is shorter than the least the pattern consumes.
    let unlimited = compile_with(&json!({ "pattern": "(?:a){2147483647}" }), &with_limit(None)).unwrap();
    assert_eq!(unlimited.try_validate(&json!("aaa")), Ok(false));
}

/// Compiling matches each pattern of patternProperties against the property names the schema declares, and those
/// matches have the limit too.
#[test]
fn a_match_while_compiling_has_the_limit() {
    let limit = Duration::from_millis(50);
    let name = never_answered();
    for schema in [
        json!({ "properties": { &name: true }, "patternProperties": { NEVER_ANSWERS: { "type": "integer" } } }),
        json!({ "properties": { &name: true }, "patternProperties": { NEVER_ANSWERS: true }, "additionalProperties": false }),
        json!({
            "properties": { &name: true },
            "allOf": [{ "patternProperties": { NEVER_ANSWERS: true } }],
            "unevaluatedProperties": false
        }),
    ] {
        let start = Instant::now();
        let compiled = compile_with(&schema, &with_limit(Some(limit)));
        let took = start.elapsed();
        match compiled {
            Ok(v) => expect_match_limit(&format!("validating {schema}"), v.try_validate(&json!({ &name: 1 })), limit),
            Err(e) => {
                let SchemaCompilationErrorKind::PatternMatchLimit(limited) = e.kind() else {
                    panic!("compiling {schema}: {e}, want the error of a match that reached its limit");
                };
                expect_match_limit(
                    &format!("compiling {schema}"),
                    Err(SchemaEvaluationError::PatternMatchLimit(limited.clone())),
                    limit,
                );
                assert_eq!(e.message(), limited.to_string());
                assert!(took < limit * 20 + GENEROUS, "compiling was abandoned after {took:?}");
            }
        }
    }
}

#[test]
fn an_ordinary_pattern_is_not_affected_by_the_limit() {
    let long = "ab".repeat(20000) + "\u{e9}";
    for (pattern, text, want) in [
        (r"^[a-z]+$", "abc", true),
        (r"^[a-z]+$", "abc1", false),
        (r"^x-", "x-a", true),
        (r"^\p{L}+$", "caf\u{e9}", true),
        (r"^\p{L}+$", "caf\u{e9} ", false),
        (r"^(?=.*\d)\w+$", "abc1", true),
        (r"^(?=.*\d)\w+$", "abcd", false),
        (r"^(a+)\1$", "aaaa", true),
        (r"^(a+)\1$", "aaa", false),
        (r"(?<=\$)\d+", "cost $42", true),
        (r"^(?:[a-z]+\.)+[a-z]+$", "a.b.c", true),
        ("^[ab\u{e9}]*c$", &long, false),
    ] {
        for limit in [Some(DEFAULT_PATTERN_MATCH_LIMIT), Some(Duration::from_secs(10)), None] {
            let v = compile_with(&json!({ "pattern": pattern }), &with_limit(limit)).unwrap();
            assert_eq!(v.try_validate(&json!(text)), Ok(want), "{pattern} with the limit {limit:?}");
            let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
            assert_eq!(
                v.try_evaluate(&json!(text), &mut c),
                Ok(want),
                "{pattern} with the limit {limit:?}, collecting"
            );
            assert_eq!(v.is_valid(&json!(text)), want, "{pattern} with the limit {limit:?}");
        }
    }
}

/// The matchers that are not the regular expression engine have no limit, so each must read a text a bounded number
/// of times. Each of these answers, or ends at the limit of the engine.
#[test]
fn no_matcher_reads_the_text_from_every_position() {
    let items = "ab,".repeat(33333);
    let limit = Duration::from_millis(200);
    for (pattern, text) in [
        (r"[a-z]{50000}[0-9]|x", "a".repeat(100000)),
        (r"^(?:[a-z]+,)*[a-z,]+[0-9]$", items.clone()),
        (r"^(?:[a-z]+,)*[a-z]{2}[0-9]$", items.clone()),
        ("^(?:[a-z\u{e9}]+,)*[a-z]{2}[0-9]$", format!("\u{e9},{items}")),
    ] {
        let v = compile_with(&json!({ "pattern": pattern }), &with_limit(Some(limit))).unwrap();
        let start = Instant::now();
        let result = v.try_validate(&json!(text));
        let took = start.elapsed();
        assert!(
            matches!(result, Ok(false) | Err(SchemaEvaluationError::PatternMatchLimit(_))) && took < limit + GENEROUS,
            "{pattern}: {result:?} after {took:?}"
        );
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// The limits of size.

fn nest(depth: usize) -> String {
    "(?:".repeat(depth) + "a" + &")".repeat(depth)
}

fn pattern_schema(keyword: &str, pattern: &str) -> Value {
    if keyword == "pattern" { json!({ "pattern": pattern }) } else { json!({ "patternProperties": { pattern: true } }) }
}

#[test]
fn a_pattern_over_a_limit_of_size_is_refused() {
    let escape = |zeros: usize| format!("\\u{{{}41}}", "0".repeat(zeros));
    let class = |members: usize| format!("[{}]", r"ĀĂ".repeat(members));
    for (name, at, over, limit, value, reason) in [
        (
            "length",
            escape((1 << 20) - 6),
            escape((1 << 20) - 5),
            "length",
            1 << 20,
            "the pattern is longer than 1048576 bytes",
        ),
        ("nesting", nest(999), nest(1000), "nesting", 1000, "the groups of the pattern nest more than 1000 deep"),
        (
            "size",
            "a".repeat(16384),
            "a".repeat(16385),
            "size",
            1 << 20,
            "the pattern has a size of more than 1048576 bytes when it is read",
        ),
        (
            "size of a class",
            class(60000),
            class(70000),
            "size",
            1 << 20,
            "the pattern has a size of more than 1048576 bytes when it is read",
        ),
        (
            "nesting, and not valid",
            nest(999),
            nest(1000) + "*+)",
            "nesting",
            1000,
            "the groups of the pattern nest more than 1000 deep",
        ),
    ] {
        for keyword in ["pattern", "patternProperties"] {
            let start = Instant::now();
            if let Err(e) = compile(&pattern_schema(keyword, &at)) {
                panic!("{name} in {keyword}: the pattern at the limit: {e}");
            }
            let e = compile(&pattern_schema(keyword, &over)).expect_err("the pattern over the limit compiles");
            assert!(start.elapsed() < GENEROUS, "{name} in {keyword}: compiling took {:?}", start.elapsed());
            let SchemaCompilationErrorKind::PatternTooLarge(large) = e.kind() else {
                panic!("{name} in {keyword}: {:.200}, want the error of a pattern that is too large", e.message());
            };
            assert_eq!((large.keyword(), large.limit(), large.value()), (keyword, limit, value), "{name} in {keyword}");
            assert_eq!(large.pattern(), over, "{name} in {keyword}");
            let want = format!(
                "' ({} bytes) in {keyword} is too large to read as an ECMA-262 regular expression: {reason}.",
                over.len()
            );
            let message = e.message();
            assert!(
                message.starts_with("The regular expression '") && message.ends_with(&want) && message.len() <= 400,
                "{name} in {keyword}: the message is {message:.300}"
            );
            assert_eq!(message, large.to_string());
            assert!(!message.to_lowercase().contains("invalid"), "{name} in {keyword}: {message:.300}");
        }
    }
}

#[test]
fn a_pattern_that_is_not_valid_is_not_too_large() {
    for pattern in [
        "(".to_string(),
        "a**".into(),
        "[b-a]".into(),
        r"(?<n>a)\k<m>".into(),
        "(?:".repeat(999) + "a" + &")".repeat(1000),
    ] {
        let e = compile(&json!({ "pattern": pattern })).expect_err("a pattern that is not valid compiles");
        assert_eq!(e.kind(), &SchemaCompilationErrorKind::InvalidPattern, "{pattern:.20}");
        assert!(e.message().starts_with("Invalid regular expression '"), "{:.100}", e.message());
    }
    // An error that is not about a pattern is neither.
    let e = compile(&json!({ "$ref": "#/nowhere" })).expect_err("a reference to nothing compiles");
    assert_eq!(e.kind(), &SchemaCompilationErrorKind::Other);
}

#[test]
fn the_regex_format_refuses_a_pattern_over_a_limit() {
    let options = CompileOptions { assert_format: Some(true), ..CompileOptions::default() };
    let v: Validator = compile_with(&json!({ "format": "regex" }), &options).unwrap();
    for (text, want) in [
        ("^[a-z]+$".to_string(), true),
        ("(".to_string(), false),
        ("a".repeat(16384), true),
        ("a".repeat(16385), false),
        (nest(999), true),
        (nest(1000), false),
    ] {
        let start = Instant::now();
        assert_eq!(v.try_validate(&json!(text)), Ok(want), "a string of {} bytes", text.len());
        assert!(start.elapsed() < GENEROUS, "a string of {} bytes took {:?}", text.len(), start.elapsed());
    }
}
