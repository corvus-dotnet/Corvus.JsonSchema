// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Point3D {
  readonly coord: readonly [number, number, number];
}

export function patchPoint3D(source: Uint8Array, changes: Partial<Point3D>, arrays?: Point3DArrayOps): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["coord"] !== undefined) { targets.push({ name: enc.encode("coord"), content: enc.encode(JSON.stringify(changes["coord"])), vbs: -1, vbe: -1 }); }
  const arrayEdits: RmwArrayEdit[] = [];
  if (arrays !== undefined && arrays["coord"] !== undefined) { arrayEdits.push({ name: enc.encode("coord"), ops: arrays["coord"]! as RmwArrayOps, prefixLen: 0 }); }
  if (arrayEdits.length > 0) {
    const removeNames: Uint8Array[] = [];
    return rmwProduceFull(source, targets, removeNames, arrayEdits);
  }
  return rmwUpsert(source, targets);
}

export interface Point3DArrayOps {
  readonly coord?: { readonly set?: { readonly 0?: number; readonly 1?: number; readonly 2?: number } };
}

export function buildPoint3D(props: Point3D): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function producePoint3D(source: Uint8Array, recipe: (draft: Draft<Point3D>) => void): Uint8Array {
  return produce<Point3D>(source, recipe);
}

export function evaluatePoint3D(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "coord")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "coord") { if (!evaluateCoord(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateCoord(value: unknown, ev: Ev): boolean {
  if (!(Array.isArray(value))) { return false; }
  if (Array.isArray(value) && value.length < 3) { return false; }
  if (Array.isArray(value)) {
    if (value.length > 0) { if (!evaluatePrefixItems(value[0], NOEV)) { return false; } ev.markItem(0); }
    if (value.length > 1) { if (!evaluatePrefixItems2(value[1], NOEV)) { return false; } ev.markItem(1); }
    if (value.length > 2) { if (!evaluatePrefixItems3(value[2], NOEV)) { return false; } ev.markItem(2); }
  }
  if (Array.isArray(value)) { for (let i = 3; i < value.length; i++) { if (!evaluateJsonNotAny(value[i], NOEV)) { return false; } ev.markItem(i); } }
  return true;
}

export function evaluatePrefixItems(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export function evaluatePrefixItems2(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export function evaluatePrefixItems3(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export function evaluateJsonNotAny(value: unknown, ev: Ev): boolean {
  return false;
}


export const evaluateRoot = (v: unknown): boolean => evaluatePoint3D(v, fresh());
export default evaluateRoot;
