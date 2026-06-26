// Recipe 019 — Format validation (branded types).
import { evaluateRoot, buildAccount, asId, asWebsite, asCreated } from "./generated.js";
const dec = new TextDecoder();
// Each `format` is a branded type with a validating factory; a raw string isn't assignable.
const bytes = buildAccount({
  id: asId("6f9619ff-8b86-d011-b42d-00cf4fc964ff"),
  website: asWebsite("https://example.com"),
  created: asCreated("2026-06-26T10:00:00Z"),
});
console.log("valid:    ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
try { asId("nope"); } catch (e) { console.log("asId threw:", (e as Error).message); }
