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
  toPlainDate,
  toInstant,
  toPlainTime,
  toDuration,
  Temporal,
  canonicalize,
  canonicalJson,
  exactNumber,
  parseLossless as rtParseLossless,
  toFloat64Array,
  toFloat32Array,
  toInt32Array,
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

test("Results.merge folds another collector's failures and annotations in (D1 disjunction sub-failures)", () => {
  const parent = new Results();
  parent.fail("/anyOf", "", "schema#/anyOf");

  const branch = new Results();
  branch.fail("/anyOf/0/minLength", "", "schema#/anyOf/0/minLength");
  branch.annotate("title", "Branch", "/anyOf/0/title", "");

  parent.merge(branch);
  assert.equal(parent.failures.length, 2, "merge appends the branch's failures");
  assert.equal(parent.failures[1].keywordLocation, "/anyOf/0/minLength");
  assert.equal(parent.annotations.length, 1, "merge appends the branch's annotations");
  assert.equal(parent.annotations[0].keyword, "title");
  // The source collector is untouched.
  assert.equal(branch.failures.length, 1, "merge does not mutate the source");
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

test("Temporal converters parse a branded temporal string into the matching Temporal type (gap B2)", () => {
  // date -> PlainDate, date-time -> the absolute Instant, time -> PlainTime, duration -> Duration. These are
  // the converters the generated `{name}AsTemporal` accessors delegate to; Temporal is re-exported here so a
  // consumer gets the types from the package.
  const d = toPlainDate("2020-01-02");
  assert.ok(d instanceof Temporal.PlainDate);
  assert.equal(d.year, 2020);
  assert.equal(d.month, 1);
  assert.equal(d.day, 2);

  const inst = toInstant("2020-01-02T03:04:05Z");
  assert.ok(inst instanceof Temporal.Instant);
  assert.equal(inst.epochMilliseconds, 1577934245000);

  // toPlainTime normalises a trailing `Z` to +00:00 (Temporal.PlainTime.from rejects a bare `Z`), so both a
  // numeric-offset and a `Z`-suffixed time parse — a valid RFC 3339 `time` may use either.
  const t = toPlainTime("13:14:15+02:00");
  assert.ok(t instanceof Temporal.PlainTime);
  assert.equal(t.hour, 13);
  assert.equal(t.minute, 14);
  assert.equal(t.second, 15);
  const tz = toPlainTime("13:14:15Z");
  assert.ok(tz instanceof Temporal.PlainTime && tz.hour === 13 && tz.second === 15);

  const dur = toDuration("P1Y2M3DT4H5M6S");
  assert.ok(dur instanceof Temporal.Duration);
  assert.equal(dur.years, 1);
  assert.equal(dur.hours, 4);
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

test("canonicalize/canonicalJson produce RFC 8785 JCS output (recursive UTF-16 key sort) — backs buildCanonical (gap F1)", () => {
  // Object keys sorted by UTF-16 code unit, recursively into nested objects and array elements.
  assert.equal(canonicalJson({ b: 1, a: 2 }), '{"a":2,"b":1}');
  assert.equal(canonicalJson({ z: { y: 1, x: 2 }, a: [3, { d: 1, c: 2 }] }), '{"a":[3,{"c":2,"d":1}],"z":{"x":2,"y":1}}');

  // Integer-like keys keep UTF-16 (string) order, NOT JS numeric-key order — "10" sorts before "2". This is
  // the case a plain key-sorted object + JSON.stringify gets WRONG (it would emit "2" before "10").
  assert.equal(canonicalJson({ "10": 0, "2": 0, a: 0 }), '{"10":0,"2":0,"a":0}');

  // ECMAScript number form (1.0 -> 1); bigint serialised as integer digits.
  assert.equal(canonicalJson({ n: 1.0, big: 9007199254740993n }), '{"big":9007199254740993,"n":1}');

  // Order-independent: different input key order yields byte-identical canonical output (content-addressing).
  assert.equal(dec(canonicalize({ b: 1, a: 2, c: { e: 1, d: 2 } })), dec(canonicalize({ c: { d: 2, e: 1 }, a: 2, b: 1 })));
  assert.equal(dec(canonicalize({ b: 1, a: 2 })), '{"a":2,"b":1}');
});

test("exactNumber returns exact digits for a lossless-parsed number, ECMAScript form otherwise (gap B3)", () => {
  // A plain JS number -> its ECMAScript string form (lossy beyond ~17 sig digits, by design).
  assert.equal(exactNumber(42), "42");
  assert.equal(exactNumber(1.5), "1.5");

  // parseLossless keeps the exact source digits (a LosslessNumber); exactNumber surfaces them verbatim, which
  // JSON.parse would have rounded. This is the big-number seam — feed the string into decimal.js / bignumber.js.
  const exactDigits = "123456789012345678901234567890.123456789";
  const losslessValue = rtParseLossless(`{"price":${exactDigits}}`);
  assert.equal(exactNumber(losslessValue.price), exactDigits);
  // The lossy round-trip differs, proving the win.
  assert.notEqual(String(JSON.parse(`{"price":${exactDigits}}`).price), exactDigits);

  // A big integer beyond Number.MAX_SAFE_INTEGER stays exact through parseLossless.
  assert.equal(exactNumber(rtParseLossless('{"n":9007199254740993}').n), "9007199254740993");
});

test("typed-array view helpers build the matching view over a numeric array (gap A6)", () => {
  const nums = [1, 2.5, -3];
  const f64 = toFloat64Array(nums);
  assert.ok(f64 instanceof Float64Array);
  assert.equal(f64.length, 3);
  assert.deepEqual([...f64], [1, 2.5, -3]);
  // Float32 narrows precision (the point of a float32 view); Int32 truncates toward zero.
  assert.ok(toFloat32Array([0.1])[0] !== 0.1);
  assert.deepEqual([...toInt32Array([1.9, -2.9])], [1, -2]);
});
