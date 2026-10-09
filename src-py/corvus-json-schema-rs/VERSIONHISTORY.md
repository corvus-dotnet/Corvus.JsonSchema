# Version History

The version history of `corvus-json-schema-rs`, the Python package backed by the `corvus-json-schema` Rust crate. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.3

V0.1.3 fixes a crash of the Python process for a schema whose `not` leads back to the schema it is in, by taking version 0.1.6 of the `corvus-json-schema` crate. There are no API changes. Versions 0.1.0 to 0.1.2 are affected. The pure-Python `corvus-json-schema` package is not.

### Bug fixes

- **A `not` that leads back to the schema it is in.** A schema can loop without consuming the instance. The package abandons such an evaluation at `max_depth` (128 by default) and raises `SchemaEvaluationDepthError`. Evaluating `not` went around that guard. For a schema such as `{"not": {"$ref": "#"}}`, validating an instance that reached the `not` recursed until the stack overflowed. That ends the Python process with a segmentation fault. No exception is raised, so nothing can catch it. Calling the validator, `is_valid`, `is_valid_json` and `evaluate` were all affected. A `not` is now under the guard, so they raise `SchemaEvaluationDepthError`. The fault is in the schema. No instance causes it for a schema without such a loop, so a program was exposed only if it compiled schemas it did not write. A schema that loops elsewhere under a `not` was not affected in this package: it raised `SchemaEvaluationDepthError`, and still does.

## V0.1.2

V0.1.2 fixes wrong validation results for one form of pattern, by taking version 0.1.4 of the `corvus-json-schema` crate. There are no API changes. Versions 0.1.0 and 0.1.1 are affected. The pure-Python `corvus-json-schema` package is not.

### Bug fixes

- **A pattern of the form `^(?=[^SET]+$)(?=(.*\w)).+$` with a character outside ASCII in its excluded set.** The crate matches this form without a regular expression engine and keeps the excluded set as one bit for each ASCII character. A member outside ASCII, such as `é` in `^(?=[^é]+$)(?=(.*\w)).+$`, was read one UTF-8 byte at a time, and set the bits of unrelated ASCII characters (`C` and `)` for `é`). A validator compiled from such a schema rejected valid strings (`"C1"`) and accepted invalid ones (`"é1"`), with no error. Such a pattern is now matched by the regular expression engine. A pattern of this form whose excluded set is all ASCII was never affected.

## V0.1.1

V0.1.1 takes version 0.1.2 of the `corvus-json-schema` crate, which validates faster, and parses JSON text faster. There are no API changes, and every result is unchanged.

### Other changes

- **Faster validation.** The crate's 0.1.2 release makes several patterns and small objects cheaper to evaluate. A lookahead pattern such as `^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$` no longer rebuilds a character set for every character it tests, a pattern valid only without the `u` flag (such as `^\/[^\*\?\&\%]*(\/\*)?$`) no longer always runs on the backtracking engine, and objects with few declared properties cost less. On the sourcemeta jsonschema-benchmark corpora, cspell validates more than five times faster and krakend about 30% faster.

- **`is_valid_json` parses into a `JsonDocument`.** JSON text is parsed into the crate's `JsonDocument`, read in place by the evaluator, instead of a `serde_json::Value`: about a quarter of the parsing time, and one allocation per document. Doubles are now correctly rounded (serde_json's parser could be one unit in the last place off). Invalid UTF-8 in `bytes` raises `ValueError` as other invalid JSON does.

## V0.1.0

The first release.

### New features

- **The Rust evaluator for Python.** It has the API of the pure-Python `corvus-json-schema` package and passes the JSON-Schema-Test-Suite (every draft, with the C# runner's single exclusion) and its annotation tests.

- **Instances are read in place.** Python objects are read through CPython's C API, without converting them to `serde_json` values first, so validating an object costs the evaluation alone. JSON text can also be validated directly, parsed in Rust.

- **Wheels for every major platform.** The extension uses the stable ABI, so one wheel per platform serves CPython 3.10 and later, on Linux (x86_64 and aarch64, glibc and musl), macOS (universal2) and Windows (x64 and arm64).
