// Optimized Model C vs current Model C vs native (wire, bytes->bytes).
// Run: node --expose-gc optbench.mjs
//
// C    = current Model C (scanInto: scans the WHOLE doc)
// Copt = optimized Model C (early-stop targeted scan + indexOf string-skip, byte-only)
// N    = native (decode + JSON.parse + mutate + JSON.stringify + encode)
//
// Two edit placements:
//   "early" — edits hit fields BEFORE the big trailing `description` (Copt skips the blob)
//   "late"  — one edit hits `description` itself (Copt must scan to it: worst case)

import { deepStrictEqual } from 'node:assert';
import { PerformanceObserver, performance, constants } from 'node:perf_hooks';
import { SLOT } from './scanner.mjs';
import { rmwModelC, rmwModelCOpt, rmwNative, makeDoc, makeChanges, makeTargets } from './rmw.mjs';

const dec = new TextDecoder();
let SINK = 0;

function expected(doc, ch) { const o = JSON.parse(doc.json); for (let i = 0; i < ch.names.length; i++) o[ch.names[i]] = ch.vals[i]; return o; }

async function timeIt(fn, warmupMs, runMs) {
  let t = performance.now(); while (performance.now() - t < warmupMs) SINK ^= fn();
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
  console.log(`Node ${process.version}  |  wire bytes->bytes  |  ops/s and gc/1k-ops\n`);
  for (const placement of ['early', 'late']) {
    // 'early' uses the standard 'few' set (seq/score/updatedAt — all before description).
    // 'late' edits description (the big trailing field) → Copt must scan through it.
    console.log(`---- edits = ${placement} ----`);
    for (const unicode of [false, true]) {
      for (const size of [256, 4096, 65536, 262144]) {
        const doc = makeDoc(size, unicode);
        const ch = placement === 'early'
          ? makeChanges(doc, 'few')
          : { names: ['description', 'seq'], slots: [], vals: ['REPLACED-' + doc.obj.description, 5],
              cChanges: [{ slot: doc.keys.indexOf('description'), content: new TextEncoder().encode(JSON.stringify('REPLACED-' + doc.obj.description)) },
                         { slot: doc.keys.indexOf('seq'), content: new TextEncoder().encode('5') }],
              strs: [] };
        const edits = ch.cChanges.map(() => ({ offset: 0, length: 0, content: null }));
        const targets = makeTargets(ch.names, ch.cChanges.map((c) => c.content));

        // verify
        const exp = expected(doc, ch);
        deepStrictEqual(JSON.parse(dec.decode(rmwModelCOpt(doc.bytes, targets, edits.slice()))), exp, 'Copt');
        deepStrictEqual(JSON.parse(dec.decode(rmwModelC(doc.bytes, new Int32Array(64 * SLOT), ch.cChanges, edits.slice()))), exp, 'C');
        deepStrictEqual(JSON.parse(dec.decode(rmwNative(doc.bytes, ch.names, ch.vals))), exp, 'N');

        const table = new Int32Array(64 * SLOT);
        const C = await timeIt(() => sb(rmwModelC(doc.bytes, table, ch.cChanges, edits)), 150, 400);
        const O = await timeIt(() => sb(rmwModelCOpt(doc.bytes, targets, edits)), 150, 400);
        const N = await timeIt(() => sb(rmwNative(doc.bytes, ch.names, ch.vals)), 150, 400);
        const k = (v) => (v.ops_s / 1000).toFixed(1).padStart(8);
        const g = (v) => v.gcPer1k.toFixed(2).padStart(5);
        const r = (a, b) => (a.ops_s / b.ops_s).toFixed(2).padStart(5);
        console.log(`${unicode ? 'uni' : 'asc'} ${String(doc.bytes.length).padStart(7)}B | C ${k(C)} | Copt ${k(O)} g${g(O)} | N ${k(N)} g${g(N)} || Copt/N ${r(O, N)}  Copt/C ${r(O, C)}`);
      }
    }
    console.log('');
  }
  console.log(`(sink=${SINK})`);
}
run();
