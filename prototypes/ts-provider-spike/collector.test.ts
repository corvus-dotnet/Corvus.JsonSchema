// Results-collector test (§15): detailed mode collects EVERY failure with correct, threaded instance +
// keyword locations and no early return, while the boolean hot path is unchanged. Regenerate + run:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- person-collector.json out-coll
//   <tsc> out-coll/generated.ts out-coll/corvus-runtime.ts collector.test.ts spike-globals.d.ts --outDir out-coll-js \
//     --strict --target es2022 --module esnext --moduleResolution bundler --lib es2022,dom
//   node out-coll-js/collector.test.js
import { evaluatePerson } from "./out-coll/generated.js";
import { Ev, Results } from "./out-coll/corvus-runtime.js";

let pass = 0;
let fail = 0;
function eq(actual: unknown, expected: unknown, msg: string): void {
  if (JSON.stringify(actual) === JSON.stringify(expected)) { pass++; }
  else { fail++; console.log("FAIL", msg, "\n  got ", JSON.stringify(actual), "\n  want", JSON.stringify(expected)); }
}

function collect(value: unknown): { ok: boolean; r: Results } {
  const r = new Results();
  const ok = evaluatePerson(value, new Ev(), "", r);
  return { ok, r };
}

// valid instance: no failures.
{
  const { ok, r } = collect({ name: "Ada", address: { zip: "12345" }, tags: ["ab"] });
  eq(ok, true, "valid -> ok");
  eq(r.valid, true, "valid -> results.valid");
  eq(r.failures.length, 0, "valid -> no failures");
}

// invalid at three nested locations: name minLength, address.zip pattern, tags[0] minLength.
// Detailed mode must collect ALL THREE (no early return), with correct instance + keyword locations.
{
  const { ok, r } = collect({ name: "ab", address: { zip: "xyz" }, tags: ["a"] });
  eq(ok, false, "invalid -> ok=false");
  eq(r.failures.map((f) => f.instanceLocation).sort(), ["/address/zip", "/name", "/tags/0"], "instanceLocations (collected all, threaded)");
  eq(r.failures.map((f) => f.keywordLocation).sort(), ["/properties/address/properties/zip", "/properties/name", "/properties/tags/items"], "keywordLocations");
}

// boolean hot path (no collector arg): returns false, records nothing.
{
  const ok = evaluatePerson({ name: "ab", address: { zip: "xyz" }, tags: ["a"] }, new Ev());
  eq(ok, false, "boolean path -> false");
}

console.log(`COLLECTOR ${pass} passed, ${fail} failed`);
