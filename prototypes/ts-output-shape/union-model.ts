// What the generator emits for `oneOf` / `anyOf` and the V5 `Match()` analog.
// This file is hand-written to MATCH the proposed generated output, then type-checked
// (tsc --strict --noEmit) to prove the union model narrows correctly and is exhaustive.
//
// Mapping from V5 C#:
//   C# Match<TContext,TResult>(... per-branch Matcher delegates, defaultMatch)  ->  matchX(value, { branch: ... })
//   C# TryGetAs{Branch}(out T)                                                   ->  isX(v): v is X  (type guard)
//   C# discriminator fast-path                                                   ->  switch on the literal discriminant
//   (TS adds: the type system narrows for free — better ergonomics than C#)

import { assertNever } from "./runtime"; // in real output: from "@endjin/corvus-json-runtime"

// =====================================================================
// CASE 1 — DISCRIMINATED union: oneOf:[Circle, Rectangle], shared const `kind`
//   (the OpenAPI `discriminator` case, and any oneOf with a shared literal const)
//   -> a genuine TS discriminated union; this is the idiomatic sweet spot.
// =====================================================================
export interface Circle {
  readonly kind: "circle";
  readonly radius: number;
}
export interface Rectangle {
  readonly kind: "rectangle";
  readonly width: number;
  readonly height: number;
}
export type Shape = Circle | Rectangle;

// The Match() analog: one typed handler per branch, dispatched on the discriminant
// (the discriminator fast-path). Exhaustive — adding a variant breaks the build.
export function matchShape<R>(
  value: Shape,
  cases: { circle: (v: Circle) => R; rectangle: (v: Rectangle) => R },
): R {
  switch (value.kind) {
    case "circle":
      return cases.circle(value);
    case "rectangle":
      return cases.rectangle(value);
    default:
      return assertNever(value);
  }
}

// Consumers can also just switch — narrowing is automatic (no helper required):
export function area(s: Shape): number {
  switch (s.kind) {
    case "circle":
      return Math.PI * s.radius ** 2; // s : Circle here
    case "rectangle":
      return s.width * s.height; // s : Rectangle here
    default:
      return assertNever(s);
  }
}

// Proof the type system enforces branch safety:
export function wrongBranchAccess(s: Shape): unknown {
  if (s.kind === "circle") {
    // @ts-expect-error -- 'width' does not exist on Circle; the compiler rejects it.
    return s.width;
  }
  return undefined;
}

// =====================================================================
// CASE 2 — NON-discriminated oneOf/anyOf: oneOf:[string, Name] (no shared const)
//   TS can't express XOR in a type, so the TYPE is a plain union and the runtime
//   GUARDS (the generated branch validators) do the narrowing. This is the literal
//   analog of V5's Match() (evaluate each branch in order). The oneOf "exactly one"
//   / anyOf "at least one" cardinality is enforced by the VALIDATOR, not the type
//   (documented limitation — see design §5.2).
// =====================================================================
export interface PersonName {
  readonly first: string;
  readonly last: string;
}
export type FullName = string | PersonName; // oneOf: [ {type:string}, PersonName ]

// Per-branch type guards = the idiomatic form of V5 TryGetAs{Branch}(out T).
// In real output the body calls the generated branch validator; here, structural checks.
export function isStringName(v: FullName): v is string {
  return typeof v === "string";
}
export function isStructuredName(v: FullName): v is PersonName {
  return typeof v === "object" && v !== null;
}

export function matchFullName<R>(
  value: FullName,
  cases: { string: (v: string) => R; name: (v: PersonName) => R; fallback?: (v: FullName) => R },
): R {
  if (isStringName(value)) {
    return cases.string(value);
  }
  if (isStructuredName(value)) {
    return cases.name(value);
  }
  if (cases.fallback) {
    return cases.fallback(value);
  }
  throw new Error("no oneOf branch matched");
}

export function render(n: FullName): string {
  return matchFullName(n, {
    string: (s) => s,
    name: (x) => `${x.first} ${x.last}`,
  });
}

// Exercise everything so the demo is live (and the guards' narrowing is checked).
export const demo = [
  matchShape({ kind: "circle", radius: 2 }, { circle: (c) => c.radius, rectangle: (r) => r.width }),
  area({ kind: "rectangle", width: 3, height: 4 }),
  wrongBranchAccess({ kind: "circle", radius: 1 }),
  render("Ada"),
  render({ first: "Ada", last: "Lovelace" }),
];
