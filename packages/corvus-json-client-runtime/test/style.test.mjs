// Oracle tests for the OpenAPI 3.x parameter-style serializers (run with `node --test`). Imports the
// BUILT dist so it exercises the published artifact. The rows are the canonical OpenAPI
// parameter-serialization examples (param name "color"; string "blue"; array [blue,black,brown];
// object {R:100,G:200,B:150}), across every style x value-shape x explode, plus percent-encoding cases.
//
// The serializers mirror the C# emitter's STRUCTURE. Where the OpenAPI spec table shows a reserved
// delimiter unencoded (pipe `|`, the deepObject brackets), the C# emitter writes the pre-encoded form
// (%7C, %5B/%5D) and so does this runtime — those rows assert the encoded wire bytes. Likewise
// spaceDelimited/pipeDelimited + explode follow the C# behavior (arrays stay <delim>-joined; only
// `form` explodes an array into repeated name=value pairs).
import { test } from "node:test";
import assert from "node:assert/strict";

import { ByteWriter } from "../dist/serializers/buffer-writer.js";
import {
  writePathParam,
  writeQueryParam,
  writeHeaderParam,
  writeCookieParam,
} from "../dist/serializers/style.js";

const decoder = new TextDecoder();

const STR = "blue";
const ARR = ["blue", "black", "brown"];
const OBJ = { R: 100, G: 200, B: 150 };

function path(value, style, explode, allowReserved = false) {
  const w = new ByteWriter();
  writePathParam(w, "color", value, style, explode, allowReserved);
  return decoder.decode(w.written);
}

function query(value, style, explode, allowReserved = false, first = true) {
  const w = new ByteWriter();
  const pairs = writeQueryParam(w, "color", value, style, explode, allowReserved, first);
  return { text: decoder.decode(w.written), pairs };
}

function cookie(value, style, explode, first = true) {
  const w = new ByteWriter();
  const pairs = writeCookieParam(w, "color", value, style, explode, first);
  return { text: decoder.decode(w.written), pairs };
}

// ── Path styles ──────────────────────────────────────────────────────────────

test("path simple (scalar/array/object)", () => {
  assert.equal(path(STR, "simple", false), "blue");
  assert.equal(path(STR, "simple", true), "blue");
  assert.equal(path(ARR, "simple", false), "blue,black,brown");
  assert.equal(path(ARR, "simple", true), "blue,black,brown");
  assert.equal(path(OBJ, "simple", false), "R,100,G,200,B,150");
  assert.equal(path(OBJ, "simple", true), "R=100,G=200,B=150");
});

test("path label (scalar/array/object)", () => {
  assert.equal(path(STR, "label", false), ".blue");
  assert.equal(path(STR, "label", true), ".blue");
  assert.equal(path(ARR, "label", false), ".blue,black,brown");
  assert.equal(path(ARR, "label", true), ".blue.black.brown");
  assert.equal(path(OBJ, "label", false), ".R,100,G,200,B,150");
  assert.equal(path(OBJ, "label", true), ".R=100.G=200.B=150");
});

test("path matrix (scalar/array/object)", () => {
  assert.equal(path(STR, "matrix", false), ";color=blue");
  assert.equal(path(STR, "matrix", true), ";color=blue");
  assert.equal(path(ARR, "matrix", false), ";color=blue,black,brown");
  assert.equal(path(ARR, "matrix", true), ";color=blue;color=black;color=brown");
  assert.equal(path(OBJ, "matrix", false), ";color=R,100,G,200,B,150");
  assert.equal(path(OBJ, "matrix", true), ";R=100;G=200;B=150");
});

// ── Query styles ─────────────────────────────────────────────────────────────

test("query form (scalar/array/object)", () => {
  assert.deepEqual(query(STR, "form", false), { text: "color=blue", pairs: 1 });
  assert.deepEqual(query(STR, "form", true), { text: "color=blue", pairs: 1 });
  assert.deepEqual(query(ARR, "form", false), { text: "color=blue,black,brown", pairs: 1 });
  assert.deepEqual(query(ARR, "form", true), {
    text: "color=blue&color=black&color=brown",
    pairs: 3,
  });
  assert.deepEqual(query(OBJ, "form", false), { text: "color=R,100,G,200,B,150", pairs: 1 });
  assert.deepEqual(query(OBJ, "form", true), { text: "R=100&G=200&B=150", pairs: 3 });
});

test("query spaceDelimited (array/object, non-explode)", () => {
  assert.deepEqual(query(ARR, "spaceDelimited", false), {
    text: "color=blue%20black%20brown",
    pairs: 1,
  });
  assert.deepEqual(query(OBJ, "spaceDelimited", false), {
    text: "color=R%20100%20G%20200%20B%20150",
    pairs: 1,
  });
});

