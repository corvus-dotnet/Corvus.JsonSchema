// Tests for the abstract + fetch transports (run with `node --test`), against the BUILT dist.
import { test } from "node:test";
import assert from "node:assert/strict";

import { AbstractApiTransport, FetchApiTransport, ByteWriter, OperationMethod } from "../dist/index.js";

const encoder = new TextEncoder();
const decoder = new TextDecoder();

// A minimal ApiRequest fixture. `opts` selects method/path/flags and the byte content each writer emits.
function fakeRequest(opts = {}) {
  const {
    method = OperationMethod.Get,
    customMethodName,
    pathTemplate = "/pets",
    resolvedPath,
    query = "",
    headers = [],
    cookies = "",
  } = opts;

  return {
    method,
    pathTemplate,
    customMethodName,
    hasPathParameters: resolvedPath !== undefined,
    hasQueryParameters: opts.hasQueryParameters ?? query.length > 0,
    hasHeaderParameters: headers.length > 0,
    hasCookieParameters: cookies.length > 0,
    writeResolvedPath(w) {
      w.writeUtf8(resolvedPath ?? pathTemplate);
    },
    writeQueryString(w) {
      w.writeUtf8(query);
      return encoder.encode(query).length;
    },
    writeHeaders(sink) {
      for (const [name, value] of headers) {
        sink(name, value);
      }
    },
    writeCookies(w) {
      w.writeUtf8(cookies);
      return encoder.encode(cookies).length;
    },
    validate() {},
  };
}

// A transport subclass that captures the WireRequest its dispatch receives and returns a canned response.
class CapturingTransport extends AbstractApiTransport {
  constructor(options) {
    super(options);
    this.captured = undefined;
    this.dispatchImpl = options.dispatchImpl;
  }

  async dispatch(wire) {
    this.captured = wire;
    if (this.dispatchImpl) {
      return this.dispatchImpl(wire);
    }

    return { statusCode: 204, headers: new Headers(), body: null };
  }
}

// A factory that just echoes the context into a trivial ApiResponse.
const echoFactory = {
  async create(ctx) {
    return {
      statusCode: ctx.statusCode,
      isSuccess: ctx.statusCode >= 200 && ctx.statusCode < 300,
      contentType: ctx.contentType,
      ctx,
      validate() {},
      async [Symbol.asyncDispose]() {},
    };
  },
};

test("buildWire: method mapping (GET, Query->QUERY, Custom->customMethodName)", async () => {
  const t = new CapturingTransport({ baseUrl: "https://api.example.com/v1" });

  await t.send(fakeRequest({ method: OperationMethod.Get }), echoFactory);
  assert.equal(t.captured.method, "GET");

  await t.send(fakeRequest({ method: OperationMethod.Query }), echoFactory);
  assert.equal(t.captured.method, "QUERY");

  await t.send(fakeRequest({ method: OperationMethod.Custom, customMethodName: "PURGE" }), echoFactory);
  assert.equal(t.captured.method, "PURGE");
});

test("buildWire: URL = base + path + query, with the ?-drop when the query is empty", async () => {
  const t = new CapturingTransport({ baseUrl: "https://api.example.com/v1/" }); // trailing slash stripped.

  // No query params at all.
  await t.send(fakeRequest({ pathTemplate: "/pets" }), echoFactory);
  assert.equal(t.captured.url, "https://api.example.com/v1/pets");

  // Query params declared but empty (all optional unset) -> drop the '?'.
  await t.send(fakeRequest({ pathTemplate: "/pets", hasQueryParameters: true, query: "" }), echoFactory);
  assert.equal(t.captured.url, "https://api.example.com/v1/pets");

  // Non-empty query.
  await t.send(fakeRequest({ pathTemplate: "/pets", query: "limit=10&sort=name" }), echoFactory);
  assert.equal(t.captured.url, "https://api.example.com/v1/pets?limit=10&sort=name");
});

test("buildWire: percent-encoded resolved path is preserved verbatim", async () => {
  const t = new CapturingTransport({ baseUrl: "https://api.example.com/v1" });
  await t.send(fakeRequest({ resolvedPath: "/pets/p%201" }), echoFactory);
  assert.equal(t.captured.url, "https://api.example.com/v1/pets/p%201");
});

test("buildWire: header params appended and Cookie header set", async () => {
  const t = new CapturingTransport({ baseUrl: "https://api.example.com" });
  await t.send(
    fakeRequest({ headers: [["X-Request-Id", "req-42"], ["Accept", "application/json"]], cookies: "a=1; b=2" }),
    echoFactory,
  );
  assert.equal(t.captured.headers.get("X-Request-Id"), "req-42");
  assert.equal(t.captured.headers.get("Accept"), "application/json");
  assert.equal(t.captured.headers.get("Cookie"), "a=1; b=2");
});

test("buildWire: Content-Type comes from a bytes body, and the body descriptor is threaded through", async () => {
  const t = new CapturingTransport({ baseUrl: "https://api.example.com" });
  const content = encoder.encode(`{"name":"Rex"}`);
  await t.send(fakeRequest(), echoFactory, { kind: "bytes", content, contentType: "application/json" });

  assert.equal(t.captured.headers.get("Content-Type"), "application/json");
  assert.equal(t.captured.body.kind, "bytes");
  assert.deepEqual(Array.from(t.captured.body.content), Array.from(content));
});

test("buildWire: a 'none' body sets no Content-Type", async () => {
  const t = new CapturingTransport({ baseUrl: "https://api.example.com" });
  await t.send(fakeRequest(), echoFactory);
  assert.equal(t.captured.headers.get("Content-Type"), null);
});

