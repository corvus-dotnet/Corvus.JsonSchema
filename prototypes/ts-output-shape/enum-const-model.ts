// What the generator emits for ENUM / CONST (design §5.3). Type-checked under --strict.
//
// `enum` is an array of ARBITRARY JSON values and `const` is ANY JSON value — NOT just strings.
// Emit a union of literal types of whatever JSON types the values are (never a TS `enum`).

import { assertNever } from "./runtime"; // in real output: from "@endjin/corvus-json-runtime"

// --- enums of each JSON type ---
export type Status = "active" | "archived" | "deleted"; // string enum
export type Priority = 1 | 2 | 3; // numeric enum
export type Tristate = true | false | null; // boolean + null  (=> boolean | null)
export type Mode = "auto" | 0 | false | null; // MIXED-type enum: ["auto", 0, false, null]

// --- const of each JSON type ---
export type CreateKind = "create"; // const "create"
export type SchemaVersion = 2; // const 2
export type Enabled = true; // const true
export type Nothing = null; // const null
export type Origin = { readonly x: 0; readonly y: 0 }; // const {"x":0,"y":0} -> literal object type
export type Unit = readonly [1]; // const [1] -> literal tuple type

// Validation: JSON deep-equality against the allowed value(s) — NOT a string Set in general.
//   strings / booleans / null : exact ===
//   numbers                   : EXACT numeric compare (§4.1) so 1.0 matches 1, big ints stay distinct
//   objects / arrays          : deep structural equality (object keys order-independent, arrays ordered)
// All-string enums keep the Set<string> fast-path (Model B) / raw-byte compare (Model A/C); mixed or
// scalar enums use a generated per-value check; object/array const/enum use a generated deep-equal.
// The TYPE is best-effort; the VALIDATOR is authoritative for exactness (e.g. it rejects extra object
// properties that TS structural typing would otherwise allow).
const PRIORITIES: ReadonlySet<number> = new Set<number>([1, 2, 3]);
export function isPriority(v: unknown): v is Priority {
  return typeof v === "number" && PRIORITIES.has(v);
}
export function isMode(v: unknown): v is Mode {
  return v === "auto" || v === 0 || v === false || v === null;
}

// object/array CONST: the validator does DEEP structural equality against the exact value (it rejects
// extra object properties that the best-effort literal TYPE would allow). isOrigin/isUnit are the
// generated deep-equal guards (design §5.3 enum/const row).
export function isOrigin(v: unknown): v is Origin {
  return typeof v === "object" && v !== null && !Array.isArray(v) &&
    Object.keys(v).length === 2 && (v as Record<string, unknown>).x === 0 && (v as Record<string, unknown>).y === 0;
}
export function isUnit(v: unknown): v is Unit {
  return Array.isArray(v) && v.length === 1 && v[0] === 1;
}

// exhaustive consumer switch over a mixed-type union
export function describe(m: Mode): string {
  switch (m) {
    case "auto":
      return "auto";
    case 0:
      return "zero";
    case false:
      return "off";
    case null:
      return "none";
    default:
      return assertNever(m);
  }
}

// @ts-expect-error -- 4 is not a Priority
const badPriority: Priority = 4;
// @ts-expect-error -- "x" is not a Mode member
const badMode: Mode = "x";
// @ts-expect-error -- Origin requires x:0 (the literal), not an arbitrary number
const badOrigin: Origin = { x: 5, y: 0 };

export const demoEnum: ReadonlyArray<unknown> = [
  describe("auto"),
  isPriority(2),
  isMode(null),
  badPriority,
  badMode,
  badOrigin,
  "create" satisfies CreateKind,
  2 satisfies SchemaVersion,
  true satisfies Enabled,
  null satisfies Nothing,
  { x: 0, y: 0 } satisfies Origin,
  [1] satisfies Unit,
  (null as Tristate),
];
