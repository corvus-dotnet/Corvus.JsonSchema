// PROVIDER REAL-OUTPUT test for OpenAPI nullable (gap E1): a `nullable:true` property (OpenAPI 3.0) and a
// `type:["string","null"]` property (OpenAPI 3.1) must BOTH emit the type `T | null` AND a validator that
// accepts the value and null, while a non-nullable property rejects null. run-access.mjs generates the 3.0
// module under ./out-openapi30/ (forced dialect "openapi30", since the doc carries no $schema) and the 3.1
// module under ./out-openapi31/ (forced dialect "openapi31"); both are transpiled there, and the raw
// generated.ts is written alongside as generated.ts.txt for the type-text assertion.
import * as oa30 from "./out-openapi30/generated.js";
import * as oa31 from "./out-openapi31/generated.js";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
let pass = 0, fail = 0;
function ok(label: string, cond: boolean): void { if (cond) { pass++; } else { fail++; console.log("FAIL " + label); } }

for (const [dialect, dir, mod] of [
  ["openapi30 (nullable:true)", "out-openapi30", oa30] as const,
  ["openapi31 (type:[string,null])", "out-openapi31", oa31] as const,
]) {
  const ts: string = readFileSync(join(HERE, dir, "generated.ts.txt"), "utf8");

  // 1) TYPE: the nullable property is `T | null`; the non-nullable property is bare `string` (no `| null`).
  const labelLine = ts.split("\n").find((l) => /\breadonly label\b/.test(l)) ?? "";
  const codeLine = ts.split("\n").find((l) => /\breadonly code\b/.test(l)) ?? "";
  ok(`${dialect}: label typed string | null`, /string\s*\|\s*null/.test(labelLine));
  ok(`${dialect}: code typed string (not nullable)`, /:\s*string\b/.test(codeLine) && !/\|\s*null/.test(codeLine));

  // 2) VALIDATOR: the nullable property accepts a string AND null, and rejects a wrong-typed value.
  ok(`${dialect}: label accepts string`, mod.evaluateLabel("hi", null, "", "", null) === true);
  ok(`${dialect}: label accepts null`, mod.evaluateLabel(null, null, "", "", null) === true);
  ok(`${dialect}: label rejects number`, mod.evaluateLabel(42, null, "", "", null) === false);

  // 3) VALIDATOR: the non-nullable property rejects null (and still accepts a string).
  ok(`${dialect}: code accepts string`, mod.evaluateCode("x", null, "", "", null) === true);
  ok(`${dialect}: code rejects null`, mod.evaluateCode(null, null, "", "", null) === false);

  // 4) WHOLE-OBJECT: a null nullable value validates, a null non-nullable value does not.
  ok(`${dialect}: object with label=null validates`, mod.evaluateRoot({ code: "c", label: null }) === true);
  ok(`${dialect}: object with label="x" validates`, mod.evaluateRoot({ code: "c", label: "x" }) === true);
  ok(`${dialect}: object with code=null rejected`, mod.evaluateRoot({ code: null }) === false);
}

console.log(`openapi-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`openapi-access: ${fail} failed`); }
console.log("OK — OpenAPI 3.0 nullable + 3.1 type-array null both emit T | null and a null-accepting validator; non-nullable rejects null.");
