#!/usr/bin/env node
// Runner for the generated-accessor "access" suites (DISTINCT from validation compliance, which lives in
// ../suite-runner.mjs). For each suite it:
//   1. drives the long-running bowtie codegen worker (NDJSON { schema, out } -> { ok | error }) to emit the
//      generated.ts + corvus-runtime.ts module for the suite's feature schema — CreateDefault codegen,
//      byte-identical to what the spike's default mode produced;
//   2. esbuild-transpiles generated.ts + corvus-runtime.ts (loader:'ts', format:'esm') into the relative
//      directory name the .test.ts imports from (e.g. ./out-arrays/), and transpiles the .test.ts itself;
//   3. runs the transpiled test in a child `node` process and parses its own "<name>: N passed, M failed"
//      tally (the suites print their own).
//
// Exit non-zero if ANY suite reports failures, errors, or fails to load/codegen.
//
// MUST be run with cwd = prototypes/ts-bench so the generated runtime's bare imports (lossless-json /
// @js-temporal/polyfill / tr46) and esbuild resolve via ts-bench/node_modules.
import { transformSync } from "esbuild";
import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import { mkdirSync, rmSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SCHEMAS = join(HERE, "schemas");
const SUITES = join(HERE, "suites");
// Per-suite scratch lives UNDER ts-bench (cwd) so Node's upward node_modules walk finds the runtime deps.
const WORK = join(HERE, "access-work");

// CORVUS_WORKER overrides the worker binary (mirrors bowtie-harness.mjs); otherwise the dev build via dotnet.
// HERE = compliance/access; the bowtie codegen worker lives at ts-bench/bowtie-worker (two levels up).
const WORKER_DLL = join(HERE, "..", "..", "bowtie-worker", "bin", "Release", "net10.0", "corvus-ts-bowtie-worker.dll");
const WORKER_RUN = process.env.CORVUS_WORKER ? [process.env.CORVUS_WORKER] : ["dotnet", WORKER_DLL];

// suite -> { schema, importDir }. importDir is the RELATIVE dir name the .test.ts imports its module(s) from
// (taken verbatim from each suite's import statement); the transpiled generated.js/corvus-runtime.js are
// written there and the transpiled test is placed alongside so its relative import resolves.
const SUITE_TABLE = [
  { test: "arrays-access.test.ts",   schema: "arrays.json",          importDir: "out-arrays" },
  { test: "formats-access.test.ts",  schema: "formats.json",         importDir: "out-formats" },
  { test: "intformat-access.test.ts", schema: "intformat.json",      importDir: "out-intformat" },
  { test: "temporal-access.test.ts", schema: "temporal.json",        importDir: "out-temporal" },
  { test: "mutation-access.test.ts", schema: "mutation.json",        importDir: "out-mutation" },
  { test: "produce-access.test.ts",  schema: "profile.json",         importDir: "out-profile" },
  { test: "rmw-access.test.ts",      schema: "rmw.json",             importDir: "out-rmw" },
  { test: "shapes-access.test.ts",   schema: "shapes.json",          importDir: "out-shapes" },
  { test: "union-access.test.ts",    schema: "union.json",           importDir: "out-union" },
  { test: "collector.test.ts",       schema: "person-collector.json", importDir: "out-coll" },
  { test: "provider-access.test.ts", schema: "person.json",          importDir: "out" },
  { test: "jsdoc-access.test.ts",    schema: "jsdoc.json",           importDir: "out-jsdoc" },
  { test: "defaults-access.test.ts", schema: "defaults.json",        importDir: "out-defaults" },
  // OpenAPI nullable (gap E1): the SAME nullable feature under both dialects. 3.0 spells it `nullable:true`
  // (only reachable via the forced openapi30 fallback vocabulary, since the doc carries no $schema); 3.1
  // spells it `type:["string","null"]` (standard 2020-12, reached via the openapi31 fallback). Both must
  // emit `T | null` and a null-accepting validator, and reject null on a non-nullable property.
  { test: "openapi-access.test.ts",  modules: [
      { schema: "openapi30-nullable.json", importDir: "out-openapi30", dialect: "openapi30" },
      { schema: "openapi31-nullable.json", importDir: "out-openapi31", dialect: "openapi31" },
    ] },
];

// ----- long-running codegen worker (one process for the whole run; same protocol as bowtie-harness.mjs) -----
const worker = spawn(WORKER_RUN[0], WORKER_RUN.slice(1), { stdio: ["pipe", "pipe", "inherit"] });
worker.on("exit", (code) => { if (code !== 0) { process.stderr.write(`codegen worker exited (${code})\n`); process.exit(1); } });
const workerOut = createInterface({ input: worker.stdout });
const pending = [];
workerOut.on("line", (line) => { const r = pending.shift(); if (r) { r(line); } });
function codegen(schema, out, dialect) {
  return new Promise((resolve) => {
    pending.push(resolve);
    // schema is passed as a parsed value so $schema (absent in some feature schemas) defaults per the worker.
    // `dialect` (optional) forces the fallback vocabulary — needed for OpenAPI 3.0/3.1 schemas, which carry
    // no $schema and so cannot be reached by the worker's $schema-based draft detection.
    const req = dialect ? { schema, out, dialect } : { schema, out };
    worker.stdin.write(JSON.stringify(req) + "\n");
  });
}

function transpile(srcFile) {
  return transformSync(readFileSync(srcFile, "utf8"), { loader: "ts", format: "esm", target: "es2022" }).code;
}

// Run the transpiled test in a child node (cwd = ts-bench) so a thrown failure can't abort the runner, and
// capture its self-printed "... N passed, M failed" tally. Returns { ok, passed, failed, out }.
function runTest(testJs) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, [testJs], { cwd: process.cwd(), stdio: ["ignore", "pipe", "pipe"] });
    let buf = "";
    child.stdout.on("data", (d) => { buf += d; });
    child.stderr.on("data", (d) => { buf += d; });
    child.on("exit", (code) => {
      const m = buf.match(/(\d+)\s+passed,\s+(\d+)\s+failed/);
      const passed = m ? Number(m[1]) : -1;
      const failed = m ? Number(m[2]) : -1;
      // A suite is OK only if it printed a tally with 0 failures AND exited 0 (most suites throw on failure;
      // collector.test.ts prints its tally WITHOUT throwing, so the parsed `failed` count is the real gate).
      const ok = code === 0 && failed === 0 && m !== null;
      resolve({ ok, passed, failed, code, out: buf.trimEnd() });
    });
  });
}

