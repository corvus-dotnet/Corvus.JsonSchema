// PROVIDER REAL-OUTPUT test for the exotic numeric-format brands (gaps B5 + B3, §5.3.1), mirroring the C#
// WellKnownNumericFormatHandler: each numeric format -> a branded `Brand<number, "fmt">` + a validating
// factory `as{Name}`. byte (0..255) / sbyte (-128..127) are integer-and-range; half is a RANGE check
// (-65504..65504, fractional allowed); single/double are unbounded type tags (the C# cast saturates);
// decimal is range-checked AND gets the gap-B3 `{name}AsExact` accessor returning exact digits from a
// lossless parse. Run after generating numformat.json into out-numformat/:
//   Codegen (numformat.json -> out-numformat/), transpile, and run are all driven by ../run-access.sh.
import Root_outnumformatgeneratedjs, { Amount, Delta, Level, Mass, Measure, Ratio, Weight } from "./out-numformat/generated.js";
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
eq("Level.from mints a byte (in-range)", Level.from(200), 200);
eq("Level.from accepts 0", Level.from(0), 0);
eq("Level.from accepts 255", Level.from(255), 255);
throws("Level.from rejects 256", () => Level.from(256));
throws("Level.from rejects -1", () => Level.from(-1));
throws("Level.from rejects a non-integer", () => Level.from(1.5));

// sbyte: integer in [-128, 127]
eq("Delta.from mints an sbyte", Delta.from(-128), -128);
eq("Delta.from accepts 127", Delta.from(127), 127);
throws("Delta.from rejects 128", () => Delta.from(128));
throws("Delta.from rejects -129", () => Delta.from(-129));

// half: RANGE check [-65504, 65504], fractional allowed (NOT integer)
eq("Ratio.from accepts a fractional half", Ratio.from(1.25), 1.25);
eq("Ratio.from accepts the max boundary", Ratio.from(65504), 65504);
eq("Ratio.from accepts the min boundary", Ratio.from(-65504), -65504);
throws("Ratio.from rejects over-range", () => Ratio.from(70000));
throws("Ratio.from rejects under-range", () => Ratio.from(-70000));

// single / double: unbounded brands (type tags only — any number mints)
eq("Weight.from mints a single with no range check", Weight.from(3.4e38), 3.4e38);
eq("Mass.from mints a double with no range check", Mass.from(1e300), 1e300);

// decimal: range-checked brand + the gap-B3 exact-digits accessor
eq("Amount.from mints a decimal", Amount.from(1.5), 1.5);
eq("Amount.toExact returns the value's digits", Amount.toExact(Amount.from(1.5)), "1.5");

// gap B3 win: a decimal parsed losslessly keeps digits a JS number would round away; Amount.toExact surfaces them.
const exact = "123456789012345678901234567890.5";
const losslessAmount = (parseLossless(`{"amount":${exact}}`) as { amount: Amount }).amount;
eq("Amount.toExact preserves exact digits from a lossless parse", Amount.toExact(losslessAmount), exact);

// validate (format is annotation-only by default) then consume as the typed interface; branded fields read as numbers
const raw: unknown = { level: 7, delta: -1, ratio: 1.25, weight: 2.5, mass: 3.5, amount: 9.99 };
if (!Root_outnumformatgeneratedjs.evaluate(raw)) { throw new Error("measure should validate (default = format annotation)"); }
const m = raw as Measure;
eq("byte field reads as its base number", m.level, 7);

console.log(`numformat-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`numformat-access: ${fail} failed`); }
console.log("OK — provider emits branded numeric-format types (byte/sbyte/half/single/double/decimal) + factories + decimal exact-digits seam.");
