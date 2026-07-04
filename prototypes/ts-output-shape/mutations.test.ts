// MUTATIONS (design §5.7): immer-style `produce` over a typed Draft<T>. Asserts (1) edits apply,
// (2) the original document is untouched (immutability / structural sharing), and (3) the recorded
// change-set IS RFC 6902 JSON Patch (the universal currency that also lowers to a Model C byte patch).
import { suite } from "./harness";
import { produce, recordChanges } from "./runtime";
import { doc } from "./mutation-model";

const t = suite("mutations");

// --- produce: "mutate" the draft, get a NEW immutable document ---
const next = produce(doc, (d) => {
  d.age = 31;                  // scalar
  d.address.city = "London";   // nested, typed
  d.tags.push("new");          // array mutation on the mutable draft
});
t.eq("scalar edit applied", next.value.age, 31);
t.eq("nested edit applied", next.value.address.city, "London");
t.ok("array edit applied", next.value.tags.includes("new"));

// --- the original document is untouched ---
t.eq("original scalar unchanged", doc.value.age, 30);
t.eq("original nested unchanged", doc.value.address.city, "Anytown");
t.eq("original array unchanged", doc.value.tags.length, 1);
t.ok("produce yields a new object graph", next !== doc && next.value !== doc.value);

// --- the recorded change-set is RFC 6902 JSON Patch (§5.7) ---
const { next: n2, patches } = recordChanges(doc.value, (d) => {
  d.age = 42;
  d.address.city = "Paris";
});
t.eq("scalar patch (RFC 6902)", patches[0], { op: "replace", path: "/age", value: 42 });
t.eq("nested patch (RFC 6902)", patches[1], { op: "replace", path: "/address/city", value: "Paris" });
t.eq("change applied to the new value", n2.address.city, "Paris");
t.eq("change does not touch the source", doc.value.address.city, "Anytown");

t.done();
