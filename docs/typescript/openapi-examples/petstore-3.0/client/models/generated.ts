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

export interface Schema {
  readonly file?: string;
  readonly id?: string;
  readonly tags?: readonly string[];
}

function patchSchema(source: Uint8Array, changes: Partial<Schema>, removals?: ReadonlyArray<"file" | "id" | "tags">, arrays?: SchemaArrayOps): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["file"] !== undefined) { targets.push({ name: enc.encode("file"), content: enc.encode(JSON.stringify(changes["file"])), vbs: -1, vbe: -1 }); }
  if (changes["id"] !== undefined) { targets.push({ name: enc.encode("id"), content: enc.encode(JSON.stringify(changes["id"])), vbs: -1, vbe: -1 }); }
  if (changes["tags"] !== undefined) { targets.push({ name: enc.encode("tags"), content: enc.encode(JSON.stringify(changes["tags"])), vbs: -1, vbe: -1 }); }
  const arrayEdits: RmwArrayEdit[] = [];
  if (arrays !== undefined && arrays["tags"] !== undefined) { arrayEdits.push({ name: enc.encode("tags"), ops: arrays["tags"]! as RmwArrayOps, prefixLen: 0 }); }
  if ((removals !== undefined && removals.length > 0) || arrayEdits.length > 0) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, arrayEdits);
  }
  return rmwUpsert(source, targets);
}

export interface SchemaArrayOps {
  readonly tags?: ListOps<string>;
}

function buildSchema(props: Schema): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalSchema(props: Schema): Uint8Array {
  return canonicalize(props);
}

function produceSchema(source: Uint8Array, recipe: (draft: Draft<Schema>) => void): Uint8Array {
  return produce<Schema>(source, recipe);
}

function evaluateSchema(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1avatar/post/requestBody/content/multipart~1form-data/schema/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "file") { if (!evaluateFile(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/file"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "id") { if (!evaluateId2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/id"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "tags") { if (!evaluateTags(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/tags"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateFile(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1avatar/post/requestBody/content/multipart~1form-data/schema/properties/file/type"); ok = false; }
  if (typeof value === "string" && !__fmt("binary", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1avatar/post/requestBody/content/multipart~1form-data/schema/properties/file/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "binary", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1avatar/post/requestBody/content/multipart~1form-data/schema/properties/file/format"); }
  return ok;
}

function evaluateId2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1avatar/post/requestBody/content/multipart~1form-data/schema/properties/id/type"); ok = false; }
  return ok;
}

export type Tags = readonly string[];
function evaluateTags(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1avatar/post/requestBody/content/multipart~1form-data/schema/properties/tags/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1avatar/post/requestBody/content/multipart~1form-data/schema/properties/tags/items/type"); ok = false; }
  return ok;
}

export interface Schema2 {
  readonly count?: number;
  readonly name?: string;
  readonly tags?: readonly string[];
}

function patchSchema2(source: Uint8Array, changes: Partial<Schema2>, removals?: ReadonlyArray<"count" | "name" | "tags">, arrays?: Schema2ArrayOps): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["count"] !== undefined) { targets.push({ name: enc.encode("count"), content: enc.encode(JSON.stringify(changes["count"])), vbs: -1, vbe: -1 }); }
  if (changes["name"] !== undefined) { targets.push({ name: enc.encode("name"), content: enc.encode(JSON.stringify(changes["name"])), vbs: -1, vbe: -1 }); }
  if (changes["tags"] !== undefined) { targets.push({ name: enc.encode("tags"), content: enc.encode(JSON.stringify(changes["tags"])), vbs: -1, vbe: -1 }); }
  const arrayEdits: RmwArrayEdit[] = [];
  if (arrays !== undefined && arrays["tags"] !== undefined) { arrayEdits.push({ name: enc.encode("tags"), ops: arrays["tags"]! as RmwArrayOps, prefixLen: 0 }); }
  if ((removals !== undefined && removals.length > 0) || arrayEdits.length > 0) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, arrayEdits);
  }
  return rmwUpsert(source, targets);
}

export interface Schema2ArrayOps {
  readonly tags?: ListOps<string>;
}

function buildSchema2(props: Schema2): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalSchema2(props: Schema2): Uint8Array {
  return canonicalize(props);
}

function produceSchema2(source: Uint8Array, recipe: (draft: Draft<Schema2>) => void): Uint8Array {
  return produce<Schema2>(source, recipe);
}

