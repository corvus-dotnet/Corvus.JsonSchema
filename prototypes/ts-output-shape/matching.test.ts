// MATCHING (design §5.2): the V5 `Match()` analog — discriminated unions, non-discriminated
// oneOf/anyOf guards, enum membership guards, and a conditional refined discriminated union.
import { suite } from "./harness";
import { matchShape, area, matchFullName, isStringName, isStructuredName, render, type FullName } from "./union-model";
import { isPriority, isMode, describe } from "./enum-const-model";
import { describePayment } from "./conditional-model";

const t = suite("matching");

// --- discriminated union: matchShape dispatches on the literal discriminant ---
const cases = { circle: (c: { radius: number }) => `c${c.radius}`, rectangle: (r: { width: number }) => `r${r.width}` };
t.eq("matchShape -> circle branch", matchShape({ kind: "circle", radius: 2 }, cases), "c2");
t.eq("matchShape -> rectangle branch", matchShape({ kind: "rectangle", width: 3, height: 4 }, cases), "r3");
t.eq("area(circle) uses radius", area({ kind: "circle", radius: 1 }).toFixed(4), Math.PI.toFixed(4));
t.eq("area(rectangle) uses w*h", area({ kind: "rectangle", width: 3, height: 4 }), 12);

// --- non-discriminated union: per-branch guards (= V5 TryGetAs{Branch}) + matchFullName ---
t.ok("isStringName(string)", isStringName("Ada"));
t.ok("isStringName rejects object", !isStringName({ first: "Ada", last: "L" }));
t.ok("isStructuredName(object)", isStructuredName({ first: "Ada", last: "L" }));
t.eq("render -> string branch", render("Ada"), "Ada");
t.eq("render -> structured branch", render({ first: "Ada", last: "Lovelace" }), "Ada Lovelace");
t.eq("matchFullName string", matchFullName("Ada", { string: (s) => "S:" + s, name: () => "N" }), "S:Ada");
t.eq("matchFullName name", matchFullName({ first: "A", last: "B" }, { string: () => "S", name: (n) => "N:" + n.first }), "N:A");
t.eq("matchFullName fallback (no branch)", matchFullName(42 as unknown as FullName, { string: () => "S", name: () => "N", fallback: () => "FB" }), "FB");

// --- enum membership guards + exhaustive mixed-type switch ---
t.ok("isPriority(2)", isPriority(2));
t.ok("isPriority rejects 4", !isPriority(4));
t.ok("isPriority rejects non-number", !isPriority("2"));
t.ok("isMode(null)", isMode(null));
t.ok("isMode(false)", isMode(false));
t.ok("isMode rejects 'x'", !isMode("x"));
t.eq("describe('auto')", describe("auto"), "auto");
t.eq("describe(0)", describe(0), "zero");
t.eq("describe(false)", describe(false), "off");
t.eq("describe(null)", describe(null), "none");

// --- conditional refined discriminated union (if/then/else overlay, §5.2/§5.3) ---
t.eq("describePayment card", describePayment({ method: "card", cardNumber: "4111111111111111" }), "card ending 1111");
t.eq("describePayment bank", describePayment({ method: "bank", iban: "GB00..." }), "bank GB00...");

t.done();
