// Oracle tests for the multipart body serializers (run with `node --test` against the BUILT dist).
// Each case uses a FIXED boundary so the framed bytes are asserted exactly, mirroring the C#
// MultipartFormDataSerializer / MultipartMixedSerializer framing (RFC 7578 form-data; ordered
// unnamed mixed parts). The serializers return a "writer" RequestBody; `collect` drives its write
// callback into a ByteWriter-backed sink (exactly as a transport does) and returns the framed bytes.
import { test } from "node:test";
import assert from "node:assert/strict";

import { ByteWriter } from "../dist/serializers/buffer-writer.js";
import { multipartFormData, multipartMixed } from "../dist/serializers/multipart.js";

const decoder = new TextDecoder();
const encoder = new TextEncoder();

async function collect(body) {
  const w = new ByteWriter();
  const sink = {
    write(bytes) {
      w.writeBytes(bytes);
    },
    async flush() {
      /* buffered */
    },
  };
  await body.write(sink, new AbortController().signal);
  return w.written.slice();
}

const text = async (body) => decoder.decode(await collect(body));

// Concatenates string + byte chunks into one Uint8Array (for asserting binary part bytes exactly).
function bytes(...chunks) {
  const parts = chunks.map((c) => (typeof c === "string" ? encoder.encode(c) : c));
  const total = parts.reduce((n, p) => n + p.length, 0);
  const out = new Uint8Array(total);
  let offset = 0;
  for (const p of parts) {
    out.set(p, offset);
    offset += p.length;
  }

  return out;
}

// ── multipart/form-data ──────────────────────────────────────────────

test("form-data: scalar fields are plain-text parts; booleans render lowercase", async () => {
  const body = multipartFormData({ name: "Rex", count: 3, good: true }, undefined, { boundary: "BOUND" });
  assert.equal(body.kind, "writer");
  assert.equal(body.contentType, "multipart/form-data; boundary=BOUND");
  assert.equal(
    await text(body),
    '--BOUND\r\nContent-Disposition: form-data; name="name"\r\n\r\nRex\r\n' +
      '--BOUND\r\nContent-Disposition: form-data; name="count"\r\n\r\n3\r\n' +
      '--BOUND\r\nContent-Disposition: form-data; name="good"\r\n\r\ntrue\r\n' +
      "--BOUND--\r\n",
  );
});

test("form-data: array/object fields are JSON parts with Content-Type application/json", async () => {
  const body = multipartFormData({ tags: ["a", "b"], meta: { k: 1 } }, undefined, { boundary: "B" });
  assert.equal(
    await text(body),
    '--B\r\nContent-Disposition: form-data; name="tags"\r\nContent-Type: application/json\r\n\r\n["a","b"]\r\n' +
      '--B\r\nContent-Disposition: form-data; name="meta"\r\nContent-Type: application/json\r\n\r\n{"k":1}\r\n' +
      "--B--\r\n",
  );
});

test("form-data: a null field writes an empty body; an undefined field is omitted", async () => {
  const body = multipartFormData({ a: "1", b: null, c: undefined }, undefined, { boundary: "B" });
  assert.equal(
    await text(body),
    '--B\r\nContent-Disposition: form-data; name="a"\r\n\r\n1\r\n' +
      '--B\r\nContent-Disposition: form-data; name="b"\r\n\r\n\r\n' +
      "--B--\r\n",
  );
});

test("form-data: a per-field Content-Type override applies to a scalar part", async () => {
  const body = multipartFormData({ note: "hi" }, undefined, {
    boundary: "B",
    fieldContentTypes: { note: "text/plain" },
  });
  assert.equal(
    await text(body),
    '--B\r\nContent-Disposition: form-data; name="note"\r\nContent-Type: text/plain\r\n\r\nhi\r\n--B--\r\n',
  );
});

