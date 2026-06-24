// Runs the compiled suite modules against the manifest and reports the pass rate per dialect.
// Regenerate + compile + run:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- --suite
//   npx -y -p typescript tsc out-suite/*.ts --strict --target es2022 --module esnext --moduleResolution bundler --outDir out-suite
//   node suite-runner.mjs
import { readFileSync } from "node:fs";

const manifest = JSON.parse(readFileSync("out-suite/manifest.json", "utf8"));
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
    const got = evaluate(t.data) === true;
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
