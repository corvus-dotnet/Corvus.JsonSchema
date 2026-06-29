// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse };

/**
 * Account
 */
export interface Account {
  readonly created?: Created;
  readonly id: Id;
  readonly website?: Website;
}

function patchAccount(source: Uint8Array, changes: Partial<Account>, removals?: ReadonlyArray<"created" | "website">): Uint8Array {
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

function buildAccount(props: Account): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalAccount(props: Account): Uint8Array {
  return canonicalize(props);
}

function produceAccount(source: Uint8Array, recipe: (draft: Draft<Account>) => void): Uint8Array {
  return produce<Account>(source, recipe);
}

function evaluateAccount(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "id")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "created") { if (!evaluateCreated(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/created"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "id") { if (!evaluateId(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/id"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "website") { if (!evaluateWebsite(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/website"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Account", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/title"); }
  return ok;
}

export type Created = Brand<string, "date-time">;
function fromCreated(value: string): Created { if (!__fmt("date-time", value)) { throw new FormatError("date-time"); } return value as Created; }
function createdToTemporal(value: Created): Temporal.Instant { return toInstant(value); }

function evaluateCreated(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/created/type"); ok = false; }
  if (typeof value === "string" && !__fmt("date-time", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/created/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "date-time", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/created/format"); }
  return ok;
}

export type Id = Brand<string, "uuid">;
function fromId(value: string): Id { if (!__fmt("uuid", value)) { throw new FormatError("uuid"); } return value as Id; }

function evaluateId(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/id/type"); ok = false; }
  if (typeof value === "string" && !__fmt("uuid", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/id/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "uuid", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/id/format"); }
  return ok;
}

export type Website = Brand<string, "uri">;
function fromWebsite(value: string): Website { if (!__fmt("uri", value)) { throw new FormatError("uri"); } return value as Website; }

function evaluateWebsite(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/website/type"); ok = false; }
  if (typeof value === "string" && !__fmt("uri", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/website/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "uri", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/019-formats/account.json#/properties/website/format"); }
  return ok;
}


export const Account = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateAccount(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Account => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Account,
  build: buildAccount,
  buildCanonical: buildCanonicalAccount,
  patch: patchAccount,
  produce: produceAccount,
};
export const Created = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCreated(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Created => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Created,
  from: fromCreated,
  toTemporal: createdToTemporal,
};
export const Id = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateId(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Id => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Id,
  from: fromId,
};
export const Website = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateWebsite(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Website => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Website,
  from: fromWebsite,
};

export default Account;