async function main() {
  rmSync(WORK, { recursive: true, force: true });
  let anyFail = false;
  const summary = [];

  for (const s of SUITE_TABLE) {
    const suiteDir = join(WORK, s.test.replace(/\.test\.ts$/, ""));

    // A suite generates ONE module from { schema, importDir } by default, or SEVERAL when it carries a
    // `modules` array (each { schema, importDir, dialect? } produces its own importDir — e.g. the OpenAPI
    // suite imports a 3.0 and a 3.1 module side by side). Each module gets its own gen dir.
    const modules = s.modules ?? [{ schema: s.schema, importDir: s.importDir, dialect: s.dialect }];
    let codegenError = null;
    for (let mi = 0; mi < modules.length; mi++) {
      const m = modules[mi];
      const genDir = join(suiteDir, "gen" + mi);     // worker writes raw generated.ts + corvus-runtime.ts here
      const importTarget = join(suiteDir, m.importDir); // transpiled .js the test imports from ./<importDir>/
      mkdirSync(importTarget, { recursive: true });

      // 1) codegen via the worker.
      const schema = JSON.parse(readFileSync(join(SCHEMAS, m.schema), "utf8"));
      const resp = JSON.parse(await codegen(schema, genDir, m.dialect));
      if (resp.error) { codegenError = resp.error; break; }

      // 2) transpile generated module(s) + runtime into the import dir.
      for (const name of ["corvus-runtime", "generated"]) {
        writeFileSync(join(importTarget, name + ".js"), transpile(join(genDir, name + ".ts")));
      }

      // Also place the RAW generated.ts in the import dir (esbuild strips comments from the .js, so a JSDoc
      // text-assertion suite — see jsdoc-access.test.ts — reads the .ts to see the emitted /** ... */ blocks).
      writeFileSync(join(importTarget, "generated.ts.txt"), readFileSync(join(genDir, "generated.ts"), "utf8"));
    }

    if (codegenError) {
      summary.push({ name: s.test, ok: false, detail: `codegen error: ${codegenError}` });
      anyFail = true;
      continue;
    }

    // 2b) transpile the test alongside (its relative imports resolve to the import dirs written above).
    const testJs = join(suiteDir, s.test.replace(/\.ts$/, ".js"));
    writeFileSync(testJs, transpile(join(SUITES, s.test)));

    // 3) run + parse the suite's own tally.
    const r = await runTest(testJs);
    if (!r.ok) { anyFail = true; }
    summary.push({ name: s.test, ok: r.ok, passed: r.passed, failed: r.failed, code: r.code, out: r.out });
    const tag = r.ok ? "PASS" : "FAIL";
    console.log(`[${tag}] ${s.test}  (${r.passed} passed, ${r.failed} failed, exit ${r.code})`);
    if (!r.ok) { console.log(r.out.split("\n").map((l) => "    " + l).join("\n")); }
  }

  worker.stdin.end();
  try { worker.kill(); } catch { /* already gone */ }

  console.log("\n==== access suites summary ====");
  for (const s of summary) {
    console.log(`  ${s.ok ? "PASS" : "FAIL"}  ${s.name}${s.detail ? "  " + s.detail : ""}`);
  }
  const passed = summary.filter((s) => s.ok).length;
  console.log(`\n${passed}/${summary.length} access suites passed`);
  process.exit(anyFail ? 1 : 0);
}

main().catch((e) => { console.error(e); process.exit(1); });
