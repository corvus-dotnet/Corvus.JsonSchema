// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * Employee
 */
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

export function buildCanonicalEmployee(props: Employee): Uint8Array {
  return canonicalize(props);
}

export function applyToEmployee(source: Uint8Array, member: Person): Uint8Array {
  return patchEmployee(source, member as Partial<Employee>);
}

export function produceEmployee(source: Uint8Array, recipe: (draft: Draft<Employee>) => void): Uint8Array {
  return produce<Employee>(source, recipe);
}

export function evaluateEmployee(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/type"); ok = false; }
  { const t = fresh(); if (!evaluatePerson(value, t, il, (r === null ? kl : kl + "/allOf/0/$ref"), r)) { if (r === null) return false; ok = false; } ev.mergeProps(t); ev.mergeItems(t); }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "employeeId")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "department") { if (!evaluateDepartment(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/department"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "employeeId") { if (!evaluateEmployeeId(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/employeeId"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Employee", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/title"); }
  return ok;
}

/**
 * Person
 */
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

export function buildCanonicalPerson(props: Person): Uint8Array {
  return canonicalize(props);
}

export function producePerson(source: Uint8Array, recipe: (draft: Draft<Person>) => void): Uint8Array {
  return produce<Person>(source, recipe);
}

export function evaluatePerson(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/$defs/person/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "name")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/$defs/person/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "email") { if (!evaluateEmail(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/email"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "name") { if (!evaluateName(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/name"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Person", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/$defs/person/title"); }
  return ok;
}

export type Email = Brand<string, "email">;
export function asEmail(value: string): Email { if (!__fmt("email", value)) { throw new FormatError("email"); } return value as Email; }

export function evaluateEmail(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/$defs/person/properties/email/type"); ok = false; }
  if (typeof value === "string" && !__fmt("email", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/$defs/person/properties/email/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "email", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/$defs/person/properties/email/format"); }
  return ok;
}

export function evaluateName(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/$defs/person/properties/name/type"); ok = false; }
  return ok;
}

export function evaluateDepartment(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/properties/department/type"); ok = false; }
  return ok;
}

export function evaluateEmployeeId(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/005-extending/employee.json#/properties/employeeId/type"); ok = false; }
  return ok;
}


export const evaluateRoot = (v: unknown, results?: Results): boolean => evaluateEmployee(v, fresh(), "", "", results ?? null);
export default evaluateRoot;