test("query pipeDelimited (array/object, non-explode; pipe encoded to %7C)", () => {
  assert.deepEqual(query(ARR, "pipeDelimited", false), {
    text: "color=blue%7Cblack%7Cbrown",
    pairs: 1,
  });
  assert.deepEqual(query(OBJ, "pipeDelimited", false), {
    text: "color=R%7C100%7CG%7C200%7CB%7C150",
    pairs: 1,
  });
});

test("query deepObject (brackets encoded to %5B/%5D)", () => {
  assert.deepEqual(query(OBJ, "deepObject", true), {
    text: "color%5BR%5D=100&color%5BG%5D=200&color%5BB%5D=150",
    pairs: 3,
  });
});

test("query space/pipe + explode mirrors C#: array stays delim-joined, object explodes", () => {
  // Only `form` explodes an array into repeated name=value pairs; space/pipe stay delim-joined.
  assert.deepEqual(query(ARR, "spaceDelimited", true), {
    text: "color=blue%20black%20brown",
    pairs: 1,
  });
  // Objects DO explode for space/pipe (the C# explode branch).
  assert.deepEqual(query(OBJ, "spaceDelimited", true), { text: "R=100&G=200&B=150", pairs: 3 });
});

// ── Header style ─────────────────────────────────────────────────────────────

test("header simple (scalar/array/object) — never percent-encoded", () => {
  assert.equal(writeHeaderParam(STR, false), "blue");
  assert.equal(writeHeaderParam(STR, true), "blue");
  assert.equal(writeHeaderParam(ARR, false), "blue,black,brown");
  assert.equal(writeHeaderParam(ARR, true), "blue,black,brown");
  assert.equal(writeHeaderParam(OBJ, false), "R,100,G,200,B,150");
  assert.equal(writeHeaderParam(OBJ, true), "R=100,G=200,B=150");
});

// ── Cookie style ─────────────────────────────────────────────────────────────

test("cookie form (scalar/array/object) — pairs separated by '; '", () => {
  assert.deepEqual(cookie(STR, "form", false), { text: "color=blue", pairs: 1 });
  assert.deepEqual(cookie(ARR, "form", false), { text: "color=blue,black,brown", pairs: 1 });
  assert.deepEqual(cookie(ARR, "form", true), {
    text: "color=blue; color=black; color=brown",
    pairs: 3,
  });
  assert.deepEqual(cookie(OBJ, "form", false), { text: "color=R,100,G,200,B,150", pairs: 1 });
  assert.deepEqual(cookie(OBJ, "form", true), { text: "R=100; G=200; B=150", pairs: 3 });
});

test("cookie form scalar is percent-encoded; cookie-style (3.2) scalar is raw", () => {
  assert.deepEqual(cookie("dark blue", "form", false), { text: "color=dark%20blue", pairs: 1 });
  assert.deepEqual(cookie("dark blue", "cookie", false), { text: "color=dark blue", pairs: 1 });
});

// ── Encoding + separator mechanics ───────────────────────────────────────────

test("percent-encoding: a space becomes %20 in path/query, stays raw in header", () => {
  assert.equal(path("dark blue", "simple", false), "dark%20blue");
  assert.equal(path(["dark blue", "light grey"], "simple", false), "dark%20blue,light%20grey");
  assert.deepEqual(query("dark blue", "form", false), { text: "color=dark%20blue", pairs: 1 });
  assert.deepEqual(query(["dark blue", "light grey"], "form", true), {
    text: "color=dark%20blue&color=light%20grey",
    pairs: 2,
  });
  assert.equal(writeHeaderParam("dark blue", false), "dark blue");
});

test("query: subsequent params (first=false) get a leading &", () => {
  assert.deepEqual(query(STR, "form", false, false, false), { text: "&color=blue", pairs: 1 });
  assert.deepEqual(query(["a", "b"], "form", true, false, false), {
    text: "&color=a&color=b",
    pairs: 2,
  });
});

test("cookie: subsequent pairs (first=false) get a leading '; '", () => {
  assert.deepEqual(cookie(STR, "form", false, false), { text: "; color=blue", pairs: 1 });
});

test("empty array/object writes nothing and reports zero pairs", () => {
  assert.deepEqual(query([], "form", false), { text: "", pairs: 0 });
  assert.deepEqual(query([], "form", true), { text: "", pairs: 0 });
  assert.deepEqual(query({}, "form", true), { text: "", pairs: 0 });
  assert.deepEqual(cookie([], "form", false), { text: "", pairs: 0 });
});

test("empty string scalar: path '', query 'color='", () => {
  assert.equal(path("", "simple", false), "");
  assert.deepEqual(query("", "form", false), { text: "color=", pairs: 1 });
});

test("numbers and booleans render without quotes", () => {
  assert.equal(path(42, "simple", false), "42");
  assert.deepEqual(query(true, "form", false), { text: "color=true", pairs: 1 });
  assert.equal(writeHeaderParam([1, 2, 3], false), "1,2,3");
});
