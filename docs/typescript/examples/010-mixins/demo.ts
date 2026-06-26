// Recipe 010 — Mix-in types (allOf of multiple bases).
import { type Widget, evaluateRoot, buildWidget, asCreatedAt } from "./generated.js";
const dec = new TextDecoder();
// allOf of MULTIPLE bases -> Widget merges Named (name) and Timestamped (createdAt), plus its own id.
const bytes = buildWidget({ name: "gauge", createdAt: asCreatedAt("2026-06-26T10:00:00Z"), id: "w-1" });
console.log("widget:", dec.decode(bytes));
console.log("valid: ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
const w = JSON.parse(dec.decode(bytes)) as Widget;
console.log("name:  ", w.name, "| id:", w.id);
