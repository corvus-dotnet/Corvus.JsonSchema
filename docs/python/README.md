# Corvus.Text.Json, Python

The Corvus.Text.Json code generator emits idiomatic, type-checked **Python** from JSON Schema. From one schema
you get:

- a **type surface** built from standard typing, `TypedDict` objects, `Literal` and `str | int` enums, `X | Y`
  unions with a generic `match`, branded `NewType` format types with validating factories, precise `tuple[...]`
  tuples, and `Mapping[str, V]` maps;
- **AOT-compiled evaluators**, a boolean `evaluate(value)` that is fully JSON-Schema-compliant across all five
  dialects, with no runtime schema interpretation, and that accepts either the wire bytes or an already-parsed
  value;
- a **byte-level construction API**, `build` / `build_canonical` (RFC 8785) / `parse` / `patch` over UTF-8 JSON
  bytes, where `patch` splices only what changed.

The generated code targets Python 3.12, type-checks clean under both `mypy --strict` and pyright, and imports a
small pure-Python runtime, [`corvus_json_runtime`](../../packages/corvus-json-runtime-py) (numbers as `Decimal`,
temporal values via `whenever`, ECMA-compatible regex via `regex`).

Generate with the CLI:

```bash
corvusjson jsonschema person.json --engine Python --outputPath ./generated
```

This writes a single `generated/__init__.py`. The type is used by name (`Person`), and its companion functions
are module-level (`evaluate`, `parse`, `build_person`, `patch_person`, `from_birth_date`, ...).

## The type surface at a glance

| JSON Schema | Python |
|---|---|
| `object` with properties | `class T(TypedDict, total=False)`, required members as `Required[...]` |
| scalar (`string`/`number`/`integer`/`boolean`) | `str` / `float` / `int` / `bool` |
| `string` + `format` (date, uuid, email, ...) | `NewType` brand + `from_<t>` factory; the 4 temporal formats add `to_temporal_<t>` |
| numeric `format` (int32, decimal, ...) | `NewType` brand + range-checked `from_<t>`; `decimal` adds `to_exact_<t>` |
| `enum` | `Literal[...]` |
| `oneOf` / `anyOf` | `X | Y` alias + `is_<member>` guards + `match_<t>` |
| `array` with `items` | `Sequence[T]` |
| `prefixItems` (tuple) | `tuple[A, B]`, variadic `tuple[A, *tuple[T, ...]]` |
| pure `additionalProperties` map | `Mapping[str, V]` |

## Worked examples

Each recipe under [`examples/`](./examples/) is a schema, the generated Python package, a runnable demo, and a
walkthrough README. Regenerate every recipe and run its checks (`mypy --strict`, pyright, and the demo) with
`pwsh examples/regenerate.ps1 -Check`.

| # | Recipe | JSON Schema construct |
|---|--------|-----------------------|
| 001 | [data-object](./examples/001-data-object/) | object, required/optional, scalar + `format` brand |
| 002 | [validation](./examples/002-validation/) | `minLength` / `minimum` / `pattern` / `format` constraints |
| 003 | [references](./examples/003-references/) | `$ref`, `$defs` |
| 004 | [open-and-closed](./examples/004-open-and-closed/) | `additionalProperties`, `unevaluatedProperties: false` |
| 005 | [extending](./examples/005-extending/) | `allOf` (base + extension) |
| 006 | [constraining](./examples/006-constraining/) | `allOf` (constrain a base) |
| 007 | [arrays](./examples/007-arrays/) | `items` (homogeneous) |
| 008 | [nested-arrays](./examples/008-nested-arrays/) | arrays of higher rank |
| 009 | [tuples](./examples/009-tuples/) | `prefixItems` / positional `items` |
| 010 | [mixins](./examples/010-mixins/) | `allOf` (multiple bases) |
| 011 | [unions](./examples/011-unions/) | `oneOf` + `match` (untagged) |
| 012 | [discriminated-unions](./examples/012-discriminated-unions/) | `oneOf` + `const` discriminator |
| 013 | [string-enums](./examples/013-string-enums/) | `enum` to `Literal[...]` |
| 014 | [numeric-enums](./examples/014-numeric-enums/) | numeric `enum` |
| 015 | [maps](./examples/015-maps/) | `additionalProperties` map |
| 016 | [mutation](./examples/016-mutation/) | `build` / `patch` (members + arrays) / `with_defaults` |
| 017 | [conditional](./examples/017-conditional/) | `if` / `then` / `else` |
| 018 | [defaults](./examples/018-defaults/) | `default` |
| 019 | [formats](./examples/019-formats/) | `format` brands + `to_temporal` / `to_exact` |
| 020 | [json-patch](./examples/020-json-patch/) | RFC 7396 merge patch |

## Guides

- [reading-and-validating](./reading-and-validating.md) — `evaluate`, `parse`, the boolean/no-exception model
- [the-type-surface](./the-type-surface.md) — the full JSON-Schema-to-Python type mapping
- [mutation](./mutation.md) — `build` / `build_canonical` / byte-native `patch` / `with_defaults`
- [json-patch](./json-patch.md) — RFC 7396 merge patch (and the RFC 6902 gap)
- [code-generation](./code-generation.md) — the CLI, output layout, and the type-check gate
- [runtime](./runtime.md) — the `corvus_json_runtime` package
