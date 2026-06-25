// Sourcemeta-dataset validation benchmark: corvus-ts (production CLI output) vs Ajv / Hyperjump /
// @exodus/schemasafe. Methodology mirrors Sourcemeta's: per schema, validate every instance once (cold),
// warm up WARMUP times, then time one warm pass over all instances. Compile time is excluded. All
// validators receive the same JSON.parse'd instances. We also cross-check that every validator agrees on
// how many instances are valid (a correctness gate — a fast-but-wrong validator is meaningless).
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { performance } from "node:perf_hooks";
import Ajv from "ajv";
import Ajv2020 from "ajv/dist/2020.js";
import AjvDraft04 from "ajv-draft-04";
import { validator as schemasafeValidator } from "@exodus/schemasafe";
import { registerSchema, validate as hjValidate } from "@hyperjump/json-schema/draft-2020-12";
await Promise.all([
  import("@hyperjump/json-schema/draft-2019-09"),
  import("@hyperjump/json-schema/draft-07"),
  import("@hyperjump/json-schema/draft-06"),
  import("@hyperjump/json-schema/draft-04"),
]);

const DATASET = process.argv[2] || new URL("./jsonschema-benchmark", import.meta.url).pathname;
const SCHEMAS_DIR = `${DATASET}/schemas`;
const GEN_DIR = new URL("./dist/bench-gen", import.meta.url).pathname;
const WARMUP = Number(process.env.WARMUP || 100);
const ROUNDS = Number(process.env.ROUNDS || 1);
const median = (a) => { const s = [...a].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };

const AJV_FOR = {
  "https://json-schema.org/draft/2020-12/schema": Ajv2020,
  "https://json-schema.org/draft/2019-09/schema": Ajv2020,
  "http://json-schema.org/draft-07/schema": Ajv,
  "http://json-schema.org/draft-06/schema": Ajv,
  "http://json-schema.org/draft-04/schema": AjvDraft04,
};

function loadInstances(path) {
  return readFileSync(path, "utf8").trim().split("\n").filter(Boolean).map((l) => JSON.parse(l));
}

// Time a synchronous validator: cold pass (records valid count; catches a throwing validator so one
// bad case doesn't abort the whole run), warmup, then one measured warm pass. The hot passes have no
// try/catch (the cold pass already proved the validator doesn't throw on these instances).
function timeSync(fn, instances) {
  let valid = 0;
  try { for (const i of instances) { if (fn(i) === true) valid++; } }
  catch (e) { return { error: String(e.message || e) }; }
  for (let w = 0; w < WARMUP; w++) { for (const i of instances) fn(i); }
  const times = [];
  for (let r = 0; r < ROUNDS; r++) { const t0 = performance.now(); for (const i of instances) fn(i); times.push((performance.now() - t0) * 1e6); }
  return { ns: median(times), valid };
}

async function timeAsync(fn, instances) {
  let valid = 0;
  try { for (const i of instances) { if ((await fn(i)) === true) valid++; } }
  catch (e) { return { error: String(e.message || e) }; }
  for (let w = 0; w < WARMUP; w++) { for (const i of instances) await fn(i); }
  const times = [];
  for (let r = 0; r < ROUNDS; r++) { const t0 = performance.now(); for (const i of instances) await fn(i); times.push((performance.now() - t0) * 1e6); }
  return { ns: median(times), valid };
}

