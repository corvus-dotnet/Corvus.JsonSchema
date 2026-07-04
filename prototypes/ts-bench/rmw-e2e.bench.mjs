// E2E mutation benchmark (§13.6 acceptance gate) — the SHIPPING generated engine vs the native default and
// immer, bytes-in -> bytes-out. Section A: the mutation surface (produce/patch/build). Section B: the full
// request round-trip (read -> validate -> mutate -> write). Median-of-7 ns/op (auto-calibrated), GC-pressure,
// correctness-gated (every variant's output must structurally equal the native result before timing counts).
//
// Generate the module first, then run — both are wired up by ./run-rmw-bench.sh:
//    ./run-rmw-bench.sh
// which builds the bowtie codegen worker (Release), generates the Catalog module for ./bench-catalog.json
// into ./rmw-bench-gen/ (gen-rmw-bench.mjs drives the worker via NDJSON then esbuild-transpiles
// generated.ts + corvus-runtime.ts — CreateDefault codegen, identical to the spike's old default mode),
// and runs:
//    node --expose-gc rmw-e2e.bench.mjs
import { produceCatalog, patchCatalog, buildCatalog, evaluateRoot } from "./rmw-bench-gen/generated.js";
import { produce as immerProduce } from "immer";
import { PerformanceObserver } from "node:perf_hooks";

const enc = new TextEncoder(), dec = new TextDecoder();

// ---- fixtures: a Catalog whose `items` array scales the payload; ascii vs non-ascii content ----
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

// ---- structural (order-insensitive) deep equality for the correctness gate ----
function jsonEq(a, b) {
  if (a === b) return true;
  if (a === null || b === null || typeof a !== "object" || typeof b !== "object") return a === b;
  const aa = Array.isArray(a), ba = Array.isArray(b);
  if (aa !== ba) return false;
  if (aa) { if (a.length !== b.length) return false; for (let i = 0; i < a.length; i++) if (!jsonEq(a[i], b[i])) return false; return true; }
  const ka = Object.keys(a), kb = Object.keys(b);
  if (ka.length !== kb.length) return false;
  for (const k of ka) { if (!(k in b) || !jsonEq(a[k], b[k])) return false; }
  return true;
}
const parse = (bytes) => JSON.parse(dec.decode(bytes));

// ---- median-of-7 ns/op timer; auto-calibrate iters so each run is ~60ms ----
const RUNS = 7, TARGET_MS = 60;
function nsPerOp(fn) {
  for (let i = 0; i < 50; i++) fn();                       // warmup
  let iters = 64;
  for (;;) { const t0 = performance.now(); for (let i = 0; i < iters; i++) fn(); const ms = performance.now() - t0; if (ms >= 20 || iters >= (1 << 24)) { iters = Math.max(8, Math.ceil(iters * TARGET_MS / Math.max(ms, 0.01))); break; } iters *= 2; }
  const samples = [];
  for (let r = 0; r < RUNS; r++) { const t0 = performance.now(); for (let i = 0; i < iters; i++) fn(); samples.push((performance.now() - t0) * 1e6 / iters); }
  samples.sort((a, b) => a - b);
  return samples[RUNS >> 1];
}
function fmt(ns) { return ns >= 1e6 ? (ns / 1e6).toFixed(2) + " ms" : ns >= 1e3 ? (ns / 1e3).toFixed(2) + " us" : ns.toFixed(0) + " ns"; }

// ---- GC pressure: count GC events + total pause over a fixed workload (proxy for allocation churn).
// PerformanceObserver delivers 'gc' entries to its callback on an event-loop turn, so the measurement
// must be async: run the synchronous loop, then await a tick to flush the buffered entries. ----
async function gcPressure(fn, ops) {
  let count = 0, dur = 0;
  const obs = new PerformanceObserver((list) => { for (const e of list.getEntries()) { count++; dur += e.duration; } });
  obs.observe({ entryTypes: ["gc"] });
  if (global.gc) { global.gc(); global.gc(); }
  let acc = 0;
  for (let i = 0; i < ops; i++) { const o = fn(); acc += o.length; }
  await new Promise((r) => setTimeout(r, 0));   // let buffered gc entries flush to the callback
  obs.disconnect();
  return { count, dur, acc };
}

// ---- scenario runner: gate ours/immer against native, then time all three ----
const A = [], B = [];
function scenarioA(name, size, content, bytes, oursFn, nativeFn, immerFn) {
  const nat = nativeFn(); const ours = oursFn();
  if (!jsonEq(parse(ours), parse(nat))) { console.error(`CORRECTNESS FAIL [${name} ${size}/${content}] ours != native`); process.exitCode = 1; return; }
  let immerNs = null;
  if (immerFn) { const im = immerFn(); if (!jsonEq(parse(im), parse(nat))) { console.error(`CORRECTNESS FAIL [${name} ${size}/${content}] immer != native`); process.exitCode = 1; } else immerNs = nsPerOp(immerFn); }
  A.push({ name, size, content, ours: nsPerOp(oursFn), native: nsPerOp(nativeFn), immer: immerNs });
}

