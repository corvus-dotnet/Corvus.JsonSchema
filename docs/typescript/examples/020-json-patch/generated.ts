// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError, type JsonPatch, type JsonPatchOp, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError };
export type { JsonPatch, JsonPatchOp };

/**
 * Contact
 */
export interface Contact {
  readonly address?: Address;
  readonly displayName?: string;
  readonly email?: string;
  readonly name: string;
  readonly phones?: readonly string[];
  readonly version?: number;
}

function patchContact(source: Uint8Array, changes: Partial<Contact>, removals?: ReadonlyArray<"address" | "displayName" | "email" | "phones" | "version">, arrays?: ContactArrayOps): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["address"] !== undefined) { targets.push({ name: enc.encode("address"), content: enc.encode(JSON.stringify(changes["address"])), vbs: -1, vbe: -1 }); }
  if (changes["displayName"] !== undefined) { targets.push({ name: enc.encode("displayName"), content: enc.encode(JSON.stringify(changes["displayName"])), vbs: -1, vbe: -1 }); }
  if (changes["email"] !== undefined) { targets.push({ name: enc.encode("email"), content: enc.encode(JSON.stringify(changes["email"])), vbs: -1, vbe: -1 }); }
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  if (changes["phones"] !== undefined) { targets.push({ name: enc.encode("phones"), content: enc.encode(JSON.stringify(changes["phones"])), vbs: -1, vbe: -1 }); }
  if (changes["version"] !== undefined) { targets.push({ name: enc.encode("version"), content: enc.encode(JSON.stringify(changes["version"])), vbs: -1, vbe: -1 }); }
  const arrayEdits: RmwArrayEdit[] = [];
  if (arrays !== undefined && arrays["phones"] !== undefined) { arrayEdits.push({ name: enc.encode("phones"), ops: arrays["phones"]! as RmwArrayOps, prefixLen: 0 }); }
  if ((removals !== undefined && removals.length > 0) || arrayEdits.length > 0) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, arrayEdits);
  }
  return rmwUpsert(source, targets);
}

export interface ContactArrayOps {
  readonly phones?: ListOps<string>;
}

function buildContact(props: Contact): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalContact(props: Contact): Uint8Array {
  return canonicalize(props);
}

function produceContact(source: Uint8Array, recipe: (draft: Draft<Contact>) => void): Uint8Array {
  return produce<Contact>(source, recipe);
}

function evaluateContact(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "name")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "address") { if (!evaluateAddress(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/address"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "displayName") { if (!evaluateDisplayName(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/displayName"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "email") { if (!evaluateEmail(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/email"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "name") { if (!evaluateName(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/name"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "phones") { if (!evaluatePhones(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/phones"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "version") { if (!evaluateVersion(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/version"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Contact", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/title"); }
  return ok;
}

export interface Address {
  readonly city?: string;
  readonly zip?: string;
}

function patchAddress(source: Uint8Array, changes: Partial<Address>, removals?: ReadonlyArray<"city" | "zip">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["city"] !== undefined) { targets.push({ name: enc.encode("city"), content: enc.encode(JSON.stringify(changes["city"])), vbs: -1, vbe: -1 }); }
  if (changes["zip"] !== undefined) { targets.push({ name: enc.encode("zip"), content: enc.encode(JSON.stringify(changes["zip"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildAddress(props: Address): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalAddress(props: Address): Uint8Array {
  return canonicalize(props);
}

function produceAddress(source: Uint8Array, recipe: (draft: Draft<Address>) => void): Uint8Array {
  return produce<Address>(source, recipe);
}

function evaluateAddress(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/address/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "city") { if (!evaluateCity(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/city"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "zip") { if (!evaluateZip(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/zip"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateCity(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/address/properties/city/type"); ok = false; }
  return ok;
}

function evaluateZip(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/address/properties/zip/type"); ok = false; }
  return ok;
}

function evaluateDisplayName(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/displayName/type"); ok = false; }
  return ok;
}

function evaluateEmail(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/email/type"); ok = false; }
  return ok;
}

function evaluateName(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/name/type"); ok = false; }
  return ok;
}

function evaluatePhones(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/phones/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/phones/items/type"); ok = false; }
  return ok;
}

function evaluateVersion(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/020-json-patch/contact.json#/properties/version/type"); ok = false; }
  return ok;
}


export const Contact = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateContact(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Contact => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Contact,
  build: buildContact,
  buildCanonical: buildCanonicalContact,
  patch: patchContact,
  produce: produceContact,
  applyPatch: (doc: Uint8Array | Contact, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Contact, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Contact, target: Uint8Array | Contact): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Contact, target: Uint8Array | Contact): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Address = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateAddress(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Address => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Address,
  build: buildAddress,
  buildCanonical: buildCanonicalAddress,
  patch: patchAddress,
  produce: produceAddress,
  applyPatch: (doc: Uint8Array | Address, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Address, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Address, target: Uint8Array | Address): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Address, target: Uint8Array | Address): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const City = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCity(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Zip = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateZip(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const DisplayName = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateDisplayName(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Email = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateEmail(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Name = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateName(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Phones = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePhones(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Items = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Version = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateVersion(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};

export default Contact;
