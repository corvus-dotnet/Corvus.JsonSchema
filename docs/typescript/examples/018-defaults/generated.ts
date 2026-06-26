// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from "../corvus-runtime.js";

export interface Settings {
  readonly fontSize?: number;
  readonly theme?: string;
}

export function patchSettings(source: Uint8Array, changes: Partial<Settings>, removals?: ReadonlyArray<"fontSize" | "theme">): Uint8Array {
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

export function buildSettings(props: Settings): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

export function produceSettings(source: Uint8Array, recipe: (draft: Draft<Settings>) => void): Uint8Array {
  return produce<Settings>(source, recipe);
}

export function evaluateSettings(value: unknown, ev: Ev): boolean {
  if (!(__isObj(value))) { return false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "fontSize") { if (!evaluateFontSize(o[k], NOEV)) { return false; } ev.markProp(i); }
      else if (k === "theme") { if (!evaluateTheme(o[k], NOEV)) { return false; } ev.markProp(i); }
    }
  }
  return true;
}

export function evaluateFontSize(value: unknown, ev: Ev): boolean {
  if (!((__isNum(value) && __isInt(String(value))))) { return false; }
  return true;
}

export function evaluateTheme(value: unknown, ev: Ev): boolean {
  if (!(typeof value === "string")) { return false; }
  return true;
}


export const evaluateRoot = (v: unknown): boolean => evaluateSettings(v, fresh());
export default evaluateRoot;
