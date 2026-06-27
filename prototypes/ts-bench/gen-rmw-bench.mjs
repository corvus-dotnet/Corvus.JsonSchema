#!/usr/bin/env node
// Generates the Catalog mutation module that rmw-e2e.bench.mjs imports — the standalone, spike-free
// replacement for what the feasibility spike used to compile into out-catalog-js/. It:
//   1. drives the long-running bowtie codegen worker (NDJSON { schema, out } -> { ok | error }) to emit
//      generated.ts + corvus-runtime.ts for ./bench-catalog.json — CreateDefault codegen, byte-identical to
//      what the spike's default mode produced (the worker appends the same RootEvaluatorExport, so the
//      module exports produceCatalog / patchCatalog / buildCatalog / evaluateRoot);
//   2. esbuild-transpiles generated.ts + corvus-runtime.ts (loader:'ts', format:'esm') into ./rmw-bench-gen/
//      — the directory rmw-e2e.bench.mjs imports "./rmw-bench-gen/generated.js" from.
//
// This mirrors the access-suite flow in compliance/access/run-access.mjs (same worker protocol, same
// esbuild transpile). MUST be run with cwd = prototypes/ts-bench so the generated runtime's bare imports
// (lossless-json / @js-temporal/polyfill / tr46) and esbuild resolve via ts-bench/node_modules.
import { transformSync } from "esbuild";
import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import { mkdirSync, rmSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SCHEMA = join(HERE, "bench-catalog.json");
const OUT = join(HERE, "rmw-bench-gen");      // transpiled .js the bench imports from ./rmw-bench-gen/
const GEN = join(OUT, "gen");                 // worker writes raw generated.ts + corvus-runtime.ts here

// CORVUS_WORKER overrides the worker binary (mirrors run-access.mjs); otherwise the Release build.
const WORKER_DLL = join(HERE, "bowtie-worker", "bin", "Release", "net10.0", "corvus-ts-bowtie-worker.dll");
const WORKER_RUN = process.env.CORVUS_WORKER ? [process.env.CORVUS_WORKER] : ["dotnet", WORKER_DLL];

function transpile(srcFile) {
  return transformSync(readFileSync(srcFile, "utf8"), { loader: "ts", format: "esm", target: "es2022" }).code;
}

async function main() {
  rmSync(OUT, { recursive: true, force: true });
  mkdirSync(GEN, { recursive: true });

  // ----- one-shot codegen via the long-running worker (same protocol as run-access.mjs) -----
  const worker = spawn(WORKER_RUN[0], WORKER_RUN.slice(1), { stdio: ["pipe", "pipe", "inherit"] });
  worker.on("exit", (code) => { if (code !== 0 && code !== null) { process.stderr.write(`codegen worker exited (${code})\n`); process.exit(1); } });
  const workerOut = createInterface({ input: worker.stdout });
  const pending = [];
  workerOut.on("line", (line) => { const r = pending.shift(); if (r) { r(line); } });
  const codegen = (schema, out) => new Promise((resolve) => {
    pending.push(resolve);
    // schema passed as a parsed value so $schema defaults per the worker if absent.
    worker.stdin.write(JSON.stringify({ schema, out }) + "\n");
  });

  const schema = JSON.parse(readFileSync(SCHEMA, "utf8"));
  const resp = JSON.parse(await codegen(schema, GEN));
  worker.stdin.end();
  try { worker.kill(); } catch { /* already gone */ }
  if (resp.error) { console.error(`codegen error: ${resp.error}`); process.exit(1); }

  // ----- esbuild-transpile generated module + runtime into ./rmw-bench-gen/ -----
  for (const name of ["corvus-runtime", "generated"]) {
    writeFileSync(join(OUT, name + ".js"), transpile(join(GEN, name + ".ts")));
  }
  console.log(`generated ${join(OUT, "generated.js")} (+ corvus-runtime.js)`);
}

main().catch((e) => { console.error(e); process.exit(1); });
