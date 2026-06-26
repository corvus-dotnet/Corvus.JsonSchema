// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Task {
  readonly priority?: Priority;
  readonly status: Status;
}

export function patchTask(source: Uint8Array, changes: Partial<Task>, removals?: ReadonlyArray<"priority">): Uint8Array {
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

export function buildTask(props: Task): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceTask(source: Uint8Array, recipe: (draft: Draft<Task>) => void): Uint8Array {
  return produce<Task>(source, recipe);
}

export function evaluateTask(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "status")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "priority") { if (!evaluatePriority(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "status") { if (!evaluateStatus(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export type Priority = "low" | "medium" | "high";

export function evaluatePriority(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["low", "medium", "high"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}

export type Status = "todo" | "in_progress" | "done";

export function evaluateStatus(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["todo", "in_progress", "done"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateTask(v, fresh());
export default evaluateRoot;
