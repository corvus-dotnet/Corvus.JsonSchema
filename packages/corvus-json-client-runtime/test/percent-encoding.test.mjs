// Unit tests for the RFC 3986 percent-encoder (run with `node --test`). Imports the BUILT dist so it
// exercises the published artifact.
import { test } from "node:test";
import assert from "node:assert/strict";

import { encodeData, encodeAllowReserved } from "../dist/serializers/percent-encoding.js";

test("encodeData escapes everything outside the RFC 3986 unreserved set", () => {
  // Unreserved (ALPHA / DIGIT / - . _ ~) passes through untouched.
  assert.equal(encodeData("hello~world-1.0_x"), "hello~world-1.0_x");
  // Space becomes %20 (not +); reserved characters are escaped in the data set.
  assert.equal(encodeData("a b"), "a%20b");
  assert.equal(encodeData("a/b?c#d"), "a%2Fb%3Fc%23d");
  assert.equal(encodeData("100%"), "100%25");
  // Non-ASCII is encoded at the UTF-8 byte level with uppercase hex.
  assert.equal(encodeData("café"), "caf%C3%A9");
});

test("encodeAllowReserved leaves the RFC 3986 reserved set intact", () => {
  assert.equal(encodeAllowReserved("a/b?c#d"), "a/b?c#d");
  assert.equal(encodeAllowReserved("path:sub,part;x=1"), "path:sub,part;x=1");
  // A space is neither unreserved nor reserved, so it is still escaped.
  assert.equal(encodeAllowReserved("a b"), "a%20b");
  assert.equal(encodeAllowReserved("café"), "caf%C3%A9");
});
