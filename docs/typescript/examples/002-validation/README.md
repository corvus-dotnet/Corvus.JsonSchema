# TypeScript Patterns - Validation

This recipe adds **constraints** to a data object (length, range, pattern, numeric step, format) and shows that the generated `Registration.evaluate` enforces them while the interface stays the plain shape.

## The Pattern

A JSON Schema validation keyword (`minLength`, `pattern`, `minimum`/`maximum`, `multipleOf`, `format`, …) constrains a value beyond its type. So the generator splits the two concerns: the **interface is the shape**, and **`Registration.evaluate` is the constraint authority**. Evaluation returns a plain `boolean`: a rejection is `false`, with no thrown exception and no allocated error graph. (Collecting *why* a value failed, the per-keyword results, is a separate opt-in, like the engine's results collector.)

A `format` keyword additionally produces a **branded** type with an eager factory (`Email.from`) that throws on a malformed value at construction. This is useful when you want to fail fast on one field rather than evaluate a whole document.

## The Schema

File: [`registration.json`](./registration.json)

```json
{
  "title": "Registration",
  "type": "object",
  "required": ["username", "age", "email"],
  "properties": {
    "username": { "type": "string", "minLength": 3, "maxLength": 20, "pattern": "^[a-z][a-z0-9_]*$" },
    "age": { "type": "integer", "minimum": 18, "maximum": 120 },
    "email": { "type": "string", "format": "email" },
    "score": { "type": "number", "exclusiveMinimum": 0, "multipleOf": 0.5 }
  }
}
```

## Generated Code Usage

[Example code](./demo.ts)

### Evaluate a value

`Registration.evaluate` returns a boolean, `true` when every constraint holds:

```typescript
const bytes = Registration.build({ username: "ada_lovelace", age: 36, email: Email.from("ada@example.com"), score: 4.5 });
Registration.evaluate(bytes); // true
```

Each constraint rejection is `false`:

```typescript
Registration.evaluate({ username: "ab",  age: 36, email: "a@b.com" });               // false — minLength 3
Registration.evaluate({ username: "Ada", age: 36, email: "a@b.com" });               // false — pattern ^[a-z][a-z0-9_]*$
Registration.evaluate({ username: "ada", age: 17, email: "a@b.com" });               // false — minimum 18
Registration.evaluate({ username: "ada", age: 36, email: "a@b.com", score: 0.3 });   // false — multipleOf 0.5
```

### Fail fast on a single formatted field

```typescript
Email.from("ada@example.com"); // Email
Email.from("not-an-email");    // throws FormatError("email")
```

## Running the Example

From `docs/typescript/examples/` (run `npm install` once):

```bash
npm run build
node dist/002-validation/demo.js
```

## Related Patterns

- [001-data-object](../001-data-object/): the shape without constraints
- [019-formats](../019-formats/): branded format types in depth

## Frequently Asked Questions

### Why does `Registration.evaluate` return a boolean instead of a list of errors?

The common path ("is this value acceptable?") wants a fast yes/no with no allocation, so that is the default. The detailed *why* (per-keyword failures, instance/keyword locations) is produced by passing an optional results collector to the evaluator; the boolean is the zero-overhead shape of the same call.

### Are numeric constraints exact?

Yes. `minimum`/`maximum`/`multipleOf` are evaluated against the number's source text using big-integer arithmetic, not a lossy IEEE-754 double, so `multipleOf: 0.1` and large-integer bounds behave mathematically, not according to floating-point rounding.

### Where do constraints live, the type or the evaluator?

The evaluator. A constraint like `minLength` or `multipleOf` can't be expressed in a TypeScript type, so the interface carries only the shape (`username: string`) and `Registration.evaluate` carries the constraint. A `format`, however, is surfaced in *both*: as a branded type (so a raw `string` isn't assignable) and as a runtime check.
