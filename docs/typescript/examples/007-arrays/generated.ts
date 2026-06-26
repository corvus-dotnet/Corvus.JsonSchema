// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Cart {
  readonly items: readonly LineItem[];
}

export function patchCart(source: Uint8Array, changes: Partial<Cart>, arrays?: CartArrayOps): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["items"] !== undefined) { targets.push({ name: enc.encode("items"), content: enc.encode(JSON.stringify(changes["items"])), vbs: -1, vbe: -1 }); }
  const arrayEdits: RmwArrayEdit[] = [];
  if (arrays !== undefined && arrays["items"] !== undefined) { arrayEdits.push({ name: enc.encode("items"), ops: arrays["items"]! as RmwArrayOps, prefixLen: 0 }); }
  if (arrayEdits.length > 0) {
    const removeNames: Uint8Array[] = [];
    return rmwProduceFull(source, targets, removeNames, arrayEdits);
  }
  return rmwUpsert(source, targets);
}

export interface CartArrayOps {
  readonly items?: ListOps<LineItem>;
}

export function buildCart(props: Cart): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceCart(source: Uint8Array, recipe: (draft: Draft<Cart>) => void): Uint8Array {
  return produce<Cart>(source, recipe);
}

export function evaluateCart(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "items")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "items") { if (!evaluateItems(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateItems(value: unknown, ev: Ev): boolean {
  if (!(Array.isArray(value))) { return false; }
  if (Array.isArray(value) && value.length < 1) { return false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateLineItem(value[i], NOEV)) { return false; } ev.markItem(i); } }
  return true;
}

export interface LineItem {
  readonly qty: number;
  readonly sku: string;
}

export function patchLineItem(source: Uint8Array, changes: Partial<LineItem>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["qty"] !== undefined) { targets.push({ name: enc.encode("qty"), content: enc.encode(JSON.stringify(changes["qty"])), vbs: -1, vbe: -1 }); }
  if (changes["sku"] !== undefined) { targets.push({ name: enc.encode("sku"), content: enc.encode(JSON.stringify(changes["sku"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildLineItem(props: LineItem): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceLineItem(source: Uint8Array, recipe: (draft: Draft<LineItem>) => void): Uint8Array {
  return produce<LineItem>(source, recipe);
}

export function evaluateLineItem(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "sku")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "qty")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "qty") { if (!evaluateQty(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "sku") { if (!evaluateSku(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateQty(value: unknown, ev: Ev): boolean {
  if (!((__isNum(value) && __isInt(String(value))))) { return false; }
  if (__isNum(value) && __cmp(String(value), "1") < 0) { return false; }
  return true;
}

export function evaluateSku(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateCart(v, fresh());
export default evaluateRoot;
