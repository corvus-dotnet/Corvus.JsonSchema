// What the generator emits for OBJECTS (design §5.1). Type-checked with
// --strict --exactOptionalPropertyTypes (so `?:` means exactly "T or absent").
//
// Mapping rule: a property in `required` -> non-optional; not in `required` -> `?:`.
// The C# V5 `Undefined` value-kind sentinel is intentionally DROPPED — TS idiom is the
// `?:` optional modifier (+ `in`/`hasOwnProperty`), not a distinct Undefined value.

import { Failure } from "./runtime"; // in real output: from "@corvus/json-runtime"

export interface Address {
  readonly street: string;
  readonly city: string;
  readonly postcode?: string; // optional (not in `required`)
}

export interface Person {
  readonly name: string; // required
  readonly age?: number; // optional
  readonly address: Address; // required; reference to another generated type
}

// AOT validator surface (the generator emits this body — design §5.4; here a hand-written reference
// equivalent so the model is runnable as well as type-checked).
export function validatePerson(value: unknown): Failure | undefined {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return { path: "", keyword: "type", detail: "object" };
  }
  const o = value as Record<string, unknown>;
  if (!("name" in o)) { return { path: "/name", keyword: "required" }; }
  if (typeof o.name !== "string") { return { path: "/name", keyword: "type", detail: "string" }; }
  if (!("address" in o)) { return { path: "/address", keyword: "required" }; }
  if ("age" in o && typeof o.age !== "number") { return { path: "/age", keyword: "type", detail: "number" }; }
  return undefined;
}
export function isPerson(value: unknown): value is Person {
  return validatePerson(value) === undefined;
}

// consumer code: optional is read with an `undefined` check (idiomatic)
export function greet(p: Person): string {
  return p.age === undefined ? `Hi ${p.name}` : `Hi ${p.name} (age ${p.age})`;
}

// omitting an optional property is allowed
const ok: Person = { name: "Ada", address: { street: "1 St", city: "London" } };

// @ts-expect-error -- exactOptionalPropertyTypes: explicit `undefined` is NOT allowed for `age?: number`
const explicitUndefined: Person = { name: "Ada", age: undefined, address: { street: "1 St", city: "London" } };

// @ts-expect-error -- required property `name` is missing
const missingRequired: Person = { address: { street: "1 St", city: "London" } };

export const demoObject = [greet(ok), isPerson(ok), explicitUndefined, missingRequired];
