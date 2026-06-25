// Worked example of the byte-RMW surface (build / patch / applyTo). Compile with the generated.ts +
// corvus-runtime.ts of both out-profile and out-employee, run with node.
import { buildProfile, patchProfile, type Profile } from "./out-profile/generated.js";
import { buildEmployee, applyToEmployee, type Person, type Worker } from "./out-employee/generated.js";

const dec = new TextDecoder();
const show = (label: string, bytes: Uint8Array): void => console.log(label.padEnd(24), dec.decode(bytes));

// ── Profile (a plain object): build, patch, nested-array ──────────────────────────────────────────
// BUILD — typed construction; canonical (schema) order regardless of literal order.
const doc = buildProfile({
  id: "u-1", name: "Ada Lovelace", email: "ada@example.com",
  roles: ["admin", "author"],
  address: { street: "12 Mayfair", city: "London", country: "UK" },
});
show("1. built:", doc);

// PATCH — set/upsert + delete: name replaced, age ADDED (wasn't present), email DELETED.
show("2. patch +age -email:", patchProfile(doc, { name: "Ada, Countess of Lovelace", age: 36 }, ["email"]));

// PATCH (nested array) — edit `roles` element-wise: drop element 0, append "reviewer".
show("3. roles edited:", patchProfile(doc, {}, undefined, { roles: { removeAt: [0], append: ["reviewer"] } }));

// ── Employee = allOf[Person, Worker]: applyTo overlays ONE constituent (allOf composition) ─────────
const emp = buildEmployee({ name: "Alan Turing", age: 41, employeeId: "E-7", department: "Research" });
show("4. employee built:", emp);

// applyTo only accepts a Person or a Worker (the allOf members) — not arbitrary data.
const asPerson: Person = { name: "A. M. Turing" };
const asWorker: Worker = { department: "Cryptanalysis" };
show("5. applyTo Person:", applyToEmployee(emp, asPerson));
show("6. applyTo Worker:", applyToEmployee(emp, asWorker));

// ── byte-preservation: change one scalar, the nested address + roles bytes survive verbatim ────────
const v = patchProfile(doc, { name: "X" } as Partial<Profile>);
console.log("\nnested address copied verbatim:", dec.decode(v).includes('"address":{"street":"12 Mayfair","city":"London","country":"UK"}'));
console.log("roles array copied verbatim:   ", dec.decode(v).includes('"roles":["admin","author"]'));
