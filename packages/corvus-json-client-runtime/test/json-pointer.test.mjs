// Oracle tests for RFC 6901 JSON Pointer evaluation (run with `node --test` against the BUILT dist).
import { test } from "node:test";
import assert from "node:assert/strict";

import { getByPointer } from "../dist/serializers/json-pointer.js";

test("navigates object and nested keys", () => {
  assert.equal(getByPointer({ id: "p-1" }, "/id"), "p-1");
  assert.equal(getByPointer({ a: { b: 2 } }, "/a/b"), 2);
});

test("indexes into arrays by numeric segment", () => {
  assert.equal(getByPointer({ tags: ["x", "y", "z"] }, "/tags/1"), "y");
});

test("empty pointer returns the whole value", () => {
  assert.deepEqual(getByPointer({ x: 1 }, ""), { x: 1 });
});

test("missing or non-traversable segment yields undefined", () => {
  assert.equal(getByPointer({ a: 1 }, "/missing"), undefined);
  assert.equal(getByPointer({ a: 1 }, "/a/b"), undefined);
  assert.equal(getByPointer(null, "/a"), undefined);
});

test("unescapes ~1 (slash) and ~0 (tilde) segments", () => {
  assert.equal(getByPointer({ "a/b": 5 }, "/a~1b"), 5);
  assert.equal(getByPointer({ "m~n": 9 }, "/m~0n"), 9);
});
