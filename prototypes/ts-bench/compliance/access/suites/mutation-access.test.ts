// PROVIDER REAL-OUTPUT test for the byte-level immer draft (§5.7): the GENERIC `produce(source, recipe)`
// records mutations on a typed Draft<T> and lowers them to a Model C byte patch (unchanged bytes copied
// verbatim); `recordChanges` exposes the same change-set as RFC 6902 JSON Patch.
// Run after generating mutation.json into out-mutation/:
//   Codegen (mutation.json -> out-mutation/), transpile, and run are all driven by ../run-access.sh.
import { produce, recordChanges } from "./out-mutation/corvus-runtime.js";
import { Doc, decodeAndParse } from "./out-mutation/generated.js";

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

// array STRUCTURAL mutation (the clone+diff recorder handles push/splice/pop, not just index-set):
// a length change re-serialises that one array; every sibling member is copied through verbatim.
const grown = dec.decode(produce<Doc>(enc.encode(base), (d) => {
  d.tags.push("stats");       // ["math"] -> ["math","stats"]
  d.age = 31;                 // same-pass scalar edit
}));
const g = JSON.parse(grown);
eq("array push applied", g.tags, ["math", "stats"]);
eq("push + scalar same pass", g.age, 31);
eq("push byte-preserve siblings", g.name, "Ada");

// the recorded change-set is RFC 6902 JSON Patch
const { patches } = recordChanges(JSON.parse(base) as Doc, (d) => {
  d.age = 42;
  d.address.city = "Paris";
});
eq("scalar patch (RFC 6902)", patches[0], { op: "replace", path: "/age", value: 42 });
eq("nested patch (RFC 6902)", patches[1], { op: "replace", path: "/address/city", value: "Paris" });

// a push surfaces as a single whole-array replace op (length changed)
const { patches: ap } = recordChanges(JSON.parse(base) as Doc, (d) => { d.tags.push("stats"); });
eq("array push patch (RFC 6902)", ap, [{ op: "replace", path: "/tags", value: ["math", "stats"] }]);

// Convenience surface: evaluate accepts bytes; parse decodes bytes or JSON.parses a string -> typed;
// decodeAndParse is re-exported from the generated module.
const bytes = enc.encode(base);
eq("evaluate(bytes)", Doc.evaluate(bytes), true);
eq("evaluate(parsed value)", Doc.evaluate(JSON.parse(base)), true);
eq("parse(bytes).name", Doc.parse(bytes).name, "Ada");
eq("parse(bytes).nested", Doc.parse(bytes).address.city, "Anytown");
eq("parse(string).age", Doc.parse(base).age, 30);
eq("decodeAndParse(bytes)", (decodeAndParse(bytes) as Doc).name, "Ada");

console.log(`mutation-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`mutation-access: ${fail} failed`); }
console.log("OK — byte produce(recipe) over the typed Draft: edits apply, unchanged bytes verbatim, RFC 6902 patches.");
