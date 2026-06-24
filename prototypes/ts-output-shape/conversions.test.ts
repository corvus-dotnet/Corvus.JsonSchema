// CONVERSIONS (design §5.3.1): the From/As model. Most allOf/union conversions are FREE under
// structural typing; the genuine V5 conversions worth porting are brand/format factories
// (asUuid/asDateTime/toDate) and 64-bit -> bigint. Union widening to a branch is also free.
import { suite } from "./harness";
import { asUuid, asDateTime, toDate, useId } from "./format-brand-model";
import { type Shape, type Circle } from "./union-model";

const t = suite("conversions");

// --- brand/format validating factories: mint only after the check (§5.3.1) ---
const ID = "00000000-0000-0000-0000-000000000000";
t.eq("asUuid mints a brand that IS its base string at runtime", useId(asUuid(ID)), ID);
t.throws("asUuid rejects an invalid uuid", () => asUuid("not-a-uuid"));
const dt = asDateTime("2026-06-24T10:00:00Z");
t.ok("asDateTime accepts a valid date-time", typeof dt === "string");
t.throws("asDateTime rejects nonsense", () => asDateTime("nonsense"));

// --- richer parse helper: date-time -> Date (the JS analog of V5's NodaTime conversions) ---
t.eq("toDate parses to the right instant", toDate(dt).getTime(), Date.parse("2026-06-24T10:00:00Z"));

// --- 64-bit+ numeric format -> bigint (exact past 2^53; §4.1) ---
const big = 9007199254740993n; // == 2^53 + 1, NOT representable exactly as a JS number
t.ok("bigint keeps precision a number would lose", big !== (BigInt(Number(big))));

// --- union widening is FREE: a branch value already IS the union (no conversion call, §5.3.1) ---
const c: Circle = { kind: "circle", radius: 2 };
const s: Shape = c; // widening
t.eq("widen Circle -> Shape (structural, zero-cost)", s.kind, "circle");

t.done();
