// Tests for the wire-level middleware handlers + the Node end-to-end transport (run with `node --test`),
// against the BUILT dist.
import { test } from "node:test";
import assert from "node:assert/strict";
import * as http from "node:http";

import {
  retryHandler,
  redirectHandler,
  timeoutHandler,
  defaultHandlers,
  compose,
} from "../dist/index.js";
import { NodeApiTransport } from "../dist/node.js";
import { OperationMethod } from "../dist/index.js";

const encoder = new TextEncoder();
const decoder = new TextDecoder();

function wire(overrides = {}) {
  return {
    method: "GET",
    url: "https://api.example.com/v1/pets",
    headers: new Headers(),
    body: { kind: "none" },
    signal: new AbortController().signal,
    ...overrides,
  };
}

// A WireResponse with a real ReadableStream body so cancel()/drain can be observed.
function response(statusCode, headers = {}, opts = {}) {
  let cancelled = false;
  const body = opts.noBody
    ? null
    : new ReadableStream({
        start(c) {
          c.enqueue(encoder.encode("body"));
          c.close();
        },
        cancel() {
          cancelled = true;
        },
      });
  const res = {
    statusCode,
    headers: new Headers(headers),
    body,
    wasCancelled: () => cancelled,
    disposeCount: 0,
  };
  res.dispose = () => {
    res.disposeCount++;
  };
  return res;
}

test("retryHandler retries a retryable status, drains the first body, then returns success", async () => {
  const seen = [];
  const first = response(503, { "Retry-After": "0" });
  const second = response(200);
  const next = async (req) => {
    seen.push(req);
    return seen.length === 1 ? first : second;
  };

  const res = await retryHandler({ baseDelayMs: 1 })(wire(), next);
  assert.equal(res.statusCode, 200);
  assert.equal(seen.length, 2); // retried once.
  assert.equal(first.wasCancelled(), true); // first body drained.
  assert.equal(first.disposeCount, 1);
});

test("retryHandler stops at maxRetries and returns the last failure", async () => {
  let calls = 0;
  const next = async () => {
    calls++;
    return response(503, { "Retry-After": "0" });
  };
  const res = await retryHandler({ maxRetries: 2, baseDelayMs: 1 })(wire(), next);
  assert.equal(res.statusCode, 503);
  assert.equal(calls, 3); // initial + 2 retries.
});

test("retryHandler passes a non-retryable status straight through", async () => {
  let calls = 0;
  const next = async () => {
    calls++;
    return response(404);
  };
  const res = await retryHandler({ baseDelayMs: 1 })(wire(), next);
  assert.equal(res.statusCode, 404);
  assert.equal(calls, 1);
});

test("retryHandler does NOT retry a consumed stream body", async () => {
  let calls = 0;
  const streamBody = {
    kind: "stream",
    content: new ReadableStream({ start: (c) => c.close() }),
    contentType: "application/octet-stream",
  };
  const next = async () => {
    calls++;
    return response(503, { "Retry-After": "0" });
  };
  const res = await retryHandler({ baseDelayMs: 1 })(wire({ body: streamBody }), next);
  assert.equal(res.statusCode, 503);
  assert.equal(calls, 1); // not retried.
});

test("redirectHandler follows a 301 and resolves a relative Location reference", async () => {
  const urls = [];
  const next = async (req) => {
    urls.push(req.url);
    return urls.length === 1
      ? response(301, { Location: "moved" }) // a relative reference: replaces the last path segment.
      : response(200);
  };
  const res = await redirectHandler()(wire(), next);
  assert.equal(res.statusCode, 200);
  // RFC 3986: "moved" replaces the last segment "pets" of /v1/pets.
  assert.deepEqual(urls, [
    "https://api.example.com/v1/pets",
    "https://api.example.com/v1/moved",
  ]);
});

test("redirectHandler resolves an absolute-path Location against the origin", async () => {
  const urls = [];
  const next = async (req) => {
    urls.push(req.url);
    return urls.length === 1
      ? response(301, { Location: "/elsewhere/here" }) // an absolute path replaces the whole path.
      : response(200);
  };
  await redirectHandler()(wire(), next);
  assert.equal(urls[1], "https://api.example.com/elsewhere/here");
});

