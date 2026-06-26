// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export type Event = Click | KeyPress | Scroll;
export function isClick(value: unknown): value is Click { return evaluateClick(value, fresh()); }
export function isKeyPress(value: unknown): value is KeyPress { return evaluateKeyPress(value, fresh()); }
export function isScroll(value: unknown): value is Scroll { return evaluateScroll(value, fresh()); }
export function matchEvent<R>(value: Event, cases: { click: (v: Click) => R; keyPress: (v: KeyPress) => R; scroll: (v: Scroll) => R }): R {
  if (isClick(value)) { return cases.click(value); }
  if (isKeyPress(value)) { return cases.keyPress(value); }
  if (isScroll(value)) { return cases.scroll(value); }
  throw new Error("no Event branch matched");
}

export function evaluateEvent(value: unknown, ev: Ev): boolean {
  { let c = 0; const acc = fresh();
    { const t = fresh(); if (evaluateClick(value, t)) { c++; acc.mergeProps(t); acc.mergeItems(t); } }
    { const t = fresh(); if (evaluateKeyPress(value, t)) { c++; acc.mergeProps(t); acc.mergeItems(t); } }
    { const t = fresh(); if (evaluateScroll(value, t)) { c++; acc.mergeProps(t); acc.mergeItems(t); } }
    if (c !== 1) { return false; }
    ev.mergeProps(acc); ev.mergeItems(acc);
  }
  return true;
}

export interface Click {
  readonly type: "click";
  readonly x: number;
  readonly y: number;
}

export function patchClick(source: Uint8Array, changes: Partial<Click>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["type"] !== undefined) { targets.push({ name: enc.encode("type"), content: enc.encode(JSON.stringify(changes["type"])), vbs: -1, vbe: -1 }); }
  if (changes["x"] !== undefined) { targets.push({ name: enc.encode("x"), content: enc.encode(JSON.stringify(changes["x"])), vbs: -1, vbe: -1 }); }
  if (changes["y"] !== undefined) { targets.push({ name: enc.encode("y"), content: enc.encode(JSON.stringify(changes["y"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildClick(props: Click): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceClick(source: Uint8Array, recipe: (draft: Draft<Click>) => void): Uint8Array {
  return produce<Click>(source, recipe);
}

export function evaluateClick(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "type")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "x")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "y")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "type") { if (!evaluateType(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "x") { if (!evaluateX(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "y") { if (!evaluateY(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateType(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["click"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}

export function evaluateX(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export function evaluateY(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export interface KeyPress {
  readonly key: string;
  readonly type: "keypress";
}

export function patchKeyPress(source: Uint8Array, changes: Partial<KeyPress>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["key"] !== undefined) { targets.push({ name: enc.encode("key"), content: enc.encode(JSON.stringify(changes["key"])), vbs: -1, vbe: -1 }); }
  if (changes["type"] !== undefined) { targets.push({ name: enc.encode("type"), content: enc.encode(JSON.stringify(changes["type"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildKeyPress(props: KeyPress): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceKeyPress(source: Uint8Array, recipe: (draft: Draft<KeyPress>) => void): Uint8Array {
  return produce<KeyPress>(source, recipe);
}

export function evaluateKeyPress(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "type")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "key")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "key") { if (!evaluateKey(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "type") { if (!evaluateType2(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateKey(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}

export function evaluateType2(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["keypress"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}

export interface Scroll {
  readonly delta: number;
  readonly type: "scroll";
}

export function patchScroll(source: Uint8Array, changes: Partial<Scroll>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["delta"] !== undefined) { targets.push({ name: enc.encode("delta"), content: enc.encode(JSON.stringify(changes["delta"])), vbs: -1, vbe: -1 }); }
  if (changes["type"] !== undefined) { targets.push({ name: enc.encode("type"), content: enc.encode(JSON.stringify(changes["type"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildScroll(props: Scroll): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceScroll(source: Uint8Array, recipe: (draft: Draft<Scroll>) => void): Uint8Array {
  return produce<Scroll>(source, recipe);
}

export function evaluateScroll(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "type")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "delta")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "delta") { if (!evaluateDelta(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "type") { if (!evaluateType3(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateDelta(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export function evaluateType3(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["scroll"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateEvent(v, fresh());
export default evaluateRoot;
