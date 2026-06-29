# TypeScript Patterns - Simple Data Objects

This recipe demonstrates how to define a simple data object of primitive values in JSON Schema, and how the Corvus.Text.Json **TypeScript** generator produces an idiomatic `readonly` interface plus a small, allocation-lean API to evaluate, read, build and mutate it.

## The Pattern

It is very common to define a simple data object composed of primitive values for exchange through an API.

The generator emits, for the root schema, a `readonly` **interface** whose name is derived from the schema (here `Person`). Required properties are `name: T`; optional properties are `name?: T`. JSON Schema scalar types map to TypeScript primitives (`string`, `number`, `boolean`), and a `format` keyword becomes a **branded** type with a validating factory (here `birthDate` is `Brand<string, "date">` with `BirthDate.from(...)`).

There is **nothing to wrap**: a JSON value parsed with `JSON.parse` *is* a `Person` once `Person.evaluate` accepts it — you read it with ordinary property access. The generator adds only what the language can't express for free:

- `Person.evaluate(value)` / `Person.evaluate(value, ev)` — an AOT-compiled boolean evaluator (no exceptions, no error-object graph).
- `Person.build(props)` — construct canonical UTF-8 JSON **bytes** from plain values.
- `Person.patch(source, changes, removals?)` — change only the named fields, spliced at the byte level (unchanged bytes copied verbatim).
- `Person.produce(source, recipe)` — an immer-style `produce(draft => …)` over a typed, mutable `Draft<Person>`.
- `BirthDate.from(value)` — a validating factory for the `date`-formatted branded type.

## The Schema

File: [`person.json`](./person.json)

```json
{
  "title": "Person",
  "type": "object",
  "required": ["familyName", "givenName"],
  "properties": {
    "familyName": { "type": "string" },
    "givenName": { "type": "string" },
    "otherNames": { "type": "string" },
    "birthDate": { "type": "string", "format": "date" },
    "height": { "type": "number" }
  }
}
```

The generated interface is:

```typescript
export interface Person {
  readonly birthDate?: BirthDate;   // Brand<string, "date">
  readonly familyName: string;      // required
  readonly givenName: string;       // required
  readonly height?: number;
  readonly otherNames?: string;
}
```

## Generated Code Usage

[Example code](./demo.ts)

### Build from plain values

`{Type}.build` produces canonical UTF-8 JSON bytes (the wire / persistence shape):

```typescript
const bytes = Person.build({
  familyName: "Brontë",
  givenName: "Anne",
  birthDate: BirthDate.from("1820-01-17"),
  height: 1.52,
});
// {"familyName":"Brontë","givenName":"Anne","birthDate":"1820-01-17","height":1.52}
```

### Evaluate untrusted input

`Person.evaluate` is a boolean — no exceptions, no allocated error graph:

```typescript
Person.evaluate(bytes);                     // true — evaluate accepts bytes (decodes them) or a value
Person.evaluate({ givenName: "Anne" });     // false — familyName is required
```

### Read

Once `Person.evaluate` accepts it, the value *is* a `Person`; read it with ordinary property access. `Person.parse` decodes the bytes (or JSON.parses a string) and returns it typed:

```typescript
const person = Person.parse(bytes);
person.familyName;                  // "Brontë"
person.birthDate;                   // "1820-01-17"
person.otherNames !== undefined;    // false — optional and absent
```

### Patch — change only what you name

```typescript
const patched = Person.patch(bytes, { height: 1.55 });
// only "height" is rewritten; every other byte is copied through
```

### Produce — immer-style recipe

```typescript
const produced = Person.produce(bytes, (d) => {
  d.birthDate = BirthDate.from("1984-06-03");
});
```

### Remove an optional property

```typescript
const removed = Person.patch(bytes, {}, ["otherNames"]);
```

## Running the Example

From `docs/typescript/examples/` (run `npm install` once):

```bash
npm run build
node dist/001-data-object/demo.js
```

## Related Patterns

- [002-validation](../002-validation/) — adding validation constraints
- [013-string-enums](../013-string-enums/) — `enum` as a string-literal union
- [016-mutation](../016-mutation/) — `produce` / `patch` / `build` in depth

## Frequently Asked Questions

### How do JSON Schema property types map to TypeScript?

`"type": "string"` → `string`, `"number"`/`"integer"` → `number`, `"boolean"` → `boolean`, `"null"` → `null`, an object → a `readonly interface`, an array → `readonly T[]`. A `format` keyword (e.g. `"date"`) becomes a **branded** type (`Brand<string, "date">`) so a plain `string` is *not* assignable without going through the validating `BirthDate.from(...)` factory.

### Why is there no wrapper type to "parse into"?

Because TypeScript already expresses the shape. A `JSON.parse`'d value, once `Person.evaluate`'d, is a sound `Person` — there is nothing to beat reading a plain object you trust. The generated functions cover only what the type system can't: evaluation, byte-level mutation, and construction.

### What is the difference between an absent property and `null`?

An **optional** property (`name?: T`) is absent when the key is missing — read it as `value.name === undefined`. A property typed to include `null` is present with the JSON literal `null`. JSON Schema treats these differently, and so does the generated interface (`?:` vs a `| null` member).

### When do I use `build` vs `patch` vs `produce`?

`{Type}.build` constructs a fresh document from values. `{Type}.patch` is the leanest update — name the fields to change (and any to remove) and it splices them into the source bytes without re-parsing or re-serialising the rest. `{Type}.produce` gives the ergonomic immer-style recipe for deeper or conditional edits, lowering the recorded change-set to the same byte patch.
