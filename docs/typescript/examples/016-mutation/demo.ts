// Recipe 016 — Mutation (build / patch / produce).
import { Document } from "./generated.js";
const dec = new TextDecoder();
// build from scratch.
const bytes = Document.build({ title: "Draft", owner: { name: "Ada", email: "ada@x.com" }, tags: ["wip"], version: 1 });
console.log("built:    ", dec.decode(bytes));
// patch — change only the named top-level fields (byte-spliced; the rest copied verbatim).
console.log("patched:  ", dec.decode(Document.patch(bytes, { version: 2 })));
// produce — an immer-style recipe for nested + array edits.
const edited = Document.produce(bytes, (d) => {
  d.title = "Final";
  d.owner!.name = "Ada Lovelace"; // nested edit
  d.tags!.push("published");      // array append
});
console.log("produced: ", dec.decode(edited));
// remove an optional field.
console.log("trimmed:  ", dec.decode(Document.patch(bytes, {}, ["version"])));
