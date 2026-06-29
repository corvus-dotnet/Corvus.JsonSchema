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
| 001 | [data-object](/typescript/examples/data-object.html) | object, required/optional, scalar + `format` |
| 002 | [validation](/typescript/examples/validation.html) | `minLength` / `minimum` / `pattern` / … constraints |
| 003 | [references](/typescript/examples/references.html) | `$ref`, `$defs` |
| 004 | [open-and-closed](/typescript/examples/open-and-closed.html) | `additionalProperties`, `unevaluatedProperties: false` |
| 005 | [extending](/typescript/examples/extending.html) | `allOf` (base + extension) |
| 006 | [constraining](/typescript/examples/constraining.html) | `allOf` (constrain a base) |
| 007 | [arrays](/typescript/examples/arrays.html) | `items` (homogeneous) |
| 008 | [nested-arrays](/typescript/examples/nested-arrays.html) | arrays of higher rank |
| 009 | [tuples](/typescript/examples/tuples.html) | `prefixItems` / positional `items` |
| 010 | [mixins](/typescript/examples/mixins.html) | `allOf` (multiple bases) |
| 011 | [unions](/typescript/examples/unions.html) | `oneOf` + `match` (untagged) |
| 012 | [discriminated-unions](/typescript/examples/discriminated-unions.html) | `oneOf` + `const` discriminator |
| 013 | [string-enums](/typescript/examples/string-enums.html) | `enum` → string-literal union |
| 014 | [numeric-enums](/typescript/examples/numeric-enums.html) | numeric `enum` / `oneOf` + `const` |
| 015 | [maps](/typescript/examples/maps.html) | `additionalProperties` / `patternProperties` |
| 016 | [mutation](/typescript/examples/mutation.html) | `build` / `patch` / `produce` |
| 017 | [conditional](/typescript/examples/conditional.html) | `if` / `then` / `else` |
| 018 | [defaults](/typescript/examples/defaults.html) | `default` |
| 019 | [formats](/typescript/examples/formats.html) | `date` / `uuid` / `uri` / … branded types |
