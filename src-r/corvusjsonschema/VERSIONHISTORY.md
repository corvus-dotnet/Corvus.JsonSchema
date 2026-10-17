# Version History

The version history of the `corvusjsonschema` R package. It is versioned independently of the Corvus NuGet packages and of the [corvus-json-schema](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) Rust crate it is built on.

## V0.1.2

V0.1.2 takes version 0.1.7 of the corvus-json-schema crate. That fixes wrong results for several forms of pattern and a validation that did not end, replaces the crate's regular expression engine, and gives every match of a pattern a limit of time. Nothing that was there has changed. There is one new option and there are two new conditions. Versions 0.1.0 and 0.1.1 are affected.

### The regular expression engine

The crate no longer uses the `regress` crate for the patterns with a lookaround, a backreference or a modifier group. It matches them with an engine of its own, a port of the ECMA-262 engine of the Corvus Go module, and matches every pattern as it is written. The crate's [version history](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) lists the patterns that were answered wrongly before (a repetition inside a repetition that lost its count or did not end, a backreference from inside its group, two groups with one name, case-insensitive matching without the `u` flag, a pattern that is valid only without the `u` flag and a character beyond the Basic Multilingual Plane, `\B` beside a character of several bytes, and others), and what was measured: V8's answer for all 4,268,313 texts of the ECMA-262 regular expression test data that can be asked.

### Limits on regular expressions

A schema and an instance may both come from outside, so a pattern is now bounded in what it costs.

- **A match has a limit of one second.** `pattern_match_limit`, a new argument of `compile_schema()`, is the seconds one match of a `pattern` or `patternProperties` regular expression against one string may take. The default is `1`. `NULL`, or zero or less, turns the limit off for a caller who trusts both the schema and the instances. The limit is on each match, not on the validation.
- **`corvus_pattern_match_limit_error`** is raised by `is_valid()`, `is_valid_json()`, `evaluate()` and `evaluate_json()` when a match reached the limit, or when its backtracking held 1,048,576 entries. The value is then neither valid nor not valid. The message names the pattern and the limit. `compile_schema()` can raise it too, because a pattern of `patternProperties` is matched against the property names the schema declares. It inherits from `corvus_json_schema_error`, and not from `corvus_depth_error`.
- **`corvus_pattern_too_large_error`** is raised by `compile_schema()` for a schema with a regular expression over a limit of size: 1,048,576 bytes of pattern, 1,000 levels of groups, or 1,048,576 counted for it as it is read (a pattern of plain characters can be 16,384 characters long). It is a `corvus_compilation_error`, so code that handles those still does. A regular expression that is not valid raises `corvus_compilation_error` itself, as before.

### Notices

The package no longer holds `regress`. [inst/THIRD-PARTY-NOTICES.txt](inst/THIRD-PARTY-NOTICES.txt) now gives the notice of the Unicode Character Database for the crate's own tables, and the notice of `regress`, from whose tables they were first written.

## V0.1.1

V0.1.1 fixes a crash of the R session for one kind of schema, by taking version 0.1.6 of the corvus-json-schema crate. There are no API changes. Version 0.1.0 is affected.

### Bug fixes

- **A `not` that leads back to the schema it is in.** A schema can loop without consuming the instance. The crate abandons such an evaluation at `max_depth` (128 by default), and the package raises `corvus_depth_error`. Evaluating `not` went around that guard. For a schema such as `{"not": {"$ref": "#"}}`, validating an instance that reached the `not` recursed until the stack was full, and R ended with "segfault from C stack overflow". The trigger is in the schema, not in the instance. Such a schema now raises `corvus_depth_error` from `is_valid()`, `is_valid_json()`, `evaluate()` and `evaluate_json()`, as other schemas that loop do.

## V0.1.0

The first release: an R package over the corvus-json-schema Rust crate (0.1.5), for drafts 4, 6, 7, 2019-09 and 2020-12, on R 4.2 and later. It reads R lists and vectors in place, validates JSON text without creating R values for it, and collects results (basic, detailed and verbose) and annotations as data frames. It passes the whole JSON-Schema-Test-Suite, as JSON text and read in place.

### New features

- **`compile_schema()`.** Compiles a schema from JSON text or R values, with the crate's options: the default dialect, format and content assertion, custom formats and a document resolver written in R, a base URI, an entry point and a maximum depth.
- **`is_valid()` and `is_valid_json()`.** Whether an R value, or each JSON text of a character vector, is valid.
- **`evaluate()` and `evaluate_json()`.** An exhaustive evaluation at a results level, giving the results and the annotations as data frames. The default level is detailed, which has the messages.
- **Conditions.** Each failure is a condition with a class of its own, all inheriting from `corvus_json_schema_error`.
