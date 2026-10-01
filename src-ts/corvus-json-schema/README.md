# @corvus-dotnet/json-schema

A JSON Schema evaluator for JavaScript and TypeScript (draft 4, 6, 7, 2019-09 and 2020-12), ported from the
Corvus.Text.Json V5 standalone evaluator (`Corvus.Text.Json.RuntimeEvaluator`, see
[docs/RuntimeEvaluator.md](../../docs/RuntimeEvaluator.md)). It compiles a schema once into specialised JavaScript and
validates parsed JSON values (`JSON.parse` output) against it.

- **Conformant**: passes all 7,966 tests of the JSON-Schema-Test-Suite (required, optional and `optional/format`,
  every draft), with the same single exclusion as the C# runner (`draft4/optional/zeroTerminatedFloats.json`, which
  `JSON.parse` cannot express).
- **Fast**: see [Performance](#performance).
- **Results and annotations**: evaluate with a results collector at the Basic, Detailed or Verbose level for the
  same rows (locations, messages, order) as the C# `JsonSchemaResultsCollector`, and annotations as
  `JsonSchemaAnnotationProducer` extracts them; passes all of the suite's annotation tests.
- **Standalone**: a schema can be emitted as an ES module that depends only on the small runtime, for environments
  where evaluating generated code at run time is not allowed.
- No dependencies.

## Install

```sh
npm install @corvus-dotnet/json-schema
```

Node.js 20 or later; the package is an ES module.

## Usage

```ts
import { compile } from '@corvus-dotnet/json-schema';

const validate = compile({
  $schema: 'https://json-schema.org/draft/2020-12/schema',
  type: 'object',
  properties: { id: { type: 'integer', minimum: 1 } },
  required: ['id'],
  unevaluatedProperties: false,
});

validate({ id: 3 }); // true
validate({ id: 0 }); // false
```

`compile` takes the schema as a parsed value or as JSON text, and these options (the TypeScript counterparts of
`JsonSchemaEvaluatorOptions`):

| Option | Meaning |
|---|---|
| `defaultDialect` | Dialect for schemas without `$schema` (`Dialect.Draft4` … `Dialect.Draft202012`, default 2020-12). |
| `assertFormat` | `true` asserts `format`, `false` never does; unset follows the vocabularies (2020-12 `format-assertion`). |
| `assertFormatInLegacyDrafts` | With `assertFormat` unset, also assert `format` in drafts 4 to 7. |
| `assertContent` | Assert `contentEncoding`/`contentMediaType` in draft 7 (default `true`). |
| `formats` | Custom format assertions by name, e.g. `{ 'even': (s) => s.length % 2 === 0 }`. |
| `resolveDocument` | `(uri) => schema \| JSON text \| undefined`, for remote `$ref`s. The standard metaschemas are built in. |
| `baseUri` | Base URI of the root document. |
| `entryPoint` | Evaluate from a subschema, e.g. `#/$defs/item`. |
| `maxDepth` | Depth limit for in-place recursion on a cycle (default 128); exceeding it throws `SchemaEvaluationDepthError`. |

An unresolvable reference throws `SchemaCompilationError` at compile time.

### Results and annotations

```ts
import { compile, collectAnnotations, JsonSchemaResultsCollector, ResultsLevel } from '@corvus-dotnet/json-schema';

const validate = compile(schema);
const collector = JsonSchemaResultsCollector.create(ResultsLevel.Detailed);
validate.evaluate({ id: 0 }, collector); // false
for (const r of collector.results) {
  // r.isMatch, r.message, r.evaluationLocation, r.schemaEvaluationLocation, r.documentEvaluationLocation
}

const verbose = JsonSchemaResultsCollector.create(ResultsLevel.Verbose);
validate.evaluate({ id: 3 }, verbose);
collectAnnotations(verbose); // { "": { "title": { "#": "Person" } }, "/id": { ... } }
```

The levels and rows are those of the C# collector: `Basic` records failures without message text, `Detailed` adds the
text, `Verbose` records every keyword (passing ones and annotations included). Evaluation without a collector runs the
generated flag-mode code; with one, an interpreter over the same compiled graph evaluates every keyword and reports it
(`collecting.ts`), so results collection costs nothing when unused.

### Standalone modules

```ts
import { generateModule } from '@corvus-dotnet/json-schema';
import fs from 'node:fs';

fs.writeFileSync('person-validator.mjs', generateModule(schema));
// import validate from './person-validator.mjs';  -- needs only '@corvus-dotnet/json-schema/runtime'
```

Or from the command line:

```sh
corvus-json-schema generate schema.json -o validator.mjs
corvus-json-schema validate schema.json doc1.json doc2.json
```

## How it works

The pipeline follows the C# evaluator stage for stage:

1. **Load** (`loader.ts`, from `SchemaLoader`): documents, resources (`$id`/`id`), anchors (`$anchor`,
   `$dynamicAnchor`, legacy `#name` ids), `$recursiveAnchor`, and the dialect and vocabularies of each resource,
   including custom metaschemas read through `$vocabulary`.
2. **Compile** (`compiler.ts`, from `SchemaCompiler`): one `SchemaNode` per distinct schema location with its keywords
   pre-digested; `$ref` resolved to node ids at compile time; `$dynamicRef`/`$recursiveRef` resolved statically
   wherever the entry resource decides them, so a dynamic scope is maintained only for references that stay dynamic.
3. **Analyse**: which nodes can mark evaluated properties/items (so tracking exists only under
   `unevaluatedProperties`/`unevaluatedItems`), in-place recursion cycles (Tarjan; only those nodes carry a depth
   guard), and `oneOf`/`anyOf` discriminators.
4. **Generate** (`codegen.ts`). Here the port departs from the C# design on purpose. The C# evaluator interprets
   its graph through fused plans chosen at compile time, because emitting IL at run time is expensive. In
   JavaScript, generating source is cheap and the engine's optimising compiler specialises it far better than an
   interpreter loop, so every node becomes one function containing exactly the checks it needs. The analyses that
   select C# plans select the shape of the emitted code instead:
   - pure-`$ref` chains are elided; type-only and small `const`/`enum` children are tested inline;
   - small objects with required properties are checked by direct lookups, everything else by one pass over the
     instance's keys, dispatching on the key length and then the few names of that length (as `Utf8NameMap` does);
     required properties become bits in that pass; map-like objects iterate `Object.values`;
   - an `allOf`/`$ref` composition of plain object schemas (declared properties, `required` and count bounds, each
     name with one schema) checks an object in one pass over the merged names, instead of a call per branch that
     each re-test the kind and the properties (the C# and Rust flat fused plan);
   - `oneOf`/`anyOf` narrow by a discriminator property (`const`/`enum` values) or by type dispatch;
   - `unevaluatedProperties`/`unevaluatedItems` whose contributors are all unconditional (or add nothing beyond
     them) are decided from static coverage, with no run-time tracking (the idea behind the fused object plan);
   - anchored patterns made of literals and ASCII classes match without the regular expression engine
     (`PatternMatcher`'s class sequences), everything else uses a native `RegExp` with the `u` flag;
   - structurally identical functions are merged by partition refinement.

## Performance

Measured with [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)'s corpora and
harness protocol (`bench/jsonschema-benchmark/main.mjs`), each implementation in fresh processes, median of three
runs, on the same machine. See [bench/README.md](bench/README.md) for how to reproduce.

Node.js 22.22, Intel(R) Xeon(R) Processor @ 2.10GHz, jsu 0.9.20 (json-model compiler 2.0.62), 2026-09-29. "Warm" is one pass over all
instances after warm-up, "cold" the first pass after compilation, "compile" the schema compilation. jsu-js's compile
time is its `jsu-compile` run (a Python process that generates the JavaScript), timed as the benchmark's jsu-js
implementation times it, so it includes the Python start-up.

| Corpus | Instances | Corvus TS warm | jsu-js warm | Corvus / jsu | Corvus TS cold | jsu-js cold | Corvus / jsu | Corvus TS compile | jsu-js compile | Corvus / jsu |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| ansible-meta | 333 | 53.9 µs | 293.8 µs | 0.18 | 1.40 ms | 4.74 ms | 0.29 | 19.68 ms | 693.50 ms | 0.03 |
| aws-cdk | 483 | 13.2 µs | 61.5 µs | 0.21 | 314.9 µs | 403.2 µs | 0.78 | 7.70 ms | 375.76 ms | 0.02 |
| babelrc | 794 | 61.6 µs | 313.3 µs | 0.20 | 793.2 µs | 3.52 ms | 0.23 | 8.88 ms | 400.27 ms | 0.02 |
| clang-format | 133 | 41.4 µs | 305.4 µs | 0.14 | 662.7 µs | 3.05 ms | 0.22 | 15.26 ms | 502.63 ms | 0.03 |
| cmake-presets | 967 | 2.90 ms | 12.42 ms | 0.23 | 18.26 ms | 47.33 ms | 0.39 | 29.57 ms | 968.51 ms | 0.03 |
| code-climate | 2484 | 196.6 µs | 589.0 µs | 0.33 | 1.28 ms | 1.68 ms | 0.76 | 7.10 ms | 378.74 ms | 0.02 |
| cql2 | 109 | 38.1 µs | 48.0 µs | 0.79 | 1.19 ms | 2.54 ms | 0.47 | 19.13 ms | 592.85 ms | 0.03 |
| cspell | 981 | 348.2 µs | 990.3 µs | 0.35 | 2.72 ms | 9.93 ms | 0.27 | 20.58 ms | 587.57 ms | 0.04 |
| cypress | 981 | 150.6 µs | 544.0 µs | 0.28 | 1.13 ms | 4.06 ms | 0.28 | 9.45 ms | 410.44 ms | 0.02 |
| deno | 987 | 340.3 µs | 1.82 ms | 0.19 | 3.41 ms | 7.01 ms | 0.49 | 12.20 ms | 455.74 ms | 0.03 |
| dependabot | 967 | 185.7 µs | 780.8 µs | 0.24 | 1.79 ms | 2.34 ms | 0.77 | 9.60 ms | 395.93 ms | 0.02 |
| draft-04 | 563 | 4.85 ms | 25.20 ms | 0.19 | 19.44 ms | 59.24 ms | 0.33 | 10.03 ms | 398.59 ms | 0.03 |
| fabric-mod | 911 | 371.4 µs | 1.58 ms | 0.24 | 3.26 ms | 11.42 ms | 0.29 | 11.49 ms | 420.10 ms | 0.03 |
| geojson | 500 | 4.27 ms | 6.00 ms | 0.71 | 16.62 ms | 37.65 ms | 0.44 | 23.65 ms | 649.46 ms | 0.04 |
| gitpod-configuration | 986 | 128.7 µs | 957.5 µs | 0.13 | 1.40 ms | 6.28 ms | 0.22 | 11.14 ms | 438.46 ms | 0.03 |
| helm-chart-lock | 3888 | 404.3 µs | 833.1 µs | 0.49 | 2.58 ms | 2.97 ms | 0.87 | 7.48 ms | 391.95 ms | 0.02 |
| importmap | 964 | 349.1 µs | 969.8 µs | 0.36 | 1.29 ms | 6.03 ms | 0.21 | 5.75 ms | 367.43 ms | 0.02 |
| jasmine | 980 | 53.6 µs | 212.9 µs | 0.25 | 915.5 µs | 844.6 µs | 1.08 | 7.39 ms | 377.50 ms | 0.02 |
| jsconfig | 981 | 262.3 µs | 732.6 µs | 0.36 | 3.48 ms | 7.19 ms | 0.48 | 16.47 ms | 553.72 ms | 0.03 |
| jshintrc | 966 | 1.18 ms | 1.99 ms | 0.59 | 3.70 ms | 8.78 ms | 0.42 | 7.66 ms | 407.09 ms | 0.02 |
| krakend | 47 | 109.4 µs | 466.0 µs | 0.23 | 6.32 ms | 9.98 ms | 0.63 | 42.44 ms | 1.53 s | 0.03 |
| lazygit | 280 | 61.6 µs | 491.3 µs | 0.13 | 1.81 ms | 7.31 ms | 0.25 | 18.77 ms | 579.36 ms | 0.03 |
| lerna | 985 | 123.4 µs | 618.1 µs | 0.20 | 1.00 ms | 1.53 ms | 0.66 | 7.60 ms | 395.03 ms | 0.02 |
| nest-cli | 1025 | 140.4 µs | 863.0 µs | 0.16 | 1.34 ms | 4.95 ms | 0.27 | 11.65 ms | 434.06 ms | 0.03 |
| omnisharp | 987 | 107.6 µs | 1.11 ms | 0.10 | 1.70 ms | 8.51 ms | 0.20 | 11.10 ms | 433.48 ms | 0.03 |
| openapi | 107 | 8.11 ms | 17.78 ms | 0.46 | 30.53 ms | 69.91 ms | 0.44 | 60.49 ms | 927.45 ms | 0.07 |
| pre-commit-hooks | 985 | 191.0 µs | 1.32 ms | 0.14 | 1.88 ms | 8.63 ms | 0.22 | 7.62 ms | 691.94 ms | 0.01 |
| pulumi | 3807 | 358.2 µs | 2.62 ms | 0.14 | 2.64 ms | 6.27 ms | 0.42 | 12.00 ms | 419.69 ms | 0.03 |
| semantic-release | 794 | 58.1 µs | 243.2 µs | 0.24 | 824.9 µs | 2.38 ms | 0.35 | 8.07 ms | 374.07 ms | 0.02 |
| stale | 961 | 149.8 µs | 320.6 µs | 0.47 | 1.08 ms | 1.14 ms | 0.95 | 6.97 ms | 376.78 ms | 0.02 |
| stylecop | 983 | 156.4 µs | 1.04 ms | 0.15 | 2.11 ms | 7.75 ms | 0.27 | 10.53 ms | 401.89 ms | 0.03 |
| tmuxinator | 382 | 48.2 µs | 234.3 µs | 0.21 | 697.2 µs | 2.31 ms | 0.30 | 9.22 ms | 382.08 ms | 0.02 |
| ui5 | 942 | 230.4 µs | 1.42 ms | 0.16 | 4.13 ms | 16.44 ms | 0.25 | 30.77 ms | 1.36 s | 0.02 |
| ui5-manifest | 611 | 1.52 ms | 5.48 ms | 0.28 | 21.05 ms | 30.92 ms | 0.68 | 83.64 ms | 2.91 s | 0.03 |
| unreal-engine-uproject | 859 | 674.9 µs | 4.89 ms | 0.14 | 6.22 ms | 17.63 ms | 0.35 | 9.99 ms | 416.77 ms | 0.02 |
| vercel | 710 | 129.0 µs | 1.84 ms | 0.07 | 1.95 ms | 8.86 ms | 0.22 | 18.95 ms | 618.57 ms | 0.03 |
| yamllint | 966 | 8.6 µs | 65.4 µs | 0.13 | 209.2 µs | 1.19 ms | 0.18 | 5.31 ms | 350.62 ms | 0.02 |

- Warm: Corvus TS faster on 37 of 37; geometric mean Corvus / jsu-js 0.23.
- Cold: Corvus TS faster on 36 of 37; geometric mean Corvus / jsu-js 0.38.
- Compile: Corvus TS faster on 37 of 37; geometric mean Corvus / jsu-js 0.02.

The one cold exception, jasmine (915.5 µs vs 844.6 µs), was faster than jsu-js in the previous run (888 µs vs
943 µs).

## Integrations

- **jsonschema-benchmark**: `bench/jsonschema-benchmark/` is a ready-to-copy `implementations/corvus-ts` directory
  (`Dockerfile`, `main.mjs`, `version.sh`, and the Makefile rules in `Makefile.fragment`).
- **Bowtie**: `bowtie/` is a ready-to-copy `implementations/js-corvus-jsonschema` harness. `node test/bowtie-ihop.mjs`
  drives it over IHOP exactly as Bowtie does, without containers.

## Tests

```sh
npm install
npm test                 # everything below
node test/suite.mjs      # the JSON-Schema-Test-Suite (--draft, --filter, --verbose; --module through generateModule;
                         #   --collect basic|detailed|verbose through a results collector)
node test/annotations.mjs  # the suite's annotation tests through a verbose collector
node test/bowtie-ihop.mjs
```

The suite is read from the repository's `JSON-Schema-Test-Suite` submodule (`git submodule update --init
JSON-Schema-Test-Suite`), or from `$JSON_SCHEMA_TEST_SUITE`.

## Releasing

The package is versioned independently of the Corvus NuGet packages. To release:

1. Change `version` in `package.json` and merge to `main`. If npm doesn't have that version, the `npm-publish`
   workflow tests the package, stages it on npm (`npm stage publish`, with npm trusted publishing, so no token is
   stored), and tags the commit `ts-v<version>`.
2. A maintainer approves the staged version with 2FA, on the package's **Staged Packages** tab on npmjs.com or with
   `npm stage list` and `npm stage approve <stage-id>`. Only then is the version published. If you reject it instead,
   delete its `ts-v` tag.

Don't push `ts-v` tags yourself.

## Limitations and differences from the C# evaluator

- **Results collection** matches the C# collector's rows, with these differences:
  - Numbers in messages are printed from JavaScript numbers (`1e2` reads `100`), and annotation values are the
    values re-serialised (`JSON.stringify`), not the schema's source text, because schemas arrive as parsed values.
  - Instance properties are visited in JavaScript's key order, which puts integer-like keys first.
  - Both evaluators report a subschema's own schema location even when an identical subschema occurs elsewhere,
    evaluate a static `$dynamicRef` alongside a sibling `$ref`, report `dependencies` rows under `dependencies` in
    every dialect, and name an elided static `$dynamicRef`/`$recursiveRef` hop by its own keyword in the evaluation
    path (the C# evaluator was fixed to match on all four).
- **Numbers are JavaScript numbers.** Instances come from `JSON.parse`, so integers beyond 2^53 and long decimals have
  already lost precision before validation (as in every JavaScript validator); `multipleOf` with a fractional
  divisor is computed exactly on the decimal forms of the doubles.
- **Regular expressions** are native ECMAScript (`u` flag), which is the dialect JSON Schema specifies; the C#
  evaluator translates them to .NET.
- Custom `formats` functions cannot be serialised into standalone modules.

Code-generation heuristics can be varied for experiments with environment variables read at load time:
`CORVUS_TS_UNROLL` (optional properties checked by direct lookup, default 3), `CORVUS_TS_UNROLL_REQUIRED` (required-only
objects unrolled up to this size, default 24), `CORVUS_TS_SWITCH` (names dispatched by a plain `switch`, default 4),
`CORVUS_TS_DISPATCH_MAP=1` (dispatch through a `Map` instead of by length).
