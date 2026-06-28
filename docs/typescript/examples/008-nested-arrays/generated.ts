// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * Grid
 */
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

export function buildCanonicalGrid(props: Grid): Uint8Array {
  return canonicalize(props);
}

export function produceGrid(source: Uint8Array, recipe: (draft: Draft<Grid>) => void): Uint8Array {
  return produce<Grid>(source, recipe);
}

export function evaluateGrid(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/008-nested-arrays/grid.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "cells")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/008-nested-arrays/grid.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "cells") { if (!evaluateCells(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/cells"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Grid", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/008-nested-arrays/grid.json#/title"); }
  return ok;
}

export function evaluateCells(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/008-nested-arrays/grid.json#/properties/cells/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

export function evaluateItems(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/008-nested-arrays/grid.json#/properties/cells/items/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems2(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

export function evaluateItems2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/008-nested-arrays/grid.json#/properties/cells/items/items/type"); ok = false; }
  return ok;
}


export const evaluateRoot = (v: unknown, results?: Results): boolean => evaluateGrid(v, fresh(), "", "", results ?? null);
export default evaluateRoot;
