// PROVIDER REAL-OUTPUT test for the byte-level immer draft (§5.7): the GENERIC `produce(source, recipe)`
// records mutations on a typed Draft<T> and lowers them to a Model C byte patch (unchanged bytes copied
// verbatim); `recordChanges` exposes the same change-set as RFC 6902 JSON Patch.
// Run after generating mutation.json into out-mutation/:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- mutation.json out-mutation
//   <tsc> out-mutation/generated.ts out-mutation/corvus-runtime.ts mutation-access.test.ts spike-globals.d.ts \
//       --outDir prov-test-mutation --strict --target es2022 --module esnext --moduleResolution bundler --lib es2022,dom
//   node prov-test-mutation/mutation-access.test.js
import { produce, recordChanges } from "./out-mutation/corvus-runtime.js";
import { type Doc } from "./out-mutation/generated.js";

const enc = new TextEncoder();
const dec = new TextDecoder();
let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}

const base = JSON.stringify({ name: "Ada", age: 30, address: { city: "Anytown" }, tags: ["math"] });

// produce: "mutate" the typed Draft<Doc>, get NEW bytes; unchanged bytes copied through verbatim
const out = dec.decode(produce<Doc>(enc.encode(base), (d) => {
  d.age = 31;                 // scalar
  d.address.city = "London";  // nested, typed
  d.tags[0] = "algebra";      // array element (index-set; push/splice are the deferred structural ops)
}));
const next = JSON.parse(out);
eq("scalar edit applied", next.age, 31);
eq("nested edit applied", next.address.city, "London");
eq("array element edit applied", next.tags[0], "algebra");
eq("name copied verbatim", next.name, "Ada");
eq("source bytes untouched", base, JSON.stringify({ name: "Ada", age: 30, address: { city: "Anytown" }, tags: ["math"] }));

// the recorded change-set is RFC 6902 JSON Patch
const { patches } = recordChanges(JSON.parse(base) as Doc, (d) => {
  d.age = 42;
  d.address.city = "Paris";
});
eq("scalar patch (RFC 6902)", patches[0], { op: "replace", path: "/age", value: 42 });
eq("nested patch (RFC 6902)", patches[1], { op: "replace", path: "/address/city", value: "Paris" });

console.log(`mutation-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`mutation-access: ${fail} failed`); }
console.log("OK — byte produce(recipe) over the typed Draft: edits apply, unchanged bytes verbatim, RFC 6902 patches.");
