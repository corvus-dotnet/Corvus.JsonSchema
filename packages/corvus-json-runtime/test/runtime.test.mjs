// Smoke tests for the BUILT @endjin/corvus-json-runtime package (run with `node --test`).
// These lock a handful of load-bearing runtime primitives so a regenerate that silently breaks one
// of them is caught here, not three layers downstream in a generated validator. Importing the built
// dist (not src) is deliberate: it exercises the published artifact, including its runtime imports of
// lossless-json / @js-temporal/polyfill / tr46.
import { test } from "node:test";
import assert from "node:assert/strict";
import { parse as parseLossless } from "lossless-json";

import {
  __isNum,
  __re,
  __ptr,
  Results,
  produce,
  scanTargets,
  applyEditsBytes,
} from "../dist/index.js";

test("__isNum recognises plain numbers and lossless-json source-text numbers", () => {
  assert.equal(__isNum(42), true);
  assert.equal(__isNum(3.14), true);
  // A LosslessNumber retains the exact source digits (numbers beyond IEEE-754 range).
  const big = parseLossless("9007199254740993");
  assert.equal(__isNum(big), true);
  assert.equal(__isNum("42"), false);
  assert.equal(__isNum(null), false);
  assert.equal(__isNum({}), false);
  assert.equal(__isNum([]), false);
});

test("__re compiles patterns in unicode mode (\\p{Letter} works)", () => {
  const re = __re("^\\p{Letter}+$");
  // u-mode is required for \p{...} property escapes.
  assert.equal(re.unicode, true);
  assert.equal(re.test("héllo"), true);
  assert.equal(re.test("Ω"), true);
  assert.equal(re.test("abc123"), false);
  // The cache returns the same compiled RegExp for the same source.
  assert.equal(__re("^\\p{Letter}+$"), re);
});

test("__ptr applies RFC 6901 escaping (~ -> ~0, / -> ~1)", () => {
  assert.equal(__ptr("plain"), "plain");
  assert.equal(__ptr("a/b"), "a~1b");
  assert.equal(__ptr("a~b"), "a~0b");
  // ~ must be escaped before /, so "~/" -> "~0~1" not "~01".
  assert.equal(__ptr("~/"), "~0~1");
});

test("Results collects failures and annotations", () => {
  const r = new Results();
  assert.equal(r.valid, true);
  r.annotate("title", "X", "/title", "");
  assert.equal(r.valid, true, "annotations do not invalidate");
  assert.equal(r.annotations.length, 1);
  assert.equal(r.annotations[0].value, "X");
  r.fail("/type", "/foo", "https://example/schema#/type");
  assert.equal(r.valid, false);
  assert.equal(r.failures.length, 1);
  assert.equal(r.failures[0].keywordLocation, "/type");
  assert.equal(r.failures[0].absoluteKeywordLocation, "https://example/schema#/type");
});

const enc = (s) => new TextEncoder().encode(s);
const dec = (b) => new TextDecoder().decode(b);

test("produce performs a structural-sharing RMW round-trip", () => {
  const source = enc('{"name":"Alice","age":30}');
  const next = produce(source, (d) => {
    d.age = 31;
  });
  // Original bytes are untouched (structural sharing).
  assert.equal(dec(source), '{"name":"Alice","age":30}');
  const parsed = JSON.parse(dec(next));
  assert.deepEqual(parsed, { name: "Alice", age: 31 });
  // The unchanged "name" field must be copied through verbatim.
  assert.ok(dec(next).includes('"name":"Alice"'));
});

test("scanTargets + applyEditsBytes splice a value at the byte level", () => {
  const source = enc('{"name":"Alice","age":30}');
  const targets = [{ name: enc("age"), content: enc("99"), vbs: 0, vbe: 0 }];
  const found = scanTargets(source, targets);
  assert.equal(found, true);
  const t = targets[0];
  // The scan located the value span; replace it.
  const result = applyEditsBytes(source, [
    { offset: t.vbs, length: t.vbe - t.vbs, content: enc("99") },
  ]);
  assert.deepEqual(JSON.parse(dec(result)), { name: "Alice", age: 99 });
});
