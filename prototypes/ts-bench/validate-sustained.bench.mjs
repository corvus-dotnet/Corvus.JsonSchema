// Sustained-load VALIDATION benchmark (gap G5) — the SHIPPING generated validator under sustained load:
// throughput + GC pressure (allocation churn) of the validate path, across payload sizes and ascii / non-ascii
// content. This is the validation analog of rmw-e2e.bench.mjs (mutation), and is DISTINCT from bench.mjs (a
// one-pass cross-validator throughput comparison over the Sourcemeta dataset). It measures three paths so the
// validator's MARGINAL cost over the unavoidable parse is visible:
//
//   parse-only     : JSON.parse(text)                     (baseline — the cost you pay regardless)
//   validate-only  : evaluateRoot(value)                  (value parsed ONCE; isolates the validator's work)
//   parse+validate : JSON.parse(text); evaluateRoot(v)    (the realistic per-request server path)
//
// "Sustained load" is the point: a minor GC fires whenever New Space fills, so per-op allocation drives
// main-thread pause time under throughput (design §4). We therefore report, over a fixed large workload,
// the GC event count + total pause for each path — the allocation-churn signal — alongside ns/op and a
// best-effort bytes/op estimate. Correctness-gated: the validator must accept the valid fixture and reject
// an invalid one before any timing counts.
//
// Run via ./run-validate-bench.sh (builds the codegen worker, generates the Catalog module via
// gen-rmw-bench.mjs, then runs this with `node --expose-gc`). global.gc is required for the GC-pressure and
// bytes/op measurements; without it they are reported as n/a.
import { evaluateRoot } from "./rmw-bench-gen/generated.js";
import { performance, PerformanceObserver } from "node:perf_hooks";

const enc = new TextEncoder();

// ---- fixtures: a Catalog whose `items` array scales the payload; ascii vs non-ascii content (shared shape
// with rmw-e2e.bench.mjs so the two benchmarks profile the same documents). ----
function makeCatalog(itemCount, nonAscii) {
  const nm = nonAscii ? "りんご農園プレミアム果実" : "Premium Orchard Fruit Selection";
  const org = nonAscii ? "果実株式会社" : "Fruit Co";
  const items = [];
  for (let i = 0; i < itemCount; i++) {
    items.push({ sku: "SKU-" + i.toString().padStart(6, "0"), name: nm + " #" + i, price: 1.5 + (i % 100) / 10, qty: i % 50, active: (i & 1) === 0 });
  }
  return { id: "catalog-0001", title: nonAscii ? "果物カタログ" : "Fruit Catalog", version: 7, owner: { name: nonAscii ? "管理者" : "Ada Lovelace", email: "owner@example.com", org }, tags: ["fruit", "produce", "seasonal"], items };
}
const SIZES = [
  { label: "small", items: 2 },
  { label: "mid", items: 60 },
  { label: "large", items: 2000 },
];

// ---- median-of-7 ns/op timer; auto-calibrate iters so each run is ~60ms (same as rmw-e2e.bench.mjs). ----
const RUNS = 7, TARGET_MS = 60;
function nsPerOp(fn) {
  for (let i = 0; i < 50; i++) { fn(); }
  let iters = 64;
  for (;;) { const t0 = performance.now(); for (let i = 0; i < iters; i++) { fn(); } const ms = performance.now() - t0; if (ms >= 20 || iters >= (1 << 24)) { iters = Math.max(8, Math.ceil(iters * TARGET_MS / Math.max(ms, 0.01))); break; } iters *= 2; }
  const samples = [];
  for (let r = 0; r < RUNS; r++) { const t0 = performance.now(); for (let i = 0; i < iters; i++) { fn(); } samples.push((performance.now() - t0) * 1e6 / iters); }
  samples.sort((a, b) => a - b);
  return samples[RUNS >> 1];
}
function fmtNs(ns) { return ns >= 1e6 ? (ns / 1e6).toFixed(2) + " ms" : ns >= 1e3 ? (ns / 1e3).toFixed(2) + " us" : ns.toFixed(0) + " ns"; }
function fmtBytes(b) { return b == null ? "n/a" : b >= 1024 ? (b / 1024).toFixed(1) + " KB" : Math.round(b) + " B"; }

// ---- GC pressure: count GC events + total pause over a fixed sustained workload (the allocation-churn proxy
// under throughput). PerformanceObserver delivers 'gc' entries on an event-loop turn, so we run the loop then
// await a tick to flush. `sink` defeats dead-code elimination of the validate result. ----
async function gcPressure(fn, ops) {
  let count = 0, dur = 0;
  const obs = new PerformanceObserver((list) => { for (const e of list.getEntries()) { count++; dur += e.duration; } });
  obs.observe({ entryTypes: ["gc"] });
  if (global.gc) { global.gc(); global.gc(); }
  let sink = 0;
  const t0 = performance.now();
  for (let i = 0; i < ops; i++) { const r = fn(); sink ^= (r === true ? 1 : r === false ? 0 : (r && r.length) | 0); }
  const ms = performance.now() - t0;
  await new Promise((r) => setTimeout(r, 0));
  obs.disconnect();
  if (sink === 0x7fffffff) { process.stdout.write(""); }
  return { count, dur, opsPerSec: ops / (ms / 1000) };
}

