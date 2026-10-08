---
name: corvus-julia-evaluator
description: >
  Work on the Julia port of the V5 standalone schema evaluator (src-jl/CorvusJsonSchema, package
  CorvusJsonSchema): loader, compiler, the fail-fast plans and fused object plans, results collector and
  annotations, the document parser, the ECMA-262 pattern translator over PCRE2, the precompile workload,
  the allocation and type stability tests, the conformance and annotation suites, the jsonschema-benchmark
  and Bowtie integrations, and performance measurement.
  USE FOR: changing or debugging the Julia evaluator, measuring it against the other Corvus evaluators and
  Blaze, running its Bowtie harness, releasing the package to Julia's General registry, keeping it in
  parity with the C# runtime evaluator.
  DO NOT USE FOR: the C# runtime evaluator itself (see corvus-standalone-evaluator and
  docs/RuntimeEvaluator.md), the Go port (corvus-go-evaluator), the Java port (corvus-java-evaluator), the
  TypeScript port (corvus-typescript-evaluator).
---

# Julia Standalone Evaluator

## Overview

`src-jl/CorvusJsonSchema` ports the `Corvus.Text.Json.RuntimeEvaluator` pipeline to Julia, from the Go port
(`src-go/corvus-json-schema`), which it follows file for file: each source file says which Go file it was ported
from. `loader.jl` and `compiler.jl` build the node graph and its analyses (marking, in-place cycles,
discriminators). `plan.jl` compiles each node to a fail-fast plan (the types are in `plan_types.jl`), and `fused.jl`
fuses an object's keywords across `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` into one
pass. `eval.jl` is the general evaluator: it serves results collection, and the nodes that track evaluated
properties or items. `Document` (`document.jl`) is the instance model: the UTF-8 text plus a flat tape, two words
per value. `names.jl` is the property name lookup, `pattern.jl` the regex-free pattern matchers, and
`src/ecmaregex` (the `EcmaRegex` submodule) the ECMA-262 translator over PCRE2 behind every other pattern.
`validator.jl` has the public functions, and `precompile.jl` the workload that puts the evaluator in the package
image.

The package has no dependencies. Keep it that way: the benchmark and Bowtie harnesses are projects of their own for
that reason (`src-jl/CorvusJsonSchemaBench`, `src-jl/CorvusJsonSchemaBowtie`), and PackageCompiler is used only by
the benchmark's optional system image.

The package README describes the design. `OPTIMIZATIONS.md` maps every optimisation to its counterpart in the C#,
Rust, Java and Go evaluators, and its Todo list is the order of the performance work. Keep it current: a technique
added to any of the other evaluators should be checked off (or ruled out, with the reason) there.

## Build and test

Julia 1.10 or later. From `src-jl/CorvusJsonSchema`:

```bash
julia --startup-file=no --project=. -t 4 test/runtests.jl               # every test, as CI runs them
julia --startup-file=no --project=. -e 'using Pkg; Pkg.test()'          # the same, as a user's Pkg.test does
julia --startup-file=no -t 4 test/ecmaregex/runtests.jl                 # the pattern engine alone, from its sources
JSONSCHEMA_BENCHMARK=<jsonschema-benchmark> julia --startup-file=no --project=. -t 4 test/runtests.jl   # also plans against the general evaluator
```

Run them on the oldest Julia (1.10) and the latest before a change is done. `Pkg.test` adds `--check-bounds=yes`
on Julia 1.10 to 1.12 and not on 1.13, so the script is what CI runs on every version.