test("redirectHandler downgrades a 303 POST to a bodyless GET", async () => {
  const seen = [];
  const next = async (req) => {
    seen.push({ method: req.method, body: req.body, ct: req.headers.get("Content-Type") });
    return seen.length === 1
      ? response(303, { Location: "https://api.example.com/v1/result" })
      : response(200);
  };
  const start = wire({
    method: "POST",
    body: { kind: "bytes", content: encoder.encode("x"), contentType: "application/json" },
    headers: new Headers({ "Content-Type": "application/json" }),
  });
  const res = await redirectHandler()(start, next);
  assert.equal(res.statusCode, 200);
  assert.equal(seen[1].method, "GET");
  assert.equal(seen[1].body.kind, "none");
  assert.equal(seen[1].ct, null); // Content-Type dropped.
});

test("redirectHandler strips Authorization + Cookie on a cross-origin redirect", async () => {
  const seen = [];
  const next = async (req) => {
    seen.push(req);
    return seen.length === 1
      ? response(307, { Location: "https://evil.example.org/landing" })
      : response(200);
  };
  const start = wire({
    headers: new Headers({ Authorization: "Bearer secret", Cookie: "session=1" }),
  });
  await redirectHandler()(start, next);
  assert.equal(seen[1].headers.get("Authorization"), null);
  assert.equal(seen[1].headers.get("Cookie"), null);
});

test("redirectHandler honours maxRedirects and returns the last 3xx", async () => {
  let calls = 0;
  const next = async () => {
    calls++;
    return response(302, { Location: "https://api.example.com/v1/loop" });
  };
  const res = await redirectHandler({ maxRedirects: 3 })(wire(), next);
  assert.equal(res.statusCode, 302);
  assert.equal(calls, 4); // initial + 3 hops.
});

test("timeoutHandler aborts the downstream signal when the deadline elapses", async () => {
  // A next that resolves only when its signal aborts. AbortSignal.timeout uses an unref'd timer, so a
  // ref'd keep-alive timer holds the event loop open until the abort fires (in real use the live socket
  // does this).
  const keepAlive = setInterval(() => {}, 5);
  try {
    const next = (req) =>
      new Promise((resolve) => {
        req.signal.addEventListener(
          "abort",
          () => resolve(response(0, {}, { noBody: true })),
          { once: true },
        );
      });
    const res = await timeoutHandler({ timeoutMs: 10 })(wire(), next);
    assert.equal(res.statusCode, 0); // the combined signal fired -> next resolved.
  } finally {
    clearInterval(keepAlive);
  }
});

test("defaultHandlers composes redirect -> retry -> timeout in order", async () => {
  const handlers = defaultHandlers({ retry: { baseDelayMs: 1 }, timeout: { timeoutMs: 5000 } });
  assert.equal(handlers.length, 3);
  // The composed chain drives a 503->200 retry end to end.
  const seen = [];
  const terminal = async () => {
    seen.push(1);
    return seen.length === 1 ? response(503, { "Retry-After": "0" }) : response(200);
  };
  const res = await compose(handlers, terminal)(wire());
  assert.equal(res.statusCode, 200);
});

// ---- Node end-to-end against a real http server ----

function startServer(handler) {
  return new Promise((resolve) => {
    const server = http.createServer(handler);
    server.listen(0, "127.0.0.1", () => {
      const addr = server.address();
      resolve({ server, baseUrl: `http://127.0.0.1:${addr.port}` });
    });
  });
}

function closeServer(server) {
  return new Promise((resolve) => server.close(() => resolve()));
}

test("NodeApiTransport: GET with a header param decodes a JSON response", async () => {
  let receivedHeader;
  const { server, baseUrl } = await startServer((req, res) => {
    receivedHeader = req.headers["x-request-id"];
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ ok: true }));
  });
  try {
    const t = new NodeApiTransport({ baseUrl });
    const factory = {
      async create(ctx) {
        const chunks = [];
        const reader = ctx.body.getReader();
        for (;;) {
          const { done, value } = await reader.read();
          if (done) break;
          chunks.push(value);
        }
        const text = decoder.decode(concat(chunks));
        return {
          statusCode: ctx.statusCode,
          contentType: ctx.contentType,
          json: JSON.parse(text),
          isSuccess: true,
          validate() {},
          async [Symbol.asyncDispose]() {},
        };
      },
    };
    const res = await t.send(makeReq({ headers: [["X-Request-Id", "abc"]] }), factory);
    assert.equal(res.statusCode, 200);
    assert.equal(res.contentType, "application/json");
    assert.deepEqual(res.json, { ok: true });
    assert.equal(receivedHeader, "abc");
  } finally {
    await closeServer(server);
  }
});

