# The runtime evaluator branch: a map for review

`feature/runtime-evaluator` replaces the V5 standalone (code-generated, per-keyword) schema evaluator with a
runtime evaluator: schemas are compiled once into a plan graph and evaluated by a small set of loops, and the
source generator embeds the compiled program (a program image) rather than emitting validation code. It also
carries the measurement apparatus that drove the work, a comparison against Blaze on the 37 Sourcemeta corpora,
and the packaging that keeps native AOT within 6% of the JIT.

## Where to start

1. **[RuntimeEvaluator.md](RuntimeEvaluator.md)**: the design (compilation, plans, evaluation, conformance), and
   "Publishing an application", the consumer-facing guidance (native AOT for one-shot processes, the JIT for
   long-running ones, the ReadyToRun trade-off).
2. **[RuntimeEvaluatorPrecompilation.md](RuntimeEvaluatorPrecompilation.md)**: the engineering notes, round by
   round, with the measurements that decided each step. Long; the sections "The four-axis table", "The entry
   floor", "The parse step" and "Where ui5 spends its time" are the ones with the conclusions.
3. **[measurements/2026-09-13](measurements/2026-09-13/)**: the end-of-branch tables. `four-axis-table.md` is the
   whole comparison (cold, warm, parse-and-evaluate, compile, memory; nine implementations by 37 corpora);
   `parse-and-evaluate-corvus-vs-blaze.md` is the one that describes a service validating each request once.
4. **[ReleaseProcess.md](ReleaseProcess.md)**, "The native AOT profile": what the build does before packaging.

## The code, in reading order

| area | files | what to know |
|---|---|---|
| compilation | `src/Corvus.Text.Json/Corvus/Text/Json/RuntimeEvaluator/Compilation/` | `SchemaLoader` resolves documents, resources and references; `SchemaCompiler` builds `SchemaNode`s and then runs the passes that pick each node's plan (elision of pure `$ref`s, canonical references, discriminators, unrolled properties, simple arrays, in-place cycles, fusion, forwards, object details, plans, flags); `FusedObjects` merges the branches of `allOf`/`$ref`/`if`-`then`-`else`/`anyOf`/`oneOf` over one object into one pass; `Utf8NameMap` is the allocation-free name lookup; `ProgramImage` serialises a compiled schema. |
| evaluation | `.../RuntimeEvaluator/Evaluation/` | `Evaluator.cs` is the engine: the entry, the per-plan loops (leaf, simple array, object, strict object, map, array items, fused object, conditional, type dispatch, dynamic ref), and the general path for everything else; `EvaluationState.cs` holds the per-evaluation state and the two document accesses (raw rows, or the document interface). |
| public surface | `.../RuntimeEvaluator/JsonSchemaEvaluator.cs`, `JsonSchemaEvaluatorOptions.cs` | `Compile`, `Evaluate`, entry points, program images. |
| document | `.../Document/ParsedJsonDocument.cs`, `.../Document/Internal/JsonDocument.cs`, `RawDocumentAccess.cs` | The evaluator reads a parsed document's rows and text as spans (`TryGetRawSpans`); the pooled buffers grow to cover a request. |
| validator and generator | `src/Corvus.Text.Json.Validator/`, `src/Corvus.Text.Json.CodeGeneration/`, `src/Corvus.Text.Json.SourceGenerator/` | The Validator wraps the runtime evaluator (its Roslyn pipeline is gone); generated types embed a program image and validate through the evaluator (`CorvusJsonSchemaProgram.cs` in each generated set). |
| package | `src/Corvus.Text.Json/buildTransitive/Corvus.Text.Json.targets`, `profiles/` | The static profile handed to ILC when an application publishes native AOT. |
| build | `.zf/config.ps1` (`GenerateAotProfile`) | Records the profile from the code being packaged, in CI's compile phase. |
| harness and tools | `benchmarks/Corvus.Text.Json.RuntimeEvaluator.Benchmarks/`, `.../ColdRunner/`, `.../tools/` | `tools/measure.sh` reproduces every table; `tools/README.md` says how. |
| tests | `tests/Corvus.Text.Json.RuntimeEvaluator.Tests/` (unit and the official suite), `tests/Corvus.Text.Json.EvaluatorTestSuite.Tests/` | 1,063 evaluator tests; the solution gate is 95,620. |

## The commits, grouped

The branch is 123 commits. Read them in this order rather than chronologically; the hashes are on the branch.

- **The evaluator and its merge** (3a2c4a9 to 84af8fb): the runtime evaluator as a project, direct document access,
  the Validator replaced, node flags and in-place cycles, fused plans, results-collector messages, Stage 0 (generated
  types validate through the evaluator), program images, the regex provider, static `$dynamicRef` resolution, the
  fused object plan, images from the CLI and the source generator, the merge into `Corvus.Text.Json`.
