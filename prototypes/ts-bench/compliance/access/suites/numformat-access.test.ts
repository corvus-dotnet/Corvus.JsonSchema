// PROVIDER REAL-OUTPUT test for the exotic numeric-format brands (gaps B5 + B3, §5.3.1), mirroring the C#
// WellKnownNumericFormatHandler: each numeric format -> a branded `Brand<number, "fmt">` + a validating
// factory `as{Name}`. byte (0..255) / sbyte (-128..127) are integer-and-range; half is a RANGE check
// (-65504..65504, fractional allowed); single/double are unbounded type tags (the C# cast saturates);
// decimal is range-checked AND gets the gap-B3 `{name}AsExact` accessor returning exact digits from a
// lossless parse. Run after generating numformat.json into out-numformat/:
//   Codegen (numformat.json -> out-numformat/), transpile, and run are all driven by ../run-access.sh.
import {
  evaluateRoot,
  asLevel,
  asDelta,
  asRatio,
  asWeight,
  asMass,
  asAmount,
  amountAsExact,
  type Measure,
  type Amount,
} from "./out-numformat/generated.js";
import { parseLossless } from "./out-numformat/corvus-runtime.js";

let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}
function throws(label: string, fn: () => unknown): void {
  try { fn(); fail++; console.log(`FAIL ${label}: expected throw`); } catch { pass++; }
}

// byte: integer in [0, 255]
eq("asLevel mints a byte (in-range)", asLevel(200), 200);
eq("asLevel accepts 0", asLevel(0), 0);
eq("asLevel accepts 255", asLevel(255), 255);
throws("asLevel rejects 256", () => asLevel(256));
throws("asLevel rejects -1", () => asLevel(-1));
throws("asLevel rejects a non-integer", () => asLevel(1.5));

// sbyte: integer in [-128, 127]
eq("asDelta mints an sbyte", asDelta(-128), -128);
eq("asDelta accepts 127", asDelta(127), 127);
throws("asDelta rejects 128", () => asDelta(128));
throws("asDelta rejects -129", () => asDelta(-129));

// half: RANGE check [-65504, 65504], fractional allowed (NOT integer)
eq("asRatio accepts a fractional half", asRatio(1.25), 1.25);
eq("asRatio accepts the max boundary", asRatio(65504), 65504);
eq("asRatio accepts the min boundary", asRatio(-65504), -65504);
throws("asRatio rejects over-range", () => asRatio(70000));
throws("asRatio rejects under-range", () => asRatio(-70000));

// single / double: unbounded brands (type tags only — any number mints)
eq("asWeight mints a single with no range check", asWeight(3.4e38), 3.4e38);
eq("asMass mints a double with no range check", asMass(1e300), 1e300);

// decimal: range-checked brand + the gap-B3 exact-digits accessor
eq("asAmount mints a decimal", asAmount(1.5), 1.5);
eq("amountAsExact returns the value's digits", amountAsExact(asAmount(1.5)), "1.5");

// gap B3 win: a decimal parsed losslessly keeps digits a JS number would round away; amountAsExact surfaces them.
const exact = "123456789012345678901234567890.5";
const losslessAmount = (parseLossless(`{"amount":${exact}}`) as { amount: Amount }).amount;
eq("amountAsExact preserves exact digits from a lossless parse", amountAsExact(losslessAmount), exact);

// validate (format is annotation-only by default) then consume as the typed interface; branded fields read as numbers
const raw: unknown = { level: 7, delta: -1, ratio: 1.25, weight: 2.5, mass: 3.5, amount: 9.99 };
if (!evaluateRoot(raw)) { throw new Error("measure should validate (default = format annotation)"); }
const m = raw as Measure;
eq("byte field reads as its base number", m.level, 7);

console.log(`numformat-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`numformat-access: ${fail} failed`); }
console.log("OK — provider emits branded numeric-format types (byte/sbyte/half/single/double/decimal) + factories + decimal exact-digits seam.");
