---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-06-29T00:00:00.0+00:00
Title: "TypeScript Examples — JSON Schema Patterns"
---
This section collects worked examples of generating TypeScript from JSON Schema, each covering one construct at a time. Every recipe is a schema, the generated TypeScript, a runnable demo, and a short walkthrough README; the recipes are ordered by increasing complexity.

## Running the examples

The recipes live in [`docs/typescript/examples`](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples) in the repository. From that directory:

```bash
npm install          # once
npm run build        # type-check and compile every recipe (strict TypeScript)
node dist/001-data-object/demo.js   # run a recipe's demo
```

Regenerate a recipe's `generated.ts` from its schema with `npm run regenerate`.

## Recipes

| # | Recipe | JSON Schema construct |
|---|--------|-----------------------|
| 001 | [data-object](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/001-data-object) | object, required/optional, scalar + `format` |
| 002 | [validation](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/002-validation) | `minLength` / `minimum` / `pattern` / … constraints |
| 003 | [references](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/003-references) | `$ref`, `$defs` |
| 004 | [open-and-closed](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/004-open-and-closed) | `additionalProperties`, `unevaluatedProperties: false` |
| 005 | [extending](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/005-extending) | `allOf` (base + extension) |
| 006 | [constraining](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/006-constraining) | `allOf` (constrain a base) |
| 007 | [arrays](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/007-arrays) | `items` (homogeneous) |
| 008 | [nested-arrays](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/008-nested-arrays) | arrays of higher rank |
| 009 | [tuples](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/009-tuples) | `prefixItems` / positional `items` |
| 010 | [mixins](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/010-mixins) | `allOf` (multiple bases) |
| 011 | [unions](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/011-unions) | `oneOf` + `match` (untagged) |
| 012 | [discriminated-unions](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/012-discriminated-unions) | `oneOf` + `const` discriminator |
| 013 | [string-enums](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/013-string-enums) | `enum` → string-literal union |
| 014 | [numeric-enums](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/014-numeric-enums) | numeric `enum` / `oneOf` + `const` |
| 015 | [maps](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/015-maps) | `additionalProperties` / `patternProperties` |
| 016 | [mutation](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/016-mutation) | `build` / `patch` / `produce` |
| 017 | [conditional](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/017-conditional) | `if` / `then` / `else` |
| 018 | [defaults](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/018-defaults) | `default` |
| 019 | [formats](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs/typescript/examples/019-formats) | `date` / `uuid` / `uri` / … branded types |
