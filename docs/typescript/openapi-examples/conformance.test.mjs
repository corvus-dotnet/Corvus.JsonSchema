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

import { MockApiTransport, bytes, decoder } from "./mock-transport.mjs";

const VERSIONS = ["petstore-3.0", "petstore-3.1", "petstore-3.2"];

for (const version of VERSIONS) {
  test(`${version}: listPets decodes an ARRAY response body`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
      status: 200,
      bytes: new TextEncoder().encode(JSON.stringify([{ id: "p-1", name: "Rex" }, { id: "p-2", name: "Milo" }])),
      contentType: "application/json",
    });
    const response = await new ApiStatusClient(transport).listPets();

    // The array-valued body decodes via the NAMED array type's model companion (Pets.parse) — a named
    // array type now emits a `type` alias + `parse`, so `match`/`tryGet` return a readonly Pet[].
    assert.equal(transport.captured.url, "https://api.example.com/v1/pets");
    const names = response.match({ ok: (pets) => pets.map((p) => p.name), otherwise: () => [] });
    assert.deepEqual(names, ["Rex", "Milo"]);
  });

  test(`${version}: serverUri substitutes server variables (defaults + overrides)`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    // The server URL is templated as https://{host}/{basePath}; the defaults reconstruct the base.
    assert.equal(ApiStatusClient.serverUri().toString(), "https://api.example.com/v1");
    assert.equal(
      ApiStatusClient.serverUri("staging.example.com", "v2").toString(),
      "https://staging.example.com/v2",
    );
  });

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

  test(`${version}: updatePet exposes a getPet link follower bound from $response.body`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""));
    const client = new ApiStatusClient(transport);

    const response = await client.updatePet(
      { petId: "p 1", xRequestId: "req-42" },
      { name: "Rex", tag: "dog" },
    );

    // The link binds the target's petId from $response.body#/id (the canned Pet's id "p-1") and
    // invokes getPet via the same transport.
    const followed = await response.links.getPet();

    const wire = transport.captured;
    assert.equal(wire.method, "GET");
    assert.equal(wire.url, "https://api.example.com/v1/pets/p-1");
    // The followed response decodes through the target's factory.
    assert.deepEqual(followed.tryGetOk(), { id: "p-1", name: "Rex", tag: "dog" });
  });

  test(`${version}: updatePet link followers resolve $request.* and $response.header`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
      status: 200,
      bytes: new TextEncoder().encode(JSON.stringify({ id: "p-1", name: "Rex", tag: "dog" })),
      contentType: "application/json",
      // X-Pet-Id is not a declared response header; the headers field exists because a link reads it.
      headers: { "X-Pet-Id": "p-hdr" },
    });
    const client = new ApiStatusClient(transport);

    const response = await client.updatePet(
      { petId: "p 1", xRequestId: "req-42" },
      { name: "Rex", tag: "dog" },
    );

    // $request.path.petId -> the ORIGINAL request's petId ("p 1", percent-encoded in the path).
    await response.links.getPetByPathId();
    assert.equal(transport.captured.url, "https://api.example.com/v1/pets/p%201");

    // $request.body#/name -> the request body's name ("Rex").
    await response.links.getPetByBodyName();
    assert.equal(transport.captured.url, "https://api.example.com/v1/pets/Rex");

    // $response.header.X-Pet-Id -> the response header value ("p-hdr").
    await response.links.getPetByHeader();
    assert.equal(transport.captured.url, "https://api.example.com/v1/pets/p-hdr");
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

  test(`${version}: upload sends a raw octet-stream body`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""));
    const client = new ApiStatusClient(transport);

    await client.upload(new Uint8Array([1, 2, 3, 4]));

    const wire = transport.captured;

    assert.equal(wire.method, "POST");
    assert.equal(wire.url, "https://api.example.com/v1/upload");
    // The raw bytes are sent verbatim as a "bytes" body with the octet-stream content type.
    assert.equal(wire.body.kind, "bytes");
    assert.equal(wire.body.contentType, "application/octet-stream");
    assert.deepEqual(Array.from(wire.body.content), [1, 2, 3, 4]);
  });

  test(`${version}: note sends a text/plain body`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""));
    const client = new ApiStatusClient(transport);

    await client.note("hello world");

    const wire = transport.captured;

    assert.equal(wire.method, "POST");
    assert.equal(wire.url, "https://api.example.com/v1/note");
    // The string is UTF-8 encoded into a "bytes" body with the text/plain content type.
    assert.equal(wire.body.kind, "bytes");
    assert.equal(wire.body.contentType, "text/plain");
    assert.equal(decoder.decode(wire.body.content), "hello world");
  });

  test(`${version}: form sends a form-urlencoded body`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""));
    const client = new ApiStatusClient(transport);

    await client.form({ name: "Rex", count: 3, tags: ["a", "b"] });

    const wire = transport.captured;

    assert.equal(wire.method, "POST");
    assert.equal(wire.url, "https://api.example.com/v1/form");
    // The object is serialized as form-urlencoded with the default form/explode=true encoding: scalars
    // as name=value, the array exploded into repeated pairs.
    assert.equal(wire.body.kind, "bytes");
    assert.equal(wire.body.contentType, "application/x-www-form-urlencoded");
    assert.equal(decoder.decode(wire.body.content), "name=Rex&count=3&tags=a&tags=b");
  });

  test(`${version}: avatar sends a multipart/form-data body`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""));
    const client = new ApiStatusClient(transport);

    // body = the non-binary fields; the binary `file` part is a separate, hoisted parameter.
    await client.avatar(
      { id: "p1", tags: ["x", "y"] },
      { content: new Uint8Array([1, 2, 3]), filename: "a.png", contentType: "image/png" },
    );

    const wire = transport.captured;

    assert.equal(wire.method, "POST");
    assert.equal(wire.url, "https://api.example.com/v1/avatar");
    assert.equal(wire.body.kind, "writer");

    // The boundary is minted at runtime; recover it from the content type and reconstruct the framing.
    const prefix = "multipart/form-data; boundary=";
    assert.ok(wire.body.contentType.startsWith(prefix));
    const b = wire.body.contentType.slice(prefix.length);

    assert.deepEqual(
      Array.from(wire.body.content),
      Array.from(
        bytes(
          `--${b}\r\nContent-Disposition: form-data; name="id"\r\n\r\np1\r\n`,
          `--${b}\r\nContent-Disposition: form-data; name="tags"\r\nContent-Type: application/json\r\n\r\n["x","y"]\r\n`,
          `--${b}\r\nContent-Disposition: form-data; name="file"; filename="a.png"\r\nContent-Type: image/png\r\n\r\n`,
          new Uint8Array([1, 2, 3]),
          `\r\n--${b}--\r\n`,
        ),
      ),
    );
  });

  // multipart/mixed (prefixEncoding / itemEncoding) is an OpenAPI 3.2 feature; only that spec defines it.
  if (version === "petstore-3.2") {
    test(`${version}: batch sends a multipart/mixed body`, async () => {
      const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

      const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""));
      const client = new ApiStatusClient(transport);

      // A homogeneous (itemEncoding) batch: each item is framed as an application/json part.
      await client.batch([
        { name: "A", tag: "x" },
        { name: "B", tag: "y" },
      ]);

      const wire = transport.captured;

      assert.equal(wire.method, "POST");
      assert.equal(wire.url, "https://api.example.com/v1/batch");
      assert.equal(wire.body.kind, "writer");

      const prefix = "multipart/mixed; boundary=";
      assert.ok(wire.body.contentType.startsWith(prefix));
      const b = wire.body.contentType.slice(prefix.length);
      assert.match(b, /^----CorvusBoundary[0-9a-f]{32}$/);

      assert.equal(
        decoder.decode(wire.body.content),
        `--${b}\r\nContent-Type: application/json\r\n\r\n{"name":"A","tag":"x"}\r\n` +
          `--${b}\r\nContent-Type: application/json\r\n\r\n{"name":"B","tag":"y"}\r\n` +
          `--${b}--\r\n`,
      );
    });

    test(`${version}: events streams Server-Sent Events as parsed items`, async () => {
      const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

      const sse =
        'data: {"id":"p-1","name":"Rex"}\n\nevent: update\ndata: {"id":"p-2","name":"Milo"}\n\n';
      const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
        status: 200,
        bytes: new TextEncoder().encode(sse),
        contentType: "text/event-stream",
      });
      const response = await new ApiStatusClient(transport).events();

      const items = [];
      for await (const pet of response.enumerateOkItems()) {
        items.push(pet);
      }

      assert.equal(transport.captured.url, "https://api.example.com/v1/events");
      assert.deepEqual(items, [
        { id: "p-1", name: "Rex" },
        { id: "p-2", name: "Milo" },
      ]);
    });

    test(`${version}: events exposes SSE event metadata via enumerateOkSseItems`, async () => {
      const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

      const sse =
        'data: {"id":"p-1","name":"Rex"}\n\nevent: update\ndata: {"id":"p-2","name":"Milo"}\n\n';
      const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
        status: 200,
        bytes: new TextEncoder().encode(sse),
        contentType: "text/event-stream",
      });
      const response = await new ApiStatusClient(transport).events();

      const events = [];
      for await (const ev of response.enumerateOkSseItems()) {
        events.push(ev);
      }

      assert.deepEqual(events, [
        { data: { id: "p-1", name: "Rex" } },
        { data: { id: "p-2", name: "Milo" }, event: "update" },
      ]);
    });

    test(`${version}: feed streams newline-delimited JSON as parsed items`, async () => {
      const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

      const ndjson = '{"id":"p-1"}\n{"id":"p-2"}\n{"id":"p-3"}\n';
      const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
        status: 200,
        bytes: new TextEncoder().encode(ndjson),
        contentType: "application/x-ndjson",
      });
      const response = await new ApiStatusClient(transport).feed();

      const items = [];
      for await (const pet of response.enumerateOkItems()) {
        items.push(pet);
      }

      assert.equal(transport.captured.url, "https://api.example.com/v1/feed");
      assert.deepEqual(items, [{ id: "p-1" }, { id: "p-2" }, { id: "p-3" }]);
    });
  }

  test(`${version}: exposes spec-derived security constants (securitySchemes / securityRequirements)`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    // The spec-derived security constants are now emitted for every OpenAPI version (3.0/3.1/3.2); the
    // three petstore specs declare identical securitySchemes / security, so the generated constants are
    // identical across versions.
    //
    // securitySchemes mirrors the C# SecuritySchemes nested class as a camelCased flat `as const`
    // object: one member per present field per scheme (same conditionals as C#).
    assert.equal(ApiStatusClient.securitySchemes.oauth2Name, "oauth2");
    assert.equal(ApiStatusClient.securitySchemes.oauth2Type, "oauth2");
    assert.equal(ApiStatusClient.securitySchemes.oauth2TokenUrl, "https://auth.example.com/token");
    assert.equal(
      ApiStatusClient.securitySchemes.oauth2AuthorizationUrl,
      "https://auth.example.com/authorize",
    );
    // The available-scope union is deduped and ordinal-sorted across every flow.
    assert.deepEqual(ApiStatusClient.securitySchemes.oauth2AvailableScopes, ["read:pets", "write:pets"]);
    // The apiKey scheme surfaces its name + location.
    assert.equal(ApiStatusClient.securitySchemes.apiKeyName, "apiKey");
    assert.equal(ApiStatusClient.securitySchemes.apiKeyType, "apiKey");
    assert.equal(ApiStatusClient.securitySchemes.apiKeyKeyName, "X-API-Key");
    assert.equal(ApiStatusClient.securitySchemes.apiKeyKeyLocation, "header");

    // securityRequirements mirrors the C# SecurityRequirements nested class: per-operation
    // `{method}{Scheme}Scopes` plus a deduped, ordinal-sorted `all{Scheme}Scopes` union.
    assert.deepEqual(ApiStatusClient.securityRequirements.getStatusOauth2Scopes, ["read:pets"]);
    assert.deepEqual(ApiStatusClient.securityRequirements.getPetOauth2Scopes, ["read:pets"]);
    assert.deepEqual(ApiStatusClient.securityRequirements.updatePetOauth2Scopes, ["write:pets"]);
    assert.deepEqual(ApiStatusClient.securityRequirements.allOauth2Scopes, ["read:pets", "write:pets"]);
  });

  test(`${version}: download decodes a raw octet-stream response body`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
      status: 200,
      bytes: new Uint8Array([5, 6, 7, 8]),
      contentType: "application/octet-stream",
    });
    const client = new ApiStatusClient(transport);

    const response = await client.download();

    assert.equal(transport.captured.method, "GET");
    assert.equal(transport.captured.url, "https://api.example.com/v1/download");
    // The 200 accessor returns the raw response bytes verbatim (no model companion).
    assert.deepEqual(Array.from(response.tryGetOk()), [5, 6, 7, 8]);
    // match() routes the bytes to the ok handler.
    const viaMatch = response.match({ ok: (body) => Array.from(body) });
    assert.deepEqual(viaMatch, [5, 6, 7, 8]);
  });

  test(`${version}: ping decodes a text/plain response body`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
      status: 200,
      bytes: new TextEncoder().encode("pong"),
      contentType: "text/plain",
    });
    const client = new ApiStatusClient(transport);

    const response = await client.ping();

    assert.equal(transport.captured.method, "GET");
    assert.equal(transport.captured.url, "https://api.example.com/v1/ping");
    // The 200 accessor decodes the bytes to a string.
    assert.equal(response.tryGetOk(), "pong");
    assert.equal(
      response.match({ ok: (body) => body.toUpperCase() }),
      "PONG",
    );
  });

  test(`${version}: limits exposes typed response headers`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
      status: 200,
      bytes: new TextEncoder().encode(JSON.stringify({ id: "p-1", name: "Rex", tag: "dog" })),
      contentType: "application/json",
      headers: {
        "X-Rate-Limit": "100",
        "X-Request-Id": "req-7",
        "X-Tags": "a,b,c",
        "X-Expires-At": "2026-01-02T03:04:05Z",
        "X-Scope": "kind,read,region,eu",
      },
    });
    const client = new ApiStatusClient(transport);

    const response = await client.limits();

    assert.equal(transport.captured.method, "GET");
    assert.equal(transport.captured.url, "https://api.example.com/v1/limits");
    // Scalar integer header parses to a number; string header is verbatim; array header splits on comma.
    assert.equal(response.xRateLimitHeader, 100);
    assert.equal(response.xRequestIdHeader, "req-7");
    assert.deepEqual(response.xTagsHeader, ["a", "b", "c"]);
    // A date-time header returns the schema's BRANDED model type (Brand<string,"date-time">, with a
    // {Name}ToTemporal accessor available on the model), not a lossy plain string.
    assert.equal(response.xExpiresAtHeader, "2026-01-02T03:04:05Z");
    // An object header returns the schema's typed interface, not a loose Record<string,string>.
    assert.deepEqual(response.xScopeHeader, { kind: "read", region: "eu" });
    // The body still decodes via the JSON model companion.
    assert.deepEqual(response.tryGetOk(), { id: "p-1", name: "Rex", tag: "dog" });
  });

  test(`${version}: limits header getter validates via the model and throws on a malformed value`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);

    const transport = new MockApiTransport(ApiStatusClient.serverUri().toString().replace(/\/$/, ""), {
      status: 200,
      bytes: new TextEncoder().encode(JSON.stringify({ id: "p-1", name: "Rex", tag: "dog" })),
      contentType: "application/json",
      // "3.5" is a valid number but not an integer; "nope" is not an RFC 3339 date-time. Both parse
      // to their scalar value but fail the header schema's model validation.
      headers: { "X-Rate-Limit": "3.5", "X-Expires-At": "nope" },
    });
    const client = new ApiStatusClient(transport);
    const response = await client.limits();

    // The getter validates the parsed value against the header schema's model (evaluate) and throws.
    assert.throws(() => response.xRateLimitHeader, /X-Rate-Limit response header failed schema validation/);
    assert.throws(() => response.xExpiresAtHeader, /X-Expires-At response header failed schema validation/);
  });
}