function evaluateSchema2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1form/post/requestBody/content/application~1x-www-form-urlencoded/schema/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "count") { if (!evaluateCount(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/count"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "name") { if (!evaluateName3(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/name"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "tags") { if (!evaluateTags2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/tags"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateCount(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1form/post/requestBody/content/application~1x-www-form-urlencoded/schema/properties/count/type"); ok = false; }
  return ok;
}

function evaluateName3(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1form/post/requestBody/content/application~1x-www-form-urlencoded/schema/properties/name/type"); ok = false; }
  return ok;
}

export type Tags2 = readonly string[];
function evaluateTags2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1form/post/requestBody/content/application~1x-www-form-urlencoded/schema/properties/tags/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems2(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1form/post/requestBody/content/application~1x-www-form-urlencoded/schema/properties/tags/items/type"); ok = false; }
  return ok;
}

export type Schema3 = Brand<string, "date-time">;
function fromSchema3(value: string): Schema3 { if (!__fmt("date-time", value)) { throw new FormatError("date-time"); } return value as Schema3; }
function schema3ToTemporal(value: Schema3): Temporal.Instant { return toInstant(value); }

function evaluateSchema3(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Expires-At/schema/type"); ok = false; }
  if (typeof value === "string" && !__fmt("date-time", value)) { if (r === null) return false; r.fail(kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Expires-At/schema/format"); ok = false; }
  if (r !== null && r.verbose && ok) { r.annotate("format", "date-time", kl + "/format", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Expires-At/schema/format"); }
  return ok;
}

function evaluateSchema4(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!((__isNum(value) && __isInt(String(value))))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Rate-Limit/schema/type"); ok = false; }
  return ok;
}

function evaluateSchema5(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Request-Id/schema/type"); ok = false; }
  return ok;
}

export interface Schema6 {
  readonly kind?: string;
  readonly region?: string;
}

function patchSchema6(source: Uint8Array, changes: Partial<Schema6>, removals?: ReadonlyArray<"kind" | "region">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["kind"] !== undefined) { targets.push({ name: enc.encode("kind"), content: enc.encode(JSON.stringify(changes["kind"])), vbs: -1, vbe: -1 }); }
  if (changes["region"] !== undefined) { targets.push({ name: enc.encode("region"), content: enc.encode(JSON.stringify(changes["region"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildSchema6(props: Schema6): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalSchema6(props: Schema6): Uint8Array {
  return canonicalize(props);
}

function produceSchema6(source: Uint8Array, recipe: (draft: Draft<Schema6>) => void): Uint8Array {
  return produce<Schema6>(source, recipe);
}

function evaluateSchema6(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Scope/schema/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "kind") { if (!evaluateKind(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/kind"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "region") { if (!evaluateRegion(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/region"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateKind(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Scope/schema/properties/kind/type"); ok = false; }
  return ok;
}

function evaluateRegion(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Scope/schema/properties/region/type"); ok = false; }
  return ok;
}

export type Schema7 = readonly string[];
function evaluateSchema7(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Tags/schema/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems3(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems3(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1limits/get/responses/200/headers/X-Tags/schema/items/type"); ok = false; }
  return ok;
}

export type Schema8 = readonly Pet[];
function evaluateSchema8(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets/get/responses/200/content/application~1json/schema/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluatePet(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items/$ref"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateSchema9(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/get/parameters/0/schema/type"); ok = false; }
  return ok;
}

function evaluateSchema10(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/0/schema/type"); ok = false; }
  return ok;
}

export type Schema11 = readonly string[];
function evaluateSchema11(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/1/schema/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems4(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems4(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/1/schema/items/type"); ok = false; }
  return ok;
}

function evaluateSchema12(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "boolean")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/2/schema/type"); ok = false; }
  return ok;
}

function evaluateSchema13(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1pets~1{petId}/post/parameters/3/schema/type"); ok = false; }
  return ok;
}

export interface Schema14 {
  readonly kind?: string;
  readonly region?: string;
}

function patchSchema14(source: Uint8Array, changes: Partial<Schema14>, removals?: ReadonlyArray<"kind" | "region">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["kind"] !== undefined) { targets.push({ name: enc.encode("kind"), content: enc.encode(JSON.stringify(changes["kind"])), vbs: -1, vbe: -1 }); }
  if (changes["region"] !== undefined) { targets.push({ name: enc.encode("region"), content: enc.encode(JSON.stringify(changes["region"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildSchema14(props: Schema14): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalSchema14(props: Schema14): Uint8Array {
  return canonicalize(props);
}

function produceSchema14(source: Uint8Array, recipe: (draft: Draft<Schema14>) => void): Uint8Array {
  return produce<Schema14>(source, recipe);
}

function evaluateSchema14(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/0/schema/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "kind") { if (!evaluateKind2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/kind"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "region") { if (!evaluateRegion2(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/region"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateKind2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/0/schema/properties/kind/type"); ok = false; }
  return ok;
}

function evaluateRegion2(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/0/schema/properties/region/type"); ok = false; }
  return ok;
}

export type Schema15 = readonly string[];
function evaluateSchema15(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/1/schema/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems5(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems5(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/1/schema/items/type"); ok = false; }
  return ok;
}

export type Schema16 = readonly string[];
function evaluateSchema16(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/2/schema/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems6(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems6(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/2/schema/items/type"); ok = false; }
  return ok;
}

export interface Schema17 {
  readonly max?: string;
  readonly min?: string;
}

function patchSchema17(source: Uint8Array, changes: Partial<Schema17>, removals?: ReadonlyArray<"max" | "min">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["max"] !== undefined) { targets.push({ name: enc.encode("max"), content: enc.encode(JSON.stringify(changes["max"])), vbs: -1, vbe: -1 }); }
  if (changes["min"] !== undefined) { targets.push({ name: enc.encode("min"), content: enc.encode(JSON.stringify(changes["min"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildSchema17(props: Schema17): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalSchema17(props: Schema17): Uint8Array {
  return canonicalize(props);
}

function produceSchema17(source: Uint8Array, recipe: (draft: Draft<Schema17>) => void): Uint8Array {
  return produce<Schema17>(source, recipe);
}

function evaluateSchema17(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/3/schema/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "max") { if (!evaluateMax(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/max"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "min") { if (!evaluateMin(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/min"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateMax(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/3/schema/properties/max/type"); ok = false; }
  return ok;
}

function evaluateMin(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/3/schema/properties/min/type"); ok = false; }
  return ok;
}

export type Schema18 = readonly string[];
function evaluateSchema18(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/4/schema/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems7(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems7(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/4/schema/items/type"); ok = false; }
  return ok;
}

export interface Schema19 {
  readonly dir?: string;
  readonly sort?: string;
}

function patchSchema19(source: Uint8Array, changes: Partial<Schema19>, removals?: ReadonlyArray<"dir" | "sort">): Uint8Array {
  const enc = new TextEncoder();
  const targets: RmwTarget[] = [];
  if (changes["dir"] !== undefined) { targets.push({ name: enc.encode("dir"), content: enc.encode(JSON.stringify(changes["dir"])), vbs: -1, vbe: -1 }); }
  if (changes["sort"] !== undefined) { targets.push({ name: enc.encode("sort"), content: enc.encode(JSON.stringify(changes["sort"])), vbs: -1, vbe: -1 }); }
  if ((removals !== undefined && removals.length > 0)) {
    const removeNames: Uint8Array[] = [];
    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }
    return rmwProduceFull(source, targets, removeNames, []);
  }
  return rmwUpsert(source, targets);
}

function buildSchema19(props: Schema19): Uint8Array {
  return new TextEncoder().encode(JSON.stringify(props));
}

function buildCanonicalSchema19(props: Schema19): Uint8Array {
  return canonicalize(props);
}

function produceSchema19(source: Uint8Array, recipe: (draft: Draft<Schema19>) => void): Uint8Array {
  return produce<Schema19>(source, recipe);
}

function evaluateSchema19(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(__isObj(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/5/schema/type"); ok = false; }
  if (__isObj(value)) {
    const o = value as Record<string, unknown>;
    let i = -1;
    for (const k in o) {
      i++;
      if (k === "dir") { if (!evaluateDir(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/dir"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
      else if (k === "sort") { if (!evaluateSort(o[k], NOEV, (r === null ? il : il + "/" + __ptr(k)), (r === null ? kl : kl + "/properties/sort"), r)) { if (r === null) return false; ok = false; } ev.markProp(i); }
    }
  }
  return ok;
}

function evaluateDir(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/5/schema/properties/dir/type"); ok = false; }
  return ok;
}

function evaluateSort(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/5/schema/properties/sort/type"); ok = false; }
  return ok;
}

function evaluateSchema20(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/6/schema/type"); ok = false; }
  return ok;
}

export type Schema21 = readonly string[];
function evaluateSchema21(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(Array.isArray(value))) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/7/schema/type"); ok = false; }
  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!evaluateItems8(value[i], NOEV, (r === null ? il : il + "/" + i), (r === null ? kl : kl + "/items"), r)) { if (r === null) return false; ok = false; } ev.markItem(i); } }
  return ok;
}

function evaluateItems8(value: unknown, ev: Ev, il: string = "", kl: string = "", r: Results | null = null): boolean {
  let ok = true;
  if (!(typeof value === "string")) { if (r === null) return false; r.fail(kl + "/type", il, "/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/ts-codegen-design/docs/typescript/openapi-examples/petstore-3.0/openapi.json#/paths/~1search~1{scope}/get/parameters/7/schema/items/type"); ok = false; }
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
  parse: (v: Uint8Array | string): Schema => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema,
  build: buildSchema,
  buildCanonical: buildCanonicalSchema,
  patch: patchSchema,
  produce: produceSchema,
  applyPatch: (doc: Uint8Array | Schema, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Schema, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Schema, target: Uint8Array | Schema): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Schema, target: Uint8Array | Schema): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const File = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateFile(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Id2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateId2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Tags = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateTags(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Tags => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Tags,
};
export const Items = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema2 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema2,
  build: buildSchema2,
  buildCanonical: buildCanonicalSchema2,
  patch: patchSchema2,
  produce: produceSchema2,
  applyPatch: (doc: Uint8Array | Schema2, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Schema2, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Schema2, target: Uint8Array | Schema2): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Schema2, target: Uint8Array | Schema2): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Count = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateCount(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Name3 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateName3(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Tags2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateTags2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Tags2 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Tags2,
};
export const Items2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema3 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema3(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema3 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema3,
  from: fromSchema3,
  toTemporal: schema3ToTemporal,
};
export const Schema4 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema4(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema5 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema5(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema6 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema6(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema6 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema6,
  build: buildSchema6,
  buildCanonical: buildCanonicalSchema6,
  patch: patchSchema6,
  produce: produceSchema6,
  applyPatch: (doc: Uint8Array | Schema6, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Schema6, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Schema6, target: Uint8Array | Schema6): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Schema6, target: Uint8Array | Schema6): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Kind = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateKind(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Region = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateRegion(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema7 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema7(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema7 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema7,
};
export const Items3 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems3(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema8 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema8(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema8 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema8,
};
export const Schema9 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema9(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema10 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema10(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema11 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema11(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema11 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema11,
};
export const Items4 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems4(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema12 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema12(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema13 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema13(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema14 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema14(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema14 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema14,
  build: buildSchema14,
  buildCanonical: buildCanonicalSchema14,
  patch: patchSchema14,
  produce: produceSchema14,
  applyPatch: (doc: Uint8Array | Schema14, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Schema14, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Schema14, target: Uint8Array | Schema14): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Schema14, target: Uint8Array | Schema14): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Kind2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateKind2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Region2 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateRegion2(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema15 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema15(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema15 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema15,
};
export const Items5 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems5(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema16 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema16(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema16 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema16,
};
export const Items6 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems6(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema17 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema17(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema17 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema17,
  build: buildSchema17,
  buildCanonical: buildCanonicalSchema17,
  patch: patchSchema17,
  produce: produceSchema17,
  applyPatch: (doc: Uint8Array | Schema17, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Schema17, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Schema17, target: Uint8Array | Schema17): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Schema17, target: Uint8Array | Schema17): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Max = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateMax(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Min = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateMin(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema18 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema18(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema18 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema18,
};
export const Items7 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems7(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema19 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema19(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema19 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema19,
  build: buildSchema19,
  buildCanonical: buildCanonicalSchema19,
  patch: patchSchema19,
  produce: produceSchema19,
  applyPatch: (doc: Uint8Array | Schema19, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch)),
  applyMergePatch: (doc: Uint8Array | Schema19, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch)),
  createPatch: (source: Uint8Array | Schema19, target: Uint8Array | Schema19): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
  createMergePatch: (source: Uint8Array | Schema19, target: Uint8Array | Schema19): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target),
};
export const Dir = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateDir(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Sort = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSort(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema20 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema20(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
export const Schema21 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateSchema21(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
  parse: (v: Uint8Array | string): Schema21 => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as Schema21,
};
export const Items8 = {
  evaluate: (v: unknown, results?: Results): boolean => evaluateItems8(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), "", "", results ?? null),
};
