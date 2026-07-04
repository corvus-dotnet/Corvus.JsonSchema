// JSDOC access test (gap C1–C4): the provider emits a `/** ... */` JSDoc block on each generated interface
// (from the type's title/description) and on each property (from the property subschema's title/description),
// with @deprecated / @example / @readonly / @writeonly tags and `*/`-safe escaping.
//
// JSDoc lives ONLY in the generated .ts — esbuild strips comments when it transpiles to .js — so this suite
// asserts on the RAW generated.ts text, NOT the imported .js. run-access.mjs writes that raw text alongside
// the transpiled module as ./out-jsdoc/generated.ts.txt (see the schema jsdoc.json); we read it from disk.
//
// Codegen (jsdoc.json -> out-jsdoc/), the raw-text copy, and run are all driven by ../run-access.sh.
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const ts: string = readFileSync(join(HERE, "out-jsdoc", "generated.ts.txt"), "utf8");

let pass = 0;
let fail = 0;
function ok(label: string, cond: boolean): void {
  if (cond) { pass++; }
  else { fail++; console.log(`FAIL ${label}`); }
}
function contains(label: string, needle: string): void {
  ok(`${label}  (expected to contain: ${JSON.stringify(needle)})`, ts.includes(needle));
}
function absentIn(label: string, haystack: string, needle: string): void {
  ok(`${label}  (expected NOT to contain: ${JSON.stringify(needle)})`, !haystack.includes(needle));
}

// The interface block, so property-relative assertions can scope to its body.
const ifaceStart = ts.indexOf("export interface Widget {");
ok("interface Widget is emitted", ifaceStart >= 0);
const ifaceEnd = ts.indexOf("\n}", ifaceStart);
const iface = ifaceStart >= 0 ? ts.slice(ifaceStart, ifaceEnd) : "";

// 1) interface-level JSDoc: the type title + (multi-line) description, immediately before the interface.
contains("interface JSDoc opens with /**", "/**\n * Widget\n");
contains("interface description line 1", " * A widget carrying assorted annotations.\n");
contains("interface description line 2 (newline -> continuation)", " * Second line of the description.\n");
ok(
  "interface JSDoc precedes `export interface Widget`",
  /\/\*\*[\s\S]*?\*\/\nexport interface Widget \{/.test(ts),
);

// 2) property-level JSDoc (indented two spaces to match the property), from the property subschema.
contains("property `id` title", "   * Identifier\n");
contains("property `id` description", "   * The unique id of the widget.\n");

// 3a) @readonly tag from readOnly:true on `id`.
contains("@readonly tag on read-only property", "   * @readonly\n");
// 3b) one @example per examples entry, each as compact single-line JSON (scalar + object).
contains("@example scalar entry", '   * @example "abc-123"\n');
contains("@example object entry (compact JSON)", '   * @example {"nested":"value"}\n');
// 3c) @deprecated tag from deprecated:true on `legacyCode`.
contains("@deprecated tag", "   * @deprecated\n");
// 3d) @writeonly tag from writeOnly:true on `secret`.
contains("@writeonly tag on write-only property", "   * @writeonly\n");

// 4a) `*/` inside a description must NOT terminate the comment: it is broken to `* /` inside the JSDoc.
// (The validator below the interface still embeds the description as a JS *string literal* where a raw `*/`
// is harmless and expected — so the no-raw-`*/` assertion is scoped to the interface's JSDoc region only.)
contains("description `*/` is neutralised to `* /`", "Avoid the * / terminator here.");
absentIn("no raw `*/` smuggled into the interface JSDoc", iface, "the */ terminator");
// 4b) a property with NO annotations gets NO JSDoc block: `plain` sits directly after a `;` line.
ok(
  "plain property carries no JSDoc block",
  /;\n  readonly plain\?: number;/.test(iface) && !/\*\/\n  readonly plain\?:/.test(iface),
);

console.log(`jsdoc-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`jsdoc-access: ${fail} failed`); }
console.log("OK — the provider emits JSDoc from schema annotations (title/description/@deprecated/@example/read-write markers).");
