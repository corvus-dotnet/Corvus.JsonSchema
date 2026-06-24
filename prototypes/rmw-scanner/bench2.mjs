// Round 2 — the two patterns round 1 under-measured.  Run: node --expose-gc bench2.mjs
//
// Part A — incremental editor session: open ONCE, then M cheap edits. Model C emits a
//   minimal TextEdit + O(members) fixup, never re-serialising; native must re-stringify the
//   whole doc per edit (JSON.parse has no source positions). Measures amortized per-edit cost.
// Part B — tail latency under sustained load: many full RMW ops, per-op latency distribution
//   (GC pauses land in p99/max) + total GC pause time. The allocation→GC→tail thesis.

import { deepStrictEqual } from 'node:assert';
import { PerformanceObserver, performance, constants } from 'node:perf_hooks';
import { SLOT } from './scanner.mjs';
import { rmwModelC, rmwNative, makeDoc, makeChanges } from './rmw.mjs';
import { openC, editEmit, editSplice, openNative, editNative } from './editor.mjs';

const enc = new TextEncoder();
const dec = new TextDecoder();
let SINK = 0;

function buildScript(doc) {
  // cycle of edits with varying-length values (exercises the offset fixup)
  const spec = [
    ['seq', 7], ['name', 'Ann'], ['level', 88], ['balance', 123456.789],
    ['seq', 999999], ['name', 'a really quite long replacement name'], ['level', 1], ['balance', 0],
  ];
  return spec.map(([key, val]) => {
    const text = JSON.stringify(val);
    return { slot: doc.keys.indexOf(key), key, val, text, bytes: enc.encode(text) };
  });
}

function verifySession(doc, script, M) {
  const ns = openNative(doc.json);
  let nativeFinal = doc.json;
  for (let m = 0; m < M; m++) { const e = script[m % script.length]; nativeFinal = editNative(ns, e.key, e.val); }
  const cs = openC(doc.bytes.slice());
  let spliceFinal;
  for (let m = 0; m < M; m++) { const e = script[m % script.length]; spliceFinal = editSplice(cs, e.slot, e.bytes); }
  deepStrictEqual(JSON.parse(dec.decode(spliceFinal)), JSON.parse(nativeFinal), 'incremental session mismatch');
}

async function timeSession(makeRunner, warmupMs, runMs) {
  // makeRunner() returns a fn that runs ONE full session and returns the number of edits done.
  let t = performance.now();
  while (performance.now() - t < warmupMs) { SINK ^= makeRunner()(); }
  let gc = 0;
  const obs = new PerformanceObserver((l) => { for (const _ of l.getEntries()) gc++; });
  obs.observe({ entryTypes: ['gc'] });
  if (global.gc) global.gc();
  let edits = 0, workMs = 0;
  const deadline = performance.now() + runMs;
  while (performance.now() < deadline) {
    const run = makeRunner();
    const s = performance.now();
    const n = run(); SINK ^= n;
    workMs += performance.now() - s; edits += n;
    await new Promise((r) => setImmediate(r));
  }
  await new Promise((r) => setImmediate(r));
  obs.disconnect();
  return { perEditUs: (workMs * 1000) / edits, edits, gcPer1k: gc / (edits / 1000) };
}

