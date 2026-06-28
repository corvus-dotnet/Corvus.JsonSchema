// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

export type Event = Click | KeyPress | Scroll;
function isClick(value: unknown): value is Click { return evaluateClick(value, fresh()); }
function isKeyPress(value: unknown): value is KeyPress { return evaluateKeyPress(value, fresh()); }
function isScroll(value: unknown): value is Scroll { return evaluateScroll(value, fresh()); }
function matchEvent<R>(value: Event, cases: { click: (v: Click) => R; keyPress: (v: KeyPress) => R; scroll: (v: Scroll) => R }): R {
  if (isClick(value)) { return cases.click(value); }
  if (isKeyPress(value)) { return cases.keyPress(value); }
  if (isScroll(value)) { return cases.scroll(value); }
  throw new Error("no Event branch matched");
}

function evaluateEvent(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { let c = 0; const acc = fresh(); const subs: Results[] | null = r === null ? null : [];
    { const t = fresh(); const rb = r === null ? null : new Results(r.verbose); if (evaluateClick(value, t, il, (rb === null ? kl : kl + "/oneOf/0"), rb)) { c++; acc.mergeProps(t); acc.mergeItems(t); } else if (rb !== null && subs !== null) { subs.push(rb); } }
    { const t = fresh(); const rb = r === null ? null : new Results(r.verbose); if (evaluateKeyPress(value, t, il, (rb === null ? kl : kl + "/oneOf/1"), rb)) { c++; acc.mergeProps(t); acc.mergeItems(t); } else if (rb !== null && subs !== null) { subs.push(rb); } }
    { const t = fresh(); const rb = r === null ? null : new Results(r.verbose); if (evaluateScroll(value, t, il, (rb === null ? kl : kl + "/oneOf/2"), rb)) { c++; acc.mergeProps(t); acc.mergeItems(t); } else if (rb !== null && subs !== null) { subs.push(rb); } }
    if (c !== 1) { if (r === null) return false; if (subs !== null && c === 0) { for (const s of subs) { r.merge(s); } } r.fail(kl + "/oneOf", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf"); ok = false; }
    ev.mergeProps(acc); ev.mergeItems(acc);
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Event", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/title"); }
  return ok;
}

/**
 * Click
 */
export interface Click {
  readonly type: "click";
  readonly x: number;
  readonly y: number;
}

function patchClick(source: Uint8Array, changes: Partial<Click>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["type"] !== undefined) { targets.push({ name: enc.encode("type"), content: enc.encode(JSON.stringify(changes["type"])), vbs: -1, vbe: -1 }); }
  if (changes["x"] !== undefined) { targets.push({ name: enc.encode("x"), content: enc.encode(JSON.stringify(changes["x"])), vbs: -1, vbe: -1 }); }
  if (changes["y"] !== undefined) { targets.push({ name: enc.encode("y"), content: enc.encode(JSON.stringify(changes["y"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildClick(props: Click): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalClick(props: Click): Uint8Array {
  return canonicalize(props);
}

function produceClick(source: Uint8Array, recipe: (draft: Draft<Click>) => void): Uint8Array {
  return produce<Click>(source, recipe);
}

function evaluateClick(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/0/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "type")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/0/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "x")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/0/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "y")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/0/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "type") { if (!evaluateType(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/type"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "x") { if (!evaluateX(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/x"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "y") { if (!evaluateY(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/y"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Click", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/0/title"); }
  return ok;
}

function evaluateType(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["click"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/const", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/0/properties/type/const"); ok = false; } }
  return ok;
}

function evaluateX(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/0/properties/x/type"); ok = false; }
  return ok;
}

function evaluateY(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/0/properties/y/type"); ok = false; }
  return ok;
}

/**
 * KeyPress
 */
export interface KeyPress {
  readonly key: string;
  readonly type: "keypress";
}

function patchKeyPress(source: Uint8Array, changes: Partial<KeyPress>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["key"] !== undefined) { targets.push({ name: enc.encode("key"), content: enc.encode(JSON.stringify(changes["key"])), vbs: -1, vbe: -1 }); }
  if (changes["type"] !== undefined) { targets.push({ name: enc.encode("type"), content: enc.encode(JSON.stringify(changes["type"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildKeyPress(props: KeyPress): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalKeyPress(props: KeyPress): Uint8Array {
  return canonicalize(props);
}

function produceKeyPress(source: Uint8Array, recipe: (draft: Draft<KeyPress>) => void): Uint8Array {
  return produce<KeyPress>(source, recipe);
}

function evaluateKeyPress(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/1/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "type")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/1/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "key")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/1/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "key") { if (!evaluateKey(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/key"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "type") { if (!evaluateType2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/type"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "KeyPress", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/1/title"); }
  return ok;
}

function evaluateKey(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/1/properties/key/type"); ok = false; }
  return ok;
}

function evaluateType2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["keypress"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/const", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/1/properties/type/const"); ok = false; } }
  return ok;
}

/**
 * Scroll
 */
export interface Scroll {
  readonly delta: number;
  readonly type: "scroll";
}

function patchScroll(source: Uint8Array, changes: Partial<Scroll>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["delta"] !== undefined) { targets.push({ name: enc.encode("delta"), content: enc.encode(JSON.stringify(changes["delta"])), vbs: -1, vbe: -1 }); }
  if (changes["type"] !== undefined) { targets.push({ name: enc.encode("type"), content: enc.encode(JSON.stringify(changes["type"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildScroll(props: Scroll): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalScroll(props: Scroll): Uint8Array {
  return canonicalize(props);
}

function produceScroll(source: Uint8Array, recipe: (draft: Draft<Scroll>) => void): Uint8Array {
  return produce<Scroll>(source, recipe);
}

function evaluateScroll(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/2/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "type")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/2/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "delta")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/2/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "delta") { if (!evaluateDelta(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/delta"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "type") { if (!evaluateType3(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/type"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Scroll", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/2/title"); }
  return ok;
}

function evaluateDelta(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/2/properties/delta/type"); ok = false; }
  return ok;
}

function evaluateType3(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = ["scroll"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/const", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/012-discriminated-unions/event.json#/oneOf/2/properties/type/const"); ok = false; } }
  return ok;
}


export const Event = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateEvent(v, fresh(), "", "", results ?? null),
  match: matchEvent,
};
export const Click = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateClick(v, fresh(), "", "", results ?? null),
  build: buildClick,
  buildCanonical: buildCanonicalClick,
  patch: patchClick,
  produce: produceClick,
  is: isClick,
};
export const Type = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateType(v, fresh(), "", "", results ?? null),
};
export const X = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateX(v, fresh(), "", "", results ?? null),
};
export const Y = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateY(v, fresh(), "", "", results ?? null),
};
export const KeyPress = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateKeyPress(v, fresh(), "", "", results ?? null),
  build: buildKeyPress,
  buildCanonical: buildCanonicalKeyPress,
  patch: patchKeyPress,
  produce: produceKeyPress,
  is: isKeyPress,
};
export const Key = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateKey(v, fresh(), "", "", results ?? null),
};
export const Type2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateType2(v, fresh(), "", "", results ?? null),
};
export const Scroll = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateScroll(v, fresh(), "", "", results ?? null),
  build: buildScroll,
  buildCanonical: buildCanonicalScroll,
  patch: patchScroll,
  produce: produceScroll,
  is: isScroll,
};
export const Delta = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateDelta(v, fresh(), "", "", results ?? null),
};
export const Type3 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateType3(v, fresh(), "", "", results ?? null),
};

export default Event;
