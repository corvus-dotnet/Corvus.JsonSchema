// Oracle tests for the response-header value parsers (run with `node --test` against the BUILT dist).
// These are the inverse of the request-side writeHeaderParam serializer (simple style); the rows mirror
// the OpenAPI simple-style header examples (scalar verbatim, comma-joined arrays, key,value objects).
import { test } from "node:test";
import assert from "node:assert/strict";

import {
  parseHeaderString,
  parseHeaderNumber,
  parseHeaderBoolean,
  parseHeaderArray,
  parseHeaderObject,
} from "../dist/serializers/header.js";

test("scalar parsers: string verbatim, number, boolean (lowercase 'true')", () => {
  assert.equal(parseHeaderString("req-7"), "req-7");
  assert.equal(parseHeaderNumber("100"), 100);
  assert.equal(parseHeaderNumber("-3.5"), -3.5);
  assert.equal(parseHeaderBoolean("true"), true);
  assert.equal(parseHeaderBoolean("false"), false);
});

test("array header: comma-separated elements, trimmed and per-element parsed", () => {
  assert.deepEqual(parseHeaderArray("a,b,c", parseHeaderString), ["a", "b", "c"]);
  assert.deepEqual(parseHeaderArray("1, 2, 3", parseHeaderNumber), [1, 2, 3]);
  assert.deepEqual(parseHeaderArray("", parseHeaderString), []);
});

test("object header (explode=false): flat key,value pairs", () => {
  assert.deepEqual(parseHeaderObject("role,admin,dept,eng", false), { role: "admin", dept: "eng" });
});

test("object header (explode=true): key=value pairs", () => {
  assert.deepEqual(parseHeaderObject("role=admin,dept=eng", true), { role: "admin", dept: "eng" });
});

test("object header: empty value yields an empty record", () => {
  assert.deepEqual(parseHeaderObject("", false), {});
});
