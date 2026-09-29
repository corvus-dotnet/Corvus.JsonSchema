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
| ansible-meta | 333 | 53.7 µs | 285.2 µs | 0.19 | 2.34 ms | 4.79 ms | 17.41 ms |
| aws-cdk | 483 | 12.9 µs | 60.1 µs | 0.21 | 463.0 µs | 396.5 µs | 5.78 ms |
| babelrc | 794 | 50.9 µs | 293.3 µs | 0.17 | 1.11 ms | 3.54 ms | 7.49 ms |
| clang-format | 133 | 43.0 µs | 207.9 µs | 0.21 | 1.73 ms | 3.15 ms | 14.96 ms |
| cmake-presets | 967 | 2.84 ms | 12.58 ms | 0.23 | 19.22 ms | 48.22 ms | 28.87 ms |
| code-climate | 2484 | 225.2 µs | 558.9 µs | 0.40 | 1.50 ms | 1.83 ms | 8.16 ms |
| cql2 | 109 | 41.0 µs | 47.0 µs | 0.87 | 2.67 ms | 2.57 ms | 19.75 ms |
| cspell | 981 | 332.8 µs | 981.6 µs | 0.34 | 3.71 ms | 9.35 ms | 20.62 ms |
| cypress | 981 | 172.8 µs | 606.2 µs | 0.28 | 1.41 ms | 4.04 ms | 9.79 ms |
| deno | 987 | 367.0 µs | 1.90 ms | 0.19 | 3.21 ms | 7.55 ms | 11.89 ms |
| dependabot | 967 | 180.0 µs | 805.7 µs | 0.22 | 2.28 ms | 2.45 ms | 9.36 ms |
| draft-04 | 563 | 4.77 ms | 23.99 ms | 0.20 | 23.17 ms | 59.10 ms | 10.12 ms |
| fabric-mod | 911 | 297.0 µs | 1.53 ms | 0.19 | 3.61 ms | 12.71 ms | 11.36 ms |
| geojson | 500 | 4.16 ms | 5.88 ms | 0.71 | 15.92 ms | 38.43 ms | 24.78 ms |
| gitpod-configuration | 986 | 132.7 µs | 962.5 µs | 0.14 | 2.07 ms | 6.55 ms | 11.10 ms |
| helm-chart-lock | 3888 | 430.8 µs | 723.8 µs | 0.60 | 2.63 ms | 2.84 ms | 5.41 ms |
| importmap | 964 | 334.0 µs | 889.2 µs | 0.38 | 1.26 ms | 5.81 ms | 4.77 ms |
| jasmine | 980 | 50.4 µs | 530.8 µs | 0.09 | 1.24 ms | 832.5 µs | 7.18 ms |
| jsconfig | 981 | 218.9 µs | 772.0 µs | 0.28 | 4.46 ms | 6.95 ms | 15.03 ms |
| jshintrc | 966 | 1.06 ms | 1.99 ms | 0.54 | 3.70 ms | 8.20 ms | 6.72 ms |
| krakend | 47 | 104.9 µs | 489.6 µs | 0.21 | 6.70 ms | 10.14 ms | 47.12 ms |
| lazygit | 280 | 57.5 µs | 406.4 µs | 0.14 | 3.39 ms | 7.40 ms | 18.04 ms |
| lerna | 985 | 113.2 µs | 636.9 µs | 0.18 | 1.13 ms | 1.39 ms | 7.23 ms |
| nest-cli | 1025 | 128.6 µs | 867.9 µs | 0.15 | 1.87 ms | 5.13 ms | 11.52 ms |
| omnisharp | 987 | 107.3 µs | 1.19 ms | 0.09 | 2.18 ms | 7.04 ms | 10.08 ms |
| openapi | 107 | 7.72 ms | 18.64 ms | 0.41 | 30.36 ms | 70.22 ms | 57.65 ms |
| pre-commit-hooks | 985 | 205.4 µs | 1.48 ms | 0.14 | 2.04 ms | 8.22 ms | 6.95 ms |
| pulumi | 3807 | 320.3 µs | 2.77 ms | 0.12 | 3.25 ms | 5.75 ms | 10.26 ms |
| semantic-release | 794 | 56.8 µs | 246.9 µs | 0.23 | 1.03 ms | 2.35 ms | 8.65 ms |
| stale | 961 | 130.0 µs | 289.3 µs | 0.45 | 1.16 ms | 1.10 ms | 6.68 ms |
| stylecop | 983 | 172.1 µs | 1.13 ms | 0.15 | 2.51 ms | 7.46 ms | 10.65 ms |
| tmuxinator | 382 | 49.5 µs | 258.4 µs | 0.19 | 846.7 µs | 2.55 ms | 7.54 ms |
| ui5 | 942 | 230.2 µs | 1.25 ms | 0.18 | 7.51 ms | 16.68 ms | 29.31 ms |
| ui5-manifest | 611 | 1.54 ms | 5.48 ms | 0.28 | 19.00 ms | 31.71 ms | 80.53 ms |
| unreal-engine-uproject | 859 | 631.3 µs | 4.79 ms | 0.13 | 6.16 ms | 16.88 ms | 9.29 ms |
| vercel | 710 | 97.8 µs | 1.93 ms | 0.05 | 2.76 ms | 7.97 ms | 16.14 ms |
| yamllint | 966 | 12.1 µs | 56.9 µs | 0.21 | 241.1 µs | 1.23 ms | 5.61 ms |

Corvus TS faster on 37 of 37; geometric mean Corvus / jsu-js 0.22.

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
