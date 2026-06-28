// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * Order
 */
export interface Order {
  readonly billTo?: Address;
  readonly id: string;
  readonly shipTo: Address;
}

export function patchOrder(source: Uint8Array, changes: Partial<Order>, removals?: ReadonlyArray<"billTo">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["billTo"] !== undefined) { targets.push({ name: enc.encode("billTo"), content: enc.encode(JSON.stringify(changes["billTo"])), vbs: -1, vbe: -1 }); }
  if (changes["id"] !== undefined) { targets.push({ name: enc.encode("id"), content: enc.encode(JSON.stringify(changes["id"])), vbs: -1, vbe: -1 }); }
  if (changes["shipTo"] !== undefined) { targets.push({ name: enc.encode("shipTo"), content: enc.encode(JSON.stringify(changes["shipTo"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildOrder(props: Order): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function buildCanonicalOrder(props: Order): Uint8Array {
  return canonicalize(props);
}

export function produceOrder(source: Uint8Array, recipe: (draft: Draft<Order>) => void): Uint8Array {
  return produce<Order>(source, recipe);
}

export function evaluateOrder(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "id")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "shipTo")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "billTo") { if (!evaluateAddress(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/billTo/$ref"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "id") { if (!evaluateId(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/id"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "shipTo") { if (!evaluateAddress(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/shipTo/$ref"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Order", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/title"); }
  return ok;
}

/**
 * Address
 */
export interface Address {
  readonly city: string;
  readonly line1: string;
  readonly postcode: string;
}

export function patchAddress(source: Uint8Array, changes: Partial<Address>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["city"] !== undefined) { targets.push({ name: enc.encode("city"), content: enc.encode(JSON.stringify(changes["city"])), vbs: -1, vbe: -1 }); }
  if (changes["line1"] !== undefined) { targets.push({ name: enc.encode("line1"), content: enc.encode(JSON.stringify(changes["line1"])), vbs: -1, vbe: -1 }); }
  if (changes["postcode"] !== undefined) { targets.push({ name: enc.encode("postcode"), content: enc.encode(JSON.stringify(changes["postcode"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildAddress(props: Address): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function buildCanonicalAddress(props: Address): Uint8Array {
  return canonicalize(props);
}

export function produceAddress(source: Uint8Array, recipe: (draft: Draft<Address>) => void): Uint8Array {
  return produce<Address>(source, recipe);
}

export function evaluateAddress(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/$defs/address/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "line1")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/$defs/address/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "city")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/$defs/address/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "postcode")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/$defs/address/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "city") { if (!evaluateCity(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/city"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "line1") { if (!evaluateLine1(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/line1"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "postcode") { if (!evaluatePostcode(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/postcode"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Address", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/$defs/address/title"); }
  return ok;
}

export function evaluateCity(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/$defs/address/properties/city/type"); ok = false; }
  return ok;
}

export function evaluateLine1(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/$defs/address/properties/line1/type"); ok = false; }
  return ok;
}

export function evaluatePostcode(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/$defs/address/properties/postcode/type"); ok = false; }
  return ok;
}

export function evaluateId(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/003-references/order.json#/properties/id/type"); ok = false; }
  return ok;
}


export const evaluateRoot = (v: unknown, results?: Results): boolean => evaluateOrder(v, fresh(), "", "", results ?? null);
export default evaluateRoot;
