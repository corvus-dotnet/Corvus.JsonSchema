// Sustained-load RMW benchmark (§13.6 acceptance gate).
// Run with:  node --expose-gc bench.mjs
//
// Compares, under a steady-state RMW loop (read -> modify k fields -> write):
//   Scenario A (wire: bytes -> bytes)
//     C  modelC        dual-offset scan + copy-through byte splice
//     N  native        TextDecoder + JSON.parse + mutate + JSON.stringify + TextEncoder
//     S  stringSplice  TextDecoder + UTF-16 scan + substring splice + encode (bytes contract)
//   Scenario B (editor: string -> string; the VS Code/LSP in-memory case)
//     NS native(str)   JSON.parse + mutate + JSON.stringify (no transcode — native's best case)
//     SS splice(str)   UTF-16 scan + substring splice (Model-C analog on a string; no transcode)
//     CS modelC(str)   encode + modelC + decode (byte core forced through transcode)
//
// Metric: throughput (ops/s) measured over synchronous work only; GC events &
// ms per 1000 ops (allocation-rate proxy) captured via an async-yielding loop so
// the PerformanceObserver actually fires. Verifies equivalence before timing.

import { deepStrictEqual } from 'node:assert';
import { PerformanceObserver, performance, constants } from 'node:perf_hooks';
import { SLOT, SSLOT } from './scanner.mjs';
import {
  rmwModelC, rmwNative, rmwStringSplice, rmwNativeStr, rmwModelCStr, rmwStringSpliceStr,
  makeDoc, makeChanges,
} from './rmw.mjs';

const dec = new TextDecoder();
let SINK = 0;

function expectedObj(doc, ch) {
  const o = JSON.parse(doc.json);
  for (let i = 0; i < ch.names.length; i++) o[ch.names[i]] = ch.vals[i];
  return o;
}

function verify(doc, ch, table, table2, edits) {
  const exp = expectedObj(doc, ch);
  deepStrictEqual(JSON.parse(dec.decode(rmwModelC(doc.bytes, table, ch.cChanges, edits.slice()))), exp, 'modelC');
  deepStrictEqual(JSON.parse(dec.decode(rmwNative(doc.bytes, ch.names, ch.vals))), exp, 'native');
  deepStrictEqual(JSON.parse(dec.decode(rmwStringSplice(doc.bytes, table2, ch.slots, ch.strs))), exp, 'stringSplice');
  deepStrictEqual(JSON.parse(rmwNativeStr(doc.json, ch.names, ch.vals)), exp, 'nativeStr');
  deepStrictEqual(JSON.parse(rmwStringSpliceStr(doc.json, table2, ch.slots, ch.strs)), exp, 'spliceStr');
  deepStrictEqual(JSON.parse(rmwModelCStr(doc.json, table, ch.cChanges, edits.slice())), exp, 'modelCStr');
}

async function timeVariant(fn, warmupMs, runMs) {
  let t = performance.now();
  while (performance.now() - t < warmupMs) { SINK ^= fn(); }
  let gcCount = 0, gcMs = 0, minor = 0;
  const obs = new PerformanceObserver((list) => {
    for (const e of list.getEntries()) {
      gcCount++; gcMs += e.duration;
      if (e.detail && e.detail.kind === constants.NODE_PERFORMANCE_GC_MINOR) minor++;
    }
  });
  obs.observe({ entryTypes: ['gc'] });
  if (global.gc) global.gc();
  let ops = 0, workMs = 0;
  const deadline = performance.now() + runMs;
  while (performance.now() < deadline) {
    const s = performance.now();
    for (let i = 0; i < 64; i++) { SINK ^= fn(); }
    workMs += performance.now() - s; ops += 64;
    await new Promise((r) => setImmediate(r)); // let GC entries flush to the observer
  }
  await new Promise((r) => setImmediate(r));
  obs.disconnect();
  return { opsPerSec: ops / (workMs / 1000), ops, gcCount, minor, gcMs };
}

const sinkBytes = (u) => u.length | u[0];
const sinkStr = (s) => s.length | s.charCodeAt(0);

async function run() {
  const sizes = [256, 4096, 65536, 262144];
  const contents = [false, true];
  const modes = ['few', 'half'];
  const WARM = 150, RUN = 500;

  const table = new Int32Array(64 * SLOT);
  const table2 = new Int32Array(64 * SSLOT);

  console.log(`Node ${process.version}  |  warmup ${WARM}ms run ${RUN}ms/variant  |  g = gc-events per 1000 ops (alloc proxy)\n`);

  for (const mode of modes) {
    console.log(`\n================ change set = "${mode}" ================`);
    console.log('  doc          |  WIRE (bytes->bytes)                                  ||        | EDITOR (string->string)');
    console.log('               |  C ops/s  g     N ops/s  g     S ops/s     C/N  S/N || ratio  | NS ops/s  SS ops/s  CS ops/s   SS/NS CS/NS');
    for (const unicode of contents) {
      for (const size of sizes) {
        const doc = makeDoc(size, unicode);
        const ch = makeChanges(doc, mode);
        const edits = ch.cChanges.map(() => ({ offset: 0, length: 0, content: null }));
        verify(doc, ch, table, table2, edits);

        const C = await timeVariant(() => sinkBytes(rmwModelC(doc.bytes, table, ch.cChanges, edits)), WARM, RUN);
        const N = await timeVariant(() => sinkBytes(rmwNative(doc.bytes, ch.names, ch.vals)), WARM, RUN);
        const S = await timeVariant(() => sinkBytes(rmwStringSplice(doc.bytes, table2, ch.slots, ch.strs)), WARM, RUN);
        const NS = await timeVariant(() => sinkStr(rmwNativeStr(doc.json, ch.names, ch.vals)), WARM, RUN);
        const SS = await timeVariant(() => sinkStr(rmwStringSpliceStr(doc.json, table2, ch.slots, ch.strs)), WARM, RUN);
        const CS = await timeVariant(() => sinkStr(rmwModelCStr(doc.json, table, ch.cChanges, edits)), WARM, RUN);

        const k = (v) => (v.opsPerSec / 1000).toFixed(1).padStart(8);
        const g = (v) => (v.gcCount / (v.ops / 1000)).toFixed(2).padStart(5);
        const r = (a, b) => (a.opsPerSec / b.opsPerSec).toFixed(2).padStart(4);
        const label = `${unicode ? 'uni' : 'asc'} ${String(doc.bytes.length).padStart(7)}B`;
        console.log(
          `${label} | ${k(C)} ${g(C)} ${k(N)} ${g(N)} ${k(S)}   ${r(C, N)} ${r(S, N)} || ` +
          `      | ${k(NS)} ${k(SS)} ${k(CS)}   ${r(SS, NS)} ${r(CS, NS)}`
        );
      }
    }
  }
  console.log(`\n(sink=${SINK})`);
}

run();
