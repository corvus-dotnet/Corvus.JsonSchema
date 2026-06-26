// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface StrictPoint {
  readonly x: number;
  readonly y: number;
}

export function patchStrictPoint(source: Uint8Array, changes: Partial<StrictPoint>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["x"] !== undefined) { targets.push({ name: enc.encode("x"), content: enc.encode(JSON.stringify(changes["x"])), vbs: -1, vbe: -1 }); }
  if (changes["y"] !== undefined) { targets.push({ name: enc.encode("y"), content: enc.encode(JSON.stringify(changes["y"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildStrictPoint(props: StrictPoint): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceStrictPoint(source: Uint8Array, recipe: (draft: Draft<StrictPoint>) => void): Uint8Array {
  return produce<StrictPoint>(source, recipe);
}

export function evaluateStrictPoint(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "x")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "y")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "x") { if (!evaluateX(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "y") { if (!evaluateY(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (!ev.hasProp(i) && !evaluateJsonNotAny(o[k], NOEV)) { return false; }
      ev.markProp(i);
    }
  }
  return true;
}

export function evaluateX(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export function evaluateY(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export function evaluateJsonNotAny(value: unknown, ev: Ev): boolean {
  return false;
}


export const evaluateRoot = (v: unknown): boolean => evaluateStrictPoint(v, fresh());
export default evaluateRoot;
