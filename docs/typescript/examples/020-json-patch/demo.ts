// Runnable demo for recipe 020 — JSON Patch (RFC 6902) and JSON Merge Patch (RFC 7396).
import { Contact, type JsonPatch } from "./generated.js";

const dec = new TextDecoder();

// A document to patch — canonical UTF-8 JSON bytes (the wire / persistence shape).
const original = Contact.build({
  name: "Ada Lovelace",
  email: "ada@example.com",
  phones: ["+1-555-0100"],
  address: { city: "London", zip: "EC1" },
  version: 1,
});
console.log("original:        ", dec.decode(original));

// 1. RFC 6902 JSON Patch — an array of operations applied atomically (a failed `test` aborts the whole patch).
const patch: JsonPatch = [
  { op: "test", path: "/version", value: 1 },              // guard: only apply if version is 1
  { op: "replace", path: "/version", value: 2 },
  { op: "add", path: "/phones/-", value: "+1-555-0199" },  // "-" appends to the array
  { op: "copy", from: "/name", path: "/displayName" },     // copy a value to another field
  { op: "remove", path: "/address/zip" },
];
const patched = Contact.applyPatch(original, patch);       // bytes in -> patched bytes out
console.log("1. applyPatch:   ", dec.decode(patched));

// 2. Atomicity — a failed `test` aborts the entire patch; the original is untouched.
try {
  Contact.applyPatch(original, [
    { op: "test", path: "/version", value: 99 },           // fails (version is 1)
    { op: "replace", path: "/name", value: "Nope" },       // never applied
  ]);
} catch (e) {
  console.log("2. test aborts:  ", (e as Error).message);
}

// 3. RFC 7396 Merge Patch — a JSON document: a member set to null deletes that key, objects merge recursively.
const merged = Contact.applyMergePatch(original, {
  email: "ada@new.example.com",   // replace
  address: { zip: null },         // delete address.zip, keep address.city
  phones: null,                   // delete phones entirely
});
console.log("3. applyMerge:   ", dec.decode(merged));

// 4. Diff two documents -> the patch that turns one into the other (apply it, store it, or send it).
//    applyPatch returns canonical bytes (RFC 8785), so compare against the canonical target.
const target = Contact.buildCanonical({ name: "Ada L.", phones: ["+1-555-0100"], address: { city: "Oxford" }, version: 2 });
const diff = Contact.createPatch(original, target);
console.log("4. createPatch:  ", JSON.stringify(diff));
console.log("   round-trips:  ", dec.decode(Contact.applyPatch(original, diff)) === dec.decode(target)); // true

// 5. The RFC 7396 merge-patch diff between the same two documents.
console.log("5. createMerge:  ", JSON.stringify(Contact.createMergePatch(original, target)));
