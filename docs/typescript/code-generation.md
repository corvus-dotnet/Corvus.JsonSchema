# Code generation

This is the reference for generating TypeScript from a JSON Schema: the `corvusjson` command, its options, the
shape of the output, and the code-generation modes. For a first walkthrough, start with
[Getting Started](./getting-started.md).

## Overview

The generator is a .NET command-line tool that reads a JSON Schema and writes TypeScript. The same engine
backs the C# generator; the `--engine TypeScript` option selects the TypeScript emitter. The output is plain
TypeScript with a small runtime library, so nothing in your project depends on .NET at run time.

## Installation

The generator is a .NET global tool and requires the [.NET SDK](https://dotnet.microsoft.com/download):

```bash
dotnet tool install --global Corvus.Json.Cli
```

This installs the `corvusjson` command.

## Generating a module

```bash
corvusjson jsonschema <schema.json> --engine TypeScript --outputPath <directory>
```

For example:

```bash
corvusjson jsonschema person.json --engine TypeScript --outputPath ./src/generated
```

By default this writes two files into the output directory:

- **`generated.ts`** — the types, validators, and builders for the schema.
- **`corvus-runtime.ts`** — the shared runtime the generated code imports (see [The runtime](./runtime.md)).

## Options

| Option | Default | Effect |
|---|---|---|
| `--engine TypeScript` | C# | Select the TypeScript emitter. |
| `--outputPath <directory>` | | Directory for the generated files. |
| `--tsRuntimeModule <specifier>` | emit `corvus-runtime.ts` | Import the runtime from this module specifier (such as the npm package) instead of emitting it alongside the module. |
| `--tsModulePerType` | single file | Emit one module per type plus a barrel `index.ts`. |
| `--codeGenerationMode <mode>` | `TypeGeneration` | `TypeGeneration` (types and validators) or `SchemaEvaluationOnly` (validators only). |
| `--assertFormat <bool>` | `true` | Whether `format` keywords are enforced by `evaluate`. |

### The runtime module

By default the generator writes a self-contained `corvus-runtime.ts` next to the module. Pass
`--tsRuntimeModule @endjin/corvus-json-runtime` to import the runtime from the installed npm package instead —
the right choice when you generate many modules, because they all resolve to one copy:

```bash
corvusjson jsonschema person.json --engine TypeScript \
  --tsRuntimeModule @endjin/corvus-json-runtime --outputPath ./src/generated
```

With a bare package specifier the generator emits only `generated.ts`. See [The runtime](./runtime.md).

### Single file vs. module per type

By default the generator emits a single `generated.ts` containing every type. Pass `--tsModulePerType` to emit
one module per type plus a barrel `index.ts` that re-exports them — better for tree-shaking and IDE navigation
in large schemas.

### Code-generation modes

`--codeGenerationMode` selects what is emitted:

- **`TypeGeneration`** (default) — the full surface: the `interface` and type aliases, the companion objects
  with `build` / `patch` / `produce` / `evaluate`, the brand factories, and the validators.
- **`SchemaEvaluationOnly`** — validators only. Each type's companion carries just `evaluate`, with no
  interfaces, brands, or builders. Use it for a server that only needs to check request or response bodies
  against a schema; it is the TypeScript counterpart of the C# standalone schema evaluator.

### Format assertion

By default `format` keywords (`date`, `uuid`, `uri`, and so on) are *asserted*: `evaluate` rejects a value
whose format is invalid, and a `format` becomes a [branded type](./value-types.md). Pass `--assertFormat false`
to treat `format` as an annotation — recorded, but not enforced by `evaluate` (the JSON Schema 2020-12
default).

## The generated surface

For each type in the schema the generator emits a **companion object** named after the type, declaration-merged
with the type's `interface` or alias, so the name is both the type and the place its operations live:

| Member | Present when | Purpose |
|---|---|---|
| `Type.evaluate(value, results?)` | always | Validate a value against the schema; returns a `boolean`. |
| `Type.build(props)` / `Type.buildCanonical(props)` | type surface | Construct UTF-8 JSON bytes (canonical per RFC 8785). |
| `Type.patch(bytes, changes)` / `Type.produce(bytes, recipe)` | type surface | Edit existing bytes, splicing only what changed. |
| `Type.from(value)` | `format` brands | Validate a plain value and brand it. |
| `Type.match(value, cases)` | `oneOf` unions | Exhaustive dispatch over the union's branches. |

The root type's companion is the module's `default` export. See [Reading and validating](./reading-and-validating.md),
[The type surface](./the-type-surface.md), and [Mutation](./mutation.md) for each of these in depth.

## JSON Schema draft support

The generator supports JSON Schema **draft 4, 6, 7, 2019-09, and 2020-12**, selected from the schema's
`$schema` keyword and defaulting to 2020-12. The generated validators pass the full JSON-Schema-Test-Suite
across all five dialects, including `format` assertion.

## See also

- [Getting Started](./getting-started.md) — the first end-to-end walkthrough.
- [The runtime](./runtime.md) — the runtime library the generated code imports, and how to supply it.
- [The type surface](./the-type-surface.md) — every JSON Schema construct and its generated TypeScript.
- [Reading and validating](./reading-and-validating.md) — the read surface and the evaluators.
