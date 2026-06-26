# Reading and evaluating

This guide covers the read side of a generated module: the `readonly` type surface and the boolean evaluators.

## Reading: there is nothing to wrap

The generated `interface` describes the parsed value directly. A value from `JSON.parse`, once it passes evaluation, *is* an instance of the type — you read it with ordinary property access, indexing and iteration. There is no wrapper object, no accessor layer, and no allocation beyond the parse itself.

```typescript
const person = JSON.parse(text) as Person;
person.familyName;                 // string
person.otherNames !== undefined;   // optional, absent?
```

Optionality is honest: a required property is `name: T`, an optional one is `name?: T`. An absent optional reads as `undefined` (the key is missing); a property that may be the JSON literal `null` is typed `| null`. The two are distinct in JSON Schema and in the generated type.

## Evaluating: a boolean by default

`evaluateRoot(value)` evaluates a whole document and returns a `boolean` — `true` if every constraint holds. It is the module's `default` export.

```typescript
import evaluate from "./generated.js";     // the default export is evaluateRoot
evaluate(value);                           // true | false
```

A rejection is just `false`. There is no thrown exception and no allocated error graph on the common "is this acceptable?" path — that keeps validation cheap on hot request paths. (Producing the *why* — per-keyword failures with instance and keyword locations — is done by passing an optional results collector to the evaluator; the boolean is the zero-overhead shape of the same call.)

### Why it's called `evaluateRoot`, not `validate`

The function is an *evaluator*: it walks the schema collecting evaluation results (which keywords applied to which locations) — that's what `unevaluatedProperties`/`unevaluatedItems` need, and what a results collector reports. "Validate" would describe only the boolean outcome; "evaluate" describes what it actually does.

## Per-type evaluators

Every type gets its own `evaluate{Type}(value, ev)`. These take an evaluation tracker (`Ev`) because they **compose**: a parent threads its tracker into its children so that in-place applicators (`allOf`, `if`, `$ref`) and `unevaluated*` see a consistent picture. `evaluateRoot` is just the convenience that seeds a fresh tracker for a whole-document check:

```typescript
export const evaluateRoot = (v: unknown): boolean => evaluatePerson(v, fresh());
```

You rarely call `evaluate{Type}` directly — reach for `evaluateRoot` (or the `default`) unless you are composing evaluation yourself.

## Narrowing unions

A `oneOf`/`anyOf` generates a union with per-branch type guards and an exhaustive `match*`:

```typescript
if (isCircle(shape)) {
  shape.radius;     // narrowed to Circle
}

const area = matchShape(shape, {
  circle: (c) => Math.PI * c.radius * c.radius,
  rectangle: (r) => r.width * r.height,
});
```

`match*` is exhaustive by construction — adding a branch to the schema makes a missing handler a compile error.

## Trusting evaluated data

After `evaluateRoot(value)` returns `true`, `value as T` is sound: the value matched the schema, so the assertion isn't a lie. A common pattern at a trust boundary (an HTTP body, a queue message) is *evaluate then cast*:

```typescript
const incoming: unknown = JSON.parse(body);
if (!evaluateRoot(incoming)) return badRequest();
const order = incoming as Order;   // sound from here on
```

## See also

- [the-type-surface](./the-type-surface.md) — what each construct reads as.
- [mutation](./mutation.md) — producing changed documents.
