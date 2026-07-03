// FetchApiTransport round-trip coverage.
//
// The rest of the suite drives the custom MockApiTransport, so the portable FetchApiTransport — the transport
// the browser playgrounds and any browser consumer actually use (via globalThis.fetch) — was untested. A bug
// there (or a fetch-mock body/stream mismatch) would slip straight through. These tests drive the REAL
// FetchApiTransport through a globalThis.fetch stub and assert BOTH directions: the request body is serialized
// onto the wire (init.body), and the response body is decoded back into typed models.
import test from "node:test";
import assert from "node:assert/strict";
import { FetchApiTransport } from "@endjin/corvus-json-client-runtime";

const savedFetch = globalThis.fetch;
test.after(() => { globalThis.fetch = savedFetch; });

const clientCtor = async () =>
  (await import("./conformance/dist/petstore-3.0/client/ApiStatusClient.js")).ApiStatusClient;
const baseUrl = (ApiStatusClient) => ApiStatusClient.serverUri().toString().replace(/\/$/, "");

test("FetchApiTransport: response body decodes into typed models (listPets)", async () => {
  const ApiStatusClient = await clientCtor();
  const bytes = new TextEncoder().encode(JSON.stringify([{ id: "p-1", name: "Rex" }, { id: "p-2", name: "Milo" }]));
  globalThis.fetch = async () => new Response(bytes, { status: 200, headers: { "Content-Type": "application/json" } });

  const response = await new ApiStatusClient(new FetchApiTransport({ baseUrl: baseUrl(ApiStatusClient) })).listPets();
  const names = response.match({ ok: (pets) => pets.map((p) => p.name), otherwise: () => [] });
  assert.deepEqual(names, ["Rex", "Milo"]);
});

test("FetchApiTransport: request body is serialized onto the wire (updatePet)", async () => {
  const ApiStatusClient = await clientCtor();
  let wireBody = null;
  globalThis.fetch = async (_input, init) => {
    // Decode init.body exactly as a fetch-based mock (e.g. the playground's) would to echo the posted body.
    wireBody = init && init.body ? new TextDecoder().decode(init.body) : "";
    return new Response(new TextEncoder().encode("{}"), { status: 200, headers: { "Content-Type": "application/json" } });
  };

  await new ApiStatusClient(new FetchApiTransport({ baseUrl: baseUrl(ApiStatusClient) }))
    .updatePet({ petId: "p1", xRequestId: "req-1" }, { name: "Rex", tag: "dog" });
  assert.notEqual(wireBody, "", "the request body must be sent on the wire, not left empty");
  assert.deepEqual(JSON.parse(wireBody), { name: "Rex", tag: "dog" });
});