The suites read the repository's `JSON-Schema-Test-Suite` submodule (or `JSON_SCHEMA_TEST_SUITE`, and
`CORVUS_JSON_SCHEMA_TEST_SUITE` for the pattern engine's tests). Every required, optional and format test must pass
(`draft4/optional/zeroTerminatedFloats.json` is excluded, as in the C# runner), and all annotation assertions. Each
case runs fail-fast as a document, as bytes and as a string, and through a collector at each level, and the
verdicts must agree. `SUITE_DRAFT` and `SUITE_FILTER` narrow the run.

`test/metaschemas.jl` checks the embedded metaschemas (`src/metaschemas/`) against
`src/Corvus.Text.Json/metaschema`. `UPDATE_METASCHEMAS=1` copies them again.

`src-jl/CorvusJsonSchemaBowtie` runs the Bowtie harness over IHOP on the required and annotation suites, and
`src-jl/CorvusJsonSchemaBench` runs the benchmark programs on small corpora (their READMEs say how to set the
projects up). CI (`.github/workflows/julia.yml`) runs all of these on Linux x64 and arm64, Windows and macOS, with
Julia 1.10, 1.11, 1.12 and the latest. `Manifest.toml` files are not committed.

## Julia versions, PCRE2 and Unicode data

`Project.toml` says `julia = "1.10"`. Use no language or library feature newer than Julia 1.10.

The package takes no Unicode data from Julia or from the PCRE2 that Julia bundles, so it gives the same results
with every Julia release. Each Julia minor bundles a PCRE2 of its own with its own Unicode version (10.42 in Julia
1.10 and 1.11, 10.44 in 1.12, 10.46 in 1.13). `EcmaRegex` never gives PCRE2 the `PCRE2_UCP` option and never asks
it what a character is: every class, property and case-insensitive set is written as explicit ranges from
`src/ecmaregex/unicode_data.jl` (Unicode 17). The formats read the same tables through `src/unicode.jl`. Never call
a Julia function that reads Unicode data (`isletter`, `lowercase`, `Base.Unicode`), and never build a `Base.Regex`,
outside a test. `test/unicode.jl` scans the sources and fails if one appears.

`src/ecmaregex/EcmaRegex.jl`, `emitter.jl` and `pcre.jl` say what is written around the two PCRE2 versions the
module was written against, and why. A pattern that cannot be written with exactly the same meaning is refused
(`PatternError` with `unsupported`, which `compile_schema` reports as a `CompileError`). Never translate one
approximately. A match that reaches a PCRE2 limit throws `EcmaRegex.MatchError` and is never reported as no match.
The engine's tests compare with answers recorded from V8: `test/ecmaregex/v8_oracle.json`, a copy of the file the
Go and Java ports are tested with (the Go port's `testdata/gen_oracle.js` writes it), and `v8_fuzz.json`, which
`test/ecmaregex/gen_fuzz_oracle.js` writes. To move to a later Unicode version, change the tables and record both
again with a V8 of the same version, together.

The PCRE2 callout (for a lookbehind of no fixed length) is a C function pointer. `makecallout` in `pcre.jl` makes
it at first use in a module of its own, and with `@cfunction` only while a package is being precompiled. A
`@cfunction` compiled into a package image allocates on every call on Julia 1.10, which the allocation tests of
`test/ecmaregex/runtimetests.jl` catch. Do not simplify it without running them on Julia 1.10.

## Zero allocation and type stability

Validation must allocate nothing in the steady state, from a `Document`, a vector of bytes or a `String`.
`test/allocations.jl` measures each case with `@allocated`. Add a case for every new keyword path. A `Validator`
owns one evaluation state (`Evaluator`) and takes it with one atomic exchange. Validations that overlap take a state
from the validator's pool. Evaluated-property sets come from the state's arena, and fused passes are reused through
it. The README's "What allocates" lists the exceptions. Keep it true.

`test/stability.jl` fails if the optimised code of a hot function has a dynamic dispatch, or a hot structure has a
field of an abstract type. A field of an abstract type, a `Union` beyond a concrete type and `Nothing`, or a
function whose result type depends on a value, makes Julia box values and dispatch at run time, which allocates.

The reads of the tape and of the document's bytes are checked. `@inbounds` on them was measured and declined
(`OPTIMIZATIONS.md`, "Measured and declined"). Do not add `@inbounds` or pointer reads to the evaluator.

## The precompile workload

A schema is interpreted from compiled plans and nothing is generated at run time (`OPTIMIZATIONS.md`, "The
execution model", has the measurement). `precompile.jl` runs a workload while Julia writes the package image, so the
image holds the compiled evaluator and a process that loads the package compiles nothing to validate. When a change
adds a code path (a new plan shape, a new entry point), add a case to the workload that takes it, and check with
`julia --trace-compile=stderr` that a validation compiles nothing. The workload must leave nothing behind but
compiled code: a compiled engine pattern holds memory of the process that made it.

## Performance work

- Work through `OPTIMIZATIONS.md` in its Todo order. Measure each change before keeping it.
- In process: `src-jl/CorvusJsonSchemaBench/compare.jl` (see its README) runs the jsonschema-benchmark corpora with
  CorvusJsonSchema and JSONSchema.jl, interleaving the engines' passes.
- Against Blaze and the other Corvus evaluators: build the image (`pwsh Build-Image.ps1` in
  `src-jl/CorvusJsonSchemaBench/jsonschema-benchmark`) and run `Compare-Images.ps1`, pinned with `-CpuSet`. Every
  harness must warm up by time (2 seconds, at least 100 passes) and report the last warm-up pass, timed inside the
  loop. Compare like with like.
- Measure before and after on the same machine, interleaved, with nothing else running. Do not publish a figure
  that was not measured that way. The Performance tables of the README and `docs/JsonSchemaForJulia.md` say "to be
  measured" until they are.
- To see what Julia compiled and inferred: `@code_warntype`, `@code_llvm`, and `--trace-compile=stderr` for what a
  process compiles at run time.

## Parity with the C# evaluator

When the C# collecting mode changes (paths, messages, row order, which subschema results are kept), update the
collecting paths of `eval.jl` and `test/results.jl`, which reproduces the C# `ResultsTests`/`ResultPathTests`
expectations. Format recognition follows `SchemaCompiler.GetFormatKind` (`formats.jl`). A change to a plan in the
Go module (`plan.go`, `fused.go`) or the Rust crate (`eval/plan.rs`, `eval/plan/fused.rs`) usually ports line for
line to `plan.jl` and `fused.jl`. Patterns have ECMA-262 semantics through `EcmaRegex`, which is a port of the Java
library's `EcmaRegex.java` (see corvus-ecma-regex for the .NET translator).

## Releasing the package

Versioned independently, with its history in `src-jl/CorvusJsonSchema/VERSIONHISTORY.md`. Bump `version` in
`Project.toml`, add the history entry, and merge. `.github/workflows/julia-publish.yml` runs the tests, asks
JuliaRegistrator to register the version in Julia's General registry with a comment on the commit, waits for the
registry to merge it, and tags `CorvusJsonSchema-v<version>`. See `docs/ReleaseProcess.md`, "The Julia package".
Never push that tag by hand before the version is registered. In 0.x a new minor version is a breaking release.

## Integrations

- `src-jl/CorvusJsonSchemaBench/jsonschema-benchmark/`: the `implementations/corvus-jl` directory for
  jsonschema-benchmark. Its Dockerfile needs the registered package, so local builds use `Build-Image.ps1`.
- `src-jl/CorvusJsonSchemaBowtie/`: the Bowtie harness and its image.
- Docs: `docs/JsonSchemaForJulia.md`, its row in `docs/OtherLanguages.md`, and the home page's language list. The
  code samples in the docs and the README are run by `test/docs.jl`, which reads the two files: a statement with a
  comment after it is checked against the comment, and so is what a sample prints.
