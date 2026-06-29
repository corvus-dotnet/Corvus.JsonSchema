# Getting started

This guide walks you through generating TypeScript from a JSON Schema and using it. You will install the tools, write a schema, generate a module, and then build, validate, and read a value with the generated types.

The code generator is a .NET command-line tool, but the code it produces is plain TypeScript with a small runtime library — your project does not depend on .NET at run time.

## Install the tools

First, the code generator. It is distributed as a .NET global tool and requires the [.NET SDK](https://dotnet.microsoft.com/download):

```bash
dotnet tool install --global Corvus.Json.Cli
```

This installs the `corvusjson` command. The same tool generates both C# and TypeScript; the `--engine` option selects which.

Second, the runtime. The generated code imports a small npm package at run time:

```bash
npm install @endjin/corvus-json-runtime
```

The runtime is ESM-only and pulls in three dependencies: `lossless-json` for exact numeric evaluation, `@js-temporal/polyfill` for dates and times, and `tr46` for internationalised formats.

## Define a schema

Create a file called `person.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Person",
  "type": "object",
  "required": [ "familyName" ],
  "properties": {
    "familyName": { "type": "string" },
    "givenName": { "type": "string" },
    "birthDate": { "type": "string", "format": "date" },
    "height": { "type": "number" }
  }
}
```

The `title` (`Person`) becomes the generated type name. `familyName` is required; the other properties are optional. `birthDate` has a `format`, which becomes a branded type with a validating factory.

## Generate the module

Run the generator, selecting the TypeScript engine:

```bash
corvusjson jsonschema person.json --engine TypeScript --tsRuntimeModule @endjin/corvus-json-runtime --outputPath ./src/generated
```

This writes a single file, `./src/generated/generated.ts`, containing the types, validators, and builders for the schema. `--tsRuntimeModule @endjin/corvus-json-runtime` tells the generator to import the runtime from the npm package you installed. (Omit that option and it writes a self-contained `corvus-runtime.ts` next to the module instead — see [The runtime](#the-runtime) below.)

## Use the generated types

Each type in the schema produces a **companion object** that carries every operation for that type — for `person.json` that is `Person`. The companion has the same name as the type, so you import it once and use it both as a type and as a value:

```typescript
import { Person, BirthDate } from "./generated/generated.js";

const decoder = new TextDecoder();

// Build a Person from plain values. The result is canonical UTF-8 JSON bytes.
const bytes = Person.build({
  familyName: "Brontë",
  givenName: "Anne",
  birthDate: BirthDate.from("1820-01-17"), // a format:date factory; throws on a malformed date
});

console.log(decoder.decode(bytes));
// {"familyName":"Brontë","givenName":"Anne","birthDate":"1820-01-17"}

// Validate untrusted input. evaluate returns a boolean and never throws.
const incoming: unknown = JSON.parse(decoder.decode(bytes));

if (Person.evaluate(incoming)) {
  // The value matched the schema, so this assertion cannot be wrong.
  const person = incoming as Person;
  console.log(person.familyName); // "Brontë"
}

console.log(Person.evaluate({ givenName: "Anne" })); // false — familyName is required
```

The root type's companion (`Person`) is also the module's `default` export, so `import Person from "./generated/generated.js"` gives you the document entry point without naming the type.

## What you get

For each type in the schema, the generated module emits:

- an **`interface`** describing the value's shape — here, `Person` with `readonly` properties;
- a **companion object** of the same name carrying that type's operations:
  - `Person.evaluate(value, results?)` — validate against the schema; returns a `boolean`, or collects detailed failures into an optional results object;
  - `Person.build(props)` / `Person.buildCanonical(props)` — construct UTF-8 JSON bytes;
  - `Person.patch(bytes, changes)` / `Person.produce(bytes, recipe)` — edit existing bytes, splicing only what changed;
  - for a property with a `format`, a factory such as `BirthDate.from(value)` (plus `BirthDate.toTemporal()` for dates and times);
  - for a `oneOf` union, `Shape.match(value, cases)` for exhaustive dispatch.

## The runtime

The generated code imports a small runtime library for the parts TypeScript cannot express on its own — format checks, exact numeric comparison, and the byte-level edit engine. You supply it in one of two ways:

- **As a package** (used above): pass `--tsRuntimeModule @endjin/corvus-json-runtime` and `npm install @endjin/corvus-json-runtime`. This is the right choice when you generate many modules, because they all share one installed copy.
- **Self-contained**: omit the option, and the generator writes a `corvus-runtime.ts` next to each generated module. You then install its three dependencies yourself: `npm install lossless-json @js-temporal/polyfill tr46`.

Either way the runtime is ESM-only, so your `tsconfig.json` should target ES modules — for example `"module": "ESNext"` with `"moduleResolution": "Bundler"` or `"NodeNext"`.

## CLI options

| Option | Effect |
|--------|--------|
| `--tsRuntimeModule <specifier>` | import the runtime from this module specifier (such as the npm package) instead of emitting `corvus-runtime.ts` alongside the module |
| `--tsModulePerType` | emit one module per type plus a barrel `index.ts` (for tree-shaking and IDE navigation) instead of a single `generated.ts` |
| `--codeGenerationMode SchemaEvaluationOnly` | emit validators only — no interfaces, builders, or mutators |
| `--assertFormat false` | treat `format` as an annotation: recorded, but not enforced by `evaluate` |

## Next

- [Reading and validating](./reading-and-validating.md) — the read surface and the evaluators.
- [Mutation](./mutation.md) — `build`, `patch`, and `produce` over JSON bytes.
- [The type surface](./the-type-surface.md) — every JSON Schema construct and its generated TypeScript.
- [Value types](./value-types.md) — branded formats, exact numbers, dates, and times.
- [Examples](./examples/) — 19 runnable, worked recipes.
- [Playground](../playground-typescript/) — generate and run in the browser.
