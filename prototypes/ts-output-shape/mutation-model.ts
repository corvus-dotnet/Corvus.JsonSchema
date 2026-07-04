// The mutation API (design §5.7): idiomatic immer-style `produce`, type-checked under --strict.
// `Draft<T>`/`JsonDocument<T>`/`produce` are runtime-library helpers (NOT per-type generated noise),
// so they come from @endjin/corvus-json-runtime; this file shows a per-type document USING them. The recipe
// assigns to the draft; the recorded change-set lowers to a Model C byte patch (proven runnable in
// `prototypes/rmw-scanner/produce.mjs`) and doubles as RFC 6902 JSON Patch / LSP TextEdits.

import { type JsonDocument, produce } from "./runtime"; // real output: "@endjin/corvus-json-runtime"

export interface Address {
  readonly street: string;
  readonly city: string;
}
export interface Person {
  readonly name: string;
  readonly age?: number;
  readonly address: Address;
  readonly tags: readonly string[];
}

export const doc: JsonDocument<Person> = {
  value: { name: "Ada", age: 30, address: { street: "1 St", city: "Anytown" }, tags: ["math"] },
};

// idiomatic: "mutate" the draft, get a new immutable document
const next: JsonDocument<Person> = produce(doc, (d) => {
  d.age = 31; // scalar
  d.name = "Ada Lovelace"; // scalar
  d.address.city = "London"; // nested, typed
  d.tags.push("new"); // array mutation (draft array is mutable)
});

// type safety holds THROUGH the draft:
// @ts-expect-error -- age must be a number
produce(doc, (d) => { d.age = "oops"; });
// @ts-expect-error -- city is a string
produce(doc, (d) => { d.address.city = 123; });
// @ts-expect-error -- cannot push a number into a string[] draft
produce(doc, (d) => { d.tags.push(5); });

export const demoMutation = [next];
