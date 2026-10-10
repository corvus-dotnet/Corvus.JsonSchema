---
name: corvus-ocaml-evaluator
description: >
  Work on the OCaml port of the V5 standalone schema evaluator (src-ml/corvus-json-schema, opam package
  corvus-json-schema, module Corvus_json_schema): loader, compiler, the fail-fast plans and fused object
  plans, results collector and annotations, the document parser and number handling, the package's own
  ECMA-262 regular expression engine and Unicode tables, the allocation tests, the conformance and
  annotation suites, the README samples test, the jsonschema-benchmark harness, and the opam release.
  USE FOR: changing or debugging the OCaml evaluator, keeping it building on OCaml 4.14 and 5.x,
  measuring it against the other Corvus evaluators and Blaze, releasing the package through its
  publication repository and submitting it to the opam repository, keeping it in parity with the C#
  runtime evaluator.
  DO NOT USE FOR: the C# runtime evaluator itself (see corvus-standalone-evaluator and
  docs/RuntimeEvaluator.md), the Go port (corvus-go-evaluator), the Julia port (corvus-julia-evaluator),
  the Java port (corvus-java-evaluator), the TypeScript port (corvus-typescript-evaluator).
---

# OCaml Standalone Evaluator

## Overview

`src-ml/corvus-json-schema` ports the `Corvus.Text.Json.RuntimeEvaluator` pipeline to OCaml, from the Go port
(`src-go/corvus-json-schema`), which it follows file for file. Where Go leans on its standard library (the decimal
to float conversion, the regular expression engine, Unicode data) it follows the Object Pascal port
(`src-pas/corvus-json-schema`). It is a dune project with three libraries.

- `src/` is `corvus-json-schema`, whose one public module is `Corvus_json_schema` (`corvus_json_schema.mli`).
  `loader.ml` and `compiler.ml` build the node graph and its analyses. `plan.ml` compiles each node to a fail-fast
  plan (the types are in `program.ml`), and `fused.ml` fuses an object's keywords into one pass. `eval.ml` runs the
  plans and is the general evaluator behind results collection. `document.ml` is the instance model (the UTF-8 text
  and a flat tape of two words per value), `numbers.ml` the exact number comparisons and conversions, `names.ml`
  the property name lookup, `pattern.ml` the regex-free pattern matchers and `formats.ml` the format checks.
- `ecmaregex/` is `corvus-json-schema.ecmaregex`, the ECMA-262 engine: an automaton over ASCII text for the
  patterns that allow one, and a backtracking matcher with an explicit stack for the rest.
- `ucd/` is `corvus-json-schema.ucd`, the Unicode 17 tables (`tables.ml`, written by `tools/gen-ucd-tables.ps1`).

The two internal libraries are installed with the package and say in their interfaces that they are not supported.

The package README describes the design, the limits and the differences from the Go and Object Pascal ports.
`OPTIMIZATIONS.md` checks every technique of the Go module against this source, and its todo list is the order of
the performance work. Keep it current.

## Rules of the port

- **No dependencies.** The OCaml standard library only: no `str`, no `unix` and no C stubs in the libraries. Tests
  may use `unix` and `threads`. The benchmark harness is a project of its own for that reason.
- **Bounds checks stay on.** No `Obj`, no `unsafe_get`, `unsafe_set` or any other `unsafe_*`, no `-unsafe`. This
  is a standing decision. Do not add unchecked reads to gain speed.
- **OCaml 4.14 is the minimum.** No domains, no effects, and nothing of the 5.x standard library that 4.14 lacks.
  A test that needs domains is built on OCaml 5 only (see `test/api/dune` and `test/ecmaregex/dune`).
- **No global state.** A validator is immutable after compilation and safe to share between threads and domains.
  The working memory of an evaluation belongs to the validator (one set, taken with `Atomic.compare_and_set`) or
  to a `Scratch.t` the caller passes.
- **A 63-bit `int`.** A mask that holds 64 members in the Go source holds 62 here, and a set that needs more takes
  the path Go takes beyond 64. `test/limits_test.ml` crosses those edges. Each 64-bit quantity of the Go code has a
  comment that says how it is held. The package is for 64-bit platforms (`corvus-json-schema.opam.template`).
- **Every public item has a doc comment**, and `dune build @doc` gives no warning.

## Build and test

