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

**Every** type's companion has an `evaluate` — `Person.evaluate(value)` evaluates a value against `Person` and returns a `boolean` (`true` if every constraint holds), mirroring .NET's `EvaluateSchema()`. The **root** type's companion is the module's `default` export, so it's also the document entry point.

```typescript
import Root, { Person } from "./generated.js";  // the default export is the root type's companion
Person.evaluate(value);                         // true | false
Root.evaluate(value);                           // same, via the default export
```

A rejection is just `false`. There is no thrown exception and no allocated error graph on the common "is this acceptable?" path — that keeps validation cheap on hot request paths. (Producing the *why* — per-keyword failures with instance and keyword locations — is done by passing an optional results collector as the second argument; the boolean is the zero-overhead shape of the same call.)

### Why it's `.evaluate`, not `.validate`

It's an *evaluator*: it walks the schema collecting evaluation results (which keywords applied to which locations) — that's what `unevaluatedProperties`/`unevaluatedItems` need, and what a results collector reports. "Validate" would describe only the boolean outcome; "evaluate" describes what it actually does.

## Composition (under the companion)

Internally every type has an `evaluate{Type}(value, ev, …)` that takes an evaluation tracker (`Ev`) because validators **compose**: a parent threads its tracker into its children so that in-place applicators (`allOf`, `if`, `$ref`) and `unevaluated*` see a consistent picture. `{Type}.evaluate` is the public convenience that seeds a fresh tracker for a whole-value check — you don't see the tracker.

## Detailed results

`{Type}.evaluate(value, results?)` takes an optional `Results` collector. Omit it (the default) for the zero-allocation boolean fast path; pass one to gather **why** a value failed — per-keyword failures carrying `instanceLocation` / `keywordLocation` / `absoluteKeywordLocation` (and annotations in verbose mode) — which you can standardise to JSON-Schema basic/detailed output with `toOutput`.

## Validators-only output

A generated module carries both the type surface and the validators by default. When you only need validation — a server checking request/response bodies, for instance — generate with `--codeGenerationMode SchemaEvaluationOnly`: each companion then carries just `evaluate`, with no interfaces, brands, or mutators.

## Narrowing unions

A `oneOf`/`anyOf` generates a union with per-branch type guards and an exhaustive `match*`:

```typescript
if (Circle.is(shape)) {
  shape.radius;     // narrowed to Circle
}

const area = Shape.match(shape, {
  circle: (c) => Math.PI * c.radius * c.radius,
  rectangle: (r) => r.width * r.height,
});
```

`{Union}.match` is exhaustive by construction — adding a branch to the schema makes a missing handler a compile error.

## Trusting evaluated data

After `Order.evaluate(value)` returns `true`, `value as T` is sound: the value matched the schema, so the assertion isn't a lie. A common pattern at a trust boundary (an HTTP body, a queue message) is *evaluate then cast*:

```typescript
const incoming: unknown = JSON.parse(body);
if (!Order.evaluate(incoming)) return badRequest();
const order = incoming as Order;   // sound from here on
```

## See also

- [the-type-surface](./the-type-surface.md) — what each construct reads as.
- [mutation](./mutation.md) — producing changed documents.