// ---- best-effort bytes/op: gc-bracket a batch small enough to (usually) not trigger an intermediate GC, and
// take the min positive heapUsed delta over a few tries (a mid-batch GC makes the delta small/negative; the
// min approximates the true per-op allocation). Unreliable once a single batch out-allocates New Space, so it
// is reported as a hint, with the GC-count table as the load-bearing allocation signal. ----
function bytesPerOp(fn, batch) {
  if (!global.gc) { return null; }
  let best = Infinity, sink = 0;
  for (let t = 0; t < 6; t++) {
    global.gc(); global.gc();
    const before = process.memoryUsage().heapUsed;
    for (let i = 0; i < batch; i++) { const r = fn(); sink ^= (r === true ? 1 : r === false ? 0 : (r && r.length) | 0); }
    const per = (process.memoryUsage().heapUsed - before) / batch;
    if (per > 0 && per < best) { best = per; }
  }
  if (sink === 0x7fffffff) { process.stdout.write(""); }
  return best === Infinity ? null : best;
}

// ---- correctness gate: the validator must accept the valid fixture and reject invalid inputs. ----
function correctnessGate() {
  const ok = evaluateRoot(makeCatalog(3, false)) === true && evaluateRoot(makeCatalog(3, true)) === true;
  const rejNonObject = evaluateRoot(42) === false;
  const rejMissingRequired = evaluateRoot({ id: "x", title: "y", version: 1 }) === false; // no `items`
  if (!ok || !rejNonObject || !rejMissingRequired) {
    console.error(`CORRECTNESS FAIL: valid=${ok} rejNonObject=${rejNonObject} rejMissingRequired=${rejMissingRequired}`);
    process.exitCode = 1;
    return false;
  }
  return true;
}

async function main() {
  if (!correctnessGate()) { return; }
  console.log(`# Sustained-load validation benchmark — generated Catalog validator. node ${process.version}${global.gc ? "" : "  (no --expose-gc: GC/alloc = n/a)"}`);

  // ---- throughput: ns/op + bytes/op for parse / validate / parse+validate, per size + content ----
  console.log("\n## Throughput (ns/op, median-of-7) + best-effort bytes/op\n");
  console.log("size   content    parse-only            validate-only         parse+validate");
  console.log("-".repeat(86));
  for (const content of ["ascii", "nonAscii"]) {
    for (const sz of SIZES) {
      const doc = makeCatalog(sz.items, content === "nonAscii");
      const text = JSON.stringify(doc);
      const value = JSON.parse(text);
      const parseOnly = () => JSON.parse(text);
      const validateOnly = () => evaluateRoot(value);
      const parseValidate = () => evaluateRoot(JSON.parse(text));
      const batch = sz.items >= 2000 ? 200 : 2000;
      const col = (fn) => `${fmtNs(nsPerOp(fn)).padStart(9)} / ${fmtBytes(bytesPerOp(fn, batch)).padStart(8)}`;
      console.log(`${sz.label.padEnd(6)} ${content.padEnd(9)}  ${col(parseOnly)}   ${col(validateOnly)}   ${col(parseValidate)}`);
    }
  }

  // ---- sustained GC pressure: fixed large workload per path; how often the validator forces a minor GC ----
  console.log("\n## Sustained GC pressure (fixed workload per path) — GC events / total pause / ops-per-sec\n");
  console.log("size   content    path             ops      gc-events   gc-pause     ops/sec");
  console.log("-".repeat(82));
  for (const content of ["ascii", "nonAscii"]) {
    for (const sz of SIZES) {
      const doc = makeCatalog(sz.items, content === "nonAscii");
      const text = JSON.stringify(doc);
      const value = JSON.parse(text);
      const ops = sz.items >= 2000 ? 20000 : sz.items >= 60 ? 200000 : 1000000;
      const paths = [
        ["parse-only", () => JSON.parse(text)],
        ["validate-only", () => evaluateRoot(value)],
        ["parse+validate", () => evaluateRoot(JSON.parse(text))],
      ];
      for (const [name, fn] of paths) {
        const g = await gcPressure(fn, ops);
        const gc = global.gc ? `${String(g.count).padStart(6)}   ${(g.dur).toFixed(1).padStart(7)} ms` : `   n/a         n/a`;
        console.log(`${sz.label.padEnd(6)} ${content.padEnd(9)}  ${name.padEnd(15)} ${String(ops).padStart(8)}   ${gc}   ${Math.round(g.opsPerSec).toLocaleString().padStart(11)}`);
      }
    }
  }

  console.log("\nReading it: validate-only isolates the validator's allocation; comparing its gc-events to");
  console.log("parse-only shows how much GC pressure validation adds over the unavoidable parse. parse+validate");
  console.log("is the realistic per-request path. Lower gc-events at the same ops = less allocation churn.");
}

main().catch((e) => { console.error(e); process.exitCode = 1; });
