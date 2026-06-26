// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Payment {
  readonly cardNumber?: string;
  readonly method: Method2;
}

export function patchPayment(source: Uint8Array, changes: Partial<Payment>, removals?: ReadonlyArray<"cardNumber">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["cardNumber"] !== undefined) { targets.push({ name: enc.encode("cardNumber"), content: enc.encode(JSON.stringify(changes["cardNumber"])), vbs: -1, vbe: -1 }); }
  if (changes["method"] !== undefined) { targets.push({ name: enc.encode("method"), content: enc.encode(JSON.stringify(changes["method"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildPayment(props: Payment): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function producePayment(source: Uint8Array, recipe: (draft: Draft<Payment>) => void): Uint8Array {
  return produce<Payment>(source, recipe);
}

export function evaluatePayment(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  { const t = fresh();
    if (evaluateIf(value, t)) {
      ev.mergeProps(t); ev.mergeItems(t);
      { const t2 = fresh(); if (!evaluateThen(value, t2)) { return false; } ev.mergeProps(t2); ev.mergeItems(t2); }
    } else {
    }
  }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "method")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "cardNumber") { if (!evaluateCardNumber(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "method") { if (!evaluateMethod2(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export interface If {
  readonly method?: "card";
}

export function patchIf(source: Uint8Array, changes: Partial<If>, removals?: ReadonlyArray<"method">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["method"] !== undefined) { targets.push({ name: enc.encode("method"), content: enc.encode(JSON.stringify(changes["method"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

export function buildIf(props: If): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceIf(source: Uint8Array, recipe: (draft: Draft<If>) => void): Uint8Array {
  return produce<If>(source, recipe);
}

export function evaluateIf(value: unknown, ev: Ev): boolean {
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "method") { if (!evaluateMethod(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateMethod(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["card"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}

export function evaluateCardNumber(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export type Method2 = "card" | "cash";

export function evaluateMethod2(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["card", "cash"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}

export function evaluateThen(value: unknown, ev: Ev): boolean {
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "cardNumber")) { return false; }
  }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluatePayment(v, fresh());
export default evaluateRoot;
