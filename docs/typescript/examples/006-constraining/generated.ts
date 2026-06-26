// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface SmallBatch {
  readonly size: unknown;
}

export function patchSmallBatch(source: Uint8Array, changes: Partial<SmallBatch>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["size"] !== undefined) { targets.push({ name: enc.encode("size"), content: enc.encode(JSON.stringify(changes["size"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildSmallBatch(props: SmallBatch): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function applyToSmallBatch(source: Uint8Array, member: Batch): Uint8Array {
  return patchSmallBatch(source, member as Partial<SmallBatch>);
}

export function produceSmallBatch(source: Uint8Array, recipe: (draft: Draft<SmallBatch>) => void): Uint8Array {
  return produce<SmallBatch>(source, recipe);
}

export function evaluateSmallBatch(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  { const t = fresh(); if (!evaluateBatch(value, t)) { return false; } ev.mergeProps(t); ev.mergeItems(t); }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "size") { if (!evaluateSize2(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export interface Batch {
  readonly size: number;
}

export function patchBatch(source: Uint8Array, changes: Partial<Batch>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["size"] !== undefined) { targets.push({ name: enc.encode("size"), content: enc.encode(JSON.stringify(changes["size"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildBatch(props: Batch): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceBatch(source: Uint8Array, recipe: (draft: Draft<Batch>) => void): Uint8Array {
  return produce<Batch>(source, recipe);
}

export function evaluateBatch(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "size")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "size") { if (!evaluateSize(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateSize(value: unknown, ev: Ev): boolean {
  if (!((__isNum(value) && __isInt(String(value))))) { return false; }
  if (__isNum(value) && __cmp(String(value), "1") < 0) { return false; }
  return true;
}

export function evaluateSize2(value: unknown, ev: Ev): boolean {
  if (__isNum(value) && __cmp(String(value), "100") > 0) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateSmallBatch(v, fresh());
export default evaluateRoot;
