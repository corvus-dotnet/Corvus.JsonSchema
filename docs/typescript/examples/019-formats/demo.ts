// Recipe 019 — Format validation (branded types).
import { Account, Created, Id, Website } from "./generated.js";
const dec = new TextDecoder();
// Each `format` is a branded type with a validating factory; a raw string isn't assignable.
const bytes = Account.build({
  id: Id.from("6f9619ff-8b86-d011-b42d-00cf4fc964ff"),
  website: Website.from("https://example.com"),
  created: Created.from("2026-06-26T10:00:00Z"),
});
console.log("valid:    ", Account.evaluate(JSON.parse(dec.decode(bytes)))); // true
try { Id.from("nope"); } catch (e) { console.log("Id.from threw:", (e as Error).message); }
