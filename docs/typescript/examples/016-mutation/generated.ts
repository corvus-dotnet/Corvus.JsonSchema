// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * Document
 */
export interface Document {
  /**
   * Owner
   */
  readonly owner?: Owner;
  readonly tags?: readonly string[];
  readonly title: string;
  readonly version?: number;
}

export function patchDocument(source: Uint8Array, changes: Partial<Document>, removals?: ReadonlyArray<"owner" | "tags" | "version">, arrays?: DocumentArrayOps): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["owner"] !== undefined) { targets.push({ name: enc.encode("owner"), content: enc.encode(JSON.stringify(changes["owner"])), vbs: -1, vbe: -1 }); }
  if (changes["tags"] !== undefined) { targets.push({ name: enc.encode("tags"), content: enc.encode(JSON.stringify(changes["tags"])), vbs: -1, vbe: -1 }); }
  if (changes["title"] !== undefined) { targets.push({ name: enc.encode("title"), content: enc.encode(JSON.stringify(changes["title"])), vbs: -1, vbe: -1 }); }
  if (changes["version"] !== undefined) { targets.push({ name: enc.encode("version"), content: enc.encode(JSON.stringify(changes["version"])), vbs: -1, vbe: -1 }); }
  const arrayEdits: RmwArrayEdit[] = [];
  if (arrays !== undefined && arrays["tags"] !== undefined) { arrayEdits.push({ name: enc.encode("tags"), ops: arrays["tags"]! as RmwArrayOps, prefixLen: 0 }); }
  if ((removals !== undefined && removals.length > 0) || arrayEdits.length > 0) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, arrayEdits);
  }
  return rmwUpsert(source, targets);
}

export interface DocumentArrayOps {
  readonly tags?: ListOps<string>;
}

export function buildDocument(props: Document): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function buildCanonicalDocument(props: Document): Uint8Array {
  return canonicalize(props);
}

export function produceDocument(source: Uint8Array, recipe: (draft: Draft<Document>) => void): Uint8Array {
  return produce<Document>(source, recipe);
}

export function evaluateDocument(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "title")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "owner") { if (!evaluateOwner(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/owner"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "tags") { if (!evaluateTags(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/tags"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "title") { if (!evaluateTitle(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/title"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "version") { if (!evaluateVersion(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/version"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Document", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/title"); }
  return ok;
}

/**
 * Owner
 */
export interface Owner {
  readonly email?: string;
  readonly name?: string;
}

export function patchOwner(source: Uint8Array, changes: Partial<Owner>, removals?: ReadonlyArray<"email" | "name">): Uint8Array {
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

export function buildOwner(props: Owner): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function buildCanonicalOwner(props: Owner): Uint8Array {
  return canonicalize(props);
}

export function produceOwner(source: Uint8Array, recipe: (draft: Draft<Owner>) => void): Uint8Array {
  return produce<Owner>(source, recipe);
}

export function evaluateOwner(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/properties/owner/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "email") { if (!evaluateEmail(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/email"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "name") { if (!evaluateName(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/name"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Owner", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/properties/owner/title"); }
  return ok;
}

export function evaluateEmail(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/properties/owner/properties/email/type"); ok = false; }
  return ok;
}

export function evaluateName(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/properties/owner/properties/name/type"); ok = false; }
  return ok;
}

export function evaluateTags(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/properties/tags/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

export function evaluateItems(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/properties/tags/items/type"); ok = false; }
  return ok;
}

export function evaluateTitle(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/properties/title/type"); ok = false; }
  return ok;
}

export function evaluateVersion(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/016-mutation/document.json#/properties/version/type"); ok = false; }
  return ok;
}


export const evaluateRoot = (v: unknown, results?: Results): boolean => evaluateDocument(v, fresh(), "", "", results ?? null);
export default evaluateRoot;
