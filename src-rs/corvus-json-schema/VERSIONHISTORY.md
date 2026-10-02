# Version History

The version history of the `corvus-json-schema` Rust crate. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.3

V0.1.3 validates JSON text without allocating, and reads a number in a schema and the same number in an instance as the same double. There are no breaking changes.

### New features

- **`Validator::validate_json` and `Validator::evaluate_json`.** They take JSON text, parse it into the `JsonDocument` parser's per-thread buffers and evaluate it in place: in the steady state a validation of JSON text allocates nothing (a test counts allocations). `JsonValidationError` tells invalid JSON (with its byte offset) from a schema that recurses in place beyond the maximum depth. A format validator that itself validates JSON text, during a validation of JSON text, gets buffers of its own. Use `JsonDocument` to validate the same text more than once.

### Bug fixes

- **Numbers in schemas and instances are read alike.** The crate turns on serde_json's `float_roundtrip` feature. Without it, serde_json read some numbers one unit in the last place off the correctly rounded double, including its own output (`9.727837981879871e+26`), while `JsonDocument` rounds correctly. A schema passed as JSON text and an instance validated as JSON text could then hold two different doubles for one literal, so an `exclusiveMaximum` equal to the instance passed. The JSON-Schema-Test-Suite's `bignum.json` found it, run through the C library.

## V0.1.2

V0.1.2 adds `JsonDocument`, a parsed form of JSON text that the evaluator reads in place, and makes validation faster on schemas where the Blaze C++ validator was faster. There are no breaking changes, and every result is unchanged.

### New features

- **`JsonDocument` parses JSON text for evaluation.** `JsonDocument::parse` reads JSON text into one flat array of values, with strings borrowed from the text where they have no escapes, and `JsonDocument::root` is an `Instance` for `Validator::validate_instance` and `Validator::evaluate_instance`. Parsing takes about a quarter of the time of parsing into a `serde_json::Value`, and a document costs one allocation (two when strings need unescaping), against one or more per string, array and object for a `Value`. Validation reads it at least as fast as a `Value`. It accepts what serde_json accepts: nesting up to 127 levels, no lone surrogates in `\u` escapes, numbers within the range of a double; of duplicate property names, the last value is kept at the position of the first. Doubles are correctly rounded, where serde_json's parser (without its `float_roundtrip` feature) can be one unit in the last place off. `JsonDocument::to_value` converts a document to a `serde_json::Value`.

### Other changes

- **Faster validation.** On the sourcemeta jsonschema-benchmark corpora, measured with the benchmark's own harness against Blaze:
  - The `\w` test of the matcher for patterns such as `^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$` no longer rebuilds its character set for every character: cspell validates more than five times faster.
  - A pattern that is valid only without the `u` flag (an identity escape such as `\&`) runs on the `regex` crate for strings within the Basic Multilingual Plane, instead of always on the backtracking engine: krakend validates about 30% faster.
  - Small objects cost less to evaluate: a plan's shape is entered directly, a few declared names are looked up in a small object instead of matching each of its properties, a property name that is not the next declared one is tried as the next in sorted order, and fused object plans no longer allocate per evaluation.

  With a `JsonDocument`, the crate now parses and validates faster than Blaze on all 37 corpora (geometric mean about 0.37 of Blaze's time), and validates already-parsed instances faster on 34 of them (about 0.54).

## V0.1.1

V0.1.1 makes the evaluator generic over the instance it reads.

### New features

- **The `Instance` trait.** `Validator::validate_instance` and `Validator::evaluate_instance` take any `Instance`: a value shown as one of the six JSON kinds, with arrays and objects read in place through `ArrayView` and `ObjectView`. `&serde_json::Value` implements it, and so can a host language's own values, which can then be validated without converting them to `serde_json::Value` first. The `corvus-json-schema-rs` Python package reads Python objects this way.

## V0.1.0

The first release: a port of the Corvus.Text.Json V5 runtime evaluator (`Corvus.Text.Json.RuntimeEvaluator`) to Rust, for drafts 4, 6, 7, 2019-09 and 2020-12. It passes the whole JSON-Schema-Test-Suite and produces the same results and annotations as the C# evaluator.
