// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse };

/**
 * Person
 */
export interface Person {
  readonly birthDate?: BirthDate;
  readonly familyName: string;
  readonly givenName: string;
  readonly height?: number;
  readonly otherNames?: string;
}

function patchPerson(source: Uint8Array, changes: Partial<Person>, removals?: ReadonlyArray<"birthDate" | "height" | "otherNames">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["birthDate"] !== undefined) { targets.push({ name: enc.encode("birthDate"), content: enc.encode(JSON.stringify(changes["birthDate"])), vbs: -1, vbe: -1 }); }
  if (changes["familyName"] !== undefined) { targets.push({ name: enc.encode("familyName"), content: enc.encode(JSON.stringify(changes["familyName"])), vbs: -1, vbe: -1 }); }
  if (changes["givenName"] !== undefined) { targets.push({ name: enc.encode("givenName"), content: enc.encode(JSON.stringify(changes["givenName"])), vbs: -1, vbe: -1 }); }
  if (changes["height"] !== undefined) { targets.push({ name: enc.encode("height"), content: enc.encode(JSON.stringify(changes["height"])), vbs: -1, vbe: -1 }); }
  if (changes["otherNames"] !== undefined) { targets.push({ name: enc.encode("otherNames"), content: enc.encode(JSON.stringify(changes["otherNames"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildPerson(props: Person): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalPerson(props: Person): Uint8Array {
  return canonicalize(props);
}

function producePerson(source: Uint8Array, recipe: (draft: Draft<Person>) => void): Uint8Array {
  return produce<Person>(source, recipe);
}

function evaluatePerson(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "familyName")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "givenName")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "birthDate") { if (!evaluateBirthDate(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/birthDate"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "familyName") { if (!evaluateFamilyName(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/familyName"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "givenName") { if (!evaluateGivenName(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/givenName"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "height") { if (!evaluateHeight(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/height"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "otherNames") { if (!evaluateOtherNames(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/otherNames"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Person", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/title"); }
  return ok;
}

export type BirthDate = Brand<string, "date">;
function fromBirthDate(value: string): BirthDate { if (!__fmt("date", value)) { throw new FormatError("date"); } return value as BirthDate; }
function birthDateToTemporal(value: BirthDate): Temporal.PlainDate { return toPlainDate(value); }

function evaluateBirthDate(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/properties/birthDate/type"); ok = false; }
  if (typeof value === "string" && !__fmt("date", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/properties/birthDate/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "date", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/properties/birthDate/format"); }
  return ok;
}

function evaluateFamilyName(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/properties/familyName/type"); ok = false; }
  return ok;
}

function evaluateGivenName(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/properties/givenName/type"); ok = false; }
  return ok;
}

function evaluateHeight(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/properties/height/type"); ok = false; }
  return ok;
}

function evaluateOtherNames(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/001-data-object/person.json#/properties/otherNames/type"); ok = false; }
  return ok;
}


export const Person = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePerson(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Person => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Person,
  build: buildPerson,
  buildCanonical: buildCanonicalPerson,
  patch: patchPerson,
  produce: producePerson,
};
export const BirthDate = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateBirthDate(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): BirthDate => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as BirthDate,
  from: fromBirthDate,
  toTemporal: birthDateToTemporal,
};
export const FamilyName = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateFamilyName(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const GivenName = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateGivenName(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Height = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateHeight(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const OtherNames = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateOtherNames(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};

export default Person;
