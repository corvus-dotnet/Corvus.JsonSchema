// PROVIDER REAL-OUTPUT test for the opt-in canonical writer (gap F1, §5.7): alongside the native-floor
// `build{Type}` (caller key order, one JSON.stringify + UTF-8 encode), the provider emits
// `buildCanonical{Type}` — RFC 8785 (JCS) canonical bytes (object keys recursively sorted by UTF-16 code
// unit, ECMAScript number forms), mirroring the C# JsonCanonicalizer, via the shared runtime `canonicalize`.
// For content-addressing / hashing / signatures / cache keys / golden-file determinism.
// Run after generating canonical.json into out-canonical/:
//   Codegen (canonical.json -> out-canonical/), transpile, and run are all driven by ../run-access.sh.
import { buildDoc, buildCanonicalDoc, type Doc } from "./out-canonical/generated.js";

const dec = new TextDecoder();
let pass = 0;
let fail = 0;
function eq(label: string, got: string, want: string): void {
  if (got === want) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${got} != ${want}`); }
}

const doc: Doc = { zeta: "z", alpha: 1, nested: { y: "Y", x: "X" } };

// build = the native floor: caller key order preserved verbatim (one JSON.stringify).
eq("build preserves caller key order", dec.decode(buildDoc(doc)), '{"zeta":"z","alpha":1,"nested":{"y":"Y","x":"X"}}');

// buildCanonical = RFC 8785: object keys recursively sorted by UTF-16 code unit (top-level AND nested).
eq("buildCanonical sorts keys recursively", dec.decode(buildCanonicalDoc(doc)), '{"alpha":1,"nested":{"x":"X","y":"Y"},"zeta":"z"}');

// Content-addressing: a differently-ordered but value-equal Doc canonicalises to byte-identical output.
const reordered: Doc = { nested: { x: "X", y: "Y" }, alpha: 1, zeta: "z" };
eq("buildCanonical is order-independent", dec.decode(buildCanonicalDoc(doc)), dec.decode(buildCanonicalDoc(reordered)));

console.log(`canonical-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`canonical-access: ${fail} failed`); }
console.log("OK — provider emits buildCanonical (RFC 8785 canonical bytes); build stays at the native floor (caller order).");
