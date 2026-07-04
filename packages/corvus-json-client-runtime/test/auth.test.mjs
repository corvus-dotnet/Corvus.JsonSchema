// Unit tests for the auth providers (run with `node --test`), against the BUILT dist.
import { test } from "node:test";
import assert from "node:assert/strict";

import { bearerToken, apiKey, basicAuth } from "../dist/index.js";

// Minimal WireRequest fixture (only the fields the providers touch).
function wire(url = "https://api.example.com/v1/pets") {
  return { method: "GET", url, headers: new Headers(), body: { kind: "none" }, signal: AbortSignal.timeout(1000) };
}

test("bearerToken sets Authorization from a static token and from an async factory", async () => {
  const staticReq = wire();
  bearerToken("abc123").authenticate(staticReq, staticReq.signal);
  assert.equal(staticReq.headers.get("Authorization"), "Bearer abc123");

  const factoryReq = wire();
  let calls = 0;
  await bearerToken(async () => { calls++; return "fresh-token"; }).authenticate(factoryReq, factoryReq.signal);
  assert.equal(factoryReq.headers.get("Authorization"), "Bearer fresh-token");
  assert.equal(calls, 1);
});

test("apiKey places the key in header / query / cookie", () => {
  const h = wire();
  apiKey("KEY", "X-Api-Key", "header").authenticate(h, h.signal);
  assert.equal(h.headers.get("X-Api-Key"), "KEY");

  // Query: appended with the right separator and RFC 3986 encoding (space -> %20, not +).
  const q1 = wire("https://api.example.com/v1/pets");
  apiKey("a b", "api key", "query").authenticate(q1, q1.signal);
  assert.equal(q1.url, "https://api.example.com/v1/pets?api%20key=a%20b");
  const q2 = wire("https://api.example.com/v1/pets?limit=10");
  apiKey("KEY", "token", "query").authenticate(q2, q2.signal);
  assert.equal(q2.url, "https://api.example.com/v1/pets?limit=10&token=KEY");

  // Cookie: appended to an existing Cookie header.
  const c = wire();
  c.headers.set("Cookie", "session=1");
  apiKey("KEY", "auth", "cookie").authenticate(c, c.signal);
  assert.equal(c.headers.get("Cookie"), "session=1; auth=KEY");
});

test("basicAuth base64-encodes username:password at the UTF-8 byte level", () => {
  const r = wire();
  basicAuth("Aladdin", "open sesame").authenticate(r, r.signal);
  // RFC 7617 canonical example.
  assert.equal(r.headers.get("Authorization"), "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==");
});
