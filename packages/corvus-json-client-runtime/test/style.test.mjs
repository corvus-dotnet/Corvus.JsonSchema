// Oracle tests for the OpenAPI 3.x parameter-style serializers (run with `node --test`). Imports the
// BUILT dist so it exercises the published artifact. Every row is one of the canonical OpenAPI
// parameter-serialization examples (param name "color"), plus a space-containing value to prove
// percent-encoding (space -> %20).
import { test } from "node:test";
import assert from "node:assert/strict";

import { ByteWriter } from "../dist/serializers/buffer-writer.js";
import { writePathSimple, writeQueryForm, writeHeaderSimple } from "../dist/serializers/style.js";

const decoder = new TextDecoder();

function path(value, explode) {
  const w = new ByteWriter();
  writePathSimple(w, value, explode);
  return decoder.decode(w.written);
}

function query(name, value, explode, allowReserved, first) {
  const w = new ByteWriter();
  const pairs = writeQueryForm(w, name, value, explode, allowReserved, first);
  return { text: decoder.decode(w.written), pairs };
}

test("simple style, scalar (path)", () => {
  // simple, scalar "blue" => "blue" (explode either)
  assert.equal(path("blue", false), "blue");
  assert.equal(path("blue", true), "blue");
});

test("simple style, array (path)", () => {
  // simple, array [blue,black,brown] => "blue,black,brown" (explode either)
  assert.equal(path(["blue", "black", "brown"], false), "blue,black,brown");
  assert.equal(path(["blue", "black", "brown"], true), "blue,black,brown");
});

test("form style, scalar (query)", () => {
  // form, scalar "blue" => "color=blue" (explode either)
  assert.deepEqual(query("color", "blue", false, false, true), { text: "color=blue", pairs: 1 });
  assert.deepEqual(query("color", "blue", true, false, true), { text: "color=blue", pairs: 1 });
});

test("form style, array, explode=false (query)", () => {
  // form, array [blue,black,brown], explode=false => "color=blue,black,brown"
  // The "," array-delimiter is written literally (matching the C# WriteQueryString); element values
  // are percent-encoded.
  assert.deepEqual(
    query("color", ["blue", "black", "brown"], false, false, true),
    { text: "color=blue,black,brown", pairs: 1 },
  );
  assert.deepEqual(
    query("color", ["blue", "black", "brown"], false, true, true),
    { text: "color=blue,black,brown", pairs: 1 },
  );
});

test("form style, array, explode=true (query)", () => {
  // form, array [blue,black,brown], explode=true => "color=blue&color=black&color=brown"
  assert.deepEqual(
    query("color", ["blue", "black", "brown"], true, false, true),
    { text: "color=blue&color=black&color=brown", pairs: 3 },
  );
});

test("empty string scalar", () => {
  // simple -> "" (empty); form -> "color="
  assert.equal(path("", false), "");
  assert.equal(path("", true), "");
  assert.deepEqual(query("color", "", false, false, true), { text: "color=", pairs: 1 });
});

test("header style, simple (scalar + array)", () => {
  // header, simple, scalar -> the value verbatim (no percent-encoding).
  assert.equal(writeHeaderSimple("blue", false), "blue");
  assert.equal(writeHeaderSimple("blue", true), "blue");
  // header, simple, array -> comma-joined (explode either), not percent-encoded.
  assert.equal(writeHeaderSimple(["blue", "black", "brown"], false), "blue,black,brown");
  assert.equal(writeHeaderSimple(["blue", "black", "brown"], true), "blue,black,brown");
});

test("percent-encoding: a space becomes %20", () => {
  // Path scalar.
  assert.equal(path("dark blue", false), "dark%20blue");
  // Path array element.
  assert.equal(path(["dark blue", "light grey"], false), "dark%20blue,light%20grey");
  // Query scalar.
  assert.deepEqual(query("color", "dark blue", false, false, true), { text: "color=dark%20blue", pairs: 1 });
  // Query exploded array element.
  assert.deepEqual(
    query("color", ["dark blue", "light grey"], true, false, true),
    { text: "color=dark%20blue&color=light%20grey", pairs: 2 },
  );
  // Header value is NOT percent-encoded (the space stays a space).
  assert.equal(writeHeaderSimple("dark blue", false), "dark blue");
});

test("query separators: subsequent params get a leading &", () => {
  // When first=false (something already written), the helper emits a leading & before the pair.
  assert.deepEqual(query("color", "blue", false, false, false), { text: "&color=blue", pairs: 1 });
  // Exploded array with first=false: every pair (including the first element) is &-prefixed.
  assert.deepEqual(
    query("color", ["a", "b"], true, false, false),
    { text: "&color=a&color=b", pairs: 2 },
  );
});

test("empty array writes nothing and reports zero pairs", () => {
  assert.deepEqual(query("color", [], false, false, true), { text: "", pairs: 0 });
  assert.deepEqual(query("color", [], true, false, true), { text: "", pairs: 0 });
  assert.equal(path([], false), "");
});
