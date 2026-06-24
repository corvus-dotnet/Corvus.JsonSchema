// The mutation API (design §5.7): idiomatic immer-style `produce`, type-checked under --strict.
// `Draft<T>` is the deeply-mutable view of an immutable T (matches immer's `Draft`). The recipe
// assigns to the draft; the recorded change-set lowers to a Model C byte patch (proven runnable in
// `prototypes/rmw-scanner/produce.mjs`) and doubles as RFC 6902 JSON Patch / LSP TextEdits.

export type Draft<T> =
  T extends ReadonlyArray<infer E> ? Draft<E>[] :
  T extends object ? { -readonly [K in keyof T]: Draft<T[K]> } :
  T;

// A document handle. In Model C this wraps the bytes + index; here just the read view.
export interface JsonDocument<T> {
  readonly value: T;
}

// Free-function form (what immer users know). A per-type convenience method `doc.produce(recipe)`
// would lower to the same change-set.
export declare function produce<T>(doc: JsonDocument<T>, recipe: (draft: Draft<T>) => void): JsonDocument<T>;

interface Address {
  readonly street: string;
  readonly city: string;
}
interface Person {
  readonly name: string;
  readonly age?: number;
  readonly address: Address;
  readonly tags: readonly string[];
}

declare const doc: JsonDocument<Person>;

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
