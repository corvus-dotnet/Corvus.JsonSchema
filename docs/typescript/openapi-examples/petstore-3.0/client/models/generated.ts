// AUTO-GENERATED: idiomatic TS types + registry-composed validators.
import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError, type JsonPatch, type JsonPatchOp, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from "./corvus-runtime.js";
export { decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError };
export type { JsonPatch, JsonPatchOp };

export interface ErrorEntity {
  readonly code?: number;
  readonly message: string;
}

function patchErrorEntity(source: Uint8Array, changes: Partial<ErrorEntity>, removals?: ReadonlyArray<"code">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["code"] !== undefined) { targets.push({ name: enc.encode("code"), content: enc.encode(JSON.stringify(changes["code"])), vbs: -1, vbe: -1 }); }
  if (changes["message"] !== undefined) { targets.push({ name: enc.encode("message"), content: enc.encode(JSON.stringify(changes["message"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildErrorEntity(props: ErrorEntity): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalErrorEntity(props: ErrorEntity): Uint8Array {
  return canonicalize(props);
}

function produceErrorEntity(source: Uint8Array, recipe: (draft: Draft<ErrorEntity>) => void): Uint8Array {
  return produce<ErrorEntity>(source, recipe);
}

function evaluateErrorEntity(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Error/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "message")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Error/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "code") { if (!evaluateCode(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/code"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "message") { if (!evaluateMessage(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/message"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateCode(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Error/properties/code/type"); ok = false; }
  return ok;
}

function evaluateMessage(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Error/properties/message/type"); ok = false; }
  return ok;
}

export interface Pet {
  readonly id: string;
  readonly name: string;
  readonly tag?: string;
}

function patchPet(source: Uint8Array, changes: Partial<Pet>, removals?: ReadonlyArray<"tag">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["id"] !== undefined) { targets.push({ name: enc.encode("id"), content: enc.encode(JSON.stringify(changes["id"])), vbs: -1, vbe: -1 }); }
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  if (changes["tag"] !== undefined) { targets.push({ name: enc.encode("tag"), content: enc.encode(JSON.stringify(changes["tag"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildPet(props: Pet): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalPet(props: Pet): Uint8Array {
  return canonicalize(props);
}

function producePet(source: Uint8Array, recipe: (draft: Draft<Pet>) => void): Uint8Array {
  return produce<Pet>(source, recipe);
}

function evaluatePet(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Pet/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "id")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Pet/required"); ok = false; }
    if (!Object.prototype.hasOwnProperty.call(value, "name")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Pet/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "id") { if (!evaluateId(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/id"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "name") { if (!evaluateName(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/name"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "tag") { if (!evaluateTag(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/tag"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateId(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Pet/properties/id/type"); ok = false; }
  return ok;
}

function evaluateName(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Pet/properties/name/type"); ok = false; }
  return ok;
}

function evaluateTag(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/Pet/properties/tag/type"); ok = false; }
  return ok;
}

export interface PetUpdate {
  readonly name: string;
  readonly tag?: string;
}

function patchPetUpdate(source: Uint8Array, changes: Partial<PetUpdate>, removals?: ReadonlyArray<"tag">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  if (changes["tag"] !== undefined) { targets.push({ name: enc.encode("tag"), content: enc.encode(JSON.stringify(changes["tag"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildPetUpdate(props: PetUpdate): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalPetUpdate(props: PetUpdate): Uint8Array {
  return canonicalize(props);
}

function producePetUpdate(source: Uint8Array, recipe: (draft: Draft<PetUpdate>) => void): Uint8Array {
  return produce<PetUpdate>(source, recipe);
}

function evaluatePetUpdate(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/PetUpdate/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "name")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/PetUpdate/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "name") { if (!evaluateName2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/name"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "tag") { if (!evaluateTag2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/tag"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateName2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/PetUpdate/properties/name/type"); ok = false; }
  return ok;
}

function evaluateTag2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/PetUpdate/properties/tag/type"); ok = false; }
  return ok;
}

export interface ServiceStatus {
  readonly status: Status;
  readonly uptimeSeconds?: number;
}

function patchServiceStatus(source: Uint8Array, changes: Partial<ServiceStatus>, removals?: ReadonlyArray<"uptimeSeconds">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["status"] !== undefined) { targets.push({ name: enc.encode("status"), content: enc.encode(JSON.stringify(changes["status"])), vbs: -1, vbe: -1 }); }
  if (changes["uptimeSeconds"] !== undefined) { targets.push({ name: enc.encode("uptimeSeconds"), content: enc.encode(JSON.stringify(changes["uptimeSeconds"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildServiceStatus(props: ServiceStatus): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalServiceStatus(props: ServiceStatus): Uint8Array {
  return canonicalize(props);
}

function produceServiceStatus(source: Uint8Array, recipe: (draft: Draft<ServiceStatus>) => void): Uint8Array {
  return produce<ServiceStatus>(source, recipe);
}

function evaluateServiceStatus(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/ServiceStatus/type"); ok = false; }
  if (__isObj(value)) {
    if (!Object.prototype.hasOwnProperty.call(value, "status")) { if (r === null) return false; r.fail(kl + "/required", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/ServiceStatus/required"); ok = false; }
  }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "status") { if (!evaluateStatus(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/status"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "uptimeSeconds") { if (!evaluateUptimeSeconds(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/uptimeSeconds"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

export type Status = "ok" | "degraded" | "down";

function evaluateStatus(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/ServiceStatus/properties/status/type"); ok = false; }
  { const allowed: readonly unknown[] = ["ok", "degraded", "down"]; if (!allowed.some((a) => __eq(value, a))) { if (r === null) return false; r.fail(kl + "/enum", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/ServiceStatus/properties/status/enum"); ok = false; } }
  return ok;
}

function evaluateUptimeSeconds(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/components/schemas/ServiceStatus/properties/uptimeSeconds/type"); ok = false; }
  return ok;
}

function evaluateSchema(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/0/schema/type"); ok = false; }
  return ok;
}

function evaluateSchema2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/1/schema/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/1/schema/items/type"); ok = false; }
  return ok;
}

function evaluateSchema3(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "boolean")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/2/schema/type"); ok = false; }
  return ok;
}

function evaluateSchema4(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/3/schema/type"); ok = false; }
  return ok;
}


export const ErrorEntity = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateErrorEntity(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): ErrorEntity => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as ErrorEntity,
  build: buildErrorEntity,
  buildCanonical: buildCanonicalErrorEntity,
  patch: patchErrorEntity,
  produce: produceErrorEntity,
  applyPatch: (doc: Uint8Array | ErrorEntity, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | ErrorEntity, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | ErrorEntity, target: Uint8Array | ErrorEntity): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | ErrorEntity, target: Uint8Array | ErrorEntity): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Code = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCode(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Message = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateMessage(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Pet = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePet(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Pet => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Pet,
  build: buildPet,
  buildCanonical: buildCanonicalPet,
  patch: patchPet,
  produce: producePet,
  applyPatch: (doc: Uint8Array | Pet, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Pet, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Pet, target: Uint8Array | Pet): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Pet, target: Uint8Array | Pet): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Id = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateId(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Name = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateName(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Tag = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateTag(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const PetUpdate = {
  evaluate: (v: unknown, results?: Results): boolean => evaluatePetUpdate(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): PetUpdate => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as PetUpdate,
  build: buildPetUpdate,
  buildCanonical: buildCanonicalPetUpdate,
  patch: patchPetUpdate,
  produce: producePetUpdate,
  applyPatch: (doc: Uint8Array | PetUpdate, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | PetUpdate, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | PetUpdate, target: Uint8Array | PetUpdate): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | PetUpdate, target: Uint8Array | PetUpdate): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Name2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateName2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Tag2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateTag2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const ServiceStatus = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateServiceStatus(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): ServiceStatus => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as ServiceStatus,
  build: buildServiceStatus,
  buildCanonical: buildCanonicalServiceStatus,
  patch: patchServiceStatus,
  produce: produceServiceStatus,
  applyPatch: (doc: Uint8Array | ServiceStatus, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | ServiceStatus, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | ServiceStatus, target: Uint8Array | ServiceStatus): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | ServiceStatus, target: Uint8Array | ServiceStatus): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Status = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateStatus(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Status => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Status,
};
export const UptimeSeconds = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateUptimeSeconds(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Items = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema3 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema3(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema4 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema4(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
