// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse };

export interface Scores {
  readonly [key: string]: number;
}

function evaluateScores(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/015-maps/scores.json#/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (!evaluateAdditionalProperties(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/additionalProperties"), r)) { if (r === null) return false; ok = false; } ev.markProp(i);
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Scores", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/015-maps/scores.json#/title"); }
  return ok;
}

function evaluateAdditionalProperties(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isNum(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/015-maps/scores.json#/additionalProperties/type"); ok = false; }
  if (__isNum(value) && __cmp(String(value), "0") < 0) { if (r === null) return false; r.fail(kl + "/minimum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/015-maps/scores.json#/additionalProperties/minimum"); ok = false; }
  return ok;
}


export const Scores = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateScores(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Scores => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Scores,
};
export const AdditionalProperties = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateAdditionalProperties(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};

export default Scores;
