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
eq("Small.from mints an int16 brand (in-range)", Small.from(100), 100);
eq("Small.from accepts the int16 min boundary", Small.from(-32768), -32768);
eq("Small.from accepts the int16 max boundary", Small.from(32767), 32767);
eq("Port.from mints a uint16 brand (0 boundary)", Port.from(0), 0);
eq("Port.from accepts the uint16 max boundary", Port.from(65535), 65535);
eq("Count.from mints an int32 brand", Count.from(123456), 123456);
eq("Count.from accepts the int32 min boundary", Count.from(-2147483648), -2147483648);
eq("Count.from accepts the int32 max boundary", Count.from(2147483647), 2147483647);
eq("Size.from mints a uint32 brand", Size.from(4000000000), 4000000000);
eq("Size.from accepts the uint32 max boundary", Size.from(4294967295), 4294967295);

// factory rejects out-of-range / non-integer (mint only after the integer-and-range check)
throws("Small.from rejects int16 over-range", () => Small.from(32768));
throws("Small.from rejects int16 under-range", () => Small.from(-32769));
throws("Port.from rejects uint16 negative", () => Port.from(-1));
throws("Port.from rejects uint16 over-range", () => Port.from(65536));
throws("Count.from rejects int32 over-range", () => Count.from(2147483648));
throws("Count.from rejects int32 under-range", () => Count.from(-2147483649));
throws("Count.from rejects a non-integer", () => Count.from(1.5));
throws("Size.from rejects uint32 negative", () => Size.from(-1));
throws("Size.from rejects uint32 over-range", () => Size.from(4294967296));

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
