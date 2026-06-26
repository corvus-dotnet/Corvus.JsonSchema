// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Grid {
  readonly cells: readonly (readonly number[])[];
}

export function patchGrid(source: Uint8Array, changes: Partial<Grid>, arrays?: GridArrayOps): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["cells"] !== undefined) { targets.push({ name: enc.encode("cells"), content: enc.encode(JSON.stringify(changes["cells"])), vbs: -1, vbe: -1 }); }
  const arrayEdits: RmwArrayEdit[] = [];
  if (arrays !== undefined && arrays["cells"] !== undefined) { arrayEdits.push({ name: enc.encode("cells"), ops: arrays["cells"]! as RmwArrayOps, prefixLen: 0 }); }
  if (arrayEdits.length > 0) {
    const removeNames: Uint8Array[] = [];
    return rmwProduceFull(source, targets, removeNames, arrayEdits);
  }
  return rmwUpsert(source, targets);
}

export interface GridArrayOps {
  readonly cells?: ListOps<readonly number[]>;
}

export function buildGrid(props: Grid): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceGrid(source: Uint8Array, recipe: (draft: Draft<Grid>) => void): Uint8Array {
  return produce<Grid>(source, recipe);
}

export function evaluateGrid(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "cells")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "cells") { if (!evaluateCells(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateCells(value: unknown, ev: Ev): boolean {
  if (!(Array.isArray(value))) { return false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems(value[i], NOEV)) { return false; } ev.markItem(i); } }
  return true;
}

export function evaluateItems(value: unknown, ev: Ev): boolean {
  if (!(Array.isArray(value))) { return false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems2(value[i], NOEV)) { return false; } ev.markItem(i); } }
  return true;
}

export function evaluateItems2(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateGrid(v, fresh());
export default evaluateRoot;
