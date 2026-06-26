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

export function evaluatePerson(value: unknown, ev: Ev): boolean { /* … */ }   // per-type evaluator
export function buildPerson(props: Person): Uint8Array { /* … */ }            // construct
export function patchPerson(source: Uint8Array, changes: Partial<Person>, removals?: …): Uint8Array { /* … */ }
export function producePerson(source: Uint8Array, recipe: (draft: Draft<Person>) => void): Uint8Array { /* … */ }
export function asBirthDate(value: string): BirthDate { /* … */ }             // branded-format factory

export const evaluateRoot = (v: unknown): boolean => evaluatePerson(v, fresh());
export default evaluateRoot;
```

The pieces:

- An **`interface`** per object type (the `readonly` shape).
- **`evaluateRoot`** — the document entry point (and the module's `default` export): a boolean evaluator that seeds a fresh evaluation tracker. Per-type `evaluate{Type}` functions take the tracker explicitly and compose.
- **`build` / `patch` / `produce`** — construct and mutate over canonical UTF-8 JSON bytes (`Uint8Array`).
- **`as{Format}`** factories for branded format types.

## A minimal end-to-end

```typescript
import { type Person, evaluateRoot, buildPerson, asBirthDate } from "./out/generated.js";

const bytes = buildPerson({ familyName: "Brontë", givenName: "Anne", birthDate: asBirthDate("1820-01-17") });
const value: unknown = JSON.parse(new TextDecoder().decode(bytes));

if (evaluateRoot(value)) {
  const person = value as Person; // sound after evaluation
  console.log(person.familyName);
}
```

## Next

- [reading-and-validating](./reading-and-validating.md) — the read surface and the evaluators.
- [mutation](./mutation.md) — `build` / `patch` / `produce` and the byte-level engine.
- [the-type-surface](./the-type-surface.md) — every JSON Schema construct and its generated TypeScript.
- [examples](./examples/) — 19 runnable, worked recipes.
