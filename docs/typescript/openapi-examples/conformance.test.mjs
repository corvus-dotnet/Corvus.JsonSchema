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

    // Cookies compose into a single `Cookie` header value, exactly as a real transport would. Mirror
    // the path/query write contract: writeCookies streams the cookie pairs into a ByteWriter.
    if (request.hasCookieParameters) {
      const cookieWriter = new ByteWriter();
      const written = request.writeCookies(cookieWriter);
      if (written > 0) {
        headers.append("Cookie", decoder.decode(cookieWriter.written));
      }
    }

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

  test(`${version}: search composes the full parameter-style matrix`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""));
    const client = new ApiStatusClient(transport);

    await client.search({
      scope: { kind: "pet", region: "eu" }, // matrix object, explode=false -> ;scope=kind,pet,region,eu.
      tags: ["a", "b c"], // spaceDelimited array, explode=false -> tags=a%20b%20c.
      codes: ["x", "y"], // pipeDelimited array, explode=false -> codes=x%7Cy.
      filter: { min: "1", max: "9" }, // deepObject, explode=true -> filter[min]=1&filter[max]=9 (brackets %5B/%5D).
      fields: ["a", "b"], // form array, explode=true -> fields=a&fields=b.
      opts: { sort: "name", dir: "asc" }, // form object, explode=true -> sort=name&dir=asc.
      session: "abc123", // form cookie scalar -> session=abc123.
      xTags: ["t1", "t2"], // simple header array -> t1,t2.
    });

    const wire = transport.captured;

    // Method.
    assert.equal(wire.method, "GET");

    // URL: base + matrix-object path + the query params in declaration order (tags, codes, filter,
    // fields, opts) with each style's exact composition (these are the verified locked-runtime outputs).
    assert.equal(
      wire.url,
      "https://api.example.com/v1/search/;scope=kind,pet,region,eu" +
        "?tags=a%20b%20c&codes=x%7Cy&filter%5Bmin%5D=1&filter%5Bmax%5D=9&fields=a&fields=b&sort=name&dir=asc",
    );

    // The form cookie scalar composes into the Cookie header.
    assert.equal(wire.headers.get("Cookie"), "session=abc123");

    // The simple header array is comma-joined, verbatim (header values are not percent-encoded).
    assert.equal(wire.headers.get("X-Tags"), "t1,t2");

    // The Accept header for the JSON response.
    assert.equal(wire.headers.get("Accept"), "application/json");
  });
}
