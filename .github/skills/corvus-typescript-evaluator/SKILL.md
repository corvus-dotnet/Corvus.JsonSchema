---
name: corvus-typescript-evaluator
description: >
  Work on the TypeScript port of the V5 standalone schema evaluator (src-ts/corvus-json-schema,
  npm package @corvus-dotnet/json-schema): loader, compiler, JavaScript code generator, results
  collector and annotations, conformance and annotation suites, the jsonschema-benchmark and
  Bowtie integrations, and performance measurement.
  USE FOR: changing or debugging the TypeScript evaluator, measuring it against jsu-js,
  running its Bowtie harness, keeping it in parity with the C# runtime evaluator.
  DO NOT USE FOR: the C# runtime evaluator itself (see corvus-standalone-evaluator and
  docs/RuntimeEvaluator.md), .NET code generation (corvus-codegen).
---

# TypeScript Standalone Evaluator

## Overview

`src-ts/corvus-json-schema` ports the `Corvus.Text.Json.RuntimeEvaluator` pipeline to TypeScript. The loader,
compiler and analyses mirror the C# `SchemaLoader`/`SchemaCompiler`; instead of interpreting fused plans, the code
generator emits one specialised JavaScript function per node (`codegen.ts`) and instantiates the program with
`new Function`, or emits a standalone ES module (`generateModule`). Results collection (`results.ts`,
`collecting.ts`) interprets the same node graph and reproduces the C# `JsonSchemaResultsCollector` rows. The package
README (`src-ts/corvus-json-schema/README.md`) describes the design and the known differences from C#.

## Build and test

Node 22 or later. From `src-ts/corvus-json-schema`:

```bash
npm ci
npm test    # unit tests, the suite via compile(), via generateModule(), via a verbose collector,
            # the annotation suite, and the Bowtie IHOP driver
```

The suites read the repository's `JSON-Schema-Test-Suite` submodule
(`git submodule update --init JSON-Schema-Test-Suite`). Every required, optional and format test must pass
(`draft4/optional/zeroTerminatedFloats.json` is excluded, as in the C# runner), and all annotation assertions.

`src/metaschemas.ts` is generated from `src/Corvus.Text.Json/metaschema` by `npm run embed-metaschemas`; CI fails
if it is stale.

## Releasing to npm

The package is versioned independently of the NuGet packages, with its own history in
`src-ts/corvus-json-schema/VERSIONHISTORY.md` (packed with it). Bump `version` in `package.json`, add the version's entry
at the top of `VERSIONHISTORY.md` in the style of the repository's `VERSIONHISTORY.md`, and merge to `main`;
`.github/workflows/npm-publish.yml` runs the tests, stages the version if npm doesn't have it (`npm stage publish`
through npm trusted publishing: the package's trusted publisher on npmjs.com names that workflow file, so don't rename
it, and allows only staging), and tags the commit `ts-v<version>`. A maintainer then approves the staged version with
2FA (Staged Packages tab on npmjs.com, or `npm stage approve <stage-id>`); CI cannot publish a version on its own.
Packing runs `prepack`, which builds `dist/` and copies the repository `LICENSE` into the package. CI fails if
`npm pkg fix` would change `package.json`, because npm 11 silently drops fields it auto-corrects when publishing (write
`bin` paths without a leading `./`).

Never push a `ts-v` tag (or any non-version tag) by hand: `build.yml` publishes NuGet packages for the tags that
trigger it. Its tag filter only accepts release versions (`[0-9]+.[0-9]+.[0-9]+*`), and the workflow's own tag push
uses `GITHUB_TOKEN`, which starts no other workflow.

## Performance work

- Corpora: clone https://github.com/sourcemeta-research/jsonschema-benchmark and run
  `node bench/corpora.mjs --schemas <clone>/schemas [--jsu <dir>]` (see `bench/README.md` for the jsu-js setup).
- One corpus: `node bench/loop.mjs <clone>/schemas/<name> 3` (fastest pass); add `--cpu-prof` to profile, and
  `DUMP=out.js` to write the generated source.
- Compile phases: `node bench/compile-phases.mjs <schema.json>`.
- Measure before and after on the same machine, interleaving the two builds (a `git worktree` of the base commit
  with its own `dist/` works well). Heuristic thresholds are environment variables (`CORVUS_TS_UNROLL`,
  `CORVUS_TS_SWITCH`, `CORVUS_TS_UNROLL_REQUIRED`, `CORVUS_TS_EAGER_MAX`, `CORVUS_TS_DISPATCH_MAP`) for A/B runs.
- Results collection must not affect flag-mode times: flag mode runs only generated code.

## Parity with the C# evaluator

When the C# collecting mode changes (paths, messages, row order, which subschema results are kept), update
`collecting.ts` and `test/results.test.mjs`, which reproduces the C# `ResultsTests`/`ResultPathTests` expectations.
Format recognition follows `SchemaCompiler.GetFormatKind` (`formatKind` in `formats.ts`).

## Integrations

- `bench/jsonschema-benchmark/`: the `implementations/corvus-ts` directory for jsonschema-benchmark (`main.mjs`
  prints `cold,warm,compile,parse` in nanoseconds; `Makefile.fragment` holds the Makefile rules).
- `bowtie/`: the `implementations/js-corvus-jsonschema` harness for Bowtie; `node test/bowtie-ihop.mjs` drives it
  without containers.
