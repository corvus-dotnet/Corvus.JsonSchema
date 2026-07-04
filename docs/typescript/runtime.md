# The runtime

The generated TypeScript imports a small runtime library for the parts the language cannot express on its own:
format checks, exact numeric comparison, the byte-level edit engine, and `Temporal` conversions. This guide
covers what the runtime provides, how to supply it, and its dependencies.

## Overview

The runtime is a single ESM module. The generated code imports the primitives it needs from it — validators
call the format and `multipleOf` helpers, builders call the byte engine, and brand factories call the format
checks. It is the same for every generated module, so when you generate many schemas they can share one copy.

## Supplying the runtime

There are two ways to provide the runtime.

### As an npm package (recommended for projects)

```bash
npm install @endjin/corvus-json-runtime
```

Generate with `--tsRuntimeModule @endjin/corvus-json-runtime`, and the generated module imports the runtime
from the package:

```typescript
import { /* … */ } from "@endjin/corvus-json-runtime";
```

This is the right choice when you generate many modules, because they all resolve to one installed copy.

### Self-contained (emitted alongside)

Omit `--tsRuntimeModule`, and the generator writes a `corvus-runtime.ts` next to each generated module, which
the module imports relatively as `./corvus-runtime.js`. You then install the runtime's three dependencies
yourself:

```bash
npm install lossless-json @js-temporal/polyfill tr46
```

This is convenient for a single module or a self-contained drop-in, and is what the
[example recipes](./examples/) do — one `corvus-runtime.ts` at the root, imported by each recipe.

## Dependencies

The runtime relies on three small, well-known packages for the parts the language and standard library do not
cover exactly:

| Package | Used for |
|---|---|
| [`lossless-json`](https://www.npmjs.com/package/lossless-json) | Exact numeric evaluation over a number's source text, so `multipleOf` and large integers behave mathematically rather than by floating-point rounding. |
| [`@js-temporal/polyfill`](https://www.npmjs.com/package/@js-temporal/polyfill) | `Temporal` date, time, and duration values, and RFC-accurate `date` / `time` / `date-time` / `duration` checks. |
| [`tr46`](https://www.npmjs.com/package/tr46) | IDNA processing for the `idn-email`, `idn-hostname`, and `iri` formats. |

## What the runtime provides

- **Format checks** — the RFC-accurate `format` validators (`date`, `time`, `date-time`, `duration`,
  `email` / `idn-email`, `hostname` / `idn-hostname`, `ipv4` / `ipv6`, `uri` / `iri` and relatives, `uuid`,
  `json-pointer`, `regex`, and more), shared by the validators and the brand factories.
- **Exact numeric evaluation** — `multipleOf`, numeric bounds, and large-integer handling over the number's
  source text via `lossless-json`, so results are mathematically exact rather than subject to floating-point
  rounding.
- **The byte-level edit engine** — the splice-based machinery behind `build` / `patch` / `produce` that
  rewrites only the changed spans of a UTF-8 JSON document (see [Mutation](./mutation.md)).
- **`Temporal` conversions** — the `toTemporal()` accessors that turn a branded date or time string into a
  `Temporal.PlainDate`, `ZonedDateTime`, or `Duration` (see [Value types](./value-types.md)).

## Module system and `tsconfig`

The runtime is **ESM-only**, so configure your `tsconfig.json` for ES modules — for example:

```json
{
  "compilerOptions": {
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "target": "ES2022",
    "strict": true
  }
}
```

`"moduleResolution": "NodeNext"` also works under Node. The generated code is written to the strict-TypeScript
standard — it compiles cleanly under `strict` and `exactOptionalPropertyTypes` — so it fits a strict project
without loosening your configuration.

## See also

- [Code generation](./code-generation.md) — the `--tsRuntimeModule` option and the rest of the generator output.
- [Value types](./value-types.md) — the branded formats and `Temporal` accessors the runtime backs.
- [Mutation](./mutation.md) — the byte-level engine the runtime provides.