OCaml 4.14 or later with dune 3. From `src-ml/corvus-json-schema`:

```bash
dune build            # the dev profile: a warning is an error. It also writes corvus-json-schema.opam
dune test             # every test
dune build @fmt       # ocamlformat, the version .ocamlformat names
dune build @doc       # the documentation of the interfaces, which must give no warning
JSONSCHEMA_BENCHMARK=<jsonschema-benchmark> dune test   # also the plans against the general evaluator
```

Run the build and the tests on OCaml 4.14 and on the latest 5.x before a change is done. CI
(`.github/workflows/ocaml.yml`) runs them on Linux with both, and on macOS and Windows with the latest.

The suites read the repository's `JSON-Schema-Test-Suite` submodule (or `JSON_SCHEMA_TEST_SUITE`). Every required,
optional and format test must pass (`draft4/optional/zeroTerminatedFloats.json` is excluded, as in the C# runner),
and all annotation assertions. `SUITE_DRAFT` and `SUITE_FILTER` narrow the run. Where the suite is not there and
`JSON_SCHEMA_TEST_SUITE` names none, the tests that read it say `SKIPPED` and pass: that is the published package
built on its own. CI fails on that line, so never rely on a local run that printed it.

`corvus-json-schema.opam` is generated by `dune build` from `dune-project` and `corvus-json-schema.opam.template`.
Edit those two and commit the result.

## Zero allocation

Validation must allocate nothing on the OCaml heap in the steady state, from a `Document.t`, `Bytes.t` or a
`string`. `test/allocations_test.ml` measures each case with `Gc.minor_words` and `Gc.allocated_bytes`. Add a case
for every new keyword path. What shapes the code: a float, an `Int64`, a tuple, an option or a closure that is
stored or returned is a block, so floats stay in a `Float.Array.t`, two results come back packed in one `int`, and
the loops are top-level functions that take what a closure would capture. The README's "What allocates" and the
note at the top of `corvus_json_schema.mli` list the exceptions. Keep both true.

## The README samples

Every OCaml block of `README.md` and of `docs/JsonSchemaForOCaml.md` is the body of a module of
`test/readme_test.ml`, which runs each sample, compares what it prints with the expected lines, and fails if a
block of either document is not in the test line for line. Change a sample in the test first, format it, then copy
it to the documents.

## Performance work

- Work through `OPTIMIZATIONS.md` in its todo order. Measure each change before keeping it.
- Build the image with `pwsh Build-Image.ps1` in `src-ml/corvus-json-schema-bench/jsonschema-benchmark` and compare
  with `Compare-Images.ps1` of the Go module's benchmark directory, pinned with `-CpuSet`. Every harness warms up
  by time (2 seconds, at least 100 passes) and reports the last warm-up pass. Compare like with like.
- Do not publish a figure that was not measured that way. The Performance sections of the README and of
  `docs/JsonSchemaForOCaml.md` hold the figures of the last such run. Change them only from a new one.

## Parity with the C# evaluator

When the C# collecting mode changes (paths, messages, row order, which subschema results are kept), update the
collecting paths of `eval.ml` and `results.ml`, and `test/results`, which reproduces the C# expectations. A change
to a plan in the Go module (`plan.go`, `fused.go`) usually ports line for line to `plan.ml` and `fused.ml`. A change
to the Go module's `internal/ecmaregex` ports to `ecmaregex/`, and `test/ecmaregex/v8_oracle.json` is a copy of the
Go module's oracle.

## Releasing the package

Versioned independently, with its history in `src-ml/corvus-json-schema/VERSIONHISTORY.md`. Bump `(version ...)`
in `dune-project` and `version` in `src/corvus_json_schema.ml`, run `dune build`, add the history entry, and merge.
`.github/workflows/ocaml-publish.yml` runs the tests, pushes the package's directory to
`corvus-dotnet/corvus-json-schema-ocaml` tagged `v<version>`, and tags this repository `ml-v<version>`. Submitting
the version to the opam repository is a manual step afterwards. See `docs/ReleaseProcess.md`, "The OCaml package".
Never push either tag by hand.

## Integrations

- `src-ml/corvus-json-schema-bench/jsonschema-benchmark/`: the protocol program for jsonschema-benchmark and its
  local image.
- Docs: `docs/JsonSchemaForOCaml.md`, its row in `docs/OtherLanguages.md`, and the home page's language list.