function run() {
  for (const content of ["ascii", "nonAscii"]) {
    const na = content === "nonAscii";
    for (const sz of SIZES) {
      const doc = makeCatalog(sz.items, na);
      const bytes = enc.encode(JSON.stringify(doc));
      const mid = Math.floor(sz.items / 2);
      // recipes: one recipe, three drivers (draft / immer draft / plain object) => identical semantics
      const rTitle = (d) => { d.title = "Catalog v2 — updated"; };
      const rNested = (d) => { d.owner.name = "New Owner"; };
      const rElem = (d) => { d.items[mid].price = 99.99; };
      const rPush = (d) => { d.items.push({ sku: "SKU-NEW", name: "New Item", price: 5, qty: 1, active: true }); };
      const nativeOf = (r) => () => { const v = parse(bytes); r(v); return enc.encode(JSON.stringify(v)); };
      const immerOf = (r) => () => enc.encode(JSON.stringify(immerProduce(parse(bytes), r)));
      // Section A — mutation surface
      scenarioA("produce title (1 scalar)", sz.label, content, bytes, () => produceCatalog(bytes, rTitle), nativeOf(rTitle), immerOf(rTitle));
      scenarioA("produce owner.name (nested)", sz.label, content, bytes, () => produceCatalog(bytes, rNested), nativeOf(rNested), immerOf(rNested));
      scenarioA("produce items[mid].price", sz.label, content, bytes, () => produceCatalog(bytes, rElem), nativeOf(rElem), immerOf(rElem));
      scenarioA("produce items.push", sz.label, content, bytes, () => produceCatalog(bytes, rPush), nativeOf(rPush), immerOf(rPush));
      scenarioA("patch {title} (change-set)", sz.label, content, bytes, () => patchCatalog(bytes, { title: "Catalog v2 — updated" }), nativeOf(rTitle), null);
      scenarioA("build (construct)", sz.label, content, bytes, () => buildCatalog(doc), () => enc.encode(JSON.stringify(doc)), null);
      // Section B — full round-trip: read -> validate (held constant) -> mutate -> write.
      // Two recipes: a scalar edit (shows the write-side win — produce yields bytes, no full re-serialise)
      // and an array-element edit (shows the cost — touching an array decodes it).
      for (const [tripName, rTrip] of [["round-trip scalar edit", (d) => { d.title = "Processed"; }], ["round-trip array edit", (d) => { d.items[mid].price = 0; }]]) {
        const oursTrip = () => { const v = parse(bytes); evaluateRoot(v); return produceCatalog(bytes, rTrip); };
        const natTrip = () => { const v = parse(bytes); evaluateRoot(v); rTrip(v); return enc.encode(JSON.stringify(v)); };
        const imTrip = () => { const v = parse(bytes); evaluateRoot(v); return enc.encode(JSON.stringify(immerProduce(v, rTrip))); };
        if (!jsonEq(parse(oursTrip()), parse(natTrip())) || !jsonEq(parse(imTrip()), parse(natTrip()))) { console.error(`CORRECTNESS FAIL [${tripName} ${sz.label}/${content}]`); process.exitCode = 1; }
        B.push({ name: tripName, size: sz.label, content, ours: nsPerOp(oursTrip), native: nsPerOp(natTrip), immer: nsPerOp(imTrip) });
      }
    }
  }
}

function table(rows, title) {
  console.log("\n" + title);
  console.log("scenario                         size   content   model-C      native       immer      vs native   vs immer");
  console.log("-".repeat(112));
  for (const r of rows) {
    const vn = (r.native / r.ours), vi = r.immer ? (r.immer / r.ours) : null;
    console.log(
      r.name.padEnd(32) + " " + r.size.padEnd(6) + " " + r.content.padEnd(9) + " " +
      fmt(r.ours).padStart(10) + " " + fmt(r.native).padStart(11) + " " + (r.immer ? fmt(r.immer) : "-").padStart(11) + " " +
      (vn.toFixed(2) + "x").padStart(10) + " " + (vi ? vi.toFixed(2) + "x" : "-").padStart(10));
  }
}

run();
table(A, "# Section A — mutation surface (bytes-in -> bytes-out). ns/op, median-of-7. 'vs native' = native/Model-C (>1 = Model C faster).");
table(B, "# Section B — full round-trip: read -> validate -> mutate -> write. Validator held constant (our evaluateRoot) so the delta is mutate+write.");

// GC pressure on large-doc round-trips. Two recipes: a scalar edit (Model C's lazy sweet spot -> near-zero
// alloc, the allocation WIN) and an array-element edit (touching the dominant `items` array forces a
// decode+clone+diff -> the honest allocation COST).
{
  const doc = makeCatalog(2000, false); const bytes = enc.encode(JSON.stringify(doc)); const mid = 1000; const ops = 3000;
  for (const [label, rTrip] of [["scalar edit", (d) => { d.title = "Processed"; }], ["array edit", (d) => { d.items[mid].price = 0; }]]) {
    const gOurs = await gcPressure(() => { const v = parse(bytes); evaluateRoot(v); return produceCatalog(bytes, rTrip); }, ops);
    const gNat = await gcPressure(() => { const v = parse(bytes); evaluateRoot(v); rTrip(v); return enc.encode(JSON.stringify(v)); }, ops);
    const gIm = await gcPressure(() => { const v = parse(bytes); evaluateRoot(v); return enc.encode(JSON.stringify(immerProduce(v, rTrip))); }, ops);
    console.log(`\n# GC pressure over ${ops} large-doc (${(bytes.length / 1024).toFixed(0)} KB) round-trips — ${label}. Lower = less allocation churn.`);
    console.log("variant     gc events   total pause   per-op pause");
    console.log("-".repeat(54));
    for (const [nm, g] of [["Model C", gOurs], ["native", gNat], ["immer", gIm]]) {
      console.log(nm.padEnd(12) + String(g.count).padStart(9) + "   " + (g.dur.toFixed(1) + " ms").padStart(11) + "   " + ((g.dur * 1000 / ops).toFixed(2) + " us").padStart(11));
    }
  }
}
console.log("\nDONE" + (process.exitCode ? " (with correctness failures)" : " — all variants structurally agree."));
