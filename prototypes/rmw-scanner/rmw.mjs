// RMW variants for the Model C benchmark.
//
// All "bytes" variants honour the same contract: Uint8Array in -> Uint8Array out
// (the wire / persisted-document RMW). The "_str" variants honour string in ->
// string out (the in-memory editor / VS Code scenario, where native skips the
// transcode). Each modifies the SAME fields to the SAME values so outputs match.

import { scanInto, SLOT, F, scanStringInto, SSLOT, scanTargets } from './scanner.mjs';

const enc = new TextEncoder();
const dec = new TextDecoder();

// ---- byte edit splice (the §13.4 applyEdits byte port) ----
export function applyEditsBytes(buf, edits) {
  edits.sort((a, b) => a.offset - b.offset);
  let delta = 0;
  for (let i = 0; i < edits.length; i++) delta += edits[i].content.length - edits[i].length;
  const out = new Uint8Array(buf.length + delta);
  let src = 0, dst = 0;
  for (let i = 0; i < edits.length; i++) {
    const e = edits[i];
    out.set(buf.subarray(src, e.offset), dst); dst += e.offset - src;
    out.set(e.content, dst); dst += e.content.length;
    src = e.offset + e.length;
  }
  out.set(buf.subarray(src), dst);
  return out;
}

// ---- Model C (bytes -> bytes): dual-offset scan + copy-through splice ----
// `table` is a pooled Int32Array reused across calls. `changes` = [{slot, content:Uint8Array}].
export function rmwModelC(buf, table, changes, edits /* reused array of len changes.length */) {
  const count = scanInto(buf, table);
  for (let i = 0; i < changes.length; i++) {
    const c = changes[i];
    const base = c.slot * SLOT;
    const e = edits[i];
    e.offset = table[base + F.VAL_BS];
    e.length = table[base + F.VAL_BE] - table[base + F.VAL_BS];
    e.content = c.content;
  }
  // light "access a field" read to model use + keep things live (read value-span length of slot 0)
  void (table[F.VAL_BE] - table[F.VAL_BS]);
  void count;
  return applyEditsBytes(buf, edits);
}

// ---- Model C OPTIMIZED (bytes -> bytes): early-stop targeted scan + copy-through ----
// Builds the reusable targets array once via makeTargets(); per op it locates only the
// edited fields (stopping early) and splices. `edits` is a reused array sized to targets.
export function makeTargets(names, contents) {
  return names.map((n, i) => ({ name: enc.encode(n), content: contents[i], vbs: -1, vbe: -1 }));
}
export function rmwModelCOpt(buf, targets, edits) {
  scanTargets(buf, targets);
  for (let i = 0; i < targets.length; i++) {
    const t = targets[i], e = edits[i];
    e.offset = t.vbs; e.length = t.vbe - t.vbs; e.content = t.content;
  }
  return applyEditsBytes(buf, edits);
}

// ---- native (bytes -> bytes): decode + parse + mutate + stringify + encode ----
export function rmwNative(buf, keys, vals) {
  const obj = JSON.parse(dec.decode(buf));
  for (let i = 0; i < keys.length; i++) obj[keys[i]] = vals[i];
  return enc.encode(JSON.stringify(obj));
}

// ---- string-splice (bytes -> bytes): the UTF-16 jsonc-parser analog ----
export function rmwStringSplice(buf, table2, slots, strs) {
  const s = dec.decode(buf);
  scanStringInto(s, table2);
  // build edits with char offsets, apply right-to-left
  const edits = [];
  for (let i = 0; i < slots.length; i++) {
    const slot = slots[i];
    edits.push({ offset: table2[slot * SSLOT], length: table2[slot * SSLOT + 1] - table2[slot * SSLOT], content: strs[i] });
  }
  edits.sort((a, b) => b.offset - a.offset);
  let out = s;
  for (let i = 0; i < edits.length; i++) {
    const e = edits[i];
    out = out.slice(0, e.offset) + e.content + out.slice(e.offset + e.length);
  }
  return enc.encode(out);
}

