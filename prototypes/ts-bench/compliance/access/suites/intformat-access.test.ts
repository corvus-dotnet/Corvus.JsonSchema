// PROVIDER REAL-OUTPUT test for the sub-64-bit integer-format brands the provider now emits (gap B4): an
// OpenAPI int16/int32/uint16/uint32 format -> a branded `Brand<number, "fmt">` + a validating factory
// (`as{Name}`) that mints the brand only after an integer-and-range check; a 64-bit+ format (int64) stays
// `bigint`. Run after generating intformat.json into out-intformat/:
//   Codegen (intformat.json -> out-intformat/), transpile, and run are all driven by ../run-access.sh.
import Root_outintformatgeneratedjs, { Count, Port, Reading, Size, Small } from "./out-intformat/generated.js";

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
eq("Small.as mints an int16 brand (in-range)", Small.as(100), 100);
eq("Small.as accepts the int16 min boundary", Small.as(-32768), -32768);
eq("Small.as accepts the int16 max boundary", Small.as(32767), 32767);
eq("Port.as mints a uint16 brand (0 boundary)", Port.as(0), 0);
eq("Port.as accepts the uint16 max boundary", Port.as(65535), 65535);
eq("Count.as mints an int32 brand", Count.as(123456), 123456);
eq("Count.as accepts the int32 min boundary", Count.as(-2147483648), -2147483648);
eq("Count.as accepts the int32 max boundary", Count.as(2147483647), 2147483647);
eq("Size.as mints a uint32 brand", Size.as(4000000000), 4000000000);
eq("Size.as accepts the uint32 max boundary", Size.as(4294967295), 4294967295);

// factory rejects out-of-range / non-integer (mint only after the integer-and-range check)
throws("Small.as rejects int16 over-range", () => Small.as(32768));
throws("Small.as rejects int16 under-range", () => Small.as(-32769));
throws("Port.as rejects uint16 negative", () => Port.as(-1));
throws("Port.as rejects uint16 over-range", () => Port.as(65536));
throws("Count.as rejects int32 over-range", () => Count.as(2147483648));
throws("Count.as rejects int32 under-range", () => Count.as(-2147483649));
throws("Count.as rejects a non-integer", () => Count.as(1.5));
throws("Size.as rejects uint32 negative", () => Size.as(-1));
throws("Size.as rejects uint32 over-range", () => Size.as(4294967296));

// validate, then consume as the typed interface; a branded field reads as its base number
const raw: unknown = { small: 7, port: 8080, count: 42, size: 100, big: 9 };
if (!Root_outintformatgeneratedjs.evaluate(raw)) { throw new Error("reading should validate (default = format annotation)"); }
const r = raw as Reading;
const c: Count = r.count; // branded type
eq("branded int32 field value", c, 42);
eq("brand is usable as a number", c + 1, 43);
// int64 format stays bigint (NOT branded) — read the field as bigint-typed in the interface
eq("int64 field is plain (annotation only)", r.big, 9);

console.log(`intformat-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`intformat-access: ${fail} failed`); }
console.log("OK — provider emits branded integer-format types + validating factories; mint/reject/access work.");
