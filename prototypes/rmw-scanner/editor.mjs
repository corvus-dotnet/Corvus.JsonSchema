// Incremental editor model (Model C's real editor value): open (scan) once, then
// apply many cheap edits — each produces a minimal LSP-style TextEdit and an O(members)
// offset fixup, NEVER re-parsing or re-stringifying the whole document.
//
// Compact JSON is single-line, so character offset == utf-16 offset (no line table).
// For pretty-printed docs a line-start table + binary search would be needed (noted).

import { scanInto, SLOT, F } from './scanner.mjs';
import { applyEditsBytes } from './rmw.mjs';

const enc = new TextEncoder();
const dec = new TextDecoder();

// ---- Model C: open once ----
export function openC(bytes) {
  const table = new Int32Array(64 * SLOT);
  const count = scanInto(bytes, table);
  return { table, count, bytes };
}

// Emit-only edit (VS Code owns the text): build a minimal TextEdit, fixup u16 offsets. O(members).
// Returns the {range:{start,end}, newText} the extension would hand to VS Code.
export function editEmit(state, slot, newText) {
  const t = state.table, base = slot * SLOT;
  const vs = t[base + F.VAL_US], ve = t[base + F.VAL_UE];
  const edit = { range: { start: vs, end: ve }, newText };
  const delta = newText.length - (ve - vs);
  if (delta !== 0) {
    t[base + F.VAL_UE] = ve + delta;
    for (let s = 0; s < state.count; s++) {
      const b = s * SLOT;
      if (t[b + F.VAL_US] > vs) {
        t[b + F.NAME_US] += delta; t[b + F.NAME_UE] += delta;
        t[b + F.VAL_US] += delta; t[b + F.VAL_UE] += delta;
      }
    }
  }
  return edit;
}

// Splice edit (extension must hold the updated bytes): produce new buffer + fixup byte offsets.
// O(n) memcpy for the buffer, but NO object graph and NO whole-doc stringify.
const _edit1 = [{ offset: 0, length: 0, content: null }];
export function editSplice(state, slot, newBytes) {
  const t = state.table, base = slot * SLOT;
  const vbs = t[base + F.VAL_BS], vbe = t[base + F.VAL_BE];
  _edit1[0].offset = vbs; _edit1[0].length = vbe - vbs; _edit1[0].content = newBytes;
  state.bytes = applyEditsBytes(state.bytes, _edit1);
  const delta = newBytes.length - (vbe - vbs);
  if (delta !== 0) {
    t[base + F.VAL_BE] = vbe + delta;
    for (let s = 0; s < state.count; s++) {
      const b = s * SLOT;
      if (t[b + F.VAL_BS] > vbs) {
        t[b + F.NAME_BS] += delta; t[b + F.NAME_BE] += delta;
        t[b + F.VAL_BS] += delta; t[b + F.VAL_BE] += delta;
      }
    }
  }
  return state.bytes;
}

// ---- native: open once (parse), then mutate + re-stringify per edit ----
// (JSON.parse discards source positions, so to reflect an edit in the document text
//  native must re-stringify the whole object — it cannot emit a minimal targeted edit.)
export function openNative(json) { return { obj: JSON.parse(json) }; }
export function editNative(state, key, val) { state.obj[key] = val; return JSON.stringify(state.obj); }

export { enc, dec };