// ---- editor scenario (string -> string), UTF-16 scan + substring splice ----
// The fair Model-C analog when the document is already an in-memory string
// (the VS Code/LSP case): NO encode/decode; operate on the string directly.
export function rmwStringSpliceStr(s, table2, slots, strs) {
  scanStringInto(s, table2);
  const edits = [];
  for (let i = 0; i < slots.length; i++) {
    const slot = slots[i];
    edits.push({ offset: table2[slot * SSLOT], length: table2[slot * SSLOT + 1] - table2[slot * SSLOT], content: strs[i] });
  }
  edits.sort((a, b) => b.offset - a.offset);
  let out = s;
  for (let i = 0; i < edits.length; i++) {
    const e = edits[i];
    out = out.slice(0, e.offset) + e.content + out.slice(e.offset + e.length);
  }
  return out;
}

// ---- editor scenario (string -> string), native best case (no transcode) ----
export function rmwNativeStr(s, keys, vals) {
  const obj = JSON.parse(s);
  for (let i = 0; i < keys.length; i++) obj[keys[i]] = vals[i];
  return JSON.stringify(obj);
}

// ---- editor scenario (string -> string), Model C pays encode+decode ----
export function rmwModelCStr(s, table, changes, edits) {
  const buf = enc.encode(s);
  const out = rmwModelC(buf, table, changes, edits);
  return dec.decode(out);
}

// ---- synthetic document generator ----
// Builds a flat-ish object (scalars + a nested object + an array + a big filler
// "description") whose serialized UTF-8 length ~= targetBytes. `unicode` makes
// the content non-ASCII (exercises the dual-offset divergence + transcode cost).
export function makeDoc(targetBytes, unicode) {
  const word = unicode ? '日本語のテキスト' : 'filler_payload_text';
  const obj = {
    id: '6f9619ff-8b86-d011-b42d-00cf4fc964ff',
    seq: 0,
    name: unicode ? '山田 太郎' : 'Jane Doe',
    email: 'jane.doe@example.com',
    active: true,
    score: 0.5,
    level: 3,
    balance: 12345.67,
    createdAt: '2026-06-24T10:00:00Z',
    updatedAt: '2026-06-24T10:00:00Z',
    address: { street: unicode ? '東京都千代田区一番地' : '1 Main Street', city: unicode ? '東京' : 'Anytown', zip: '12345', country: 'US' },
    tags: unicode ? ['赤', '緑', '青'] : ['alpha', 'beta', 'gamma'],
    description: '',
  };
  // grow `description` until target size is reached
  let filler = '';
  for (;;) {
    obj.description = filler;
    const n = enc.encode(JSON.stringify(obj)).length;
    if (n >= targetBytes) break;
    // append enough to approach target without overshooting wildly
    const need = targetBytes - n;
    const chunk = word.repeat(Math.max(1, Math.floor(need / (enc.encode(word).length))));
    filler += chunk;
    if (filler.length > targetBytes * 4) break; // safety
  }
  const json = JSON.stringify(obj);
  return { obj, json, bytes: enc.encode(json), keys: Object.keys(obj) };
}

// Build a change set: modify scalar fields only ('few') or about half the keys
// ('half', still excluding the big description to model "change a few scalars in
// a big doc"; the 'halfWithDesc' mode also rewrites description to show C losing).
export function makeChanges(doc, mode) {
  // new values
  const newVals = {
    seq: 42,
    score: 0.99,
    level: 7,
    balance: 99999.01,
    updatedAt: '2026-06-24T11:30:00Z',
    name: doc.obj.name + '!',
    email: 'new.address@example.com',
    active: false,
    description: 'REWRITTEN-' + doc.obj.description,
  };
  let names;
  if (mode === 'few') names = ['seq', 'score', 'updatedAt'];
  else if (mode === 'half') names = ['seq', 'score', 'level', 'balance', 'updatedAt', 'name'];
  else names = ['seq', 'score', 'level', 'balance', 'updatedAt', 'name', 'email', 'description']; // halfWithDesc

  const keys = doc.keys;
  const slots = names.map((n) => keys.indexOf(n));
  const cChanges = names.map((n, i) => ({ slot: slots[i], content: enc.encode(JSON.stringify(newVals[n])) }));
  const strs = names.map((n) => JSON.stringify(newVals[n]));
  return { names, slots, vals: names.map((n) => newVals[n]), cChanges, strs };
}
