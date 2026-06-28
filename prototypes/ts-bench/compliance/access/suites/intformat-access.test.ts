// PROVIDER REAL-OUTPUT test for the sub-64-bit integer-format brands the provider now emits (gap B4): an
// OpenAPI int16/int32/uint16/uint32 format -> a branded `Brand<number, "fmt">` + a validating factory
// (`as{Name}`) that mints the brand only after an integer-and-range check; a 64-bit+ format (int64) stays
// `bigint`. Run after generating intformat.json into out-intformat/:
//   Codegen (intformat.json -> out-intformat/), transpile, and run are all driven by ../run-access.sh.
import {
  evaluateRoot,
  asSmall,
  asPort,
  asCount,
  asSize,
  type Reading,
  type Count,
} from "./out-intformat/generated.js";

let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}
function throws(label: string, fn: () => unknown): void {
  try { fn(); fail++; console.log(`FAIL ${label}: expected throw`); } catch { pass++; }
}

// validating factory mints the brand (which IS its base number at runtime), in-range incl. boundaries
eq("asSmall mints an int16 brand (in-range)", asSmall(100), 100);
eq("asSmall accepts the int16 min boundary", asSmall(-32768), -32768);
eq("asSmall accepts the int16 max boundary", asSmall(32767), 32767);
eq("asPort mints a uint16 brand (0 boundary)", asPort(0), 0);
eq("asPort accepts the uint16 max boundary", asPort(65535), 65535);
eq("asCount mints an int32 brand", asCount(123456), 123456);
eq("asCount accepts the int32 min boundary", asCount(-2147483648), -2147483648);
eq("asCount accepts the int32 max boundary", asCount(2147483647), 2147483647);
eq("asSize mints a uint32 brand", asSize(4000000000), 4000000000);
eq("asSize accepts the uint32 max boundary", asSize(4294967295), 4294967295);

// factory rejects out-of-range / non-integer (mint only after the integer-and-range check)
throws("asSmall rejects int16 over-range", () => asSmall(32768));
throws("asSmall rejects int16 under-range", () => asSmall(-32769));
throws("asPort rejects uint16 negative", () => asPort(-1));
throws("asPort rejects uint16 over-range", () => asPort(65536));
throws("asCount rejects int32 over-range", () => asCount(2147483648));
throws("asCount rejects int32 under-range", () => asCount(-2147483649));
throws("asCount rejects a non-integer", () => asCount(1.5));
throws("asSize rejects uint32 negative", () => asSize(-1));
throws("asSize rejects uint32 over-range", () => asSize(4294967296));

// validate, then consume as the typed interface; a branded field reads as its base number
const raw: unknown = { small: 7, port: 8080, count: 42, size: 100, big: 9 };
if (!evaluateRoot(raw)) { throw new Error("reading should validate (default = format annotation)"); }
const r = raw as Reading;
const c: Count = r.count; // branded type
eq("branded int32 field value", c, 42);
eq("brand is usable as a number", c + 1, 43);
// int64 format stays bigint (NOT branded) — read the field as bigint-typed in the interface
eq("int64 field is plain (annotation only)", r.big, 9);

console.log(`intformat-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`intformat-access: ${fail} failed`); }
console.log("OK — provider emits branded integer-format types + validating factories; mint/reject/access work.");
