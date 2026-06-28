// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";

/**
 * Widget
 */
export interface Widget {
  readonly createdAt?: CreatedAt;
  readonly id?: string;
  readonly name: string;
}

function patchWidget(source: Uint8Array, changes: Partial<Widget>, removals?: ReadonlyArray<"createdAt" | "id">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["createdAt"] !== undefined) { targets.push({ name: enc.encode("createdAt"), content: enc.encode(JSON.stringify(changes["createdAt"])), vbs: -1, vbe: -1 }); }
  if (changes["id"] !== undefined) { targets.push({ name: enc.encode("id"), content: enc.encode(JSON.stringify(changes["id"])), vbs: -1, vbe: -1 }); }
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildWidget(props: Widget): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalWidget(props: Widget): Uint8Array {
  return canonicalize(props);
}

function applyToWidget(source: Uint8Array, member: Named | Timestamped): Uint8Array {
  return patchWidget(source, member as Partial<Widget>);
}

function produceWidget(source: Uint8Array, recipe: (draft: Draft<Widget>) => void): Uint8Array {
  return produce<Widget>(source, recipe);
}

function evaluateWidget(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/type"); ok = false; }
  { const t = fresh(); if (!evaluateNamed(value, t, il, (r === null ? kl : kl + "/allOf/0/$ref"), r)) { if (r === null) return false; ok = false; } ev.mergeProps(t); ev.mergeItems(t); }
  { const t = fresh(); if (!evaluateTimestamped(value, t, il, (r === null ? kl : kl + "/allOf/1/$ref"), r)) { if (r === null) return false; ok = false; } ev.mergeProps(t); ev.mergeItems(t); }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "id") { if (!evaluateId(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/id"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Widget", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/title"); }
  return ok;
}

/**
 * Named
 */
export interface Named {
  readonly name: string;
}

function patchNamed(source: Uint8Array, changes: Partial<Named>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildNamed(props: Named): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalNamed(props: Named): Uint8Array {
  return canonicalize(props);
}

function produceNamed(source: Uint8Array, recipe: (draft: Draft<Named>) => void): Uint8Array {
  return produce<Named>(source, recipe);
}

function evaluateNamed(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/named/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "name")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/named/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "name") { if (!evaluateName(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/name"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Named", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/named/title"); }
  return ok;
}

function evaluateName(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/named/properties/name/type"); ok = false; }
  return ok;
}

/**
 * Timestamped
 */
export interface Timestamped {
  readonly createdAt?: CreatedAt;
}

function patchTimestamped(source: Uint8Array, changes: Partial<Timestamped>, removals?: ReadonlyArray<"createdAt">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["createdAt"] !== undefined) { targets.push({ name: enc.encode("createdAt"), content: enc.encode(JSON.stringify(changes["createdAt"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildTimestamped(props: Timestamped): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalTimestamped(props: Timestamped): Uint8Array {
  return canonicalize(props);
}

function produceTimestamped(source: Uint8Array, recipe: (draft: Draft<Timestamped>) => void): Uint8Array {
  return produce<Timestamped>(source, recipe);
}

function evaluateTimestamped(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/timestamped/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "createdAt") { if (!evaluateCreatedAt(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/createdAt"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Timestamped", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/timestamped/title"); }
  return ok;
}

export type CreatedAt = Brand<string, "date-time">;
function asCreatedAt(value: string): CreatedAt { if (!__fmt("date-time", value)) { throw new FormatError("date-time"); } return value as CreatedAt; }
function createdAtAsTemporal(value: CreatedAt): Temporal.Instant { return toInstant(value); }

function evaluateCreatedAt(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/timestamped/properties/createdAt/type"); ok = false; }
  if (typeof value === "string" && !__fmt("date-time", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/timestamped/properties/createdAt/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "date-time", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/$defs/timestamped/properties/createdAt/format"); }
  return ok;
}

function evaluateId(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/010-mixins/widget.json#/properties/id/type"); ok = false; }
  return ok;
}


export const Widget = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateWidget(v, fresh(), "", "", results ?? null),
  build: buildWidget,
  buildCanonical: buildCanonicalWidget,
  patch: patchWidget,
  produce: produceWidget,
};
export const Named = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateNamed(v, fresh(), "", "", results ?? null),
  build: buildNamed,
  buildCanonical: buildCanonicalNamed,
  patch: patchNamed,
  produce: produceNamed,
};
export const Name = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateName(v, fresh(), "", "", results ?? null),
};
export const Timestamped = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateTimestamped(v, fresh(), "", "", results ?? null),
  build: buildTimestamped,
  buildCanonical: buildCanonicalTimestamped,
  patch: patchTimestamped,
  produce: produceTimestamped,
};
export const CreatedAt = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCreatedAt(v, fresh(), "", "", results ?? null),
  as: asCreatedAt,
  asTemporal: createdAtAsTemporal,
};
export const Id = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateId(v, fresh(), "", "", results ?? null),
};

export default Widget;
