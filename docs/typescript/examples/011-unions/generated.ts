// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export type Shape = Circle | Rectangle;
export function isCircle(value: unknown): value is Circle { return evaluateCircle(value, fresh()); }
export function isRectangle(value: unknown): value is Rectangle { return evaluateRectangle(value, fresh()); }
export function matchShape<R>(value: Shape, cases: { circle: (v: Circle) => R; rectangle: (v: Rectangle) => R }): R {
  if (isCircle(value)) { return cases.circle(value); }
  if (isRectangle(value)) { return cases.rectangle(value); }
  throw new Error("no Shape branch matched");
}

export function evaluateShape(value: unknown, ev: Ev): boolean {
  { let c = 0; const acc = fresh();
    { const t = fresh(); if (evaluateCircle(value, t)) { c++; acc.mergeProps(t); acc.mergeItems(t); } }
    { const t = fresh(); if (evaluateRectangle(value, t)) { c++; acc.mergeProps(t); acc.mergeItems(t); } }
    if (c !== 1) { return false; }
    ev.mergeProps(acc); ev.mergeItems(acc);
  }
  return true;
}

export interface Circle {
  readonly kind: "circle";
  readonly radius: number;
}

export function patchCircle(source: Uint8Array, changes: Partial<Circle>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["kind"] !== undefined) { targets.push({ name: enc.encode("kind"), content: enc.encode(JSON.stringify(changes["kind"])), vbs: -1, vbe: -1 }); }
  if (changes["radius"] !== undefined) { targets.push({ name: enc.encode("radius"), content: enc.encode(JSON.stringify(changes["radius"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildCircle(props: Circle): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceCircle(source: Uint8Array, recipe: (draft: Draft<Circle>) => void): Uint8Array {
  return produce<Circle>(source, recipe);
}

export function evaluateCircle(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "kind")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "radius")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "kind") { if (!evaluateKind(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "radius") { if (!evaluateRadius(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateKind(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["circle"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}

export function evaluateRadius(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export interface Rectangle {
  readonly height: number;
  readonly kind: "rectangle";
  readonly width: number;
}

export function patchRectangle(source: Uint8Array, changes: Partial<Rectangle>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["height"] !== undefined) { targets.push({ name: enc.encode("height"), content: enc.encode(JSON.stringify(changes["height"])), vbs: -1, vbe: -1 }); }
  if (changes["kind"] !== undefined) { targets.push({ name: enc.encode("kind"), content: enc.encode(JSON.stringify(changes["kind"])), vbs: -1, vbe: -1 }); }
  if (changes["width"] !== undefined) { targets.push({ name: enc.encode("width"), content: enc.encode(JSON.stringify(changes["width"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

export function buildRectangle(props: Rectangle): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceRectangle(source: Uint8Array, recipe: (draft: Draft<Rectangle>) => void): Uint8Array {
  return produce<Rectangle>(source, recipe);
}

export function evaluateRectangle(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "kind")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "width")) { return false; }
    if (!Object.prototype.hasOwnProperty.call(value, "height")) { return false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "height") { if (!evaluateHeight(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "kind") { if (!evaluateKind2(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "width") { if (!evaluateWidth(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateHeight(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}

export function evaluateKind2(value: unknown, ev: Ev): boolean {
  { const allowed: readonly unknown[] = ["rectangle"]; if (!allowed.some((a) => __eq(value, a))) { return false; } }
  return true;
}

export function evaluateWidth(value: unknown, ev: Ev): boolean {
  if (!(__isNum(value))) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateShape(v, fresh());
export default evaluateRoot;
