// Recipe 014 — Numeric enumerations.
import { Response } from "./generated.js";
const dec = new TextDecoder();
const bytes = Response.build({ status: 404 });
console.log("valid:    ", Response.evaluate(bytes)); // true
const r = Response.parse(bytes);
console.log("ok?:      ", r.status === 200);
console.log("bad code: ", Response.evaluate({ status: 418 })); // false — not 200 | 404 | 500
