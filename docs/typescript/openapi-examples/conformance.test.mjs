// Conformance test for the generated TypeScript OpenAPI client (run with `node --test` AFTER `tsc`,
// which compiles the recipes to ./dist). It constructs the generated `ApiStatusClient` with a
// MockApiTransport that composes the wire request exactly as a real transport would (calling the
// request's write* methods synchronously and assembling base URL + resolved path + query + headers),
// invokes `updatePet` with known params + body, and asserts the composed method, URL, headers, and
// body bytes are exactly right.
//
// Run for every recipe version (3.0 / 3.1 / 3.2) — the wire output must be identical across versions.
import { test } from "node:test";
import assert from "node:assert/strict";

import { ByteWriter } from "@endjin/corvus-json-client-runtime";

const VERSIONS = ["petstore-3.0", "petstore-3.1", "petstore-3.2"];
const decoder = new TextDecoder();

// A transport that records the composed wire request and returns a canned 200 JSON response. It mirrors
// the byte-native composition contract: writeResolvedPath + writeQueryString build the URL path/query
// into a ByteWriter; writeHeaders streams header name/value pairs; the body is taken from the supplied
// RequestBody. The base URL is the client's static serverUri().
class MockApiTransport {
  constructor(baseUrl) {
    this.baseUrl = baseUrl;
    this.captured = undefined;
  }

  async send(request, factory, body, _signal) {
    const pathWriter = new ByteWriter();
    request.writeResolvedPath(pathWriter);
    const path = decoder.decode(pathWriter.written);

    let query = "";
    if (request.hasQueryParameters) {
      const queryWriter = new ByteWriter();
      const written = request.writeQueryString(queryWriter);
      if (written > 0) {
        query = "?" + decoder.decode(queryWriter.written);
      }
    }

    const headers = new Headers();
    request.writeHeaders((name, value) => headers.append(name, value));

    const url = this.baseUrl + path + query;
    this.captured = {
      method: methodName(request.method),
      url,
      headers,
      body: body ?? { kind: "none" },
    };

    // Return a canned 200 JSON body via the generated response factory.
    const responseBytes = new TextEncoder().encode(
      JSON.stringify({ id: "p-1", name: "Rex", tag: "dog" }),
    );
    const responseStream = new ReadableStream({
      start(controller) {
        controller.enqueue(responseBytes);
        controller.close();
      },
    });
    return factory.create({
      statusCode: 200,
      body: responseStream,
      contentType: "application/json",
      headers: { tryGet: () => undefined },
      transport: this,
    });
  }

  async [Symbol.asyncDispose]() {
    /* nothing to dispose */
  }
}

// OperationMethod (a const enum-like) -> the wire verb. The generated request carries the numeric
// enum value; map the small set used by these recipes.
function methodName(method) {
  // OperationMethod: Get=0, Put=1, Post=2, Delete=3, ... (see contracts/operation-method.ts).
  return ["GET", "PUT", "POST", "DELETE", "OPTIONS", "HEAD", "PATCH", "TRACE", "QUERY"][method] ?? String(method);
}

for (const version of VERSIONS) {
  test(`${version}: updatePet composes the exact wire request`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);
    const { PetUpdate } = await import(`./conformance/dist/${version}/client/models/generated.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""));
    const client = new ApiStatusClient(transport);

    const response = await client.updatePet(
      {
        petId: "p 1", // a space proves path percent-encoding (-> %20).
        tags: ["dog", "good boy"], // form, explode=false -> tags=dog,good%20boy.
        verbose: true, // form scalar -> verbose=true.
        xRequestId: "req-42", // simple header -> verbatim.
      },
      { name: "Rex", tag: "dog" }, // JSON body.
    );

    const wire = transport.captured;

    // Method.
    assert.equal(wire.method, "POST");

    // URL: base + resolved path (petId percent-encoded) + query (tags array form non-explode, then
    // verbose scalar; the comma array-delimiter is literal, the space in "good boy" is %20).
    assert.equal(
      wire.url,
      "https://api.example.com/v1/pets/p%201?tags=dog,good%20boy&verbose=true",
    );

    // Headers: the Accept header for the JSON responses, plus the simple X-Request-Id header verbatim.
    assert.equal(wire.headers.get("Accept"), "application/json");
    assert.equal(wire.headers.get("X-Request-Id"), "req-42");

    // Body: bytes built via the model companion `PetUpdate.build` (canonical JSON of the supplied body).
    assert.equal(wire.body.kind, "bytes");
    assert.equal(wire.body.contentType, "application/json");
    const expectedBody = PetUpdate.build({ name: "Rex", tag: "dog" });
    assert.deepEqual(Array.from(wire.body.content), Array.from(expectedBody));
    // And it round-trips to the supplied object.
    assert.deepEqual(JSON.parse(decoder.decode(wire.body.content)), { name: "Rex", tag: "dog" });

    // The response decodes via the generated factory + model companion.
    const pet = response.tryGetOk();
    assert.deepEqual(pet, { id: "p-1", name: "Rex", tag: "dog" });
  });
}
