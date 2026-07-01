// C#<->TS byte-parity test (run with `node --test` AFTER `tsc`). The SAME petstore spec is generated
// as both a C# client (tests/Corvus.Text.Json.OpenApi.Parity.Tests) and a TypeScript client; the C#
// side emits a shared wire fixture (parity/wire-fixture.json) of HTTP method + operation-relative
// path+query for a fixed set of cases, and this test asserts the TS client composes the IDENTICAL wire
// for the SAME inputs. Any divergence in URI encoding (path escaping, array styles, booleans, matrix /
// deepObject) surfaces here as a byte mismatch.
//
// Target normalisation: the C# TestHarness uses a bare `http://localhost` base, so its captured
// PathAndQuery is operation-relative (no server basePath). The TS transport puts the server base
// (https://api.example.com/v1) in `baseUrl`, so we strip it from the captured URL to compare the same
// operation-relative path+query.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

import { MockApiTransport } from "./mock-transport.mjs";

const fixture = JSON.parse(readFileSync(new URL("./parity/wire-fixture.json", import.meta.url), "utf8"));

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
};

test(`parity: ${fixture.spec} C# and TS clients compose byte-identical method + target`, async () => {
  const { ApiStatusClient } = await import(`./conformance/dist/${fixture.spec}/client/ApiStatusClient.js`);
  const base = ApiStatusClient.serverUri().toString().replace(/\/$/, "");

  for (const expected of fixture.cases) {
    const invoke = INVOCATIONS[expected.name];
    assert.ok(invoke, `no TS invocation mapped for parity case '${expected.name}'`);

    const transport = new MockApiTransport(base);
    const client = new ApiStatusClient(transport);
    await invoke(client);

    // Operation-relative path + query (strip the server base the C# fixture omits).
    const target = transport.captured.url.startsWith(base)
      ? transport.captured.url.slice(base.length)
      : transport.captured.url;

    assert.equal(transport.captured.method, expected.method, `${expected.name}: HTTP method`);
    assert.equal(target, expected.target, `${expected.name}: wire target (C# fixture vs TS)`);
  }
});
