// Recipe 006 — Constraining a base type with allOf.
import { SmallBatch } from "./generated.js";
const dec = new TextDecoder();
// allOf adds a constraint to the base: Batch requires size>=1; SmallBatch also caps size<=100.
console.log("size 50:  ", SmallBatch.evaluate(SmallBatch.build({ size: 50 }))); // true
console.log("size 200: ", SmallBatch.evaluate({ size: 200 })); // false — maximum 100 (added here)
console.log("size 0:   ", SmallBatch.evaluate({ size: 0 }));   // false — minimum 1 (from the base)