test("NodeApiTransport: POST sends the exact bytes + content type", async () => {
  let receivedBody;
  let receivedType;
  const { server, baseUrl } = await startServer((req, res) => {
    receivedType = req.headers["content-type"];
    const chunks = [];
    req.on("data", (c) => chunks.push(c));
    req.on("end", () => {
      receivedBody = Buffer.concat(chunks);
      res.writeHead(204);
      res.end();
    });
  });
  try {
    const t = new NodeApiTransport({ baseUrl });
    const payload = encoder.encode(`{"name":"Rex"}`);
    await t.send(makeReq({ method: OperationMethod.Post }), drainFactory(), {
      kind: "bytes",
      content: payload,
      contentType: "application/json",
    });
    assert.equal(receivedType, "application/json");
    assert.deepEqual(Array.from(receivedBody), Array.from(payload));
  } finally {
    await closeServer(server);
  }
});

test("NodeApiTransport: 503->200 retry via retryHandler", async () => {
  let hits = 0;
  const { server, baseUrl } = await startServer((req, res) => {
    hits++;
    if (hits === 1) {
      res.writeHead(503, { "Retry-After": "0" });
      res.end("nope");
    } else {
      res.writeHead(200, { "Content-Type": "text/plain" });
      res.end("ok");
    }
  });
  try {
    const t = new NodeApiTransport({ baseUrl, handlers: [retryHandler({ baseDelayMs: 1 })] });
    const res = await t.send(makeReq(), drainFactory());
    assert.equal(res.statusCode, 200);
    assert.equal(hits, 2);
  } finally {
    await closeServer(server);
  }
});

test("NodeApiTransport: 302->200 redirect via redirectHandler", async () => {
  const { server, baseUrl } = await startServer((req, res) => {
    if (req.url === "/pets") {
      res.writeHead(302, { Location: "/pets/landing" });
      res.end();
    } else {
      res.writeHead(200, { "Content-Type": "text/plain" });
      res.end("landed");
    }
  });
  try {
    const t = new NodeApiTransport({ baseUrl, handlers: [redirectHandler()] });
    const res = await t.send(makeReq(), drainFactory());
    assert.equal(res.statusCode, 200);
    assert.equal(res.text, "landed");
  } finally {
    await closeServer(server);
  }
});

test("NodeApiTransport: a hung request times out via timeoutHandler", async () => {
  const sockets = new Set();
  const { server, baseUrl } = await startServer((req, res) => {
    sockets.add(res);
    // Never respond — let the per-attempt timeout fire.
  });
  try {
    const t = new NodeApiTransport({ baseUrl, handlers: [timeoutHandler({ timeoutMs: 50 })] });
    await assert.rejects(() => t.send(makeReq(), drainFactory()));
  } finally {
    for (const res of sockets) {
      try {
        res.end();
      } catch {
        /* ignore */
      }
    }
    await closeServer(server);
  }
});

// ---- Node test helpers ----

function concat(chunks) {
  let total = 0;
  for (const c of chunks) total += c.length;
  const out = new Uint8Array(total);
  let offset = 0;
  for (const c of chunks) {
    out.set(c, offset);
    offset += c.length;
  }
  return out;
}

// A real ApiRequest the Node transport can compose, pointing at "/pets".
function makeReq(opts = {}) {
  const { method = OperationMethod.Get, headers = [] } = opts;
  return {
    method,
    pathTemplate: "/pets",
    hasPathParameters: false,
    hasQueryParameters: false,
    hasHeaderParameters: headers.length > 0,
    hasCookieParameters: false,
    writeResolvedPath(w) {
      w.writeUtf8("/pets");
    },
    writeQueryString() {
      return 0;
    },
    writeHeaders(sink) {
      for (const [name, value] of headers) sink(name, value);
    },
    writeCookies() {
      return 0;
    },
    validate() {},
  };
}

// A factory that fully reads the body (releasing the socket) and returns the decoded text.
function drainFactory() {
  return {
    async create(ctx) {
      let text = "";
      if (ctx.body) {
        const chunks = [];
        const reader = ctx.body.getReader();
        for (;;) {
          const { done, value } = await reader.read();
          if (done) break;
          chunks.push(value);
        }
        text = decoder.decode(concat(chunks));
      }
      return {
        statusCode: ctx.statusCode,
        contentType: ctx.contentType,
        text,
        isSuccess: ctx.statusCode >= 200 && ctx.statusCode < 300,
        validate() {},
        async [Symbol.asyncDispose]() {},
      };
    },
  };
}
