# JSON Schema for Rust

[corvus-json-schema](https://crates.io/crates/corvus-json-schema) is the Corvus JSON Schema evaluator for Rust: draft
4, 6, 7, 2019-09 and 2020-12. It is a port of the .NET runtime evaluator (see [Runtime Evaluator](RuntimeEvaluator.md)),
and gives the same results and annotations. The Python, Ruby, PHP and R packages, and the C library, are built on it.

A schema is compiled once into a node graph with its keywords pre-digested: references resolved, patterns compiled,
numeric bounds exact. Any number of instances can then be validated against it.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, and all of its annotation tests.
- **Fast.** Over the [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) corpora,
  parsing and validating JSON text takes 0.37 of the time of [Blaze](https://github.com/sourcemeta/blaze), the C++
  validator, and it is faster on all 37 corpora.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations, the same as every other Corvus
  implementation.
- **Allocation-free validation of JSON text.** In the steady state, `Validator::validate_json` allocates nothing.

## Install

```sh
cargo add corvus-json-schema serde_json
```

## Validate

```rust
use serde_json::json;

let validator = corvus_json_schema::compile(&json!({
    "type": "object",
    "properties": { "id": { "type": "integer", "minimum": 1 } },
    "required": ["id"]
}))
.unwrap();
assert!(validator.is_valid(&json!({ "id": 3 })));
assert!(!validator.is_valid(&json!({ "id": 0 })));
```

## JSON text

`Validator::validate_json` parses JSON text into per-thread buffers and validates it in place, so in the steady state
it allocates nothing. To validate the same text more than once, parse it into a `JsonDocument`: one flat array of
values, with strings borrowed from the text where they have no escapes. Parsing that way takes about a quarter of the
time of parsing into a `serde_json::Value`.

```rust
let validator = corvus_json_schema::compile(&serde_json::json!({ "type": "array", "items": { "type": "integer" } }))
    .unwrap();
assert!(validator.validate_json("[1, 2, 3]").unwrap());

let document = corvus_json_schema::JsonDocument::parse("[1, 2, 3]").unwrap();
assert!(validator.validate_instance(document.root()).unwrap());
```

## Options

`compile_with(&schema, &CompileOptions { .. })` takes:

| Field | Meaning |
|---|---|
| `default_dialect` | The dialect of a schema without `$schema` (default `Dialect::Draft202012`). |
| `assert_format` | `Some(true)` asserts `format`, `Some(false)` never does; `None` follows the schema's vocabularies. |
| `assert_format_in_legacy_drafts` | With `assert_format` unset, assert `format` in drafts 4 to 7 too. |
| `assert_content` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `true`). |
| `formats` | Custom formats by name: functions from the string to whether it is valid. |
| `resolve_document` | A function from an absolute URI to the document, for remote references. The standard metaschemas are built in. |
| `base_uri` | The base URI of the root document. |
| `entry_point` | A subschema to validate against, such as `#/$defs/item`. |
| `max_depth` | The deepest the evaluator recurses in place (default 128). |
| `pattern_match_limit` | The time one match of a regular expression may take (default `Some(Duration::from_secs(1))`). `None` or zero turns the limit off. See [Regular expressions](#regular-expressions). |

## Results and annotations

```rust
use corvus_json_schema::{collect_annotations, JsonSchemaResultsCollector, ResultsLevel};

let mut collector = JsonSchemaResultsCollector::new(ResultsLevel::Detailed);
validator.evaluate(&serde_json::json!({ "id": 0 }), &mut collector).unwrap();
for r in collector.results() {
    // r.is_match, r.message, r.evaluation_location, r.schema_evaluation_location, r.document_evaluation_location
}

let mut verbose = JsonSchemaResultsCollector::new(ResultsLevel::Verbose);
validator.evaluate(&serde_json::json!({ "id": 3 }), &mut verbose).unwrap();
let annotations = collect_annotations(&verbose); // instance location -> keyword -> schema location -> value
```

`Basic` records the failures without messages, `Detailed` adds the messages, and `Verbose` records every keyword,
passing ones and annotations included.

## Other instance types

The evaluator reads instances through the `Instance` trait: a value shown as one of the six JSON kinds, with arrays and
objects read in place. `&serde_json::Value` and `JsonDocument` implement it, and so can your own types:
`Validator::validate_instance` and `Validator::evaluate_instance` take any implementation, so values need not be
converted first. The Python, Ruby, PHP and R packages read their languages' values this way.

## Regular expressions

`pattern`, `patternProperties` and the `regex` format follow ECMA-262, as JSON Schema specifies. A pattern is read
with the `u` flag. A pattern that is not valid with it is read as a pattern with no flag, which accepts what many
schemas hold (`\&`, `\-`, a lone brace), and is then matched as ECMA-262 says for such a pattern: by UTF-16 code
unit, so a character beyond the Basic Multilingual Plane is two, and `^\-?.$` does not match one. The `regex`
format accepts only the grammar of the flag.

The crate matches the common shapes itself and gives what the [`regex`](https://crates.io/crates/regex) crate can run
to it. The rest (lookarounds, backreferences, `\B`, modifier groups) is matched by the crate's own ECMA-262 engine, a
port of the engine of the Go module. No third-party backtracking engine is involved.

- **Exact.** A pattern is matched as it is written, and none is refused for what an engine cannot do. The answers
  are V8's for every pattern and text of the
  [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data) that a Rust
  string can hold: 98,839 patterns and 4,268,313 texts. That covers lookaheads and lookbehinds, backreferences into
  and out of repetitions and lookarounds, named groups, modifier groups, repetitions inside repetitions with any
  count (`^(?!-)(?:(?:(a)|){1,300}\1-)+$` compiles, and the backreference reads the last iteration that ran), and
  case-insensitive matching in both grammars.
- **A match has a limit of one second.** A schema and an instance may both come from outside, and a backtracking
  match can take time that doubles with each character. `CompileOptions::pattern_match_limit` bounds one match
  against one string. A match that reaches it is abandoned, and the validation has no answer. `None` or zero turns
  the limit off, for a caller who trusts both the schema and the instances.
- **A pattern has limits of size.** A pattern longer than 1,048,576 bytes, nested more than 1,000 groups deep, or
  with more than about 16,000 terms is refused when the schema is compiled, with a message that says it is too
  large and not that it is not valid.
- **Where V8 departs from ECMA-262,** the crate follows ECMA-262. With the `u` flag V8 starts a match between the two
  halves of a surrogate pair (`/\B/u` matches inside the one character of `"a\u{10428}a"`), compares a
  case-insensitive backreference to a group of several characters by full case folding, and answers some
  case-insensitive modifier groups differently from the same pattern with the `i` flag. V8 13.6 also takes three
  things that are not patterns in ECMA-262, and the crate refuses them: `\p{WSpace}`, a count whose first number is
  the greater when the numbers are large (`a{2147483648,2147483647}`), and a name of two groups that can both take
  part in one match (`(|((?<m>))(?<m>|))`). It refuses the Script value `Hrkt` (`Katakana_Or_Hiragana`), which
  ECMA-262 has, and the crate takes it: no character has it, so `\p{sc=Hrkt}` matches nothing.
- **Unicode versions.** Properties and case-insensitive matching use the crate's own tables for Unicode 17, the same
  tables as the Go module. They agree with V8 13.6 on every character of Unicode 16. The four scripts Unicode 17
  adds (`Sidetic`, `Tolong_Siki`, `Beria_Erfe`, `Tai_Yo`) are scripts here, where V8 13.6 does not know them.
- **Lone surrogates.** A JSON string with the escape of half of a surrogate pair is not a Rust string.
  `validate_json` and `serde_json` both refuse such a document as not valid JSON.

### Limits and errors

```rust
use std::time::Duration;

use corvus_json_schema::{CompileOptions, SchemaCompilationErrorKind, SchemaEvaluationError, compile, compile_with};
use serde_json::json;

// A group read back inside a repetition: on this string the match would run for longer than anyone waits.
let options = CompileOptions { pattern_match_limit: Some(Duration::from_millis(50)), ..CompileOptions::default() };
let validator = compile_with(&json!({ "pattern": r"^(?:(a+)\1*)+$" }), &options).unwrap();
let instance = json!("a".repeat(60) + "b");
match validator.try_validate(&instance) {
    Err(SchemaEvaluationError::PatternMatchLimit(e)) => {
        assert_eq!(e.pattern(), r"^(?:(a+)\1*)+$");
        assert_eq!(e.limit(), Duration::from_millis(50));
    }
    other => panic!("{other:?}"),
}
// is_valid has no way to say so, and answers false.
assert!(!validator.is_valid(&instance));

// A pattern over a limit of size is not a pattern that is not valid.
let error = compile(&json!({ "pattern": "a".repeat(20000) })).unwrap_err();
assert!(matches!(error.kind(), SchemaCompilationErrorKind::PatternTooLarge(large) if large.limit() == "size"));
let error = compile(&json!({ "pattern": "a**" })).unwrap_err();
assert_eq!(error.kind(), &SchemaCompilationErrorKind::InvalidPattern);
```

| | |
|---|---|
| The limit of a match | `CompileOptions::pattern_match_limit`, one second by default (`DEFAULT_PATTERN_MATCH_LIMIT`). It is for each match of a `pattern` or `patternProperties` regular expression against one string, not for the validation. With it, the backtracking of a match may hold 1,048,576 entries (24 MB) and its memory of failed places 8 MB. |
| Reaching it | `try_validate`, `try_validate_instance`, `try_validate_json`, `try_evaluate`, `try_evaluate_instance` and `try_evaluate_json` return `Err(SchemaEvaluationError::PatternMatchLimit(e))`. `e.pattern()`, `e.limit()` and `e.is_memory()` say what was reached. |
| The functions that were there | `is_valid` fails closed: it returns `false`, never `true`. `validate`, `validate_instance`, `evaluate` and `evaluate_instance` return `Err(SchemaEvaluationDepthError)`, and `validate_json` and `evaluate_json` return `JsonValidationError::DepthExceeded`. That error has one value, for the maximum depth and for the limit of a match, and its message says the validation was abandoned at a limit and that the `try_` functions say which. The `false` of `is_valid` at a limit does not mean the instance is not valid. |
| The limits of size | 1,048,576 bytes of pattern (`length`), 1,000 levels of groups (`nesting`), and 1,048,576 counted as the pattern is read (`size`: 64 for a term, 32 for an alternative, 8 for a range of a class). |
| Over one | `compile` returns a `SchemaCompilationError` whose `kind()` is `PatternTooLarge`, with the pattern, the keyword, the name of the limit and its value. A pattern that is not valid is `InvalidPattern`. The `regex` format answers `false`. |
| What the process keeps | The cache of compiled patterns holds 1,024 patterns and 64 MiB at most. The `regex` crate compiles no program over 2 MiB. |
| The format checks | They read strings of the instance, are not under the limit of a match, and take time proportional to the string: scanners that pass over it once, and regular expressions on the `regex` crate. `regex` reads the string as a pattern, with the limits of size. `idn-hostname` and the domain of an `idn-email` read nothing of a string over 1,012 bytes. |
| What is not bounded | The limit is for each match, so a validation takes the sum of its matches. The matchers that read a string once are not stopped, and `regex` is given a string only when it can answer within the limit. With the limit turned off nothing bounds a match. |

## Performance

Measured with jsonschema-benchmark's corpora and protocol against Blaze (geometric means over the 37 corpora, Corvus
time divided by Blaze time):

| | Corvus / Blaze | Corvus faster on |
|---|---:|---:|
| Parse and validate | 0.37 | 37 of 37 |
| Validate parsed instances | 0.54 | 34 of 37 |

## Links

- Crate: [crates.io](https://crates.io/crates/corvus-json-schema), [docs.rs](https://docs.rs/corvus-json-schema)
- Source and README: [src-rs/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rs/corvus-json-schema)
- The other languages: see [Other languages](OtherLanguages.md)
