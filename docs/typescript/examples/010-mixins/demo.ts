// Recipe 010 — Mix-in types (allOf of multiple bases).
import { CreatedAt, Widget } from "./generated.js";
const dec = new TextDecoder();
// allOf of MULTIPLE bases -> Widget merges Named (name) and Timestamped (createdAt), plus its own id.
const bytes = Widget.build({ name: "gauge", createdAt: CreatedAt.from("2026-06-26T10:00:00Z"), id: "w-1" });
console.log("widget:", dec.decode(bytes));
console.log("valid: ", Widget.evaluate(bytes)); // true
const w = Widget.parse(bytes);
console.log("name:  ", w.name, "| id:", w.id);