async function partA() {
  console.log('\n========== Part A — incremental editor session (open once, M edits) ==========');
  console.log('per-edit cost (µs) and gc-events/1000-edits. native re-stringifies whole doc/edit.\n');
  const M = 400;
  for (const size of [4096, 65536, 262144]) {
    const doc = makeDoc(size, false);
    const script = buildScript(doc);
    verifySession(doc, script, M);

    const emit = await timeSession(() => {
      const st = openC(doc.bytes); // note: openC scans doc.bytes; editEmit never mutates the buffer
      return () => { for (let m = 0; m < M; m++) { const e = script[m % script.length]; const ed = editEmit(st, e.slot, e.text); SINK ^= ed.newText.length; } return M; };
    }, 150, 500);
    const splice = await timeSession(() => {
      const st = openC(doc.bytes.slice());
      return () => { for (let m = 0; m < M; m++) { const e = script[m % script.length]; const b = editSplice(st, e.slot, e.bytes); SINK ^= b.length; } return M; };
    }, 150, 500);
    const nat = await timeSession(() => {
      const st = openNative(doc.json);
      return () => { for (let m = 0; m < M; m++) { const e = script[m % script.length]; const s = editNative(st, e.key, e.val); SINK ^= s.length; } return M; };
    }, 150, 500);

    const f = (v) => v.perEditUs.toFixed(2).padStart(9);
    const g = (v) => v.gcPer1k.toFixed(2).padStart(6);
    console.log(
      `${String(doc.bytes.length).padStart(7)}B | emit ${f(emit)}µs g${g(emit)} | splice ${f(splice)}µs g${g(splice)} | native ${f(nat)}µs g${g(nat)} ` +
      `|| native/emit ${(nat.perEditUs / emit.perEditUs).toFixed(0).padStart(5)}×  native/splice ${(nat.perEditUs / splice.perEditUs).toFixed(1).padStart(5)}×`
    );
  }
}

async function latency(fn, M) {
  // warmup
  let t = performance.now(); while (performance.now() - t < 150) { SINK ^= fn(); }
  const lat = new Float64Array(M);
  let gcCount = 0, gcMs = 0;
  const obs = new PerformanceObserver((l) => { for (const e of l.getEntries()) { gcCount++; gcMs += e.duration; } });
  obs.observe({ entryTypes: ['gc'] });
  if (global.gc) global.gc();
  let i = 0;
  while (i < M) {
    const end = Math.min(i + 256, M);
    for (; i < end; i++) { const t0 = performance.now(); SINK ^= fn(); lat[i] = (performance.now() - t0) * 1000; } // µs
    await new Promise((r) => setImmediate(r));
  }
  await new Promise((r) => setImmediate(r));
  obs.disconnect();
  lat.sort();
  const q = (p) => lat[Math.min(M - 1, Math.floor(p * M))];
  return { p50: q(0.50), p99: q(0.99), p999: q(0.999), max: lat[M - 1], gcCount, gcMs };
}

async function partB() {
  console.log('\n========== Part B — tail latency under sustained load (full RMW, per-op) ==========');
  console.log('latency µs (lower better); total GC pause ms over the run. p99/max capture GC hits.\n');
  const M = 20000;
  const table = new Int32Array(64 * SLOT);
  for (const unicode of [false, true]) {
    const doc = makeDoc(65536, unicode);
    const ch = makeChanges(doc, 'few');
    const edits = ch.cChanges.map(() => ({ offset: 0, length: 0, content: null }));
    const C = await latency(() => { const u = rmwModelC(doc.bytes, table, ch.cChanges, edits); return u.length | u[0]; }, M);
    const N = await latency(() => { const u = rmwNative(doc.bytes, ch.names, ch.vals); return u.length | u[0]; }, M);
    const u = (x) => x.toFixed(1).padStart(8);
    const lab = unicode ? 'unicode 64KB' : 'ascii   64KB';
    console.log(`${lab} | ModelC  p50 ${u(C.p50)} p99 ${u(C.p99)} p99.9 ${u(C.p999)} max ${u(C.max)}  GCpause ${C.gcMs.toFixed(0)}ms (${C.gcCount})`);
    console.log(`             | native  p50 ${u(N.p50)} p99 ${u(N.p99)} p99.9 ${u(N.p999)} max ${u(N.max)}  GCpause ${N.gcMs.toFixed(0)}ms (${N.gcCount})`);
    console.log(`             | native/ModelC  p99 ${(N.p99 / C.p99).toFixed(1)}×  max ${(N.max / C.max).toFixed(1)}×  GCpause ${(N.gcMs / Math.max(0.01, C.gcMs)).toFixed(1)}×\n`);
  }
}

async function main() {
  console.log(`Node ${process.version}`);
  await partA();
  await partB();
  console.log(`(sink=${SINK})`);
}
main();
