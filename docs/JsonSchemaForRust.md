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

The crate matches the common shapes itself, gives what the [`regex`](https://crates.io/crates/regex) crate can run to
it, and the rest (lookarounds, backreferences, modifier groups, patterns without the flag) to
[`regress`](https://crates.io/crates/regress), a backtracking ECMA-262 engine. Where `regress` answers a pattern
differently from ECMA-262 it is given another pattern with the same meaning.

- **Exact.** The answers are V8's for every pattern and text of the
  [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data) that a Rust
  string can hold. That covers lookaheads and lookbehinds, with none refused (`regress` matches a lookbehind
  backwards, as ECMA-262 does), backreferences into and out of repetitions and lookarounds, modifier groups, and
  case-insensitive matching in both grammars, a case-insensitive backreference without the `u` flag among it.
- **Refused.** One kind of valid pattern does not compile, and the error says that it is valid and too large: a
  repetition inside a repetition whose body can match the empty string, with a minimum too large to write out as
  copies, or with a count too large to write out and a group that a backreference outside the repetition reads.
  `^(?!-)(?:(?:(a)|){1,300}\1-)+$` is one. The same pattern with a count of 30 compiles.
- **Not proved exact.** A repetition inside a repetition with a count too large to write out as copies, such as
  `(?:a|x){1,300}`, keeps its count, and `regress` can lose it and run the repetition more times than its maximum.
  No pattern of the test data shows a wrong answer for it.
- **Where V8 departs from ECMA-262,** the crate follows ECMA-262. With the `u` flag V8 starts a match between the two
  halves of a surrogate pair (`/\B/u` matches inside the one character of `"a\u{10428}a"`), compares a
  case-insensitive backreference to a group of several characters by full case folding, and answers some
  case-insensitive modifier groups differently from the same pattern with the `i` flag.
- **Unicode versions.** Case-insensitive matching with the `u` flag uses the tables of `regress` (Unicode 17 in 0.12),
  and without the flag the uppercase forms of the Rust standard library. They agree with V8 13.6 on every character
  of Unicode 16.
- **Lone surrogates.** A JSON string with the escape of half of a surrogate pair is not a Rust string.
  `validate_json` and `serde_json` both refuse such a document as not valid JSON.

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
