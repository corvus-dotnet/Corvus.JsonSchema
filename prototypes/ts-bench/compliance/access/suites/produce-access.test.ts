// PROVIDER REAL-OUTPUT test for the immer-style draft (design §5.7): produce<T>(source, recipe) records
// mutations on a typed Draft<T> and lowers the change-set to a Model C byte patch at arbitrary depth.
//   Codegen (profile.json -> out-profile/), transpile, and run are all driven by ../run-access.sh.
import { Profile } from "./out-profile/generated.js";

const enc = new TextEncoder();
const dec = new TextDecoder();
let pass = 0, fail = 0;
function ok(label: string, cond: boolean): void { if (cond) { pass++; } else { fail++; console.log("FAIL " + label); } }

const base = JSON.stringify({ id: "u1", name: "Ada", email: "ada@x.com", age: 30, roles: ["admin", "author"], address: { street: "1 Main", city: "London", country: "UK" } });

// recipe over the typed Draft<Profile>: scalar + nested + array element, in one produce
{
  const out = dec.decode(Profile.produce(enc.encode(base), (d) => {
    d.name = "Ada Lovelace";   // top-level scalar
    d.age = 36;                // top-level scalar
    d.address!.city = "Paris"; // NESTED scalar (address is optional in Profile -> assert present)
    d.roles![0] = "owner";     // array element (roles is optional -> assert present)
  }));
  const p = JSON.parse(out);
  ok("draft scalar+nested+array", p.name === "Ada Lovelace" && p.age === 36 && p.address.city === "Paris" && JSON.stringify(p.roles) === JSON.stringify(["owner", "author"]));
  ok("draft byte-preserve untouched (address siblings, id)", out.includes('"street":"1 Main"') && out.includes('"country":"UK"') && out.includes('"id":"u1"'));
}
// add a member + delete an optional one
{
  const out = dec.decode(Profile.produce(enc.encode(base), (d) => {
    (d as { extra?: string }).extra = "new"; // add
    delete d.email;                          // remove (email is optional)
  }));
  const p = JSON.parse(out);
  ok("draft add + delete", p.extra === "new" && p.email === undefined && p.id === "u1" && p.name === "Ada");
}
// array STRUCTURAL mutation: push grows the array (length change -> that one array re-serialised, siblings preserved)
{
  const out = dec.decode(Profile.produce(enc.encode(base), (d) => {
    d.roles!.push("owner");       // ["admin","author"] -> ["admin","author","owner"]
  }));
  const p = JSON.parse(out);
  ok("draft array push", JSON.stringify(p.roles) === JSON.stringify(["admin", "author", "owner"]));
  ok("draft push byte-preserve siblings", out.includes('"id":"u1"') && out.includes('"street":"1 Main"') && out.includes('"country":"UK"'));
}
// array splice (remove + insert in one call) + a same-pass scalar edit
{
  const out = dec.decode(Profile.produce(enc.encode(base), (d) => {
    d.roles!.splice(0, 1, "owner");  // remove "admin", insert "owner" -> ["owner","author"]
    d.age = 41;
  }));
  const p = JSON.parse(out);
  ok("draft array splice + scalar", JSON.stringify(p.roles) === JSON.stringify(["owner", "author"]) && p.age === 41);
}
// pop shrinks the array
{
  const out = dec.decode(Profile.produce(enc.encode(base), (d) => { d.roles!.pop(); }));
  ok("draft array pop", JSON.stringify(JSON.parse(out).roles) === JSON.stringify(["admin"]));
}
// no-op recipe returns the source byte-identical
{
  ok("draft no-op byte-identical", dec.decode(Profile.produce(enc.encode(base), () => { /* nothing */ })) === base);
}

console.log(`produce-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`produce-access: ${fail} failed`); }
console.log("OK -- produce(recipe) on the typed Draft<Profile>: scalar/nested/array-element/add/delete + byte-preservation.");
