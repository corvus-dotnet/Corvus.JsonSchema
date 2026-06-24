// What the generator emits for ENUM / CONST (design §5.3). Type-checked under --strict.
//
// enum / anyOf-of-consts -> a union of string-literal types (NEVER a TS `enum`, which emits
// a runtime IIFE + reverse map, isn't tree-shakeable, and fights the JSON boundary).
// A single `const` -> the literal type itself.

import { assertNever } from "./runtime"; // in real output: from "@corvus/json-runtime"

// enum: { "enum": ["active","archived","deleted"] }
export type Status = "active" | "archived" | "deleted";

// Optional `as const` value object, emitted only when runtime values are needed.
// (A type and a value may share the name — they live in different namespaces.)
export const Status = { Active: "active", Archived: "archived", Deleted: "deleted" } as const;

// const: { "const": 2 } / { "const": "create" } -> the literal type
export type SchemaVersion = 2;
export type CreateKind = "create";

// Validation (generated): membership against a Set (Model B) / raw-byte compare (Model A/C).
const STATUS_VALUES: ReadonlySet<string> = new Set<string>(["active", "archived", "deleted"]);
export function isStatus(value: unknown): value is Status {
  return typeof value === "string" && STATUS_VALUES.has(value);
}

// consumer code: exhaustive switch over the literal union
export function label(s: Status): string {
  switch (s) {
    case "active":
      return "Active";
    case "archived":
      return "Archived";
    case "deleted":
      return "Deleted";
    default:
      return assertNever(s); // adding an enum value breaks the build here
  }
}

// @ts-expect-error -- "paused" is not a member of Status
const badStatus: Status = "paused";

export const demoEnum = [label("active"), isStatus("x"), Status.Active, badStatus, 2 as SchemaVersion, "create" as CreateKind];
