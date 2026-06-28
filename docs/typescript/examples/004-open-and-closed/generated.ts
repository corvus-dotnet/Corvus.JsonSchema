// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * StrictPoint
 */
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

export function buildCanonicalStrictPoint(props: StrictPoint): Uint8Array {
  return canonicalize(props);
}

export function produceStrictPoint(source: Uint8Array, recipe: (draft: Draft<StrictPoint>) => void): Uint8Array {
  return produce<StrictPoint>(source, recipe);
}

export function evaluateStrictPoint(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/004-open-and-closed/point.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "x")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/004-open-and-closed/point.json#/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "y")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/004-open-and-closed/point.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "x") { if (!evaluateX(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/x"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "y") { if (!evaluateY(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/y"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (!ev.hasProp(i) && !evaluateJsonNotAny(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/unevaluatedProperties"), r)) { if (r === null) return false; ok = false; }
      ev.markProp(i);
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "StrictPoint", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/004-open-and-closed/point.json#/title"); }
  return ok;
}

export function evaluateX(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/004-open-and-closed/point.json#/properties/x/type"); ok = false; }
  return ok;
}

export function evaluateY(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/004-open-and-closed/point.json#/properties/y/type"); ok = false; }
  return ok;
}

export function evaluateJsonNotAny(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  if (r !== null) { r.fail(kl, il, "corvus:/JsonNotAny#"); } return false;
}


export const evaluateRoot = (v: unknown, results?: Results): boolean => evaluateStrictPoint(v, fresh(), "", "", results ?? null);
export default evaluateRoot;
