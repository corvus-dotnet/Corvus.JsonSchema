// Investigates the specific optimisation flagged in §13.12: pool the target/edit arrays (and the
// draft + patches + name-bytes) in the `produce` path so it stops allocating per call.
//
//   produceNaive  = fresh patches/filter/map/makeTargets/edits every call (the §13.12 path)
//   producePooled = reuse draft Proxy + patches + targets + edits; cache field-name bytes
//   rawC          = pre-built change-set reused (the engine floor)
//   native        = JSON.parse -> mutate -> JSON.stringify -> encode
// Inherent per-op work kept in BOTH produce paths: recording the recipe + encoding the changed
// VALUES (a real mutation's values vary; rawC pre-encodes, so it is a floor, not a fair target).
// Run: node --expose-gc produce-pool-bench.mjs

import { PerformanceObserver, performance } from 'node:perf_hooks';
import { deepStrictEqual } from 'node:assert';
import { scanTargets } from './scanner.mjs';
import { applyEditsBytes, makeTargets, makeDoc, makeChanges, rmwNative } from './rmw.mjs';

const enc = new TextEncoder();
const dec = new TextDecoder();
let SINK = 0;

// ---- naive produce (allocates every call) ----
function produceEditsNaive(recipe) {
  const patches = [];
  const sub = (top) => new Proxy({}, { set(_t, k, v) { patches.push({ top, nested: k, value: v }); return true; } });
  const draft = new Proxy({}, { get(_t, k) { return sub(k); }, set(_t, k, v) { patches.push({ top: k, nested: null, value: v }); return true; } });
  recipe(draft);
  return patches;
}
function produceNaive(bytes, recipe) {
  const patches = produceEditsNaive(recipe).filter((p) => p.nested === null);
  const names = patches.map((p) => p.top);
  const contents = patches.map((p) => enc.encode(JSON.stringify(p.value)));
  const targets = makeTargets(names, contents);
  scanTargets(bytes, targets);
  const edits = targets.map((t) => ({ offset: t.vbs, length: t.vbe - t.vbs, content: t.content }));
  return applyEditsBytes(bytes, edits);
}

// ---- pooled produce (reuse everything; in real codegen these pools are per-type / thread-local) ----
const P = {
  patches: [], pc: 0, subCache: new Map(),
  targets: [], edits: [], nameCache: new Map(), draft: null,
};
P.draft = new Proxy({}, {
  get(_t, k) {
    let s = P.subCache.get(k);
    if (!s) { s = new Proxy({}, { set(_x, kk, v) { const p = P.patches[P.pc] || (P.patches[P.pc] = {}); p.top = k; p.nested = kk; p.value = v; P.pc++; return true; } }); P.subCache.set(k, s); }
    return s;
  },
  set(_t, k, v) { const p = P.patches[P.pc] || (P.patches[P.pc] = {}); p.top = k; p.nested = null; p.value = v; P.pc++; return true; },
});
function nameBytes(n) { let b = P.nameCache.get(n); if (!b) { b = enc.encode(n); P.nameCache.set(n, b); } return b; }
function producePooled(bytes, recipe) {
  P.pc = 0;
  recipe(P.draft);
  let k = 0;
  for (let i = 0; i < P.pc; i++) {
    const p = P.patches[i];
    if (p.nested !== null) continue;
    const t = P.targets[k] || (P.targets[k] = { name: null, content: null, vbs: -1, vbe: -1 });
    t.name = nameBytes(p.top);
    t.content = enc.encode(JSON.stringify(p.value)); // inherent: the new value
    t.vbs = -1; t.vbe = -1;
    k++;
  }
  P.targets.length = k;
  scanTargets(bytes, P.targets);
  P.edits.length = k;
  for (let i = 0; i < k; i++) { const e = P.edits[i] || (P.edits[i] = { offset: 0, length: 0, content: null }); const t = P.targets[i]; e.offset = t.vbs; e.length = t.vbe - t.vbs; e.content = t.content; }
  return applyEditsBytes(bytes, P.edits);
}

async function timeIt(fn, warmupMs, runMs) {
  let t = performance.now();
  while (performance.now() - t < warmupMs) SINK ^= fn();
  let gc = 0; const obs = new PerformanceObserver((l) => { for (const _ of l.getEntries()) gc++; });
  obs.observe({ entryTypes: ['gc'] });
  if (global.gc) global.gc();
  let ops = 0, work = 0; const dl = performance.now() + runMs;
  while (performance.now() < dl) { const s = performance.now(); for (let i = 0; i < 64; i++) SINK ^= fn(); work += performance.now() - s; ops += 64; await new Promise((r) => setImmediate(r)); }
  await new Promise((r) => setImmediate(r)); obs.disconnect();
  return { ops_s: ops / (work / 1000), gcPer1k: gc / (ops / 1000) };
}
const sb = (u) => u.length | u[0];

async function run() {
  console.log(`Node ${process.version}  |  pooling the produce target/edit arrays  |  ops/s, gc/1k\n`);
  const recipe = (d) => { d.seq = 42; d.score = 0.99; d.updatedAt = '2026-06-24T11:30:00Z'; };
  for (const unicode of [false, true]) {
    for (const size of [256, 4096, 65536, 262144]) {
      const doc = makeDoc(size, unicode);
      const ch = makeChanges(doc, 'few');
      const edits = ch.cChanges.map(() => ({ offset: 0, length: 0, content: null }));
      const targets = makeTargets(ch.names, ch.cChanges.map((c) => c.content));
      const exp = JSON.parse(dec.decode(rmwNative(doc.bytes, ch.names, ch.vals)));
      deepStrictEqual(JSON.parse(dec.decode(produceNaive(doc.bytes, recipe))), exp, 'naive');
      deepStrictEqual(JSON.parse(dec.decode(producePooled(doc.bytes, recipe))), exp, 'pooled');
      const rawC = (b) => { scanTargets(b, targets); for (let i = 0; i < targets.length; i++) { edits[i].offset = targets[i].vbs; edits[i].length = targets[i].vbe - targets[i].vbs; edits[i].content = targets[i].content; } return applyEditsBytes(b, edits); };

      const Nv = await timeIt(() => sb(produceNaive(doc.bytes, recipe)), 150, 400);
      const Po = await timeIt(() => sb(producePooled(doc.bytes, recipe)), 150, 400);
      const R = await timeIt(() => sb(rawC(doc.bytes)), 150, 400);
      const Na = await timeIt(() => sb(rmwNative(doc.bytes, ch.names, ch.vals)), 150, 400);
      const k = (v) => (v.ops_s / 1000).toFixed(1).padStart(8);
      const g = (v) => v.gcPer1k.toFixed(2).padStart(5);
      const r = (a, b) => (a.ops_s / b.ops_s).toFixed(2).padStart(5);
      console.log(`${unicode ? 'uni' : 'asc'} ${String(doc.bytes.length).padStart(7)}B | naive ${k(Nv)} g${g(Nv)} | pooled ${k(Po)} g${g(Po)} | rawC ${k(R)} | native ${k(Na)} || pooled/naive ${r(Po, Nv)}  pooled/rawC ${r(Po, R)}  pooled/native ${r(Po, Na)}`);
    }
  }
  console.log(`\n(sink=${SINK})`);
}
run();