- **Plans and loops** (cd8554e to 4e98a9c): per-node plans (leaf, simple array, strict object, map, type dispatch,
  forward, conditional), one-pass property matching, the name map without a hash, regex-free pattern matchers, the
  entry fast path, bounds checks by construction, the call-chain pass, nested strict objects. Each has a docs
  commit beside it with the paired measurement.
- **Cold start, AOT and the comparison with Blaze** (e0c3972 to ac6e8f6): the cold runner, native AOT and
  ReadyToRun builds, allocation and clock-overhead measurement, the regenerated benchmark models, `measure.sh`,
  the general backtracking-free matcher, the static profile for AOT and the partial ReadyToRun finding, the
  package's targets and profile, the consumer guidance.
- **The last round** (4e6ebad to e961711): the entry floor, names and enum values as words, the three loops that
  had no profile, coalesced fused applications, canonical references (both compiler builds).
- **Parse and packaging** (d221454 to b5843e1): the parse cost against Blaze, the parse-and-evaluate measure, the
  pooled-buffer growth fix, the profile recorded in the compile phase, the CLI's native AOT switch, the tables.
- **Benchmark models** (acdd861, 2511dfe, 5b20372): 39 model projects regenerated with the current generator;
  7,361 of the branch's 7,931 changed files. A reviewer can skip them: they are the generator's output, checked
  in so the harness measures the shipping code path.

## What the numbers say

Medians over the 37 corpora, each corpus in its own process, on 2026-09-13 (`measurements/2026-09-13`):

| | Blaze | Corvus JIT | Corvus native AOT | Corvus generated (JIT) |
|---|---:|---:|---:|---:|
| cold start (first validation in a fresh process) | 10 ms | 150 ms | 4.6 ms | 116 ms |
| warm evaluation, parsed instance (per corpus pass) | 112 µs | 78 µs | 88 µs | 75 µs |
| parse and evaluate once (per corpus pass) | 2.74 ms | 484 µs | 550 µs | 484 µs |
| compile the schema | 2.7 ms | 82 ms | 0.32 ms | (embedded) |
| allocations per evaluation | n/a | 0 | 0 | 0 |

Warm, Corvus is faster on 31 of 37 (geometric mean 0.72); parsing included, on 37 of 37 (0.18). Every Corvus row
agrees with Blaze on every instance of every corpus (`harness diff`).

## Verification

- Solution gate: `dotnet build Corvus.Text.Json.slnx` then `dotnet test --solution ... --filter
  "TestCategory!=failing&TestCategory!=outerloop&TestCategory!=integration"`: 95,620 tests, 0 failed, on the branch head.
- Corpus agreement: `Corvus.Text.Json.RuntimeEvaluator.Benchmarks.dll diff`: 37 of 37.
- Every table: `tools/measure.sh publish|warm|cold|table` (`tools/README.md`); the profile:
  `BUILDVAR_GenerateAotProfile=true ./build.ps1 -Tasks GenerateAotProfile`.

## Decisions for the reviewer

- **Experiment switches.** Ten `CORVUS_RT_*` environment variables in the compiler and evaluator turn individual
  optimisations off for A/B measurement (`CORVUS_RT_NO_CANONICAL`, `NO_DISCRIMINATOR`, `NO_ELIDE`, `NO_FUSE`,
  `NO_INTFAST`, `NO_LEAF`, `NO_ORDER`, `NO_PLANS`, `NO_UNROLL`, `REGEX_INTERPRETED`). They are read once into
  static fields and cost nothing at run time; the source generator's build compiles them away. They stay: they are
  how a regression is bisected to one optimisation with the harness.
- **Public surface added to `Corvus.Text.Json`**: `JsonSchemaEvaluator` and its options; `JsonDocument.TryGetRawAccess`
  and `TryGetRawSpans` (virtual), `RawDocumentAccess`; the package's `buildTransitive` targets and their two
  properties (`CorvusTextJsonUseProfile`, `CorvusTextJsonProfile`).
- **The CI profile step** is implemented and runs locally; its first run on GitHub Actions is the check that the
  .NET 11 runtime install and the dotnet-eng download work on the runner.
- **The CLI's native AOT publish** is opt-in (`-p:CliAot=true -p:CliAotRooted=true`); making it the shipped form
  means either a trim-clean command binder or a validate-only tool.

## Follow-ups, not in this PR

- A .NET 11 build and support for its native union and pattern-matching types (the next piece of work).
- Evaluator: folding a conditional chain's discriminator test into its level's own scan (about 8% on ui5);
  inlining the string-set match's word path; a smaller evaluation state. Each is a few percent on a few corpora.
- Measurement: an in-process Blaze harness would replace the CLI differential for the parse-and-evaluate column.
