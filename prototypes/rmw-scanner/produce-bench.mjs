// Benchmarks the MUTATION API itself (the immer-style `produce` layer), not just the Model C
// engine it lowers to. Key correctness/perf point: the production recorder must capture the
// change-set WITHOUT cloning/materialising the whole document (the produce.mjs spike used
// structuredClone only for its correctness asserts — that would defeat Model C). Here the recorder
// is non-cloning + write-recording, so we measure the real API overhead.
//
//   produce  = non-cloning Proxy recorder -> change-set -> Model C byte patch   (full mutation API)
//   rawC     = pre-built change-set       -> Model C byte patch                 (engine alone)
//   native   = JSON.parse -> mutate -> JSON.stringify -> encode
// Run: node --expose-gc produce-bench.mjs

import { PerformanceObserver, performance } from 'node:perf_hooks';
import { deepStrictEqual } from 'node:assert';
import { scanTargets } from './scanner.mjs';
import { applyEditsBytes, makeTargets, makeDoc, makeChanges, rmwNative } from './rmw.mjs';

const enc = new TextEncoder();
const dec = new TextDecoder();
let SINK = 0;

// Non-cloning, write-recording draft: records (topKey, value); supports one nested level.
// No structuredClone, no materialised `next` — just the change-set, which Model C lowers to bytes.
function produceEdits(recipe) {
  const patches = [];
  const sub = (top) => new Proxy({}, { set(_t, k, v) { patches.push({ top, nested: k, value: v }); return true; } });
  const draft = new Proxy({}, {
    get(_t, k) { return sub(k); },
    set(_t, k, v) { patches.push({ top: k, nested: null, value: v }); return true; },
  });
  recipe(draft);
  return patches;
}

// lower top-level replace patches to a Model C byte patch (reusing the proven scanner)
function produceToBytes(bytes, recipe) {
  const patches = produceEdits(recipe).filter((p) => p.nested === null);
  const names = patches.map((p) => p.top);
  const contents = patches.map((p) => enc.encode(JSON.stringify(p.value)));
  const targets = makeTargets(names, contents);
  scanTargets(bytes, targets);
  const edits = targets.map((t) => ({ offset: t.vbs, length: t.vbe - t.vbs, content: t.content }));
  return applyEditsBytes(bytes, edits);
}

async function timeIt(fn, warmupMs, runMs) {
  let t = performance.now();
  while (performance.now() - t < warmupMs) SINK ^= fn();
  let gc = 0;
  const obs = new PerformanceObserver((l) => { for (const _ of l.getEntries()) gc++; });
  obs.observe({ entryTypes: ['gc'] });
  if (global.gc) global.gc();
  let ops = 0, work = 0; const dl = performance.now() + runMs;
  while (performance.now() < dl) {
    const s = performance.now();
    for (let i = 0; i < 64; i++) SINK ^= fn();
    work += performance.now() - s; ops += 64;
    await new Promise((r) => setImmediate(r));
  }
  await new Promise((r) => setImmediate(r)); obs.disconnect();
  return { ops_s: ops / (work / 1000), gcPer1k: gc / (ops / 1000) };
}
const sb = (u) => u.length | u[0];

async function run() {
  console.log(`Node ${process.version}  |  mutation API (produce) vs raw Model C vs native  |  ops/s, gc/1k\n`);
  // recipe matches makeChanges('few'): set seq, score, updatedAt
  const recipe = (d) => { d.seq = 42; d.score = 0.99; d.updatedAt = '2026-06-24T11:30:00Z'; };

  for (const unicode of [false, true]) {
    for (const size of [256, 4096, 65536, 262144]) {
      const doc = makeDoc(size, unicode);
      const ch = makeChanges(doc, 'few');
      const edits = ch.cChanges.map(() => ({ offset: 0, length: 0, content: null }));
      const targets = makeTargets(ch.names, ch.cChanges.map((c) => c.content));

      // verify produce path == native output
      const exp = JSON.parse(dec.decode(rmwNative(doc.bytes, ch.names, ch.vals)));
      deepStrictEqual(JSON.parse(dec.decode(produceToBytes(doc.bytes, recipe))), exp, 'produce mismatch');

      const rawC = (b) => { scanTargets(b, targets); for (let i = 0; i < targets.length; i++) { edits[i].offset = targets[i].vbs; edits[i].length = targets[i].vbe - targets[i].vbs; edits[i].content = targets[i].content; } return applyEditsBytes(b, edits); };

      const P = await timeIt(() => sb(produceToBytes(doc.bytes, recipe)), 150, 400);
      const R = await timeIt(() => sb(rawC(doc.bytes)), 150, 400);
      const N = await timeIt(() => sb(rmwNative(doc.bytes, ch.names, ch.vals)), 150, 400);
      const k = (v) => (v.ops_s / 1000).toFixed(1).padStart(8);
      const g = (v) => v.gcPer1k.toFixed(2).padStart(5);
      const r = (a, b) => (a.ops_s / b.ops_s).toFixed(2).padStart(5);
      console.log(`${unicode ? 'uni' : 'asc'} ${String(doc.bytes.length).padStart(7)}B | produce ${k(P)} g${g(P)} | rawC ${k(R)} g${g(R)} | native ${k(N)} g${g(N)} || produce/native ${r(P, N)}  produce/rawC ${r(P, R)}`);
    }
  }
  console.log(`\n(sink=${SINK})  produce/rawC ~1.0 = the API layer adds ~no overhead over the engine.`);
}
run();
