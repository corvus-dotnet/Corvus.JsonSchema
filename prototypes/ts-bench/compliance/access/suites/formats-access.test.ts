// PROVIDER REAL-OUTPUT test for the brand/format conversions the provider now emits (§5.3.1): a
// well-known string format -> a branded type + a validating factory (mint only after the check); a
// 64-bit integer format -> bigint. Run after generating formats.json into out-formats/:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- formats.json out-formats
//   npx -y -p typescript tsc out-formats/generated.ts out-formats/corvus-runtime.ts formats-access.test.ts spike-globals.d.ts \
//       --outDir prov-test-formats --strict --target es2022 --module esnext --moduleResolution bundler
//   node prov-test-formats/formats-access.test.js
import { evaluateRoot, asId, asOwner, type Account, type Id } from "./out-formats/generated.js";

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
eq("asId mints a uuid brand", asId(ID), ID);
eq("asOwner mints an email brand", asOwner("a@b.com"), "a@b.com");

// factory rejects invalid input (mint only after the format check)
throws("asId rejects an invalid uuid", () => asId("not-a-uuid"));
throws("asOwner rejects an invalid email", () => asOwner("nope"));

// validate, then consume as the typed interface; a branded field reads as its base string
const raw: unknown = { id: ID, createdAt: "2026-06-24T10:00:00Z", balance: 100, owner: "a@b.com" };
if (!evaluateRoot(raw)) { throw new Error("account should validate"); }
const acc = raw as Account;
const id: Id = acc.id; // branded type
eq("branded field value", id, ID);
eq("brand is usable as a string", id.length, 36);

console.log(`formats-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`formats-access: ${fail} failed`); }
console.log("OK — provider emits branded format types + validating factories; mint/reject/access work.");
