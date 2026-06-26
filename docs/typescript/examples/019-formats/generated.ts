// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Account {
  readonly created?: Created;
  readonly id: Id;
  readonly website?: Website;
}

export function patchAccount(source: Uint8Array, changes: Partial<Account>, removals?: ReadonlyArray<"created" | "website">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["created"] !== undefined) { targets.push({ name: enc.encode("created"), content: enc.encode(JSON.stringify(changes["created"])), vbs: -1, vbe: -1 }); }
  if (changes["id"] !== undefined) { targets.push({ name: enc.encode("id"), content: enc.encode(JSON.stringify(changes["id"])), vbs: -1, vbe: -1 }); }
  if (changes["website"] !== undefined) { targets.push({ name: enc.encode("website"), content: enc.encode(JSON.stringify(changes["website"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildAccount(props: Account): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceAccount(source: Uint8Array, recipe: (draft: Draft<Account>) => void): Uint8Array {
  return produce<Account>(source, recipe);
}

export function evaluateAccount(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "id")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "created") { if (!evaluateCreated(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "id") { if (!evaluateId(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "website") { if (!evaluateWebsite(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export type Created = Brand<string, "date-time">;
export function asCreated(value: string): Created { if (!__fmt("date-time", value)) { throw new FormatError("date-time"); } return value as Created; }

export function evaluateCreated(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export type Id = Brand<string, "uuid">;
export function asId(value: string): Id { if (!__fmt("uuid", value)) { throw new FormatError("uuid"); } return value as Id; }

export function evaluateId(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export type Website = Brand<string, "uri">;
export function asWebsite(value: string): Website { if (!__fmt("uri", value)) { throw new FormatError("uri"); } return value as Website; }

export function evaluateWebsite(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateAccount(v, fresh());
export default evaluateRoot;
