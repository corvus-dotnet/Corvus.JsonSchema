// DEFAULTS access test (gap B1): JSON Schema `default` gets BOTH halves in the generated TS —
//   DOCUMENT: an `@default <compact-json>` JSDoc tag on every type/property that declares a default;
//   APPLY:    a tree-shakeable `withDefaults{T}(value)` per object type that returns a shallow-clone with
//             every ABSENT (`!(key in value)`) defaulted property filled, recursing into PRESENT nested
//             object / array-of-object values whose type has defaults, and NEVER overriding a present value.
//
// `default` is an ANNOTATION — it MUST NOT affect validation — so this suite touches only the surface
// helpers and the JSDoc text; the validators and shared runtime are unchanged (proven by run-compliance.sh
// staying byte-stable). The JSDoc text lives ONLY in the generated .ts (esbuild strips comments when it
// transpiles to .js), so the @default assertions read the raw ./out-defaults/generated.ts.txt; the behaviour
// assertions import the transpiled ./out-defaults/generated.js.
//
// Codegen (defaults.json -> out-defaults/), the raw-text copy, and run are all driven by ../run-access.sh.
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { withDefaultsConfig, type Config } from "./out-defaults/generated.js";

const HERE = dirname(fileURLToPath(import.meta.url));
const ts: string = readFileSync(join(HERE, "out-defaults", "generated.ts.txt"), "utf8");

let pass = 0;
let fail = 0;
function ok(label: string, cond: boolean): void {
  if (cond) { pass++; }
  else { fail++; console.log(`FAIL ${label}`); }
}
function eq<T>(label: string, got: T, want: T): void {
  ok(`${label}  (got ${JSON.stringify(got)}, want ${JSON.stringify(want)})`, JSON.stringify(got) === JSON.stringify(want));
}
function contains(label: string, needle: string): void {
  ok(`${label}  (expected to contain: ${JSON.stringify(needle)})`, ts.includes(needle));
}

// ---- 1) DOCUMENT: @default JSDoc tags, value as compact single-line JSON ----
contains("@default on a top-level integer property", "   * @default 3\n");
contains("@default on a top-level string property (JSON-quoted)", '   * @default "anonymous"\n');
contains("@default on a nested-object property", "   * @default 30\n");
contains("@default on an array-element-object property", "   * @default 1.5\n");

// ---- 2) APPLY: withDefaults fills ABSENT top-level defaults, leaves non-defaulted absent props absent ----
const empty = withDefaultsConfig({} as Config);
eq("empty: retries filled to default 3", empty.retries, 3);
eq("empty: label filled to default \"anonymous\"", empty.label, "anonymous");
ok("empty: non-defaulted `name` stays absent", !("name" in empty));
ok("empty: nested object NOT fabricated (it has no key in input)", !("nested" in empty));

// ---- 3) APPLY: recurse into a PRESENT nested object so a nested default fills ----
const withNested = withDefaultsConfig({ nested: {} } as Config);
eq("nested present-but-empty: top-level retries still filled", withNested.retries, 3);
eq("nested present-but-empty: nested.timeout filled via recursion", withNested.nested!.timeout, 30);

// ---- 4) APPLY: recurse into PRESENT array-of-object elements ----
const withArray = withDefaultsConfig({ items: [{ id: "a" }, { id: "b", weight: 9 }] } as Config);
eq("array element 0: absent weight filled to default 1.5", withArray.items![0].weight, 1.5);
eq("array element 1: present weight NOT overridden", withArray.items![1].weight, 9);
eq("array element id preserved", withArray.items![0].id, "a");

// ---- 5) APPLY: a PRESENT value is NEVER overridden (top-level + nested) ----
const present = withDefaultsConfig({ retries: 9, label: "x", name: "n", nested: { timeout: 99, enabled: true } } as Config);
eq("present retries not overridden", present.retries, 9);
eq("present label not overridden", present.label, "x");
eq("present name preserved", present.name, "n");
eq("present nested.timeout not overridden", present.nested!.timeout, 99);
eq("present nested.enabled preserved", present.nested!.enabled, true);

// ---- 6) APPLY: the input object is never mutated (shallow clone) ----
const input = {} as Config;
withDefaultsConfig(input);
ok("input object not mutated", !("retries" in input));

console.log(`defaults-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`defaults-access: ${fail} failed`); }
console.log("OK — @default JSDoc is emitted, and withDefaults{T} fills absent defaults (top-level + nested + array), recurses, and never overrides a present value.");
