// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError, type JsonPatch, type JsonPatchOp, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError };
export type { JsonPatch, JsonPatchOp };

/**
 * Settings
 */
export interface Settings {
  /**
   * @default 14
   */
  readonly fontSize?: number;
  /**
   * @default "light"
   */
  readonly theme?: string;
}

function patchSettings(source: Uint8Array, changes: Partial<Settings>, removals?: ReadonlyArray<"fontSize" | "theme">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["fontSize"] !== undefined) { targets.push({ name: enc.encode("fontSize"), content: enc.encode(JSON.stringify(changes["fontSize"])), vbs: -1, vbe: -1 }); }
  if (changes["theme"] !== undefined) { targets.push({ name: enc.encode("theme"), content: enc.encode(JSON.stringify(changes["theme"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildSettings(props: Settings): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalSettings(props: Settings): Uint8Array {
  return canonicalize(props);
}

function produceSettings(source: Uint8Array, recipe: (draft: Draft<Settings>) => void): Uint8Array {
  return produce<Settings>(source, recipe);
}

function withDefaultsSettings(value: Settings): Settings {
  const out: Record<string, unknown> = { ...(value as unknown as Record<string, unknown>) };
  if (!("fontSize" in value)) { out["fontSize"] = 14; }
  if (!("theme" in value)) { out["theme"] = "light"; }
  return out as unknown as Settings;
}

function evaluateSettings(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/018-defaults/settings.json#/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      let m = false;
      if (k === "fontSize") { if (!evaluateFontSize(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/fontSize"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); m = true; }
      else if (k === "theme") { if (!evaluateTheme(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/theme"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); m = true; }
      if (!m) { if (!evaluateJsonNotAny(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/additionalProperties"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Settings", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/018-defaults/settings.json#/title"); }
  return ok;
}

function evaluateFontSize(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/018-defaults/settings.json#/properties/fontSize/type"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("default", 14, kl + "/default", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/018-defaults/settings.json#/properties/fontSize/default"); }
  return ok;
}

function evaluateTheme(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/018-defaults/settings.json#/properties/theme/type"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("default", "light", kl + "/default", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/018-defaults/settings.json#/properties/theme/default"); }
  return ok;
}

function evaluateJsonNotAny(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  if (r !== null) { r.fail(kl, il, "corvus:/JsonNotAny#"); } return false;
}


export const Settings = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSettings(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Settings => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Settings,
  build: buildSettings,
  buildCanonical: buildCanonicalSettings,
  patch: patchSettings,
  produce: produceSettings,
  withDefaults: withDefaultsSettings,
  applyPatch: (doc: Uint8Array | Settings, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Settings, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Settings, target: Uint8Array | Settings): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Settings, target: Uint8Array | Settings): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const FontSize = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateFontSize(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Theme = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateTheme(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const JsonNotAny = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateJsonNotAny(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};

export default Settings;
