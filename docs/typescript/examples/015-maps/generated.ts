// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Scores {
  readonly [key: string]: number;
}

export function evaluateScores(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (!evaluateAdditionalProperties(o[k], NOEV)) { return false; } ev.markProp(i);
    }
  }
  return true;
}

export function evaluateAdditionalProperties(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  if (__isNum(value) && __cmp(String(value), "0") < 0) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateScores(v, fresh());
export default evaluateRoot;
