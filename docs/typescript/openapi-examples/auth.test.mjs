// Authentication conformance test for the generated TypeScript OpenAPI client (run with `node --test`
// AFTER `tsc` compiles the recipes to ./conformance/dist). It wires the `bearerToken` provider from the
// client runtime into the shared MockApiTransport — which applies the provider exactly where the real
// transport does (after the wire request is built, before dispatch) — invokes an operation, and asserts
// the request carried `Authorization: Bearer <token>`. It also reads the spec-derived scope union so a
// token factory can request the correct OAuth2 scopes without a hardcoded string.
import { test } from "node:test";
import assert from "node:assert/strict";

import { bearerToken } from "@endjin/corvus-json-client-runtime";
import { MockApiTransport } from "./mock-transport.mjs";

// The 3.2 recipe is the one that declares `securitySchemes`, so it is the one that emits the scope
// constants (the 3.0/3.1 specs in this suite declare no security schemes).
const CLIENT = "./conformance/dist/petstore-3.2/client/ApiStatusClient.js";

test("bearerToken(async factory) sets Authorization: Bearer on the outgoing request", async () => {
  const { ApiStatusClient } = await import(CLIENT);

  const base = ApiStatusClient.serverUri().toString().replace(/\/$/, "");
  const transport = new MockApiTransport(base, undefined, bearerToken(async () => "test-token"));
  const client = new ApiStatusClient(transport);

  await client.updatePet({ petId: "p1", xRequestId: "req-1" }, { name: "Rex", tag: "dog" });

  assert.equal(transport.captured.headers.get("Authorization"), "Bearer test-token");
});

test("bearerToken(static string) sets Authorization: Bearer on the outgoing request", async () => {
  const { ApiStatusClient } = await import(CLIENT);

  const base = ApiStatusClient.serverUri().toString().replace(/\/$/, "");
  const transport = new MockApiTransport(base, undefined, bearerToken("static-token"));
  const client = new ApiStatusClient(transport);

  await client.getStatus();

  assert.equal(transport.captured.headers.get("Authorization"), "Bearer static-token");
});

test("the generated client exposes the spec-derived OAuth2 scope union", async () => {
  const { ApiStatusClient } = await import(CLIENT);

  // The union a client-credentials / silent-token factory would request; taken from the spec, never
  // hardcoded. Per-operation subsets are exposed alongside it (e.g. updatePetOauth2Scopes).
  assert.deepEqual(ApiStatusClient.securityRequirements.allOauth2Scopes, ["read:pets", "write:pets"]);
  assert.deepEqual(ApiStatusClient.securityRequirements.updatePetOauth2Scopes, ["write:pets"]);
});