// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

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

export function produceOrder(source: Uint8Array, recipe: (draft: Draft<Order>) => void): Uint8Array {
  return produce<Order>(source, recipe);
}

export function evaluateOrder(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "id")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "shipTo")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "billTo") { if (!evaluateAddress(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "id") { if (!evaluateId(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "shipTo") { if (!evaluateAddress(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

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

export function produceAddress(source: Uint8Array, recipe: (draft: Draft<Address>) => void): Uint8Array {
  return produce<Address>(source, recipe);
}

export function evaluateAddress(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "line1")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "city")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "postcode")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "city") { if (!evaluateCity(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "line1") { if (!evaluateLine1(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "postcode") { if (!evaluatePostcode(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateCity(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export function evaluateLine1(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export function evaluatePostcode(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export function evaluateId(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateOrder(v, fresh());
export default evaluateRoot;
