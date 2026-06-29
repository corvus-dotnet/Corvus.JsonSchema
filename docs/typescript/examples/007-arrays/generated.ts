// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse };

/**
 * Cart
 */
export interface Cart {
  readonly items: readonly LineItem[];
}

function patchCart(source: Uint8Array, changes: Partial<Cart>, arrays?: CartArrayOps): Uint8Array {
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

function buildCart(props: Cart): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalCart(props: Cart): Uint8Array {
  return canonicalize(props);
}

function produceCart(source: Uint8Array, recipe: (draft: Draft<Cart>) => void): Uint8Array {
  return produce<Cart>(source, recipe);
}

function evaluateCart(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "items")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "items") { if (!evaluateItems(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/items"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Cart", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/title"); }
  return ok;
}

function evaluateItems(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/type"); ok = false; }
  if (Array.isArray(value) && value.length < 1) { if (r === null) return false; r.fail(kl + "/minItems", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/minItems"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateLineItem(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

/**
 * LineItem
 */
export interface LineItem {
  readonly qty: number;
  readonly sku: string;
}

function patchLineItem(source: Uint8Array, changes: Partial<LineItem>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["qty"] !== undefined) { targets.push({ name: enc.encode("qty"), content: enc.encode(JSON.stringify(changes["qty"])), vbs: -1, vbe: -1 }); }
  if (changes["sku"] !== undefined) { targets.push({ name: enc.encode("sku"), content: enc.encode(JSON.stringify(changes["sku"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildLineItem(props: LineItem): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalLineItem(props: LineItem): Uint8Array {
  return canonicalize(props);
}

function produceLineItem(source: Uint8Array, recipe: (draft: Draft<LineItem>) => void): Uint8Array {
  return produce<LineItem>(source, recipe);
}

function evaluateLineItem(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/items/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "sku")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/items/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "qty")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/items/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "qty") { if (!evaluateQty(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/qty"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "sku") { if (!evaluateSku(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/sku"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "LineItem", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/items/title"); }
  return ok;
}

function evaluateQty(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/items/properties/qty/type"); ok = false; }
  if (__isNum(value) && __cmp(String(value), "1") < 0) { if (r === null) return false; r.fail(kl + "/minimum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/items/properties/qty/minimum"); ok = false; }
  return ok;
}

function evaluateSku(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/007-arrays/cart.json#/properties/items/items/properties/sku/type"); ok = false; }
  return ok;
}


export const Cart = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCart(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Cart => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Cart,
  build: buildCart,
  buildCanonical: buildCanonicalCart,
  patch: patchCart,
  produce: produceCart,
};
export const Items = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const LineItem = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateLineItem(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): LineItem => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as LineItem,
  build: buildLineItem,
  buildCanonical: buildCanonicalLineItem,
  patch: patchLineItem,
  produce: produceLineItem,
};
export const Qty = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateQty(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Sku = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSku(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};

export default Cart;
