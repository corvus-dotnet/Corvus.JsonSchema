// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * Point3D
 */
export interface Point3D {
  readonly coord: readonly [number, number, number];
}

function patchPoint3D(source: Uint8Array, changes: Partial<Point3D>, arrays?: Point3DArrayOps): Uint8Array {
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

function buildPoint3D(props: Point3D): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalPoint3D(props: Point3D): Uint8Array {
  return canonicalize(props);
}

function producePoint3D(source: Uint8Array, recipe: (draft: Draft<Point3D>) => void): Uint8Array {
  return produce<Point3D>(source, recipe);
}

function evaluatePoint3D(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/009-tuples/point3d.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "coord")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/009-tuples/point3d.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "coord") { if (!evaluateCoord(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/coord"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Point3D", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/009-tuples/point3d.json#/title"); }
  return ok;
}

function evaluateCoord(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/009-tuples/point3d.json#/properties/coord/type"); ok = false; }
  if (Array.isArray(value) && value.length < 3) { if (r === null) return false; r.fail(kl + "/minItems", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/009-tuples/point3d.json#/properties/coord/minItems"); ok = false; }
  if (Array.isArray(value)) {
    if (value.length > 0) { if (!evaluatePrefixItems(value[0], NOEV, (r === null ? il : il + "/" + 0), (r === null ? kl : kl + "/prefixItems/0"), r)) { if (r === null) return false; ok = false; } ev.markItem(0); }
    if (value.length > 1) { if (!evaluatePrefixItems2(value[1], NOEV, (r === null ? il : il + "/" + 1), (r === null ? kl : kl + "/prefixItems/1"), r)) { if (r === null) return false; ok = false; } ev.markItem(1); }
    if (value.length > 2) { if (!evaluatePrefixItems3(value[2], NOEV, (r === null ? il : il + "/" + 2), (r === null ? kl : kl + "/prefixItems/2"), r)) { if (r === null) return false; ok = false; } ev.markItem(2); }
  }
  if (Array.isArray(value)) { for (let i = 3; i < value.length; i++) { if (!evaluateJsonNotAny(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluatePrefixItems(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/009-tuples/point3d.json#/properties/coord/prefixItems/0/type"); ok = false; }
  return ok;
}

function evaluatePrefixItems2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/009-tuples/point3d.json#/properties/coord/prefixItems/1/type"); ok = false; }
  return ok;
}

function evaluatePrefixItems3(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/009-tuples/point3d.json#/properties/coord/prefixItems/2/type"); ok = false; }
  return ok;
}

function evaluateJsonNotAny(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  if (r !== null) { r.fail(kl, il, "corvus:/JsonNotAny#"); } return false;
}


export const Point3D = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePoint3D(v, fresh(), "", "", results ?? null),
  build: buildPoint3D,
  buildCanonical: buildCanonicalPoint3D,
  patch: patchPoint3D,
  produce: producePoint3D,
};
export const Coord = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCoord(v, fresh(), "", "", results ?? null),
};
export const PrefixItems = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePrefixItems(v, fresh(), "", "", results ?? null),
};
export const PrefixItems2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePrefixItems2(v, fresh(), "", "", results ?? null),
};
export const PrefixItems3 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePrefixItems3(v, fresh(), "", "", results ?? null),
};
export const JsonNotAny = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateJsonNotAny(v, fresh(), "", "", results ?? null),
};

export default Point3D;
