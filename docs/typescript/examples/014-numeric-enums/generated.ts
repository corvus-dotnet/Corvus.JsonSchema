// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "../corvus-runtime.js";
export { decodeAndParse };

/**
 * Response
 */
export interface Response {
  readonly status: Status;
}

function patchResponse(source: Uint8Array, changes: Partial<Response>): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["status"] !== undefined) { targets.push({ name: enc.encode("status"), content: enc.encode(JSON.stringify(changes["status"])), vbs: -1, vbe: -1 }); }
  return rmwUpsert(source, targets);
}

function buildResponse(props: Response): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalResponse(props: Response): Uint8Array {
  return canonicalize(props);
}

function produceResponse(source: Uint8Array, recipe: (draft: Draft<Response>) => void): Uint8Array {
  return produce<Response>(source, recipe);
}

function evaluateResponse(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/014-numeric-enums/response.json#/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "status")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/014-numeric-enums/response.json#/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "status") { if (!evaluateStatus(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/status"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  if (r !== null && r.verbose && ok) { r.annotate("title", "Response", kl + "/title", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/014-numeric-enums/response.json#/title"); }
  return ok;
}

export type Status = 200 | 404 | 500;

function evaluateStatus(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  { const allowed: readonly unknown[] = [200, 404, 500]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/enum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/examples/014-numeric-enums/response.json#/properties/status/enum"); ok = false; } }
  return ok;
}


export const Response = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateResponse(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Response => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Response,
  build: buildResponse,
  buildCanonical: buildCanonicalResponse,
  patch: patchResponse,
  produce: produceResponse,
};
export const Status = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateStatus(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Status => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Status,
};

export default Response;
