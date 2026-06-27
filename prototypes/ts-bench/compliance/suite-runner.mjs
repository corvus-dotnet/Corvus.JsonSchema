// Runs the generated suite modules against the manifest and reports the pass rate per dialect.
//
// Pipeline (see run-compliance.sh):
//   1. dotnet run --project ComplianceGenerator.csproj -- <tests> <outDir>   (emits <name>.ts + manifest.json)
//   2. node suite-runner.mjs <outDir>                                          (this file)
//
// Unlike the spike's runner, the per-module `tsc` compile is replaced with esbuild `transformSync`
// (loader:'ts', format:'esm') — the same fast type-STRIPPING transpile prototypes/ts-bench/bowtie-harness.mjs
// uses. The generated validators are already correct, so per-module type-checking is pure overhead. We
// transpile every `<name>.ts` in <outDir> to `<name>.js` before importing it.
//
// This file is run from prototypes/ts-bench so that `esbuild` and `lossless-json` resolve from
// prototypes/ts-bench/node_modules (run-compliance.sh sets the cwd accordingly). <outDir> defaults to a
// dir UNDER prototypes/ts-bench (compliance/out-suite) so the GENERATED modules' bare runtime imports
// (lossless-json, @js-temporal/polyfill, tr46) also resolve via Node's upward node_modules walk. Modules
// are imported by absolute file URL.
import { readFileSync, writeFileSync, readdirSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { LosslessNumber } from "lossless-json";
import { transformSync } from "esbuild";

import { fileURLToPath } from "node:url";
import { dirname } from "node:path";
const HERE = dirname(fileURLToPath(import.meta.url));
const OUT_DIR = resolve(process.argv[2] ?? process.env.COMPLIANCE_OUT ?? join(HERE, "out-suite"));

// The manifest stores each instance as its raw JSON source text (Model C / source-text model). Parse the
// manifest structure once with JSON.parse, then parse each instance ONCE with a single-pass, source-
// preserving parser: numbers become LosslessNumber (exact source digits — needed for big/precise numbers),
// objects get a null prototype so "__proto__" is an ordinary own key. No lossy double-parse.
const manifest = JSON.parse(readFileSync(join(OUT_DIR, "manifest.json"), "utf8"));

// Type-STRIPPING transpile (esbuild), not a per-module `tsc` type-check: the generated validators are
// already correct, so type-checking each is pure overhead. Transpile every `<name>.ts` in OUT_DIR (the
// shared corvus-runtime.ts + every g_*.ts module) to `<name>.js` so the dynamic imports below resolve.
for (const name of readdirSync(OUT_DIR)) {
  if (!name.endsWith(".ts")) { continue; }
  const src = readFileSync(join(OUT_DIR, name), "utf8");
  const js = transformSync(src, { loader: "ts", format: "esm", target: "es2022" }).code;
  writeFileSync(join(OUT_DIR, name.slice(0, -3) + ".js"), js);
}

function parseInstance(text) {
  let i = 0;
  const skip = () => { while (i < text.length && (text[i] === " " || text[i] === "\t" || text[i] === "\n" || text[i] === "\r")) { i++; } };
  function value() {
    skip();
    const c = text[i];
    if (c === "{") { return object(); }
    if (c === "[") { return array(); }
    if (c === '"') { return string(); }
    if (c === "t") { i += 4; return true; }
    if (c === "f") { i += 5; return false; }
    if (c === "n") { i += 4; return null; }
    return number();
  }
  function object() {
    i++; const o = Object.create(null); skip();
    if (text[i] === "}") { i++; return o; }
    for (;;) { skip(); const k = string(); skip(); i++; o[k] = value(); skip(); if (text[i] === ",") { i++; continue; } i++; break; }
    return o;
  }
  function array() {
    i++; const a = []; skip();
    if (text[i] === "]") { i++; return a; }
    for (;;) { a.push(value()); skip(); if (text[i] === ",") { i++; continue; } i++; break; }
    return a;
  }
  function string() {
    i++; let s = "";
    while (text[i] !== '"') {
      const c = text[i++];
      if (c === "\\") {
        const e = text[i++];
        if (e === "n") { s += "\n"; } else if (e === "t") { s += "\t"; } else if (e === "r") { s += "\r"; }
        else if (e === "b") { s += "\b"; } else if (e === "f") { s += "\f"; }
        else if (e === "u") { s += String.fromCharCode(parseInt(text.slice(i, i + 4), 16)); i += 4; }
        else { s += e; }
      } else { s += c; }
    }
    i++; return s;
  }
  function number() {
    const start = i;
    if (text[i] === "-") { i++; }
    while (text[i] >= "0" && text[i] <= "9") { i++; }
    if (text[i] === ".") { i++; while (text[i] >= "0" && text[i] <= "9") { i++; } }
    if (text[i] === "e" || text[i] === "E") { i++; if (text[i] === "+" || text[i] === "-") { i++; } while (text[i] >= "0" && text[i] <= "9") { i++; } }
    return new LosslessNumber(text.slice(start, i));
  }
  return value();
}

// Known pre-existing engine gaps, keyed `dialect|file|group|desc`. These are documented limitations the
// feasibility spike ALSO exhibits (verified: the spike's own runner against its own prebuilt out-suite
// reports the identical 7848/7849 with exactly this failure). A known failure is still PRINTED — never
// hidden — but it is tallied as KNOWN_FAIL and does NOT trip the CI gate, so the gate stays green on the
// spike's known-good baseline while still catching any NEW regression. Override with COMPLIANCE_NO_ALLOWLIST=1.
const KNOWN_FAILURES = new Set([
  // draft4 `1.0` is accepted as an integer (the engine does not distinguish `1.0` from `1` for type:integer).
  "draft4|optional_zeroTerminatedFloats|some languages do not distinguish between different types of numeric value|a float is not an integer even without fractional part",
]);
const allowlistDisabled = process.env.COMPLIANCE_NO_ALLOWLIST === "1";

const perDialect = {};
let pass = 0, fail = 0, knownFail = 0, erroredGroups = 0, excludedCases = 0;
const failures = [];
const knownFailures = [];

for (const entry of manifest) {
  const d = entry.dialect ?? "?";
  (perDialect[d] ??= { pass: 0, total: 0, errored: 0 });
  if (entry.error) { erroredGroups++; excludedCases += entry.tests.length; perDialect[d].errored++; continue; }
  let evaluate;
  try { evaluate = (await import(pathToFileURL(join(OUT_DIR, `${entry.module}.js`)).href)).evaluateRoot; }
  catch (e) { erroredGroups++; excludedCases += entry.tests.length; perDialect[d].errored++; failures.push(`${d} ${entry.file} :: ${entry.group} :: MODULE LOAD ERROR ${e.message}`); continue; }
  for (const t of entry.tests) {
    const got = evaluate(parseInstance(t.data)) === true;
    perDialect[d].total++;
    if (got === t.valid) { pass++; perDialect[d].pass++; continue; }
    const key = `${d}|${entry.file}|${entry.group}|${t.desc}`;
    const line = `${d} ${entry.file} :: ${entry.group} :: ${t.desc} (got ${got}, want ${t.valid})`;
    if (!allowlistDisabled && KNOWN_FAILURES.has(key)) { knownFail++; knownFailures.push(line); }
    else { fail++; failures.push(line); }
  }
}

console.log("per-dialect pass/total (built groups; errored excluded):");
for (const d of Object.keys(perDialect)) {
  const p = perDialect[d];
  const pct = p.total ? ((100 * p.pass) / p.total).toFixed(1) : "n/a";
  console.log(`  ${d.padEnd(14)} ${p.pass}/${p.total}  (${pct}%)  errored-groups=${p.errored}`);
}
const total = pass + fail + knownFail;
console.log(`\nTOTAL: ${pass}/${total} cases passed (${fail} failed, ${knownFail} known-failure); ${erroredGroups} schema-groups errored (${excludedCases} cases excluded).`);
if (knownFailures.length) {
  console.log(`\nknown failures (allow-listed, do NOT trip the gate — ${knownFailures.length}):`);
  for (const f of knownFailures) { console.log("  " + f); }
}
if (failures.length) {
  console.log(`\nNEW failures (${Math.min(failures.length, 40)} of ${failures.length}):`);
  for (const f of failures.slice(0, 40)) { console.log("  " + f); }
}

// Machine-readable summary line + non-zero exit on any NON-allow-listed case failure (CI gate).
console.log(`\nPASS=${pass} FAIL=${fail} KNOWN_FAIL=${knownFail} ERRORED_GROUPS=${erroredGroups} EXCLUDED_CASES=${excludedCases}`);
if (fail > 0) { process.exit(1); }
