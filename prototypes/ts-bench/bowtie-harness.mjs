#!/usr/bin/env node
// Bowtie (bowtie.report) test harness for corvus-ts — the AOT TypeScript JSON Schema engine. corvus-ts is a
// code GENERATOR, so each schema is compiled on the fly. Rather than spawn the CLI per schema, this drives a
// LONG-RUNNING in-process codegen worker (`Corvus.Json.Cli codegen-worker`) that uses the generator directly
// and registers Bowtie's per-case `registry` of remote schemas via AddDocument (no server) — then tsc-compiles
// and dynamic-imports the default evaluateRoot to validate each instance.
//
// Protocol: newline-delimited JSON commands on stdin -> NDJSON responses on stdout (start/dialect/run/stop).
// Lives in ts-bench to reuse its node_modules (lossless-json + the generated-code runtime deps + esbuild).
import { createInterface } from "node:readline";
import { spawn } from "node:child_process";
import { mkdirSync, rmSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { LosslessNumber } from "lossless-json";
import { transformSync } from "esbuild";

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = join(HERE, "..", "..");
// CORVUS_CLI = a self-contained CLI binary (the container image sets it to /opt/cli/Corvus.Json.Cli); without
// it, fall back to the dev DLL via `dotnet`.
const CLI_RUN = process.env.CORVUS_CLI ? [process.env.CORVUS_CLI] : ["dotnet", join(ROOT, "src", "Corvus.Json.Cli", "bin", "Debug", "net9.0", "Corvus.Json.Cli.dll")];
const WORK = join(HERE, "bowtie-work");

// The long-running codegen worker: one process for the whole run (no per-schema spawn). Each request is one
// NDJSON line { schema, registry, out }; the worker writes the TS module to `out` and replies { ok | error }.
const worker = spawn(CLI_RUN[0], [...CLI_RUN.slice(1), "codegen-worker"], { stdio: ["pipe", "pipe", "inherit"] });
worker.on("exit", (code) => { process.stderr.write(`codegen worker exited (${code})\n`); process.exit(1); });
const workerOut = createInterface({ input: worker.stdout });
const pending = [];
workerOut.on("line", (line) => { const resolve = pending.shift(); if (resolve) { resolve(line); } });
// JSON stringify over parseInstance output: null-proto objects (so "__proto__" is an ordinary own key, never
// prototype pollution) and LosslessNumber (exact source for multipleOf/big-int). Feeds schemas to the worker
// losslessly — JSON.parse/stringify would lose both, and lossless-json's parse pollutes the prototype.
function jstr(v) {
  if (v === null) { return "null"; }
  if (v instanceof LosslessNumber) { return v.toString(); }
  const t = typeof v;
  if (t === "boolean") { return v ? "true" : "false"; }
  if (t === "string") { return JSON.stringify(v); }
  if (Array.isArray(v)) { return "[" + v.map(jstr).join(",") + "]"; }
  return "{" + Object.keys(v).map((k) => JSON.stringify(k) + ":" + jstr(v[k])).join(",") + "}";
}
function codegen(schema, registry, out) {
  return new Promise((resolve) => {
    pending.push(resolve);
    worker.stdin.write(jstr({ schema, registry, out }) + "\n");
  });
}

// Source-preserving single-pass parser (from suite-runner.mjs): numbers -> LosslessNumber (exact source for
// multipleOf/big-int), objects -> null-prototype (so "__proto__" is an ordinary own key). The Model C model.
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

const DIALECTS = [
  "https://json-schema.org/draft/2020-12/schema",
  "https://json-schema.org/draft/2019-09/schema",
  "http://json-schema.org/draft-07/schema#",
  "http://json-schema.org/draft-06/schema#",
  "http://json-schema.org/draft-04/schema#",
];

const out = (o) => process.stdout.write(JSON.stringify(o) + "\n");
const msg = (e) => String((e && e.stack) || e);

let dialect = "https://json-schema.org/draft/2020-12/schema";
let n = 0;
// Suite schemas usually omit $schema; the Bowtie `dialect` command tells us which one to assume, so inject it
// (the generator infers the draft from $schema). Object schemas only; booleans / already-dialected unchanged.
function injectDialect(s) {
  if (s && typeof s === "object" && !Array.isArray(s) && !("$schema" in s)) { s.$schema = dialect; }
}
async function compile(schema, registry) {
  injectDialect(schema);
  const reg = {};
  if (registry) { for (const [uri, s] of Object.entries(registry)) { injectDialect(s); reg[uri] = s; } }
  const dir = join(WORK, String(n++));
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  const resp = JSON.parse(await codegen(schema, reg, dir));
  if (resp.error) { throw new Error(resp.error); }
  // Fast type-STRIPPING transpile (esbuild), not a per-case `tsc` type-check: the generated validators are
  // already correct, so type-checking each schema is pure overhead — and a ~1-2s tsc per case makes the
  // harness fall behind bowtie's pacing under load (the source of the draft4 desync). esbuild is ~ms/case.
  for (const name of ["corvus-runtime", "generated"]) {
    const js = transformSync(readFileSync(join(dir, name + ".ts"), "utf8"), { loader: "ts", format: "esm", target: "es2022" }).code;
    writeFileSync(join(dir, name + ".js"), js);
  }
  return dir;
}

const rl = createInterface({ input: process.stdin, crlfDelay: Infinity });
for await (const line of rl) {
  const s = line.trim();
  if (!s) { continue; }
  // Parse the WHOLE command with the source-preserving parser: instances/schemas keep "__proto__" et al. as
  // ordinary own keys (lossless-json's parse would set the prototype) and numbers stay LosslessNumber.
  const cmd = parseInstance(s);
  const c = String(cmd.cmd);
  if (c === "start") {
    out({ version: 1, implementation: { language: "typescript", name: "corvus-ts", version: "0.1.0", dialects: DIALECTS,
      homepage: "https://github.com/corvus-dotnet/Corvus.JsonSchema", issues: "https://github.com/corvus-dotnet/Corvus.JsonSchema/issues",
      source: "https://github.com/corvus-dotnet/Corvus.JsonSchema", os: process.platform, language_version: process.version } });
  } else if (c === "dialect") {
    dialect = String(cmd.dialect);
    out({ ok: true });
  } else if (c === "run") {
    const seq = cmd.seq instanceof LosslessNumber ? Number(cmd.seq.toString()) : cmd.seq;
    try {
      const dir = await compile(cmd.case.schema, cmd.case.registry);
      const evaluateRoot = (await import(pathToFileURL(join(dir, "generated.js")).href)).default;
      const results = cmd.case.tests.map((t) => {
        // t.instance is already in the validator's Model-C form (parseInstance parsed the whole command):
        // null-proto objects + LosslessNumber. Pass it straight through — no lossy round-trip.
        try { return { valid: evaluateRoot(t.instance) === true }; }
        catch (e) { return { errored: true, context: { message: msg(e) } }; }
      });
      out({ seq, results });
    } catch (e) {
      out({ seq, errored: true, context: { message: msg(e) } });
    }
  } else if (c === "stop") {
    process.exit(0);
  }
}
