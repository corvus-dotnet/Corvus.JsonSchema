// C#<->TS byte-parity test (run with `node --test` AFTER `tsc`). The SAME petstore spec is generated
// as both a C# client (tests/Corvus.Text.Json.OpenApi.Parity.Tests) and a TypeScript client; the C#
// side emits a shared wire fixture (parity/wire-fixture.json) of HTTP method + operation-relative
// path+query + X-* request headers + request-body content-type/bytes for a fixed set of cases, and
// this test asserts the TS client composes the IDENTICAL wire for the SAME inputs. Any divergence in
// URI encoding (path escaping, array styles, booleans, matrix / deepObject), header emission, or body
// serialization surfaces here as a byte mismatch. The fixture is emitted from the 3.0 client and holds
// for every version (the request composition is version-invariant), so all three TS clients assert it.
//
// Target normalisation: the C# TestHarness uses a bare `http://localhost` base, so its captured
// PathAndQuery is operation-relative (no server basePath). The TS transport puts the server base
// (https://api.example.com/v1) in `baseUrl`, so we strip it to compare the same operation-relative
// path+query. Header names are compared case-insensitively (TS `Headers` lower-cases names).
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

import { ValidationMode } from "@endjin/corvus-json-client-runtime";

import { MockApiTransport, decoder } from "./mock-transport.mjs";

const fixture = JSON.parse(readFileSync(new URL("./parity/wire-fixture.json", import.meta.url), "utf8"));
const responseFixture = JSON.parse(
  readFileSync(new URL("./parity/response-fixture.json", import.meta.url), "utf8"),
);
const VERSIONS = ["petstore-3.0", "petstore-3.1", "petstore-3.2"];

// Each fixture case name maps to the TS client invocation using the SAME inputs the C# side used.
const INVOCATIONS = {
  "getPet-reserved-path": (client) => client.getPet({ petId: "a/b c" }),
  "updatePet-compound-query-boolean": (client) =>
    client.updatePet(
      { petId: "p 1", tags: ["a b", "a/b"], verbose: true, xRequestId: "req-1" },
      { name: "Rex", tag: "dog" },
    ),
  "search-spacedelimited-deepobject": (client) =>
    client.search({
      scope: { kind: "k 1", region: "r/2" },
      tags: ["a b", "c/d"],
      filter: { min: "1", max: "2" },
    }),
  "search-full-style-matrix": (client) =>
    client.search({
      scope: { kind: "k1", region: "r1" },
      tags: ["a b", "c"],
      codes: ["c1", "c2"],
      filter: { min: "1", max: "2" },
      fields: ["f1", "f2"],
      opts: { sort: "asc", dir: "up" },
      xTags: ["t1", "t2"],
    }),
  "note-text-plain-body": (client) => client.note("hello note"),
  "form-urlencoded-body": (client) => client.form({ name: "gadget", count: 3, tags: ["a", "b"] }),
  "upload-octet-stream-body": (client) => client.upload(new TextEncoder().encode("binary-data")),
};

// The X-* request headers the TS transport captured, as a lower-cased name->value map.
function capturedXHeaders(headers) {
  const out = {};
  for (const [name, value] of headers) {
    if (name.toLowerCase().startsWith("x-")) {
      out[name.toLowerCase()] = value;
    }
  }

  return out;
}

function lowerKeys(obj) {
  const out = {};
  for (const [k, v] of Object.entries(obj)) {
    out[k.toLowerCase()] = v;
  }

  return out;
}

