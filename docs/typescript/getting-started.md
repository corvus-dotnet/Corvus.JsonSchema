# Getting started

The Corvus.Text.Json code generator turns a JSON Schema into a self-contained TypeScript module: a `readonly` type surface, a boolean evaluator, and a byte-level mutation API. This guide covers generating a module and the shape of what you get back.

## Generate a module

Point the CLI at a schema and choose the TypeScript engine:

```bash
corvusjson jsonschema person.json --engine TypeScript --outputPath ./out
```

This writes two files into `./out`:

- **`generated.ts`** — your types, evaluators and mutators (one module per schema).
- **`corvus-runtime.ts`** — the shared runtime the generated code imports. It is the same for every module, so when you generate many schemas you can keep a single copy and point each module's import at it (this is what the examples do — one `corvus-runtime.ts` at the root, imported as `../corvus-runtime.js`).

### Options

| Option | Effect |
|--------|--------|
| `--tsRuntimeModule @endjin/corvus-json-runtime` | import the runtime from the installed npm package instead of re-emitting `corvus-runtime.ts` alongside the module(s) |
| `--tsModulePerType` | emit one module per type plus a barrel `index.ts` (tree-shaking, IDE navigation) instead of a single `generated.ts` |
| `--codeGenerationMode SchemaEvaluationOnly` | emit only the validators (each companion's `evaluate`), no type surface |
| `--assertFormat false` | leave `format` as an annotation — recorded, never enforced at `evaluate` |

## Runtime dependencies

The runtime relies on three small, well-known packages for the parts the language can't do exactly on its own:

```bash
npm install lossless-json @js-temporal/polyfill tr46
```

- `lossless-json` — exact numeric evaluation over a number's source text (so `multipleOf` and large integers behave mathematically, not by floating-point rounding).
- `@js-temporal/polyfill` — date/time/duration values and RFC-accurate `date`/`time`/`date-time` checks.
- `tr46` — IDNA processing for `idn-email`/`idn-hostname`/`iri` formats.

## The shape of a module

For `person.json` you get, in `generated.ts`:

```typescript
import { /* … */ } from "./corvus-runtime.js";

export interface Person {
  readonly familyName: string;       // required -> `name: T`
  readonly givenName: string;
  readonly birthDate?: BirthDate;    // optional -> `name?: T`; a `format` -> a brand
  readonly height?: number;
}

function evaluatePerson(value: unknown, ev: Ev): boolean { /* … */ }          // per-type validator (internal)
function buildPerson(props: Person): Uint8Array { /* … */ }                   // construct
function patchPerson(source: Uint8Array, changes: Partial<Person>, removals?: …): Uint8Array { /* … */ }
function producePerson(source: Uint8Array, recipe: (draft: Draft<Person>) => void): Uint8Array { /* … */ }
function asBirthDate(value: string): BirthDate { /* … */ }                    // branded-format factory

// Each type's operations are grouped on an `export const {Type}` COMPANION object — the public surface —
// declaration-merged with the interface so `Person` is both the type and the value namespace.
export const Person = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePerson(v, fresh(), "", "", results ?? null),
  build: buildPerson, buildCanonical: buildCanonicalPerson, patch: patchPerson, produce: producePerson,
};
export const BirthDate = { evaluate: /* … */, as: asBirthDate, asTemporal: birthDateAsTemporal };
export default Person;   // the root type's companion is the module default export
```

The pieces:

- An **`interface`** per object type (the `readonly` shape), declaration-merged with…
- a **companion** `export const {Type}` per type — the public surface, grouping that type's operations:
  - **`{Type}.evaluate(value, results?)`** — a boolean evaluator (seeds a fresh tracker, optional results collector); every type has one, mirroring .NET's `EvaluateSchema()`. The root type's companion is the module's `default` export.
  - **`{Type}.build` / `.buildCanonical` / `.patch` / `.produce`** — construct and mutate over canonical UTF-8 JSON bytes (`Uint8Array`).
  - **`{Type}.as`** (+ `.asTemporal` / `.asExact`) — branded-format factories.
  - **`{Union}.match`** — exhaustive dispatch over a discriminated union.

## A minimal end-to-end

```typescript
import { Person, BirthDate } from "./out/generated.js";   // `Person` is both the type and the companion

const bytes = Person.build({ familyName: "Brontë", givenName: "Anne", birthDate: BirthDate.as("1820-01-17") });
const value: unknown = JSON.parse(new TextDecoder().decode(bytes));

if (Person.evaluate(value)) {
  const person = value as Person; // sound after evaluation
  console.log(person.familyName);
}
```

## Next

- [reading-and-validating](./reading-and-validating.md) — the read surface and the evaluators.
- [mutation](./mutation.md) — `build` / `patch` / `produce` and the byte-level engine.
- [the-type-surface](./the-type-surface.md) — every JSON Schema construct and its generated TypeScript.
- [examples](./examples/) — 19 runnable, worked recipes.
