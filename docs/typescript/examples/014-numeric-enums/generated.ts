// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Response {
  readonly status: Status;
}

export function patchResponse(source: Uint8Array, changes: Partial<Response>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["status"] !== undefined) { targets.push({ name: enc.encode("status"), content: enc.encode(JSON.stringify(changes["status"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildResponse(props: Response): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceResponse(source: Uint8Array, recipe: (draft: Draft<Response>) => void): Uint8Array {
  return produce<Response>(source, recipe);
}

export function evaluateResponse(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "status")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "status") { if (!evaluateStatus(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export type Status = 200 | 404 | 500;

export function evaluateStatus(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = [200, 404, 500]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateResponse(v, fresh());
export default evaluateRoot;
