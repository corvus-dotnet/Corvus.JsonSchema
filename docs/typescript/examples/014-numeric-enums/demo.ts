// Recipe 014 — Numeric enumerations.
import { type Response, evaluateRoot, buildResponse } from "./generated.js";
const dec = new TextDecoder();
const bytes = buildResponse({ status: 404 });
console.log("valid:    ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
const r = JSON.parse(dec.decode(bytes)) as Response;
console.log("ok?:      ", r.status === 200);
console.log("bad code: ", evaluateRoot({ status: 418 })); // false — not 200 | 404 | 500
