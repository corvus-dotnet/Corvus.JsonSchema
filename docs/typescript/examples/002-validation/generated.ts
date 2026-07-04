// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError, type JsonPatch, type JsonPatchOp, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError };
export type { JsonPatch, JsonPatchOp };

/**
 * Registration
 */
export interface Registration {
  readonly age: number;
  readonly email: Email;
  readonly score?: number;
  readonly username: string;
}

function patchRegistration(source: Uint8Array, changes: Partial<Registration>, removals?: ReadonlyArray<"score">): Uint8Array {
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

function buildRegistration(props: Registration): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalRegistration(props: Registration): Uint8Array {
  return canonicalize(props);
}

function produceRegistration(source: Uint8Array, recipe: (draft: Draft<Registration>) => void): Uint8Array {
  return produce<Registration>(source, recipe);
}

function evaluateRegistration(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "username")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "age")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "email")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "age") { if (!evaluateAge(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/age"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "email") { if (!evaluateEmail(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/email"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "score") { if (!evaluateScore(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/score"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "username") { if (!evaluateUsername(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/username"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Registration", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/title"); }
  return ok;
}

function evaluateAge(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/age/type"); ok = false; }
  if (__isNum(value) && __cmp(String(value), "120") > 0) { if (r === null) return false; r.fail(kl + "/maximum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/age/maximum"); ok = false; }
  if (__isNum(value) && __cmp(String(value), "18") < 0) { if (r === null) return false; r.fail(kl + "/minimum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/age/minimum"); ok = false; }
  return ok;
}

export type Email = Brand<string, "email">;
function fromEmail(value: string): Email { if (!__fmt("email", value)) { throw new FormatError("email"); } return value as Email; }

function evaluateEmail(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/email/type"); ok = false; }
  if (typeof value === "string" && !__fmt("email", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/email/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "email", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/email/format"); }
  return ok;
}

function evaluateScore(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/score/type"); ok = false; }
  if (__isNum(value) && !__multipleOf(String(value), "0.5")) { if (r === null) return false; r.fail(kl + "/multipleOf", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/score/multipleOf"); ok = false; }
  if (__isNum(value) && __cmp(String(value), "0") <= 0) { if (r === null) return false; r.fail(kl + "/exclusiveMinimum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/score/exclusiveMinimum"); ok = false; }
  return ok;
}

function evaluateUsername(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/username/type"); ok = false; }
  if (typeof value === "string" && [...value].length > 20) { if (r === null) return false; r.fail(kl + "/maxLength", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/username/maxLength"); ok = false; }
  if (typeof value === "string" && [...value].length < 3) { if (r === null) return false; r.fail(kl + "/minLength", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/username/minLength"); ok = false; }
  if (typeof value === "string" && !__re("^[a-z][a-z0-9_]*$").test(value)) { if (r === null) return false; r.fail(kl + "/pattern", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/002-validation/registration.json#/properties/username/pattern"); ok = false; }
  return ok;
}


export const Registration = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateRegistration(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Registration => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Registration,
  build: buildRegistration,
  buildCanonical: buildCanonicalRegistration,
  patch: patchRegistration,
  produce: produceRegistration,
  applyPatch: (doc: Uint8Array | Registration, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Registration, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Registration, target: Uint8Array | Registration): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Registration, target: Uint8Array | Registration): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Age = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateAge(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Email = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateEmail(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Email => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Email,
  from: fromEmail,
};
export const Score = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateScore(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Username = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateUsername(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};

export default Registration;
