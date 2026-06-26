// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Widget {
  readonly createdAt?: CreatedAt;
  readonly id?: string;
  readonly name: string;
}

export function patchWidget(source: Uint8Array, changes: Partial<Widget>, removals?: ReadonlyArray<"createdAt" | "id">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["createdAt"] !== undefined) { targets.push({ name: enc.encode("createdAt"), content: enc.encode(JSON.stringify(changes["createdAt"])), vbs: -1, vbe: -1 }); }
  if (changes["id"] !== undefined) { targets.push({ name: enc.encode("id"), content: enc.encode(JSON.stringify(changes["id"])), vbs: -1, vbe: -1 }); }
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildWidget(props: Widget): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function applyToWidget(source: Uint8Array, member: Named | Timestamped): Uint8Array {
  return patchWidget(source, member as Partial<Widget>);
}

export function produceWidget(source: Uint8Array, recipe: (draft: Draft<Widget>) => void): Uint8Array {
  return produce<Widget>(source, recipe);
}

export function evaluateWidget(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  { const t = fresh(); if (!evaluateNamed(value, t)) { return false; } ev.mergeProps(t); ev.mergeItems(t); }
  { const t = fresh(); if (!evaluateTimestamped(value, t)) { return false; } ev.mergeProps(t); ev.mergeItems(t); }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "id") { if (!evaluateId(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export interface Named {
  readonly name: string;
}

export function patchNamed(source: Uint8Array, changes: Partial<Named>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildNamed(props: Named): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceNamed(source: Uint8Array, recipe: (draft: Draft<Named>) => void): Uint8Array {
  return produce<Named>(source, recipe);
}

export function evaluateNamed(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "name")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "name") { if (!evaluateName(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateName(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export interface Timestamped {
  readonly createdAt?: CreatedAt;
}

export function patchTimestamped(source: Uint8Array, changes: Partial<Timestamped>, removals?: ReadonlyArray<"createdAt">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["createdAt"] !== undefined) { targets.push({ name: enc.encode("createdAt"), content: enc.encode(JSON.stringify(changes["createdAt"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildTimestamped(props: Timestamped): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceTimestamped(source: Uint8Array, recipe: (draft: Draft<Timestamped>) => void): Uint8Array {
  return produce<Timestamped>(source, recipe);
}

export function evaluateTimestamped(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "createdAt") { if (!evaluateCreatedAt(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export type CreatedAt = Brand<string, "date-time">;
export function asCreatedAt(value: string): CreatedAt { if (!__fmt("date-time", value)) { throw new FormatError("date-time"); } return value as CreatedAt; }

export function evaluateCreatedAt(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export function evaluateId(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateWidget(v, fresh());
export default evaluateRoot;
