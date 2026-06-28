// Recipe 014 — Numeric enumerations.
import { Response } from "./generated.js";
const dec = new TextDecoder();
const bytes = Response.build({ status: 404 });
console.log("valid:    ", Response.evaluate(JSON.parse(dec.decode(bytes)))); // true
const r = JSON.parse(dec.decode(bytes)) as Response;
console.log("ok?:      ", r.status === 200);
console.log("bad code: ", Response.evaluate({ status: 418 })); // false — not 200 | 404 | 500
