// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Registration {
  readonly age: number;
  readonly email: Email;
  readonly score?: number;
  readonly username: string;
}

export function patchRegistration(source: Uint8Array, changes: Partial<Registration>, removals?: ReadonlyArray<"score">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["age"] !== undefined) { targets.push({ name: enc.encode("age"), content: enc.encode(JSON.stringify(changes["age"])), vbs: -1, vbe: -1 }); }
  if (changes["email"] !== undefined) { targets.push({ name: enc.encode("email"), content: enc.encode(JSON.stringify(changes["email"])), vbs: -1, vbe: -1 }); }
  if (changes["score"] !== undefined) { targets.push({ name: enc.encode("score"), content: enc.encode(JSON.stringify(changes["score"])), vbs: -1, vbe: -1 }); }
  if (changes["username"] !== undefined) { targets.push({ name: enc.encode("username"), content: enc.encode(JSON.stringify(changes["username"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildRegistration(props: Registration): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceRegistration(source: Uint8Array, recipe: (draft: Draft<Registration>) => void): Uint8Array {
  return produce<Registration>(source, recipe);
}

export function evaluateRegistration(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "username")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "age")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "email")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "age") { if (!evaluateAge(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "email") { if (!evaluateEmail(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "score") { if (!evaluateScore(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "username") { if (!evaluateUsername(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateAge(value: unknown, ev: Ev): boolean {
  if (!((__isNum(value) && __isInt(String(value))))) { return false; }
  if (__isNum(value) && __cmp(String(value), "120") > 0) { return false; }
  if (__isNum(value) && __cmp(String(value), "18") < 0) { return false; }
  return true;
}

export type Email = Brand<string, "email">;
export function asEmail(value: string): Email { if (!__fmt("email", value)) { throw new FormatError("email"); } return value as Email; }

export function evaluateEmail(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  if (typeof value === "string" && !__fmt("email", value)) { return false; }
  return true;
}

export function evaluateScore(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  if (__isNum(value) && !__multipleOf(String(value), "0.5")) { return false; }
  if (__isNum(value) && __cmp(String(value), "0") <= 0) { return false; }
  return true;
}

export function evaluateUsername(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  if (typeof value === "string" && [...value].length > 20) { return false; }
  if (typeof value === "string" && [...value].length < 3) { return false; }
  if (typeof value === "string" && !__re("^[a-z][a-z0-9_]*$").test(value)) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateRegistration(v, fresh());
export default evaluateRoot;
