// Runs the compiled suite modules against the manifest and reports the pass rate per dialect.
// Regenerate + compile + run:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- --suite
//   npx -y -p typescript tsc out-suite/*.ts --strict --target es2022 --module esnext --moduleResolution bundler --outDir out-suite
//   node suite-runner.mjs
import { readFileSync } from "node:fs";
import { LosslessNumber } from "lossless-json";

// The manifest stores each instance as its raw JSON source text (Model C / source-text model). Parse the
// manifest structure once with JSON.parse, then parse each instance ONCE with a single-pass, source-
// preserving parser: numbers become LosslessNumber (exact source digits — needed for big/precise numbers),
// objects get a null prototype so "__proto__" is an ordinary own key. No lossy double-parse.
const manifest = JSON.parse(readFileSync("out-suite/manifest.json", "utf8"));

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

const perDialect = {};
let pass = 0, fail = 0, erroredGroups = 0, excludedCases = 0;
const failures = [];

for (const entry of manifest) {
  const d = entry.dialect ?? "?";
  (perDialect[d] ??= { pass: 0, total: 0, errored: 0 });
  if (entry.error) { erroredGroups++; excludedCases += entry.tests.length; perDialect[d].errored++; continue; }
  let evaluate;
  try { evaluate = (await import(`./out-suite/${entry.module}.js`)).evaluateRoot; }
  catch (e) { erroredGroups++; excludedCases += entry.tests.length; perDialect[d].errored++; failures.push(`${d} ${entry.file} :: ${entry.group} :: MODULE LOAD ERROR ${e.message}`); continue; }
  for (const t of entry.tests) {
    const got = evaluate(parseInstance(t.data)) === true;
    perDialect[d].total++;
    if (got === t.valid) { pass++; perDialect[d].pass++; }
    else { fail++; failures.push(`${d} ${entry.file} :: ${entry.group} :: ${t.desc} (got ${got}, want ${t.valid})`); }
  }
}

console.log("per-dialect pass/total (built groups; errored excluded):");
for (const d of Object.keys(perDialect)) {
  const p = perDialect[d];
  const pct = p.total ? ((100 * p.pass) / p.total).toFixed(1) : "n/a";
  console.log(`  ${d.padEnd(14)} ${p.pass}/${p.total}  (${pct}%)  errored-groups=${p.errored}`);
}
const total = pass + fail;
console.log(`\nTOTAL: ${pass}/${total} cases passed (${fail} failed); ${erroredGroups} schema-groups errored (${excludedCases} cases excluded).`);
if (failures.length) {
  console.log(`\nsample failures (${Math.min(failures.length, 40)} of ${failures.length}):`);
  for (const f of failures.slice(0, 40)) { console.log("  " + f); }
}
