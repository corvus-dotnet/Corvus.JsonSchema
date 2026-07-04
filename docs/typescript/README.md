# Corvus.Text.Json — TypeScript

The Corvus.Text.Json code generator emits idiomatic, high-performance **TypeScript** from JSON Schema. From one schema you get:

- a `readonly` **type surface** — interfaces, string-literal-union enums, discriminated unions with `match*`, branded format types, typed arrays/tuples, maps;
- **AOT-compiled evaluators** — a boolean `evaluate` on every type's companion (`Type.evaluate(value)`), fully JSON-Schema-compliant (all five dialects), with no runtime schema interpretation;
- a **byte-level mutation API** — `build*` / `buildCanonical*` (RFC 8785) / `patch*` / `produce*` over UTF-8 JSON bytes, splicing only what changed.

Options: `--tsModulePerType` (one module per type + a barrel `index.ts`), `--codeGenerationMode SchemaEvaluationOnly` (validators only), `--tsRuntimeModule @endjin/corvus-json-runtime` (import the runtime as a package).

Generate with the CLI:

```bash
corvusjson jsonschema person.json --engine TypeScript --outputPath ./out
```

## Worked examples

Each recipe under [`examples/`](./examples/) is a schema + the generated TypeScript + a runnable demo + a walkthrough README. Run them from `examples/` (`npm install` once, then `npm run build` and `node dist/<recipe>/demo.js`); regenerate the `generated.ts` with `npm run regenerate`.

| # | Recipe | JSON Schema construct |
|---|--------|-----------------------|
| 001 | [data-object](./examples/001-data-object/) | object, required/optional, scalar + `format` |
| 002 | validation | `minLength` / `minimum` / `pattern` / … constraints |
| 003 | references | `$ref`, `$defs` |
| 004 | open-and-closed | `additionalProperties`, `unevaluatedProperties: false` |
| 005 | extending | `allOf` (base + extension) |
| 006 | constraining | `allOf` (constrain a base) |
| 007 | arrays | `items` (homogeneous) |
| 008 | nested-arrays | arrays of higher rank |
| 009 | tuples | `prefixItems` / positional `items` |
| 010 | mixins | `allOf` (multiple bases) |
| 011 | unions | `oneOf` + `match*` (untagged) |
| 012 | discriminated-unions | `oneOf` + `const` discriminator |
| 013 | string-enums | `enum` → string-literal union |
| 014 | numeric-enums | numeric `enum` / `oneOf` + `const` |
| 015 | maps | `additionalProperties` / `patternProperties` |
| 016 | mutation | `build` / `patch` / `produce` |
| 017 | conditional | `if` / `then` / `else` |
| 018 | defaults | `default` |
| 019 | formats | `date` / `uuid` / `uri` / … branded types |

## Guides

- [getting-started](./getting-started.md) — generating, the runtime, the shape of a module.
- [reading-and-validating](./reading-and-validating.md) — the `readonly` surface and the `Type.evaluate` evaluators.
- [mutation](./mutation.md) — `build` / `patch` / `produce` and the byte-level engine.
- [the-type-surface](./the-type-surface.md) — every construct → its generated TypeScript.
- [value-types](./value-types.md) — branded string + numeric formats, exact / arbitrary-precision numbers, typed-array views, and `Temporal` dates and times.
