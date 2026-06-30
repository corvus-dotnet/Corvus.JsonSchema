// Oracle tests for the application/x-www-form-urlencoded body serializer (run with `node --test`,
// against the BUILT dist). A form body is the object's properties serialized like query parameters and
// joined by `&`; these rows mirror the OpenAPI §4.8.14.4 default + the encoding-override cases.
import { test } from "node:test";
import assert from "node:assert/strict";

import { formUrlEncodedBytes } from "../dist/serializers/form-urlencoded.js";

const decoder = new TextDecoder();
const form = (value, encodings) => decoder.decode(formUrlEncodedBytes(value, encodings));

test("flat scalar object → name=value&... (form, explode default true)", () => {
  assert.equal(form({ name: "Rex", age: 3, good: true }), "name=Rex&age=3&good=true");
});

test("scalar values are percent-encoded; booleans render lowercase", () => {
  assert.equal(form({ q: "a b", flag: false }), "q=a%20b&flag=false");
});

test("array property, explode default true → repeated pairs", () => {
  assert.equal(form({ tags: ["a", "b", "c"] }), "tags=a&tags=b&tags=c");
});

test("array property, explode=false override → comma-joined single pair", () => {
  assert.equal(
    form({ tags: ["a", "b", "c"] }, { tags: { explode: false } }),
    "tags=a,b,c",
  );
});

test("array property, spaceDelimited / pipeDelimited overrides", () => {
  assert.equal(
    form({ tags: ["a", "b"] }, { tags: { style: "spaceDelimited" } }),
    "tags=a%20b",
  );
  assert.equal(
    form({ tags: ["a", "b"] }, { tags: { style: "pipeDelimited" } }),
    "tags=a%7Cb",
  );
});

test("object property: explode default true → key=value pairs", () => {
  assert.equal(form({ filter: { min: 1, max: 9 } }), "min=1&max=9");
});

test("object property: deepObject override → name[key]=value", () => {
  assert.equal(
    form({ filter: { min: 1, max: 9 } }, { filter: { style: "deepObject" } }),
    "filter%5Bmin%5D=1&filter%5Bmax%5D=9",
  );
});

test("object property: explode=false override → name=key,value,key,value", () => {
  assert.equal(
    form({ filter: { min: 1, max: 9 } }, { filter: { explode: false } }),
    "filter=min,1,max,9",
  );
});

test("null value writes name= (empty); undefined property is omitted", () => {
  assert.equal(form({ a: "1", b: null, c: undefined, d: "4" }), "a=1&b=&d=4");
});

test("allowReserved leaves reserved characters unescaped", () => {
  assert.equal(form({ path: "a/b" }), "path=a%2Fb");
  assert.equal(form({ path: "a/b" }, { path: { allowReserved: true } }), "path=a/b");
});

test("empty object → empty body", () => {
  assert.equal(form({}), "");
});