test("Publish/Subscribe operation methods throw a clear error", async () => {
  const t = new CapturingTransport({ baseUrl: "https://api.example.com" });
  await assert.rejects(
    () => t.send(fakeRequest({ method: OperationMethod.Publish }), echoFactory),
    /no HTTP wire mapping/,
  );
});

test("auth runs exactly once, after build and before dispatch, and the pipeline runs outer->inner", async () => {
  const order = [];
  let authCalls = 0;
  const authProvider = {
    authenticate(req) {
      authCalls++;
      assert.equal(req.headers.get("X-Request-Id"), "req-1"); // build already ran (header present).
      order.push("auth");
    },
  };
  const trace = (name) => async (req, next) => {
    order.push(`>${name}`);
    const res = await next(req);
    order.push(`<${name}`);
    return res;
  };

  const t = new CapturingTransport({
    baseUrl: "https://api.example.com",
    authenticationProvider: authProvider,
    handlers: [trace("a"), trace("b")],
    dispatchImpl: () => {
      order.push("dispatch");
      return { statusCode: 200, headers: new Headers(), body: null };
    },
  });

  await t.send(fakeRequest({ headers: [["X-Request-Id", "req-1"]] }), echoFactory);
  assert.equal(authCalls, 1);
  assert.deepEqual(order, ["auth", ">a", ">b", "dispatch", "<b", "<a"]);
});

test("error path: factory throwing disposes the wire response and rethrows", async () => {
  let disposed = 0;
  const t = new CapturingTransport({
    baseUrl: "https://api.example.com",
    dispatchImpl: () => ({
      statusCode: 500,
      headers: new Headers(),
      body: null,
      dispose: () => {
        disposed++;
      },
    }),
  });
  const throwingFactory = {
    async create() {
      throw new Error("decode failed");
    },
  };

  await assert.rejects(() => t.send(fakeRequest(), throwingFactory), /decode failed/);
  assert.equal(disposed, 1);
});

test("success path: the wire response is NOT force-disposed (keep-alive)", async () => {
  let disposed = 0;
  const t = new CapturingTransport({
    baseUrl: "https://api.example.com",
    dispatchImpl: () => ({
      statusCode: 200,
      headers: new Headers([["Content-Type", "application/json; charset=utf-8"]]),
      body: null,
      dispose: () => {
        disposed++;
      },
    }),
  });

  const res = await t.send(fakeRequest(), echoFactory);
  assert.equal(disposed, 0);
  // mediaType strips the ;charset parameter.
  assert.equal(res.contentType, "application/json");
});

test("the caller signal is threaded onto the ResponseContext only when supplied", async () => {
  const t = new CapturingTransport({ baseUrl: "https://api.example.com" });

  const withSignal = new AbortController().signal;
  const r1 = await t.send(fakeRequest(), echoFactory, undefined, withSignal);
  assert.equal(r1.ctx.signal, withSignal);

  const r2 = await t.send(fakeRequest(), echoFactory);
  // exactOptionalPropertyTypes: omitted, not set to undefined.
  assert.equal("signal" in r2.ctx, false);
});

test("FetchApiTransport maps url/method/headers/redirect and the four body kinds", async () => {
  const calls = [];
  const fetchImpl = async (url, init) => {
    calls.push({ url, init });
    const responseStream = new ReadableStream({
      start(c) {
        c.enqueue(encoder.encode("ok"));
        c.close();
      },
    });
    return new Response(responseStream, {
      status: 201,
      headers: { "Content-Type": "text/plain" },
    });
  };

  const t = new FetchApiTransport({
    baseUrl: "https://api.example.com/v1",
    fetchImpl,
    redirect: "manual",
  });

  // none
  let res = await t.send(fakeRequest({ pathTemplate: "/pets" }), echoFactory);
  assert.equal(calls.at(-1).url, "https://api.example.com/v1/pets");
  assert.equal(calls.at(-1).init.method, "GET");
  assert.equal(calls.at(-1).init.redirect, "manual");
  assert.equal(calls.at(-1).init.body, undefined);
  // Response -> WireResponse mapping flows through to the typed response.
  assert.equal(res.statusCode, 201);
  assert.equal(res.contentType, "text/plain");

  // bytes
  const bytes = encoder.encode("hello");
  await t.send(fakeRequest({ method: OperationMethod.Post }), echoFactory, {
    kind: "bytes",
    content: bytes,
    contentType: "application/json",
  });
  assert.equal(calls.at(-1).init.method, "POST");
  assert.deepEqual(Array.from(calls.at(-1).init.body), Array.from(bytes));
  assert.equal(calls.at(-1).init.headers.get("Content-Type"), "application/json");

  // stream -> duplex: "half"
  const streamBody = new ReadableStream({
    start(c) {
      c.enqueue(encoder.encode("streamed"));
      c.close();
    },
  });
  await t.send(fakeRequest({ method: OperationMethod.Put }), echoFactory, {
    kind: "stream",
    content: streamBody,
    contentType: "application/octet-stream",
  });
  assert.equal(calls.at(-1).init.duplex, "half");
  assert.ok(calls.at(-1).init.body instanceof ReadableStream);

  // writer -> buffered
  await t.send(fakeRequest({ method: OperationMethod.Post }), echoFactory, {
    kind: "writer",
    contentType: "application/x-www-form-urlencoded",
    write: async (sink) => {
      sink.write(encoder.encode("a=1"));
      sink.write(encoder.encode("&b=2"));
      await sink.flush(new AbortController().signal);
    },
  });
  assert.equal(decoder.decode(calls.at(-1).init.body), "a=1&b=2");
});