for (const version of VERSIONS) {
  test(`parity: ${version} TS client composes byte-identical wire to the C# ${fixture.spec} fixture`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);
    const base = ApiStatusClient.serverUri().toString().replace(/\/$/, "");

    for (const expected of fixture.cases) {
      const invoke = INVOCATIONS[expected.name];
      assert.ok(invoke, `no TS invocation mapped for parity case '${expected.name}'`);

      const transport = new MockApiTransport(base);
      const client = new ApiStatusClient(transport);
      await invoke(client);
      const wire = transport.captured;

      // Operation-relative path + query (strip the server base the C# fixture omits).
      const target = wire.url.startsWith(base) ? wire.url.slice(base.length) : wire.url;
      // Request body + content type (the JSON body is emitted as a "bytes" RequestBody; none otherwise).
      const isBytes = wire.body !== undefined && wire.body.kind === "bytes";
      const contentType = isBytes ? wire.body.contentType : null;
      const bodyUtf8 = isBytes ? decoder.decode(wire.body.content) : null;

      const label = `${version}/${expected.name}`;
      assert.equal(wire.method, expected.method, `${label}: HTTP method`);
      assert.equal(target, expected.target, `${label}: wire target (C# fixture vs TS)`);
      assert.deepEqual(capturedXHeaders(wire.headers), lowerKeys(expected.headers), `${label}: X-* headers`);
      assert.equal(contentType, expected.contentType, `${label}: request content-type`);
      assert.equal(bodyUtf8, expected.bodyUtf8, `${label}: request body bytes`);
    }
  });
}

// ── Response-decomposition parity ────────────────────────────────────────────
// Feed each client canned response bytes + headers and assert it decodes them into the SAME logical
// values the C# client produced: the matched status branch, the validation result, and the typed
// header values (proving divergence #3 is value-equivalent — C# returns model types, TS returns
// branded/interface types, but the decoded VALUES are identical).

// Each operation -> a client invocation; the response is driven entirely by the canned transport.
const RESPONSE_INVOKE = {
  getPet: (client) => client.getPet({ petId: "x" }),
  getStatus: (client) => client.getStatus(),
  limits: (client) => client.limits(),
};

// The status branch a response routes to. The generated match ignores handlers for branches it does
// not declare, so passing all three is safe; `otherwise` is the fallback key.
function matchedBranch(response) {
  return response.match({
    ok: () => "ok",
    default: () => "default",
    otherwise: () => "otherwise",
  });
}

// Whether the active body validates against its schema (Basic = boolean evaluate, throws on failure).
function validOf(response) {
  try {
    response.validate(ValidationMode.Basic);
    return true;
  } catch {
    return false;
  }
}

// The typed header getter values, keyed exactly as the C# fixture recorded them (getter stem without
// the "Header" suffix), so parity holds over whichever headers the C# generator types.
function headerValuesOf(response, keys) {
  const out = {};
  for (const key of keys) {
    const value = response[`${key}Header`];
    // A readonly array header decodes to a normal array; normalise for a structural compare.
    out[key] = Array.isArray(value) ? [...value] : value;
  }

  return out;
}

for (const version of VERSIONS) {
  test(`parity: ${version} TS client decodes responses identically to the C# ${responseFixture.spec} fixture`, async () => {
    const { ApiStatusClient } = await import(`./conformance/dist/${version}/client/ApiStatusClient.js`);
    const base = ApiStatusClient.serverUri().toString().replace(/\/$/, "");

    for (const expected of responseFixture.cases) {
      const invoke = RESPONSE_INVOKE[expected.operation];
      assert.ok(invoke, `no TS invocation mapped for response op '${expected.operation}'`);

      const transport = new MockApiTransport(base, {
        status: expected.status,
        bytes: new TextEncoder().encode(expected.bodyUtf8),
        contentType: "application/json",
        headers: expected.headers,
      });
      const response = await invoke(new ApiStatusClient(transport));

      const decoded = { matched: matchedBranch(response), valid: validOf(response) };
      if (expected.decoded.headerValues !== undefined) {
        decoded.headerValues = headerValuesOf(response, Object.keys(expected.decoded.headerValues));
      }

      assert.deepEqual(decoded, expected.decoded, `${version}/${expected.name}: decoded response`);
    }
  });
}
