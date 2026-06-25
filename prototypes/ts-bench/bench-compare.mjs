// Controlled A/B: corvus-OLD (index-bitmask + Object.keys loop) vs corvus-NEW (name-Set + direct property
// access) vs ajv, all measured in ONE process so they see identical machine state. Per schema we run the
// three adjacently, ROUNDS measured passes each, and take the MEDIAN — so transient CPU contention can't
// favour one impl over another. The headline is the geomean of NEW/OLD (the refactor's true effect).
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { performance } from "node:perf_hooks";
import Ajv from "ajv";
import Ajv2020 from "ajv/dist/2020.js";
import AjvDraft04 from "ajv-draft-04";

const DATASET = process.argv[2] || new URL("./jsonschema-benchmark", import.meta.url).pathname;
const SCHEMAS_DIR = `${DATASET}/schemas`;
const OLD = new URL("./dist/bench-gen-old", import.meta.url).pathname;
const NEW = new URL("./dist/bench-gen-new", import.meta.url).pathname;
const WARMUP = Number(process.env.WARMUP || 30);
const ROUNDS = Number(process.env.ROUNDS || 7);

const AJV_FOR = {
  "https://json-schema.org/draft/2020-12/schema": Ajv2020,
  "https://json-schema.org/draft/2019-09/schema": Ajv2020,
  "http://json-schema.org/draft-07/schema": Ajv,
  "http://json-schema.org/draft-06/schema": Ajv,
  "http://json-schema.org/draft-04/schema": AjvDraft04,
};

const loadInstances = (p) => readFileSync(p, "utf8").trim().split("\n").filter(Boolean).map((l) => JSON.parse(l));
const median = (a) => { const s = [...a].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };

function timeMedian(fn, instances) {
  let valid = 0;
  try { for (const i of instances) { if (fn(i) === true) valid++; } } catch (e) { return { error: String(e.message || e) }; }
  for (let w = 0; w < WARMUP; w++) { for (const i of instances) fn(i); }
  const times = [];
  for (let r = 0; r < ROUNDS; r++) {
    const t0 = performance.now();
    for (const i of instances) fn(i);
    times.push((performance.now() - t0) * 1e6 / instances.length);
  }
  return { ns: median(times), valid };
}

const names = readdirSync(NEW).filter((n) => existsSync(`${NEW}/${n}/generated.js`)).sort();
const rows = [];
for (const name of names) {
  const schemaPath = `${SCHEMAS_DIR}/${name}/schema.json`;
  if (!existsSync(schemaPath)) continue;
  const schema = JSON.parse(readFileSync(schemaPath, "utf8"));
  const instances = loadInstances(`${SCHEMAS_DIR}/${name}/instances.jsonl`);

  let oldFn = null, newFn = null, ajvFn = null;
  try { oldFn = (await import(`${OLD}/${name}/generated.js`)).default; } catch { /* */ }
  try { newFn = (await import(`${NEW}/${name}/generated.js`)).default; } catch { /* */ }
  try { const A = AJV_FOR[(schema.$schema || "").replace(/#$/, "")] || Ajv; ajvFn = new A({ strict: false }).compile(schema); } catch { /* */ }

  const ro = oldFn ? timeMedian((x) => oldFn(x) === true, instances) : null;
  const rn = newFn ? timeMedian((x) => newFn(x) === true, instances) : null;
  const ra = ajvFn ? timeMedian((x) => ajvFn(x) === true, instances) : null;
  rows.push({ name, n: instances.length, old: ro && !ro.error ? ro.ns : null, new: rn && !rn.error ? rn.ns : null, ajv: ra && !ra.error ? ra.ns : null });
  process.stderr.write(`${name.padEnd(22)} old ${String(ro?.ns?.toFixed(0) ?? "-").padStart(7)}  new ${String(rn?.ns?.toFixed(0) ?? "-").padStart(7)}  ajv ${String(ra?.ns?.toFixed(0) ?? "-").padStart(7)}\n`);
}

const fmt = (v) => v == null ? "    -  " : (v < 1000 ? `${v.toFixed(0)}ns` : `${(v / 1000).toFixed(2)}us`).padStart(8);
console.log("\n# Controlled A/B: corvus OLD vs NEW (same run). ns/instance, median of " + ROUNDS + " passes. Lower better.\n");
console.log("schema".padEnd(22) + "inst".padStart(6) + "      old      new      ajv   NEW/OLD   NEW vs ajv");
console.log("-".repeat(78));
for (const r of rows) {
  const speedup = (r.old && r.new) ? `${(r.old / r.new).toFixed(2)}x` : "";   // >1 => new faster than old
  const vsAjv = (r.ajv && r.new) ? `${(r.ajv / r.new).toFixed(2)}x` : "";       // >1 => new faster than ajv
  console.log(r.name.padEnd(22) + String(r.n).padStart(6) + "  " + fmt(r.old) + " " + fmt(r.new) + " " + fmt(r.ajv) + "   " + speedup.padStart(7) + "   " + vsAjv.padStart(8));
}

function geomean(vals) { const v = vals.filter((x) => x != null && x > 0); return v.length ? Math.exp(v.reduce((a, x) => a + Math.log(x), 0) / v.length) : NaN; }
const both = rows.filter((r) => r.old && r.new);
const speedups = both.map((r) => r.old / r.new);
console.log("\n# Geomean ns/instance:   old " + geomean(rows.map((r) => r.old)).toFixed(0) + "   new " + geomean(rows.map((r) => r.new)).toFixed(0) + "   ajv " + geomean(rows.map((r) => r.ajv)).toFixed(0));
console.log("# Geomean NEW/OLD speedup (per-schema, same run): " + geomean(speedups).toFixed(3) + "x   (>1 = refactor faster)");
const wins = both.filter((r) => r.old / r.new > 1.03).length, losses = both.filter((r) => r.new / r.old > 1.03).length;
console.log(`# Per-schema: NEW faster on ${wins}, OLD faster on ${losses}, ~tie on ${both.length - wins - losses} (of ${both.length})`);
