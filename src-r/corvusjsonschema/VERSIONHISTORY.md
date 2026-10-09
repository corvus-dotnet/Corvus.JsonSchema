# Version History

The version history of the `corvusjsonschema` R package. It is versioned independently of the Corvus NuGet packages and of the [corvus-json-schema](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) Rust crate it is built on.

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
