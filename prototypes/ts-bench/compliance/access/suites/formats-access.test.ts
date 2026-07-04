// PROVIDER REAL-OUTPUT test for the brand/format conversions the provider now emits (§5.3.1): a
// well-known string format -> a branded type + a validating factory (mint only after the check); a
// 64-bit integer format -> bigint. Run after generating formats.json into out-formats/:
//   Codegen (formats.json -> out-formats/), transpile, and run are all driven by ../run-access.sh.
import Root_outformatsgeneratedjs, { Account, Id, Owner } from "./out-formats/generated.js";

let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}
function throws(label: string, fn: () => unknown): void {
  try { fn(); fail++; console.log(`FAIL ${label}: expected throw`); } catch { pass++; }
}

const ID = "00000000-0000-0000-0000-000000000000";

// validating factory mints the brand (which IS its base string at runtime)
eq("Id.from mints a uuid brand", Id.from(ID), ID);
eq("Owner.from mints an email brand", Owner.from("a@b.com"), "a@b.com");

// factory rejects invalid input (mint only after the format check)
throws("Id.from rejects an invalid uuid", () => Id.from("not-a-uuid"));
throws("Owner.from rejects an invalid email", () => Owner.from("nope"));

// validate, then consume as the typed interface; a branded field reads as its base string
const raw: unknown = { id: ID, createdAt: "2026-06-24T10:00:00Z", balance: 100, owner: "a@b.com" };
if (!Root_outformatsgeneratedjs.evaluate(raw)) { throw new Error("account should validate"); }
const acc = raw as Account;
const id: Id = acc.id; // branded type
eq("branded field value", id, ID);
eq("brand is usable as a string", id.length, 36);

console.log(`formats-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`formats-access: ${fail} failed`); }
console.log("OK — provider emits branded format types + validating factories; mint/reject/access work.");
