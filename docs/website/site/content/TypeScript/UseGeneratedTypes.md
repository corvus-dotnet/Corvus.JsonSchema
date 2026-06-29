---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-06-29T00:00:00.0+00:00
Title: "Use the generated types"
---
## Use the generated types

Each type in the schema produces a **companion object** that carries every operation for that type — for `person.json` that is `Person`. The companion has the same name as the type, so you import it once and use it both as a type and as a value:

```typescript
import { Person, BirthDate } from "./generated/generated.js";

const decoder = new TextDecoder();

// Build a Person from plain values. The result is canonical UTF-8 JSON bytes.
const bytes = Person.build({
  familyName: "Brontë",
  givenName: "Anne",
  birthDate: BirthDate.from("1820-01-17"), // a format:date factory; throws on a malformed date
});

console.log(decoder.decode(bytes));
// {"familyName":"Brontë","givenName":"Anne","birthDate":"1820-01-17"}

// Validate untrusted input. evaluate accepts the JSON bytes directly (it decodes them) or an
// already-parsed value, returns a boolean, and never throws.
if (Person.evaluate(bytes)) {
  // The value matched the schema. parse() decodes and returns it typed — no JSON.parse or cast.
  const person = Person.parse(bytes);
  console.log(person.familyName); // "Brontë"
}

console.log(Person.evaluate({ givenName: "Anne" })); // false — familyName is required
```

The root type's companion (`Person`) is also the module's `default` export, so `import Person from "./generated/generated.js"` gives you the document entry point without naming the type.

For each type in the schema, the generated module emits an **`interface`** describing the value's shape, and a **companion object** of the same name carrying its operations: `evaluate` (validate), `build` / `buildCanonical` (construct UTF-8 JSON bytes), `patch` / `produce` (edit bytes, splicing only what changed), a `from` factory for each `format`, and `match` for each `oneOf` union.
