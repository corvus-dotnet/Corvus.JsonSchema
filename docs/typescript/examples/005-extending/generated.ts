// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Employee {
  readonly department?: string;
  readonly email?: Email;
  readonly employeeId: string;
  readonly name: string;
}

export function patchEmployee(source: Uint8Array, changes: Partial<Employee>, removals?: ReadonlyArray<"department" | "email">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["department"] !== undefined) { targets.push({ name: enc.encode("department"), content: enc.encode(JSON.stringify(changes["department"])), vbs: -1, vbe: -1 }); }
  if (changes["email"] !== undefined) { targets.push({ name: enc.encode("email"), content: enc.encode(JSON.stringify(changes["email"])), vbs: -1, vbe: -1 }); }
  if (changes["employeeId"] !== undefined) { targets.push({ name: enc.encode("employeeId"), content: enc.encode(JSON.stringify(changes["employeeId"])), vbs: -1, vbe: -1 }); }
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildEmployee(props: Employee): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function applyToEmployee(source: Uint8Array, member: Person): Uint8Array {
  return patchEmployee(source, member as Partial<Employee>);
}

export function produceEmployee(source: Uint8Array, recipe: (draft: Draft<Employee>) => void): Uint8Array {
  return produce<Employee>(source, recipe);
}

export function evaluateEmployee(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  { const t = fresh(); if (!evaluatePerson(value, t)) { return false; } ev.mergeProps(t); ev.mergeItems(t); }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "employeeId")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "department") { if (!evaluateDepartment(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "employeeId") { if (!evaluateEmployeeId(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export interface Person {
  readonly email?: Email;
  readonly name: string;
}

export function patchPerson(source: Uint8Array, changes: Partial<Person>, removals?: ReadonlyArray<"email">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["email"] !== undefined) { targets.push({ name: enc.encode("email"), content: enc.encode(JSON.stringify(changes["email"])), vbs: -1, vbe: -1 }); }
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildPerson(props: Person): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function producePerson(source: Uint8Array, recipe: (draft: Draft<Person>) => void): Uint8Array {
  return produce<Person>(source, recipe);
}

export function evaluatePerson(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "name")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "email") { if (!evaluateEmail(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "name") { if (!evaluateName(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export type Email = Brand<string, "email">;
export function asEmail(value: string): Email { if (!__fmt("email", value)) { throw new FormatError("email"); } return value as Email; }

export function evaluateEmail(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  if (typeof value === "string" && !__fmt("email", value)) { return false; }
  return true;
}

export function evaluateName(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export function evaluateDepartment(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export function evaluateEmployeeId(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateEmployee(v, fresh());
export default evaluateRoot;
