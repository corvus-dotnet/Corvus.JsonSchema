// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError, type JsonPatch, type JsonPatchOp, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError };
export type { JsonPatch, JsonPatchOp };

/**
 * Task
 */
export interface Task {
  readonly priority?: Priority;
  readonly status: Status;
}

function patchTask(source: Uint8Array, changes: Partial<Task>, removals?: ReadonlyArray<"priority">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["priority"] !== undefined) { targets.push({ name: enc.encode("priority"), content: enc.encode(JSON.stringify(changes["priority"])), vbs: -1, vbe: -1 }); }
  if (changes["status"] !== undefined) { targets.push({ name: enc.encode("status"), content: enc.encode(JSON.stringify(changes["status"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildTask(props: Task): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalTask(props: Task): Uint8Array {
  return canonicalize(props);
}

function produceTask(source: Uint8Array, recipe: (draft: Draft<Task>) => void): Uint8Array {
  return produce<Task>(source, recipe);
}

function evaluateTask(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/013-string-enums/task.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "status")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/013-string-enums/task.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "priority") { if (!evaluatePriority(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/priority"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "status") { if (!evaluateStatus(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/status"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Task", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/013-string-enums/task.json#/title"); }
  return ok;
}

export type Priority = "low" | "medium" | "high";

function evaluatePriority(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["low", "medium", "high"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/enum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/013-string-enums/task.json#/properties/priority/enum"); ok = false; } }
  return ok;
}

export type Status = "todo" | "in_progress" | "done";

function evaluateStatus(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["todo", "in_progress", "done"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/enum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/013-string-enums/task.json#/properties/status/enum"); ok = false; } }
  return ok;
}


export const Task = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateTask(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Task => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Task,
  build: buildTask,
  buildCanonical: buildCanonicalTask,
  patch: patchTask,
  produce: produceTask,
  applyPatch: (doc: Uint8Array | Task, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Task, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Task, target: Uint8Array | Task): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Task, target: Uint8Array | Task): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Priority = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePriority(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Priority => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Priority,
};
export const Status = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateStatus(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Status => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Status,
};

export default Task;
