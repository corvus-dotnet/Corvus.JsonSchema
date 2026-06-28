// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * Payment
 */
export interface Payment {
  readonly cardNumber?: string;
  readonly method: Method2;
}

function patchPayment(source: Uint8Array, changes: Partial<Payment>, removals?: ReadonlyArray<"cardNumber">): Uint8Array {
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

function buildPayment(props: Payment): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalPayment(props: Payment): Uint8Array {
  return canonicalize(props);
}

function producePayment(source: Uint8Array, recipe: (draft: Draft<Payment>) => void): Uint8Array {
  return produce<Payment>(source, recipe);
}

function evaluatePayment(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/017-conditional/payment.json#/type"); ok = false; }
  { const t = fresh();
    if (evaluateIf(value, t)) {
      ev.mergeProps(t); ev.mergeItems(t);
      { const t2 = fresh(); if (!evaluateThen(value, t2, il, (r === null ? kl : kl + "/then"), r)) { if (r === null) return false; ok = false; } ev.mergeProps(t2); ev.mergeItems(t2); }
    } else {
    }
  }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "method")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/017-conditional/payment.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "cardNumber") { if (!evaluateCardNumber(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/cardNumber"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "method") { if (!evaluateMethod2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/method"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Payment", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/017-conditional/payment.json#/title"); }
  return ok;
}

function evaluateIf(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "method") { if (!evaluateMethod(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/method"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateMethod(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["card"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/const", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/017-conditional/payment.json#/if/properties/method/const"); ok = false; } }
  return ok;
}

function evaluateCardNumber(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/017-conditional/payment.json#/properties/cardNumber/type"); ok = false; }
  return ok;
}

export type Method2 = "card" | "cash";

function evaluateMethod2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["card", "cash"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/enum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/017-conditional/payment.json#/properties/method/enum"); ok = false; } }
  return ok;
}

function evaluateThen(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "cardNumber")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/017-conditional/payment.json#/then/required"); ok = false; }
  }
  return ok;
}


export const Payment = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePayment(v, fresh(), "", "", results ?? null),
  build: buildPayment,
  buildCanonical: buildCanonicalPayment,
  patch: patchPayment,
  produce: producePayment,
};
export const Method = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateMethod(v, fresh(), "", "", results ?? null),
};
export const CardNumber = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCardNumber(v, fresh(), "", "", results ?? null),
};
export const Method2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateMethod2(v, fresh(), "", "", results ?? null),
};

export default Payment;
