#!/usr/bin/env node
// Bowtie (bowtie.report) test harness for corvus-ts — the AOT TypeScript JSON Schema engine. corvus-ts is a
// code GENERATOR, so each schema is compiled on the fly: write it, run the production CLI (--engine
// TypeScript), tsc-compile, dynamic-import the default evaluateRoot, then validate each instance.
//
// Protocol: newline-delimited JSON commands on stdin -> NDJSON responses on stdout (start/dialect/run/stop).
// Lives in ts-bench to reuse its node_modules (lossless-json + the generated-code runtime deps) and tsc;
// a standalone npm package + Docker image is a follow-up. Run:  node bowtie-harness.mjs  (feed it commands).
import { createInterface } from "node:readline";
import { execFileSync } from "node:child_process";
import { writeFileSync, mkdirSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { LosslessNumber, parse as ljParse, stringify as ljStringify } from "lossless-json";

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = join(HERE, "..", "..");
const CLI = join(ROOT, "src", "Corvus.Json.Cli", "bin", "Debug", "net9.0", "Corvus.Json.Cli.dll");
const TSC = join(HERE, "node_modules", ".bin", "tsc");
const GLOBALS = join(HERE, "bowtie-globals.d.ts");
const WORK = join(HERE, "bowtie-work");

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

let n = 0;
function compile(schema) {
  const dir = join(WORK, String(n++));
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, "schema.json"), ljStringify(schema));
  execFileSync("dotnet", [CLI, "jsonschema", join(dir, "schema.json"), "--engine", "TypeScript", "--outputPath", dir], { stdio: "ignore" });
  execFileSync(TSC, [join(dir, "generated.ts"), join(dir, "corvus-runtime.ts"), GLOBALS,
    "--target", "es2022", "--module", "esnext", "--moduleResolution", "bundler", "--lib", "es2022,dom", "--outDir", dir], { stdio: "ignore" });
  return dir;
}

const rl = createInterface({ input: process.stdin, crlfDelay: Infinity });
for await (const line of rl) {
  const s = line.trim();
  if (!s) { continue; }
  const cmd = ljParse(s);
  const c = String(cmd.cmd);
  if (c === "start") {
    out({ version: 1, implementation: { language: "typescript", name: "corvus-ts", version: "0.1.0", dialects: DIALECTS,
      homepage: "https://github.com/corvus-dotnet/Corvus.JsonSchema", issues: "https://github.com/corvus-dotnet/Corvus.JsonSchema/issues",
      source: "https://github.com/corvus-dotnet/Corvus.JsonSchema", os: process.platform, language_version: process.version } });
  } else if (c === "dialect") {
    out({ ok: true });
  } else if (c === "run") {
    const seq = cmd.seq instanceof LosslessNumber ? Number(cmd.seq.toString()) : cmd.seq;
    try {
      const dir = compile(cmd.case.schema);
      const evaluateRoot = (await import(pathToFileURL(join(dir, "generated.js")).href)).default;
      const results = cmd.case.tests.map((t) => {
        try { return { valid: evaluateRoot(parseInstance(ljStringify(t.instance))) === true }; }
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
