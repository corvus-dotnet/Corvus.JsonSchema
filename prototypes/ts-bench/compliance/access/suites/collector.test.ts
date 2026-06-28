// Results-collector test (§15): detailed mode collects EVERY failure with correct, threaded instance +
// keyword locations and no early return, while the boolean hot path is unchanged. Regenerate + run:
//   Codegen (person-collector.json -> out-coll/), transpile, and run are all driven by ../run-access.sh.
import { Person } from "./out-coll/generated.js";
import { Ev, Results, toOutput } from "./out-coll/corvus-runtime.js";

let pass = 0;
let fail = 0;
function eq(actual: unknown, expected: unknown, msg: string): void {
  if (JSON.stringify(actual) === JSON.stringify(expected)) { pass++; }
  else { fail++; console.log("FAIL", msg, "\n  got ", JSON.stringify(actual), "\n  want", JSON.stringify(expected)); }
}

function collect(value: unknown): { ok: boolean; r: Results } {
  const r = new Results();
  const ok = Person.evaluate(value, r);
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
  const ok = Person.evaluate({ name: "Ada", address: { zip: "12345" }, age: 5 }, r);
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
  Person.evaluate({ name: "Ada", address: { zip: "12345" }, age: 5 }, r);
  eq(r.annotations.length, 0, "detailed mode -> no annotations");
}

// standardized output (G4): toOutput renders a Results into { valid, errors?, annotations? }.
{
  const { r } = collect({ name: "ab", address: { zip: "xyz" } });
  const out = toOutput(r);
  eq(out.valid, false, "output valid=false");
  eq(out.annotations, undefined, "detailed output -> no annotations key");
  const nameErr = out.errors!.find((e) => e.instanceLocation === "/name")!;
  eq(nameErr.keywordLocation, "/properties/name/minLength", "error unit carries keywordLocation");
  eq(nameErr.error, "minLength", "error unit derives the failing keyword");
  eq(nameErr.absoluteKeywordLocation!.endsWith("#/properties/name/minLength"), true, "error unit carries absoluteKeywordLocation");
}
{
  const v = new Results(true);
  Person.evaluate({ name: "Ada", address: { zip: "12345" }, age: 5 }, v);
  const out = toOutput(v);
  eq(out.valid, true, "verbose-valid output valid=true");
  eq(out.errors, undefined, "valid output -> no errors key");
  eq(out.annotations!.length, 4, "verbose output carries annotation units");
}

// boolean hot path (no collector arg): returns false, records nothing.
{
  const ok = Person.evaluate({ name: "ab", address: { zip: "xyz" }, tags: ["a"] });
  eq(ok, false, "boolean path -> false");
}

// D1 — disjunction branch sub-failures. A failed anyOf/oneOf in detailed mode must surface EACH branch's
// own sub-failures (under .../anyOf/<i>/... or .../oneOf/<i>/...) IN ADDITION to the composite, not just the
// composite. (if/then/else is excluded by design — the `if` is a boolean selector.)
const subFails = (r: Results, prefix: string) =>
  r.failures.filter((f) => f.keywordLocation.startsWith(prefix)).map((f) => f.keywordLocation).sort();

// anyOf, all branches fail: "ab" is neither a >=5-char string nor a >=100 integer -> per-branch sub-failures
// (string/minLength + integer/type) PLUS the composite /anyOf.
{
  const r = new Results();
  const ok = Person.evaluate({ name: "Ada", address: { zip: "12345" }, disj: "ab" }, r);
  eq(ok, false, "anyOf all-fail -> ok=false");
  eq(subFails(r, "/properties/disj/anyOf"),
    ["/properties/disj/anyOf", "/properties/disj/anyOf/0/minLength", "/properties/disj/anyOf/1/type"],
    "anyOf all-fail -> per-branch sub-failures + composite");
}

// oneOf, ZERO branches match: 5 is an integer but a multiple of neither 2 nor 3 -> both branches' multipleOf
// sub-failures PLUS the composite /oneOf.
{
  const r = new Results();
  const ok = Person.evaluate({ name: "Ada", address: { zip: "12345" }, pick: 5 }, r);
  eq(ok, false, "oneOf 0-match -> ok=false");
  eq(subFails(r, "/properties/pick/oneOf"),
    ["/properties/pick/oneOf", "/properties/pick/oneOf/0/multipleOf", "/properties/pick/oneOf/1/multipleOf"],
    "oneOf 0-match -> per-branch sub-failures + composite");
}

// oneOf, MORE THAN ONE branch matches: 6 is a multiple of both 2 and 3 -> over-match. The failed-branch
// sub-failures are misleading noise here (the value matched too MANY branches), so the composite /oneOf is
// emitted ALONE (no /oneOf/<i>/... sub-failures).
{
  const r = new Results();
  const ok = Person.evaluate({ name: "Ada", address: { zip: "12345" }, pick: 6 }, r);
  eq(ok, false, "oneOf over-match -> ok=false");
  eq(subFails(r, "/properties/pick/oneOf"), ["/properties/pick/oneOf"], "oneOf over-match -> composite ONLY");
}

// boolean fast path (no collector): the verdict is unchanged by the collector detail — a failed anyOf/oneOf
// still returns false, an over-matched oneOf still returns false, a satisfied disjunction returns true.
{
  eq(Person.evaluate({ name: "Ada", address: { zip: "12345" }, disj: "ab" }), false, "boolean anyOf all-fail -> false");
  eq(Person.evaluate({ name: "Ada", address: { zip: "12345" }, pick: 5 }), false, "boolean oneOf 0-match -> false");
  eq(Person.evaluate({ name: "Ada", address: { zip: "12345" }, pick: 6 }), false, "boolean oneOf over-match -> false");
  eq(Person.evaluate({ name: "Ada", address: { zip: "12345" }, disj: "abcdef", pick: 4 }), true, "boolean disjunctions satisfied -> true");
}

// Root-level keyword failure: a document-root keyword (here `required`) must carry a '#'-fragment
// absoluteKeywordLocation (base-URI + '#' + pointer), not a path-like one missing the '#'.
{
  const { ok, r } = collect({ name: "Ada" }); // missing required `address`
  eq(ok, false, "root required -> ok=false");
  const reqErr = r.failures.find((f) => f.keywordLocation.endsWith("/required"));
  eq(reqErr !== undefined, true, "root required failure recorded");
  eq(reqErr!.keywordLocation, "/required", "root required keywordLocation = /required (path taken)");
  eq(reqErr!.absoluteKeywordLocation!.endsWith("#/required"), true, "root absoluteKeywordLocation has the '#' fragment");
}

console.log(`COLLECTOR ${pass} passed, ${fail} failed`);
