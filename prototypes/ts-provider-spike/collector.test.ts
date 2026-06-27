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
  const ok = evaluatePerson(value, new Ev(), "", "", r);
  return { ok, r };
}

// valid instance: no failures.
{
  const { ok, r } = collect({ name: "Ada", address: { zip: "12345" }, tags: ["ab"], age: 5 });
  eq(ok, true, "valid -> ok");
  eq(r.valid, true, "valid -> results.valid");
  eq(r.failures.length, 0, "valid -> no failures");
}

// invalid at five nested locations: name minLength, address.zip pattern, tags[0] minLength, age via a
// $ref to $defs/PosInt (minimum), and score via allOf->$ref to the same. Detailed mode must collect ALL
// FIVE (no early return), with correct instance locations and PER-KEYWORD path-taken keyword locations.
{
  const { ok, r } = collect({ name: "ab", address: { zip: "xyz" }, tags: ["a"], age: 0, score: 0 });
  eq(ok, false, "invalid -> ok=false");
  eq(r.failures.map((f) => f.instanceLocation).sort(), ["/address/zip", "/age", "/name", "/score", "/tags/0"], "instanceLocations (collected all, threaded)");
  eq(r.failures.map((f) => f.keywordLocation).sort(),
    ["/properties/address/properties/zip/pattern", "/properties/age/$ref/minimum", "/properties/name/minLength", "/properties/score/allOf/0/$ref/minimum", "/properties/tags/items/minLength"],
    "per-keyword keywordLocations (path taken, incl. $ref)");

  // A property $ref proves keywordLocation (path TAKEN) diverges from absoluteKeywordLocation (RESOLVED):
  const age = r.failures.find((f) => f.instanceLocation === "/age")!;
  eq(age.keywordLocation, "/properties/age/$ref/minimum", "$ref keywordLocation = path taken (through $ref)");
  eq(age.absoluteKeywordLocation!.endsWith("#/$defs/PosInt/minimum"), true, "$ref absoluteKeywordLocation = resolved target");

  // A $ref INSIDE allOf carries the /$ref token too (the closed gap): /properties/score/allOf/0/$ref/minimum.
  const score = r.failures.find((f) => f.instanceLocation === "/score")!;
  eq(score.keywordLocation, "/properties/score/allOf/0/$ref/minimum", "$ref-in-allOf keywordLocation keeps the /$ref token");
  eq(score.absoluteKeywordLocation!.endsWith("#/$defs/PosInt/minimum"), true, "$ref-in-allOf absoluteKeywordLocation = resolved target");
}

// verbose mode: a VALID instance collects annotations from every successfully-validated subschema
// (title/description/default/...), each at its instance location with the path-taken keywordLocation.
{
  const r = new Results(true);
  const ok = evaluatePerson({ name: "Ada", address: { zip: "12345" }, age: 5 }, new Ev(), "", "", r);
  eq(ok, true, "verbose valid -> ok");
  eq(r.failures.length, 0, "verbose valid -> no failures");
  const find = (kw: string, il: string) => r.annotations.find((a) => a.keyword === kw && a.instanceLocation === il);
  eq(r.annotations.length, 4, "verbose -> 4 annotations collected");
  eq(find("title", "")?.value, "Person", "root title annotation");
  eq(find("title", "/name")?.value, "Full name", "name title annotation");
  eq(find("default", "/name")?.value, "Anon", "name default annotation");
  eq(find("description", "/age")?.value, "A positive integer", "PosInt description (via $ref) annotation");
  eq(find("description", "/age")?.keywordLocation, "/properties/age/$ref/description", "annotation keywordLocation = path taken");
}

// detailed mode (not verbose): annotations are NOT collected.
{
  const r = new Results();
  evaluatePerson({ name: "Ada", address: { zip: "12345" }, age: 5 }, new Ev(), "", "", r);
  eq(r.annotations.length, 0, "detailed mode -> no annotations");
}

// boolean hot path (no collector arg): returns false, records nothing.
{
  const ok = evaluatePerson({ name: "ab", address: { zip: "xyz" }, tags: ["a"] }, new Ev());
  eq(ok, false, "boolean path -> false");
}

console.log(`COLLECTOR ${pass} passed, ${fail} failed`);