test("form-data: binary parts follow the named fields, with filename + content type", async () => {
  const body = multipartFormData(
    { id: "p1" },
    { avatar: { content: new Uint8Array([1, 2, 3]), filename: "a.png", contentType: "image/png" } },
    { boundary: "B" },
  );
  assert.deepEqual(
    Array.from(await collect(body)),
    Array.from(
      bytes(
        '--B\r\nContent-Disposition: form-data; name="id"\r\n\r\np1\r\n',
        '--B\r\nContent-Disposition: form-data; name="avatar"; filename="a.png"\r\nContent-Type: image/png\r\n\r\n',
        new Uint8Array([1, 2, 3]),
        "\r\n--B--\r\n",
      ),
    ),
  );
});

test("form-data: a binary part defaults to application/octet-stream with no filename", async () => {
  const body = multipartFormData({}, { file: { content: new Uint8Array([9]) } }, { boundary: "B" });
  assert.deepEqual(
    Array.from(await collect(body)),
    Array.from(
      bytes(
        '--B\r\nContent-Disposition: form-data; name="file"\r\nContent-Type: application/octet-stream\r\n\r\n',
        new Uint8Array([9]),
        "\r\n--B--\r\n",
      ),
    ),
  );
});

test("form-data: a minted boundary is 32 hex chars and matches the content type", async () => {
  const body = multipartFormData({ a: "1" });
  const boundary = body.contentType.slice("multipart/form-data; boundary=".length);
  assert.match(boundary, /^[0-9a-f]{32}$/);
  assert.ok((await text(body)).startsWith(`--${boundary}\r\n`));
});

// ── multipart/mixed ──────────────────────────────────────────────────

test("mixed: homogeneous JSON parts are unnamed, with Content-Type application/json", async () => {
  const body = multipartMixed(
    [
      { kind: "json", value: { a: 1 } },
      { kind: "json", value: { a: 2 } },
    ],
    { boundary: "----CorvusBoundaryX" },
  );
  assert.equal(body.kind, "writer");
  assert.equal(body.contentType, "multipart/mixed; boundary=----CorvusBoundaryX");
  assert.equal(
    await text(body),
    "------CorvusBoundaryX\r\nContent-Type: application/json\r\n\r\n{\"a\":1}\r\n" +
      "------CorvusBoundaryX\r\nContent-Type: application/json\r\n\r\n{\"a\":2}\r\n" +
      "------CorvusBoundaryX--\r\n",
  );
});

test("mixed: a binary part carries Content-Type then Content-Disposition (attachment + filename)", async () => {
  const body = multipartMixed(
    [
      { kind: "json", value: { x: 1 }, contentType: "application/json" },
      { kind: "binary", content: new Uint8Array([7, 8]), contentType: "image/png", filename: "p.png" },
    ],
    { boundary: "B" },
  );
  assert.deepEqual(
    Array.from(await collect(body)),
    Array.from(
      bytes(
        '--B\r\nContent-Type: application/json\r\n\r\n{"x":1}\r\n',
        '--B\r\nContent-Type: image/png\r\nContent-Disposition: attachment; filename="p.png"\r\n\r\n',
        new Uint8Array([7, 8]),
        "\r\n--B--\r\n",
      ),
    ),
  );
});

test("mixed: a binary part defaults to application/octet-stream with no filename", async () => {
  const body = multipartMixed([{ kind: "binary", content: new Uint8Array([5]) }], { boundary: "B" });
  assert.deepEqual(
    Array.from(await collect(body)),
    Array.from(bytes("--B\r\nContent-Type: application/octet-stream\r\n\r\n", new Uint8Array([5]), "\r\n--B--\r\n")),
  );
});

test("mixed: a minted boundary uses the ----CorvusBoundary prefix and matches the content type", async () => {
  const body = multipartMixed([{ kind: "json", value: 1 }]);
  const boundary = body.contentType.slice("multipart/mixed; boundary=".length);
  assert.match(boundary, /^----CorvusBoundary[0-9a-f]{32}$/);
  assert.ok((await text(body)).startsWith(`--${boundary}\r\n`));
});
