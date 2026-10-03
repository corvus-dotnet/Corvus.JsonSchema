# JSON Schema for JavaScript and TypeScript

[@corvus-dotnet/json-schema](https://www.npmjs.com/package/@corvus-dotnet/json-schema) is the Corvus JSON Schema
evaluator for JavaScript and TypeScript: draft 4, 6, 7, 2019-09 and 2020-12. It is a port of the .NET runtime
evaluator (see [Runtime Evaluator](RuntimeEvaluator.md)), and gives the same results and annotations.

It compiles a schema once into specialised JavaScript and validates parsed JSON values (`JSON.parse` output) against
it.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft), and
  all of its annotation tests.
- **Fast.** Over the [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) corpora it
  validates in under a quarter of the time of jsu-js, which also compiles schemas to JavaScript (see
  [Performance](#performance)).
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations, the same as every other Corvus
  implementation.
- **Standalone.** A schema can be emitted as an ES module that depends only on the package's small runtime, for
  environments that do not allow code generated at run time.
- No dependencies. Node.js 20 or later; the package is an ES module, with TypeScript types.

## Install

```sh
npm install @corvus-dotnet/json-schema
```

## Validate

```ts
import { compile } from '@corvus-dotnet/json-schema';

const validate = compile({
  type: 'object',
  properties: { id: { type: 'integer', minimum: 1 } },
  required: ['id'],
});

validate({ id: 3 }); // true
validate({ id: 0 }); // false
```

`compile` takes the schema as a parsed value or as JSON text. The validator is a function: call it with any value
`JSON.parse` produces.

## Options

| Option | Meaning |
|---|---|
| `defaultDialect` | The dialect of a schema without `$schema`: `Dialect.Draft4` to `Dialect.Draft202012` (the default). |
| `assertFormat` | `true` asserts `format`, `false` never does; unset follows the schema's vocabularies. |
| `assertFormatInLegacyDrafts` | With `assertFormat` unset, assert `format` in drafts 4 to 7 too. |
| `assertContent` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `true`). |
| `formats` | Custom formats by name, such as `{ even: (s) => s.length % 2 === 0 }`. |
| `resolveDocument` | `(uri) => schema`, JSON text or `undefined`, for remote references. The standard metaschemas are built in. |
| `baseUri` | The base URI of the root document. |
| `entryPoint` | A subschema to validate against, such as `#/$defs/item`. |
| `maxDepth` | The deepest the evaluator recurses in place (default 128); beyond it, `SchemaEvaluationDepthError`. |

A reference that cannot be resolved throws `SchemaCompilationError` when the schema is compiled.

## Results and annotations

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
collectAnnotations(verbose); // { "": { "title": { "#": "Person" } }, ... }
```

`Basic` records the failures without messages, `Detailed` adds the messages, and `Verbose` records every keyword,
passing ones and annotations included. Validation without a collector runs the generated code, so collecting costs
nothing when it is not used.

## Standalone modules

```ts
import fs from 'node:fs';
import { generateModule } from '@corvus-dotnet/json-schema';

fs.writeFileSync('person-validator.mjs', generateModule(schema));
// import validate from './person-validator.mjs'; needs only '@corvus-dotnet/json-schema/runtime'
```

Or from the command line:

```sh
npx corvus-json-schema generate schema.json -o validator.mjs
npx corvus-json-schema validate schema.json doc1.json doc2.json
```

## Performance

Measured with jsonschema-benchmark's corpora and protocol on Node.js 22, against jsu-js (geometric means over the
37 corpora, Corvus time divided by jsu-js time):

| | Corvus / jsu-js | Corvus faster on |
|---|---:|---:|
| Warm validation | 0.23 | 37 of 37 |
| Cold validation | 0.38 | 36 of 37 |
| Compilation | 0.02 | 37 of 37 |

jsu-js compiles a schema with a separate Python process, and its compilation time includes starting it.

The [package's README](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-ts/corvus-json-schema#performance)
has the figures for each corpus.

## Links

- Package: [npm](https://www.npmjs.com/package/@corvus-dotnet/json-schema)
- Source and README: [src-ts/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-ts/corvus-json-schema)
- The other languages: see [Other languages](OtherLanguages.md)
