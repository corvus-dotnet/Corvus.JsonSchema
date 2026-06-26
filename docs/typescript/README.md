# Corvus.Text.Json — TypeScript engine

The Corvus.Text.Json code generator emits idiomatic, high-performance **TypeScript** from JSON Schema, as a peer engine to the C# generator. From one schema you get:

- a `readonly` **type surface** — interfaces, string-literal-union enums, discriminated unions with `match*`, branded format types, typed arrays/tuples, maps;
- **AOT-compiled validators** — a boolean `validate(value)` per type, fully JSON-Schema-compliant (all 5 dialects), no runtime schema interpretation;
- a **byte-level mutation API** — `build*` / `patch*` / `produce*` over canonical UTF-8 JSON bytes, splicing only what changed.

Generate with the CLI:

```bash
corvusjson jsonschema person.json --engine TypeScript --outputPath ./out
```

## Worked examples

Each recipe under [`examples/`](./examples/) is a schema + generated TypeScript + a runnable demo + a walkthrough README, mirroring the C# [`docs/ExampleRecipes`](../ExampleRecipes/) set so the two engines cover the same constructs. Run them from `examples/` (`npm install` once, then `npm run build` and `node dist/<recipe>/demo.js`); regenerate the `generated.ts` with `npm run regenerate`.

| # | Recipe | JSON Schema construct | C# parallel |
|---|--------|-----------------------|-------------|
| 001 | [data-object](./examples/001-data-object/) | object, required/optional, scalar + `format` | 001-DataObject |
| 002 | validation | `minLength`/`minimum`/`pattern`/… constraints | 002-DataObjectValidation |
| 003 | reusing-types | `$ref`, `$defs` | 003-ReusingCommonTypes |
| 004 | open-closed | `additionalProperties`, `unevaluatedProperties:false` | 004-OpenVersusClosedTypes |
| 005 | extending | `allOf` (base + extension) | 005-ExtendingABaseType |
| 006 | constraining | `allOf` (constrain a base) | 006-ConstrainingABaseType |
| 007 | arrays | `items` (homogeneous) | 007-CreatingAStronglyTypedArray |
| 008 | nested-arrays | arrays of higher rank | 008/009 |
| 010 | tuples | `prefixItems` / positional `items` | 010-CreatingTuples |
| 011 | mixins | `allOf` (multiple bases) | 011-InterfacesAndMixInTypes |
| 012 | unions | `oneOf` + `match*` (untagged) | 012-PatternMatching |
| 013 | discriminated-unions | `oneOf` + `const` discriminator | 013-PolymorphismWithDiscriminators |
| 014 | string-enums | `enum` → string-literal union | 014-StringEnumerations |
| 015 | numeric-enums | numeric `enum` / `oneOf`+`const` | 015-NumericEnumerations |
| 016 | maps | `additionalProperties` / `patternProperties` | 016-Maps |
| 018 | mutation | `build` / `patch` / `produce` (RMW) | 018-CreatingAndMutatingObjects |
| 020 | conditional | `if` / `then` / `else` | 020-ConditionalSchemas |
| 021 | defaults | `default` | 021-DefaultValues |
| 022 | formats | `date`/`uuid`/`uri`/… branded types | 022-FormatValidation |

> The C# recipes 017 (type mapping) and 019 (clone/freeze) have no TypeScript counterpart — structural typing makes cross-type assignment free, and parsed values are already plain immutable data. OpenAPI/AsyncAPI/query-language recipes (024+) are not engine features.

## Guides

Prose documentation for the generated API (in progress):

- **getting-started** — installing the runtime, generating, the shape of a module.
- **reading-and-validating** — the `readonly` surface and the `validate` / `evaluate*` boolean validators.
- **mutation** — `build` / `patch` / `produce` and the byte-level Model C engine.
- **the-type-surface** — every construct → its generated TypeScript.
