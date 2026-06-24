// What the generator emits for string/numeric FORMATS (design §5.3). Type-checked under --strict.
//
// String formats -> a branded string + a validating factory that mints the brand only after
// the check (the JS analog of V5's single-value struct + conversion operators). Rich formats
// also get a parse helper (e.g. date-time -> Date). 64-bit+ numeric formats -> bigint (§4.1).

import { Brand, FormatError } from "./runtime"; // in real output: from "@corvus/json-runtime"

export type Uuid = Brand<string, "Uuid">;
export type DateTimeString = Brand<string, "DateTime">;
export type Int64 = bigint; // int64/uint64/int128: exceed JS safe-integer range

const UUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

// validating factories — the only place a brand is minted
export function asUuid(s: string): Uuid {
  if (!UUID_RE.test(s)) {
    throw new FormatError("uuid");
  }
  return s as Uuid;
}

export function asDateTime(s: string): DateTimeString {
  if (Number.isNaN(Date.parse(s))) {
    throw new FormatError("date-time");
  }
  return s as DateTimeString;
}

// parse helper for the richer runtime type
export function toDate(s: DateTimeString): Date {
  return new Date(s);
}

// a branded value IS its base type at runtime (assignable to string) ...
export function useId(id: Uuid): string {
  return id;
}

const realId = asUuid("00000000-0000-0000-0000-000000000000");

// ... but a plain string is NOT assignable to the brand (nominal safety, zero runtime cost)
// @ts-expect-error -- a raw string is not a Uuid; it must go through asUuid()
const spoofed: Uuid = "not-a-uuid";

export const demoFormat = [useId(realId), toDate(asDateTime("2026-06-24T10:00:00Z")), spoofed, 9007199254740993n as Int64];
