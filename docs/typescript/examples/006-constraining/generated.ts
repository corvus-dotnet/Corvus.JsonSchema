// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * SmallBatch
 */
export interface SmallBatch {
  readonly size: unknown;
}

function patchSmallBatch(source: Uint8Array, changes: Partial<SmallBatch>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["size"] !== undefined) { targets.push({ name: enc.encode("size"), content: enc.encode(JSON.stringify(changes["size"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildSmallBatch(props: SmallBatch): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalSmallBatch(props: SmallBatch): Uint8Array {
  return canonicalize(props);
}

function applyToSmallBatch(source: Uint8Array, member: Batch): Uint8Array {
  return patchSmallBatch(source, member as Partial<SmallBatch>);
}

function produceSmallBatch(source: Uint8Array, recipe: (draft: Draft<SmallBatch>) => void): Uint8Array {
  return produce<SmallBatch>(source, recipe);
}

function evaluateSmallBatch(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/006-constraining/small-batch.json#/type"); ok = false; }
  { const t = fresh(); if (!evaluateBatch(value, t, il, (r === null ? kl : kl + "/allOf/0/$ref"), r)) { if (r === null) return false; ok = false; } ev.mergeProps(t); ev.mergeItems(t); }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "size") { if (!evaluateSize2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/size"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "SmallBatch", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/006-constraining/small-batch.json#/title"); }
  return ok;
}

/**
 * Batch
 */
export interface Batch {
  readonly size: number;
}

function patchBatch(source: Uint8Array, changes: Partial<Batch>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["size"] !== undefined) { targets.push({ name: enc.encode("size"), content: enc.encode(JSON.stringify(changes["size"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildBatch(props: Batch): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalBatch(props: Batch): Uint8Array {
  return canonicalize(props);
}

function produceBatch(source: Uint8Array, recipe: (draft: Draft<Batch>) => void): Uint8Array {
  return produce<Batch>(source, recipe);
}

function evaluateBatch(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/006-constraining/small-batch.json#/$defs/batch/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "size")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/006-constraining/small-batch.json#/$defs/batch/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "size") { if (!evaluateSize(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/size"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Batch", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/006-constraining/small-batch.json#/$defs/batch/title"); }
  return ok;
}

function evaluateSize(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/006-constraining/small-batch.json#/$defs/batch/properties/size/type"); ok = false; }
  if (__isNum(value) && __cmp(String(value), "1") < 0) { if (r === null) return false; r.fail(kl + "/minimum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/006-constraining/small-batch.json#/$defs/batch/properties/size/minimum"); ok = false; }
  return ok;
}

function evaluateSize2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (__isNum(value) && __cmp(String(value), "100") > 0) { if (r === null) return false; r.fail(kl + "/maximum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/006-constraining/small-batch.json#/properties/size/maximum"); ok = false; }
  return ok;
}


export const SmallBatch = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSmallBatch(v, fresh(), "", "", results ?? null),
  build: buildSmallBatch,
  buildCanonical: buildCanonicalSmallBatch,
  patch: patchSmallBatch,
  produce: produceSmallBatch,
};
export const Batch = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateBatch(v, fresh(), "", "", results ?? null),
  build: buildBatch,
  buildCanonical: buildCanonicalBatch,
  patch: patchBatch,
  produce: produceBatch,
};
export const Size = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSize(v, fresh(), "", "", results ?? null),
};
export const Size2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSize2(v, fresh(), "", "", results ?? null),
};

export default SmallBatch;
