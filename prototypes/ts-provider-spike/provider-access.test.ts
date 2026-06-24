// PROVIDER REAL-OUTPUT access test (the hybrid's "what the provider already emits" half):
// validate with the EMITTED AOT validator, then consume the value as the EMITTED typed interface —
// required/optional/nested object props and the enum literal-union. (Validation itself is covered by
// validate-test.mjs + the suite; this proves the emitted *interface* gives correct typed access.)
//
// Run after generating person.json into out/:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- person.json out
//   npx -y -p typescript tsc out/generated.ts out/corvus-runtime.ts provider-access.test.ts spike-globals.d.ts \
//       --outDir prov-test --strict --target es2022 --module esnext --moduleResolution bundler
//   node prov-test/provider-access.test.js
import { evaluateRoot, type Person, type Status } from "./out/generated.js";

let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}

const raw: unknown = { name: "Ada", age: 30, status: "active", price: 1.5, contact: "a@b.com", address: { postcode: "12345", street: "Main" } };
if (!evaluateRoot(raw)) { throw new Error("expected the full instance to be valid"); }
const p = raw as Person; // post-validation: consume as the generated interface

// object access: required, optional, nested-required, nested-optional
eq("required prop (name)", p.name, "Ada");
eq("optional prop present (age)", p.age, 30);
eq("nested required prop (address.postcode)", p.address.postcode, "12345");
eq("nested optional prop present (address.street)", p.address.street, "Main");

// enum access: a literal-union value (Status = "active" | "archived" | "deleted")
const st: Status | undefined = p.status;
eq("enum literal access (status)", st, "active");

// optional-absent: the `?:` mapping means absent reads as undefined
const min: unknown = { name: "A", address: { postcode: "00000" } };
if (!evaluateRoot(min)) { throw new Error("expected the minimal instance to be valid"); }
const m = min as Person;
eq("optional absent (age)", m.age, undefined);
eq("optional absent (status)", m.status, undefined);
eq("required prop on minimal (name)", m.name, "A");
eq("nested optional absent (address.street)", m.address.street, undefined);

console.log(`provider-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`provider-access: ${fail} failed`); }
console.log("OK — the provider's emitted interface + validator give correct typed access.");
