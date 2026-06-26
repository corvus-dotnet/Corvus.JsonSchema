// Runnable demo for recipe 001 — Simple Data Objects.
//   build (compile + run):  see ../README.md  (tsc *.ts && node demo.js)
import {
  type Person,
  evaluateRoot,
  buildPerson,
  patchPerson,
  producePerson,
  asBirthDate,
} from "./generated.js";

const dec = new TextDecoder();

// 1. Build a Person from plain values -> canonical UTF-8 JSON bytes (the wire/persistence shape).
const bytes = buildPerson({
  familyName: "Brontë",
  givenName: "Anne",
  birthDate: asBirthDate("1820-01-17"), // `format: date` is a validating branded factory
  height: 1.52,
});
console.log("1. built:        ", dec.decode(bytes));

// 2. Validate untrusted input — a boolean, no exceptions, no allocation of an error graph.
const incoming: unknown = JSON.parse(dec.decode(bytes));
console.log("2. valid:        ", evaluateRoot(incoming)); // true
console.log("   missing reqd: ", evaluateRoot({ givenName: "Anne" })); // false (familyName required)

// 3. Read it as a typed, readonly Person (the parsed value IS the value — nothing to wrap).
const person = incoming as Person;
console.log("3. familyName:   ", person.familyName);
console.log("   birthDate:    ", person.birthDate);
console.log("   otherNames?:  ", person.otherNames !== undefined); // false — optional, absent

// 4. Patch — change only the named fields, spliced at the byte level (unchanged bytes copied verbatim).
const patched = patchPerson(bytes, { height: 1.55 });
console.log("4. patched:      ", dec.decode(patched));

// 5. Produce — immer-style recipe over a typed, mutable Draft<Person>.
const produced = producePerson(bytes, (d) => {
  d.birthDate = asBirthDate("1984-06-03");
});
console.log("5. produced:     ", dec.decode(produced));

// 6. Remove an optional property (the 3rd patch argument names removals).
const three = buildPerson({ familyName: "X", givenName: "Y", otherNames: "temp" });
const removed = patchPerson(three, {}, ["otherNames"]);
console.log("6. removed opt:  ", dec.decode(removed));
