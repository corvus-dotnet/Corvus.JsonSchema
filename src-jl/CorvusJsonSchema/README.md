# CorvusJsonSchema.jl

A JSON Schema evaluator for Julia, for draft 4, 6, 7, 2019-09 and 2020-12. It is a port of the Corvus.Text.Json
runtime evaluator, made from the Go module (`src-go/corvus-json-schema`) and the Rust crate
(`src-rs/corvus-json-schema`) of this repository.

The package has no dependencies. It needs Julia 1.10 or later.

```julia
using CorvusJsonSchema

validator = compile_schema("""{
    "type": "object",
    "properties": {"id": {"type": "integer", "minimum": 1}},
    "required": ["id"]
}""")

isvalid(validator, """{"id": 3}""")  # true
isvalid(validator, """{"id": 0}""")  # false
```

## What is implemented

- **Compilation.** `compile_schema` takes a schema as a string, as UTF-8 bytes or as a parsed `Document`, and
  `compile_schema_uri` takes a URI. The result is a `Validator`, which validation does not modify and which several
  tasks may use at once. The options are keyword arguments: `default_dialect`, `assert_format`,
  `assert_format_in_legacy_drafts`, `assert_content`, `formats` (custom format assertions), `resolver` (a function
  that resolves a schema document by URI), `base_uri`, `entry_point` and `max_depth`. The standard metaschemas are
  embedded.
- **Validation.** `isvalid(validator, instance)` for a `Document`, a string or a vector of bytes. `validate` does
  the same and throws `ParseError` for text that is not JSON and `DepthExceededError` for a schema that recursed in
  place beyond the maximum depth.
- **Results.** `evaluate(validator, instance, collector)` evaluates exhaustively and reports to a `ResultsCollector`
  at the `Basic`, `Detailed` or `Verbose` level. `results(collector)` gives the rows (evaluation location, schema
  location, instance location, message), in the same order and with the same text as the other Corvus
  implementations. `annotations` and `collect_annotations` give the annotations of a `Verbose` collector.
- **Its own JSON parser.** `parse_document` reads UTF-8 JSON text into a flat tape, strictly as RFC 8259, with
  strings read in place where they have no escapes. The evaluator works on the tape.
- **An interpreter over compiled plans.** A schema compiles to plans that one type-stable interpreter runs: object
  plans with a name table, fused object plans, flat composition, discriminators, type dispatch and static
  unevaluated coverage, as in the Go module and the Rust crate. Nothing is generated or compiled when a schema is
  compiled, and a precompile workload puts the interpreter in the package image, so the first validation in a
  process takes milliseconds.
- **Patterns.** Patterns have ECMA-262 semantics. The common shapes are matched with no regular expression engine.
  The rest run on the `EcmaRegex` submodule.
- **Formats.** The formats of the specification and the numeric formats of the other Corvus evaluators, asserted on
  request. The Unicode properties they read are the package's own, of Unicode 17, so a format accepts the same
  strings on every Julia.

Validating a `Document`, a string or a vector of bytes allocates nothing in the steady state. Four asserted formats
are outside this: `regex`, `idn-hostname`, `idn-email`, and `hostname` for a label that starts with `xn--`. A custom
format receives a copy of the string. JSON text whose parsed form is larger than a few megabytes is parsed into
buffers that are not kept.

## Tests

```
julia --project=src-jl/CorvusJsonSchema -t 4 src-jl/CorvusJsonSchema/test/runtests.jl
```

The tests run the JSON-Schema-Test-Suite of the repository's submodule (required, optional and optional/format, with
`draft4/optional/zeroTerminatedFloats.json` excluded as in the other ports), its annotation tests, the results
parity tests, the allocation and type stability tests, and the tests of the pattern engine. With
`JSONSCHEMA_BENCHMARK` set to a checkout of jsonschema-benchmark they also compare the plans with the general
evaluator on its corpora.

## Not here yet

Documentation pages, benchmarks, continuous integration and a Bowtie harness.
