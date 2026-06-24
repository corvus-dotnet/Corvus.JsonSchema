// Runs the compiled suite modules against the manifest and reports the pass rate.
// Regenerate + compile + run:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- --suite
//   npx -y -p typescript tsc out-suite/*.ts --strict --target es2022 --module esnext --moduleResolution bundler --outDir out-suite
//   node suite-runner.mjs
import { readFileSync } from "node:fs";

const manifest = JSON.parse(readFileSync("out-suite/manifest.json", "utf8"));
const perFile = {};
let pass = 0, fail = 0, erroredGroups = 0, excludedCases = 0;
const failures = [];

for (const entry of manifest) {
  (perFile[entry.file] ??= { pass: 0, total: 0 });
  if (entry.error) { erroredGroups++; excludedCases += entry.tests.length; continue; }
  let evaluate;
  try { evaluate = (await import(`./out-suite/${entry.module}.js`)).evaluateRoot; }
  catch (e) { erroredGroups++; excludedCases += entry.tests.length; failures.push(`${entry.file} :: ${entry.group} :: MODULE LOAD ERROR ${e.message}`); continue; }
  for (const t of entry.tests) {
    const got = evaluate(t.data) === true;
    perFile[entry.file].total++;
    if (got === t.valid) { pass++; perFile[entry.file].pass++; }
    else { fail++; failures.push(`${entry.file} :: ${entry.group} :: ${t.desc} (got ${got}, want ${t.valid})`); }
  }
}

console.log("per-keyword pass/total (built groups):");
for (const f of Object.keys(perFile)) {
  console.log(`  ${f.padEnd(12)} ${perFile[f].pass}/${perFile[f].total}`);
}
const total = pass + fail;
console.log(`\nTOTAL: ${pass}/${total} cases passed (${fail} failed); ${erroredGroups} schema-groups errored (${excludedCases} cases excluded).`);
if (failures.length) {
  console.log(`\nsample failures (${Math.min(failures.length, 25)} of ${failures.length}):`);
  for (const f of failures.slice(0, 25)) { console.log("  " + f); }
}