async function buildValidators(name, schema, schemaId) {
  const out = {};

  // corvus-ts: the production CLI output (default export = the 1-arg root validator).
  try {
    const mod = await import(`${GEN_DIR}/${name}/generated.js`);
    const v = mod.default;
    out["corvus-ts"] = { kind: "sync", fn: (i) => v(i) === true };
  } catch (e) { out["corvus-ts"] = { error: String(e.message || e) }; }

  // Ajv (no ajv-formats, matching its benchmark config — formats as annotations, like corvus default).
  try {
    const AjvClass = AJV_FOR[(schema.$schema || "").replace(/#$/, "")] || Ajv;
    const ajv = new AjvClass({ strict: false });
    const v = ajv.compile(schema);
    out["ajv"] = { kind: "sync", fn: (i) => v(i) === true };
  } catch (e) { out["ajv"] = { error: String(e.message || e) }; }

  // @exodus/schemasafe (mode: spec).
  try {
    const v = schemasafeValidator(schema, { mode: "spec", isJSON: true });
    out["schemasafe"] = { kind: "sync", fn: (i) => v(i) === true };
  } catch (e) { out["schemasafe"] = { error: String(e.message || e) }; }

  // Hyperjump: register + compile to a synchronous validator (confirmed sync in 1.x).
  try {
    registerSchema(schema, schemaId);
    const compiled = await hjValidate(schemaId);
    out["hyperjump"] = { kind: "sync", fn: (i) => compiled(i).valid === true };
  } catch (e) { out["hyperjump"] = { error: String(e.message || e) }; }

  return out;
}

const names = readdirSync(GEN_DIR).filter((n) => existsSync(`${GEN_DIR}/${n}/generated.js`)).sort();
const IMPLS = ["corvus-ts", "ajv", "hyperjump", "schemasafe"];
const rows = [];

for (const name of names) {
  const schemaPath = `${SCHEMAS_DIR}/${name}/schema.json`;
  const instancePath = `${SCHEMAS_DIR}/${name}/instances.jsonl`;
  if (!existsSync(schemaPath) || !existsSync(instancePath)) continue;

  const schema = JSON.parse(readFileSync(schemaPath, "utf8"));
  const schemaId = schema.$id || `https://bench.example/${name}`;
  const instances = loadInstances(instancePath);
  const validators = await buildValidators(name, schema, schemaId);

  const row = { name, n: instances.length, dialect: (schema.$schema || "?").replace(/^https?:\/\/json-schema\.org\/(draft[/-])?/, "").replace(/\/schema#?$/, ""), per: {}, valid: {} };
  for (const impl of IMPLS) {
    const spec = validators[impl];
    if (!spec || spec.error) { row.per[impl] = null; row.valid[impl] = spec?.error ? "ERR" : "-"; continue; }
    const r = spec.kind === "async" ? await timeAsync(spec.fn, instances) : timeSync(spec.fn, instances);
    if (r.error) { row.per[impl] = null; row.valid[impl] = "ERR"; continue; }
    row.per[impl] = r.ns / instances.length; // ns per instance
    row.valid[impl] = r.valid;
  }
  rows.push(row);
  const base = row.valid["corvus-ts"];
  const agree = IMPLS.every((i) => row.valid[i] === "-" || row.valid[i] === "ERR" || row.valid[i] === base);
  process.stderr.write(`${name} (${row.dialect}, ${instances.length} inst): valid ${JSON.stringify(row.valid)} ${agree ? "" : "  <-- DISAGREEMENT"}\n`);
}

// ---- report ----
const fmt = (v) => v == null ? "    -   " : (v < 1000 ? `${v.toFixed(0)} ns` : `${(v / 1000).toFixed(2)} us`).padStart(8);
console.log("\n# Validation throughput — ns per instance (warm, compile excluded). Lower is better.\n");
console.log("schema".padEnd(22) + "dialect".padEnd(10) + "inst".padStart(6) + "  " + IMPLS.map((i) => i.padStart(11)).join("") + "   corvus vs ajv");
console.log("-".repeat(22 + 10 + 6 + 2 + 11 * IMPLS.length + 16));
for (const r of rows) {
  const ratio = (r.per["corvus-ts"] && r.per["ajv"]) ? `${(r.per["ajv"] / r.per["corvus-ts"]).toFixed(2)}x` : "";
  console.log(r.name.padEnd(22) + r.dialect.padEnd(10) + String(r.n).padStart(6) + "  " + IMPLS.map((i) => fmt(r.per[i]).padStart(11)).join("") + "   " + ratio.padStart(8));
}

// Aggregate: geomean of ns/instance per impl (over schemas where the impl ran).
console.log("\n# Geometric mean ns/instance (over schemas each impl validated):");
for (const impl of IMPLS) {
  const vals = rows.map((r) => r.per[impl]).filter((v) => v != null && v > 0);
  const geo = vals.length ? Math.exp(vals.reduce((a, v) => a + Math.log(v), 0) / vals.length) : NaN;
  const errs = rows.filter((r) => r.valid[impl] === "ERR").map((r) => r.name);
  console.log(`  ${impl.padEnd(12)} ${vals.length ? geo.toFixed(1) + " ns" : "-"}   (ran ${vals.length}/${rows.length}${errs.length ? `; compile-failed: ${errs.join(", ")}` : ""})`);
}
const disagree = rows.filter((r) => { const b = r.valid["corvus-ts"]; return IMPLS.some((i) => r.valid[i] !== "-" && r.valid[i] !== "ERR" && r.valid[i] !== b); });
console.log(`\n# Cross-check: ${rows.length - disagree.length}/${rows.length} schemas — all validators agree on valid-counts.` + (disagree.length ? ` Disagreements: ${disagree.map((r) => r.name).join(", ")}` : ""));
