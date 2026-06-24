// PROVIDER REAL-OUTPUT test for the mutation surface (§5.7): a consumer pairs the GENERIC `produce`
// runtime helper with the provider's emitted readonly interface (the provider emits no per-type mutation
// code). Edits apply, the original is untouched (immutability), and the change-set is RFC 6902 JSON Patch.
// Run after generating mutation.json into out-mutation/:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- mutation.json out-mutation
//   npx -y -p typescript tsc out-mutation/generated.ts out-mutation/corvus-runtime.ts mutation-access.test.ts spike-globals.d.ts \
//       --outDir prov-test-mutation --strict --target es2022 --module esnext --moduleResolution bundler
//   node prov-test-mutation/mutation-access.test.js
import { produce, recordChanges, type JsonDocument } from "./out-mutation/corvus-runtime.js";
import { type Doc } from "./out-mutation/generated.js";

let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}

const doc: JsonDocument<Doc> = { value: { name: "Ada", age: 30, address: { city: "Anytown" }, tags: ["math"] } };

// produce: "mutate" the typed draft, get a NEW immutable document
const next = produce(doc, (d) => {
  d.age = 31;                 // scalar
  d.address.city = "London";  // nested, typed
  d.tags.push("new");         // array mutation on the mutable draft
});
eq("scalar edit applied", next.value.age, 31);
eq("nested edit applied", next.value.address.city, "London");
eq("array edit applied", next.value.tags.includes("new"), true);

// the original document is untouched (structural sharing / immutability)
eq("original scalar unchanged", doc.value.age, 30);
eq("original nested unchanged", doc.value.address.city, "Anytown");
eq("original array unchanged", doc.value.tags.length, 1);

// the recorded change-set is RFC 6902 JSON Patch
const { next: n2, patches } = recordChanges(doc.value, (d) => {
  d.age = 42;
  d.address.city = "Paris";
});
eq("scalar patch (RFC 6902)", patches[0], { op: "replace", path: "/age", value: 42 });
eq("nested patch (RFC 6902)", patches[1], { op: "replace", path: "/address/city", value: "Paris" });
eq("change applied to the new value", n2.address.city, "Paris");
eq("source left untouched", doc.value.address.city, "Anytown");

console.log(`mutation-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`mutation-access: ${fail} failed`); }
console.log("OK — generic produce over the emitted interface: edits apply, original immutable, RFC 6902 patches.");
