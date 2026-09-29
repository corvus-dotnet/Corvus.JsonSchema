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
instances after warm-up, "cold" the first pass after compilation, "compile" the schema compilation (jsu-js compiles
in a separate Python process, about 0.6 s per schema, so its compile column is omitted).

| Corpus | Instances | Corvus TS warm | jsu-js warm | Corvus / jsu | Corvus TS cold | jsu-js cold | Corvus TS compile |
|---|---:|---:|---:|---:|---:|---:|---:|
| ansible-meta | 333 | 53.9 µs | 293.8 µs | 0.18 | 1.40 ms | 4.74 ms | 19.68 ms |
| aws-cdk | 483 | 13.2 µs | 61.5 µs | 0.21 | 314.9 µs | 403.2 µs | 7.70 ms |
| babelrc | 794 | 61.6 µs | 313.3 µs | 0.20 | 793.2 µs | 3.52 ms | 8.88 ms |
| clang-format | 133 | 41.4 µs | 305.4 µs | 0.14 | 662.7 µs | 3.05 ms | 15.26 ms |
| cmake-presets | 967 | 2.90 ms | 12.42 ms | 0.23 | 18.26 ms | 47.33 ms | 29.57 ms |
| code-climate | 2484 | 196.6 µs | 589.0 µs | 0.33 | 1.28 ms | 1.68 ms | 7.10 ms |
| cql2 | 109 | 38.1 µs | 48.0 µs | 0.79 | 1.19 ms | 2.54 ms | 19.13 ms |
| cspell | 981 | 348.2 µs | 990.3 µs | 0.35 | 2.72 ms | 9.93 ms | 20.58 ms |
| cypress | 981 | 150.6 µs | 544.0 µs | 0.28 | 1.13 ms | 4.06 ms | 9.45 ms |
| deno | 987 | 340.3 µs | 1.82 ms | 0.19 | 3.41 ms | 7.01 ms | 12.20 ms |
| dependabot | 967 | 185.7 µs | 780.8 µs | 0.24 | 1.79 ms | 2.34 ms | 9.60 ms |
| draft-04 | 563 | 4.85 ms | 25.20 ms | 0.19 | 19.44 ms | 59.24 ms | 10.03 ms |
| fabric-mod | 911 | 371.4 µs | 1.58 ms | 0.24 | 3.26 ms | 11.42 ms | 11.49 ms |
| geojson | 500 | 4.27 ms | 6.00 ms | 0.71 | 16.62 ms | 37.65 ms | 23.65 ms |
| gitpod-configuration | 986 | 128.7 µs | 957.5 µs | 0.13 | 1.40 ms | 6.28 ms | 11.14 ms |
| helm-chart-lock | 3888 | 404.3 µs | 833.1 µs | 0.49 | 2.58 ms | 2.97 ms | 7.48 ms |
| importmap | 964 | 349.1 µs | 969.8 µs | 0.36 | 1.29 ms | 6.03 ms | 5.75 ms |
| jasmine | 980 | 53.6 µs | 212.9 µs | 0.25 | 915.5 µs | 844.6 µs | 7.39 ms |
| jsconfig | 981 | 262.3 µs | 732.6 µs | 0.36 | 3.48 ms | 7.19 ms | 16.47 ms |
| jshintrc | 966 | 1.18 ms | 1.99 ms | 0.59 | 3.70 ms | 8.78 ms | 7.66 ms |
| krakend | 47 | 109.4 µs | 466.0 µs | 0.23 | 6.32 ms | 9.98 ms | 42.44 ms |
| lazygit | 280 | 61.6 µs | 491.3 µs | 0.13 | 1.81 ms | 7.31 ms | 18.77 ms |
| lerna | 985 | 123.4 µs | 618.1 µs | 0.20 | 1.00 ms | 1.53 ms | 7.60 ms |
| nest-cli | 1025 | 140.4 µs | 863.0 µs | 0.16 | 1.34 ms | 4.95 ms | 11.65 ms |
| omnisharp | 987 | 107.6 µs | 1.11 ms | 0.10 | 1.70 ms | 8.51 ms | 11.10 ms |
| openapi | 107 | 8.11 ms | 17.78 ms | 0.46 | 30.53 ms | 69.91 ms | 60.49 ms |
| pre-commit-hooks | 985 | 191.0 µs | 1.32 ms | 0.14 | 1.88 ms | 8.63 ms | 7.62 ms |
| pulumi | 3807 | 358.2 µs | 2.62 ms | 0.14 | 2.64 ms | 6.27 ms | 12.00 ms |
| semantic-release | 794 | 58.1 µs | 243.2 µs | 0.24 | 824.9 µs | 2.38 ms | 8.07 ms |
| stale | 961 | 149.8 µs | 320.6 µs | 0.47 | 1.08 ms | 1.14 ms | 6.97 ms |
| stylecop | 983 | 156.4 µs | 1.04 ms | 0.15 | 2.11 ms | 7.75 ms | 10.53 ms |
| tmuxinator | 382 | 48.2 µs | 234.3 µs | 0.21 | 697.2 µs | 2.31 ms | 9.22 ms |
| ui5 | 942 | 230.4 µs | 1.42 ms | 0.16 | 4.13 ms | 16.44 ms | 30.77 ms |
| ui5-manifest | 611 | 1.52 ms | 5.48 ms | 0.28 | 21.05 ms | 30.92 ms | 83.64 ms |
| unreal-engine-uproject | 859 | 674.9 µs | 4.89 ms | 0.14 | 6.22 ms | 17.63 ms | 9.99 ms |
| vercel | 710 | 129.0 µs | 1.84 ms | 0.07 | 1.95 ms | 8.86 ms | 18.95 ms |
| yamllint | 966 | 8.6 µs | 65.4 µs | 0.13 | 209.2 µs | 1.19 ms | 5.31 ms |

Warm: Corvus TS faster on 37 of 37; geometric mean Corvus / jsu-js 0.23. Cold: faster on 36 of 37, geometric mean
0.38; the exception, jasmine (915.5 µs vs 844.6 µs), was faster than jsu-js in the previous run (888 µs vs 943 µs).

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
