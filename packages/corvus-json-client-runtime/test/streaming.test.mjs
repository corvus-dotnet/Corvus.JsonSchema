// Oracle tests for the streaming response readers (run with `node --test` against the BUILT dist).
// NDJSON = one JSON value per line; SSE = frames terminated by a blank line, `data:` joined as JSON.
import { test } from "node:test";
import assert from "node:assert/strict";

import { readNdjsonItems, readSseEvents, readSseItems } from "../dist/serializers/streaming.js";

const encoder = new TextEncoder();

// A ReadableStream that emits the given chunks (strings or byte arrays) in order — multiple chunks
// exercise the cross-chunk line buffering.
function streamOf(...chunks) {
  const bytes = chunks.map((c) => (typeof c === "string" ? encoder.encode(c) : c));
  return new ReadableStream({
    start(controller) {
      for (const b of bytes) {
        controller.enqueue(b);
      }

      controller.close();
    },
  });
}

async function collect(generator) {
  const out = [];
  for await (const item of generator) {
    out.push(item);
  }

  return out;
}

test("NDJSON: one JSON document per line, blank lines skipped", async () => {
  const items = await collect(readNdjsonItems(streamOf('{"id":"p-1"}\n\n{"id":"p-2"}\n')));
  assert.deepEqual(items, [{ id: "p-1" }, { id: "p-2" }]);
});

test("NDJSON: a line split across chunk boundaries is reassembled", async () => {
  const items = await collect(readNdjsonItems(streamOf('{"id":"p-', '1"}\n{"id', '":"p-2"}\n')));
  assert.deepEqual(items, [{ id: "p-1" }, { id: "p-2" }]);
});

test("NDJSON: a final unterminated line is still yielded", async () => {
  const items = await collect(readNdjsonItems(streamOf('{"id":"p-1"}')));
  assert.deepEqual(items, [{ id: "p-1" }]);
});

test("SSE: frames dispatch on a blank line, with event/id/retry metadata", async () => {
  const events = await collect(
    readSseEvents(streamOf('data: {"id":"p-1"}\n\nevent: update\nid: 7\nretry: 1500\ndata: {"id":"p-2"}\n\n')),
  );
  assert.deepEqual(events, [
    { data: { id: "p-1" } },
    { data: { id: "p-2" }, event: "update", id: "7", retry: 1500 },
  ]);
});

test("SSE: multi-line data is joined with newlines before JSON parse; comments ignored", async () => {
  const events = await collect(readSseEvents(streamOf(': a comment\ndata: {"a":\ndata: 1}\n\n')));
  assert.deepEqual(events, [{ data: { a: 1 } }]);
});

test("SSE: a trailing frame with no terminating blank line is dispatched at end-of-stream", async () => {
  const events = await collect(readSseEvents(streamOf('data: {"id":"p-9"}\n')));
  assert.deepEqual(events, [{ data: { id: "p-9" } }]);
});

test("SSE items: yields just the parsed data payloads", async () => {
  const items = await collect(readSseItems(streamOf('data: {"id":"p-1"}\n\ndata: {"id":"p-2"}\n\n')));
  assert.deepEqual(items, [{ id: "p-1" }, { id: "p-2" }]);
});
