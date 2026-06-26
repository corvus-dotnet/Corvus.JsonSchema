// Recipe 005 — Extending a base type with allOf.
import { type Employee, evaluateRoot, buildEmployee, asEmail } from "./generated.js";
const dec = new TextDecoder();
// allOf merges the base Person (name, email) into Employee, which adds employeeId/department.
const bytes = buildEmployee({ name: "Ada", email: asEmail("ada@example.com"), employeeId: "E-1", department: "R&D" });
console.log("employee:     ", dec.decode(bytes));
console.log("valid:        ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
const e = JSON.parse(dec.decode(bytes)) as Employee;
console.log("name (base):  ", e.name);
console.log("id (own):     ", e.employeeId);
console.log("missing name: ", evaluateRoot({ employeeId: "E-2" })); // false — name required by the base
