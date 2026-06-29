// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse };

export type Shape = Circle | Rectangle;
function isCircle(value: unknown): value is Circle { return evaluateCircle(value, fresh()); }
function isRectangle(value: unknown): value is Rectangle { return evaluateRectangle(value, fresh()); }
function matchShape<R>(value: Shape, cases: { circle: (v: Circle) => R; rectangle: (v: Rectangle) => R }): R {
  if (isCircle(value)) { return cases.circle(value); }
  if (isRectangle(value)) { return cases.rectangle(value); }
  throw new Error("no Shape branch matched");
}

function evaluateShape(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { let c = 0; const acc = fresh(); const subs: Results[] | null = r === null ? null : [];
    { const t = fresh(); const rb = r === null ? null : new Results(r.verbose); if (evaluateCircle(value, t, il, (rb === null ? kl : kl + "/oneOf/0"), rb)) { c++; acc.mergeProps(t); acc.mergeItems(t); } else if (rb !== null && subs !== null) { subs.push(rb); } }
    { const t = fresh(); const rb = r === null ? null : new Results(r.verbose); if (evaluateRectangle(value, t, il, (rb === null ? kl : kl + "/oneOf/1"), rb)) { c++; acc.mergeProps(t); acc.mergeItems(t); } else if (rb !== null && subs !== null) { subs.push(rb); } }
    if (c !== 1) { if (r === null) return false; if (subs !== null && c === 0) { for (const s of subs) { r.merge(s); } } r.fail(kl + "/oneOf", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf"); ok = false; }
    ev.mergeProps(acc); ev.mergeItems(acc);
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Shape", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/title"); }
  return ok;
}

/**
 * Circle
 */
export interface Circle {
  readonly kind: "circle";
  readonly radius: number;
}

function patchCircle(source: Uint8Array, changes: Partial<Circle>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["kind"] !== undefined) { targets.push({ name: enc.encode("kind"), content: enc.encode(JSON.stringify(changes["kind"])), vbs: -1, vbe: -1 }); }
  if (changes["radius"] !== undefined) { targets.push({ name: enc.encode("radius"), content: enc.encode(JSON.stringify(changes["radius"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildCircle(props: Circle): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalCircle(props: Circle): Uint8Array {
  return canonicalize(props);
}

function produceCircle(source: Uint8Array, recipe: (draft: Draft<Circle>) => void): Uint8Array {
  return produce<Circle>(source, recipe);
}

function evaluateCircle(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/0/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "kind")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/0/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "radius")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/0/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "kind") { if (!evaluateKind(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/kind"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "radius") { if (!evaluateRadius(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/radius"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Circle", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/0/title"); }
  return ok;
}

function evaluateKind(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["circle"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/const", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/0/properties/kind/const"); ok = false; } }
  return ok;
}

function evaluateRadius(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/0/properties/radius/type"); ok = false; }
  return ok;
}

/**
 * Rectangle
 */
export interface Rectangle {
  readonly height: number;
  readonly kind: "rectangle";
  readonly width: number;
}

function patchRectangle(source: Uint8Array, changes: Partial<Rectangle>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["height"] !== undefined) { targets.push({ name: enc.encode("height"), content: enc.encode(JSON.stringify(changes["height"])), vbs: -1, vbe: -1 }); }
  if (changes["kind"] !== undefined) { targets.push({ name: enc.encode("kind"), content: enc.encode(JSON.stringify(changes["kind"])), vbs: -1, vbe: -1 }); }
  if (changes["width"] !== undefined) { targets.push({ name: enc.encode("width"), content: enc.encode(JSON.stringify(changes["width"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildRectangle(props: Rectangle): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalRectangle(props: Rectangle): Uint8Array {
  return canonicalize(props);
}

function produceRectangle(source: Uint8Array, recipe: (draft: Draft<Rectangle>) => void): Uint8Array {
  return produce<Rectangle>(source, recipe);
}

function evaluateRectangle(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/1/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "kind")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/1/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "width")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/1/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "height")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/1/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "height") { if (!evaluateHeight(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/height"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "kind") { if (!evaluateKind2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/kind"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "width") { if (!evaluateWidth(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/width"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Rectangle", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/1/title"); }
  return ok;
}

function evaluateHeight(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/1/properties/height/type"); ok = false; }
  return ok;
}

function evaluateKind2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["rectangle"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/const", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/1/properties/kind/const"); ok = false; } }
  return ok;
}

function evaluateWidth(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/011-unions/shape.json#/oneOf/1/properties/width/type"); ok = false; }
  return ok;
}


export const Shape = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateShape(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Shape => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Shape,
  match: matchShape,
};
export const Circle = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCircle(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Circle => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Circle,
  build: buildCircle,
  buildCanonical: buildCanonicalCircle,
  patch: patchCircle,
  produce: produceCircle,
  is: isCircle,
};
export const Kind = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateKind(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Radius = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateRadius(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Rectangle = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateRectangle(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Rectangle => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Rectangle,
  build: buildRectangle,
  buildCanonical: buildCanonicalRectangle,
  patch: patchRectangle,
  produce: produceRectangle,
  is: isRectangle,
};
export const Height = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateHeight(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Kind2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateKind2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Width = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateWidth(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};

export default Shape;
