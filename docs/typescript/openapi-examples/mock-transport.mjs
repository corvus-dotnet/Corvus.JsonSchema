// Shared MockApiTransport + wire-composition helpers for the conformance and parity suites. It
// composes the wire request exactly as a real transport would (calling the request write* methods
// synchronously, assembling base URL + resolved path + query + headers) and records it on `captured`.
import { ByteWriter } from "@endjin/corvus-json-client-runtime";

export const decoder = new TextDecoder();
export const encoder = new TextEncoder();

// Concatenates string + byte chunks into one Uint8Array (for reconstructing multipart framing bytes).
export function bytes(...chunks) {
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

// A transport that records the composed wire request and returns a canned 200 JSON response. It mirrors
// the byte-native composition contract: writeResolvedPath + writeQueryString build the URL path/query
// into a ByteWriter; writeHeaders streams header name/value pairs; the body is taken from the supplied
// RequestBody. The base URL is the client's static serverUri().
export class MockApiTransport {
  constructor(baseUrl, canned, authenticationProvider) {
    this.baseUrl = baseUrl;
    this.captured = undefined;
    // An optional AuthenticationProvider, applied after the request is composed and before dispatch —
    // exactly where the real AbstractApiTransport.send runs it (see transports/abstract-transport.ts).
    this.authenticationProvider = authenticationProvider;
    // The canned response the factory decodes: { status, bytes, contentType }. Defaults to a 200 JSON Pet.
    this.canned = canned ?? {
      status: 200,
      bytes: new TextEncoder().encode(JSON.stringify({ id: "p-1", name: "Rex", tag: "dog" })),
      contentType: "application/json",
    };
  }

  async send(request, factory, body, _signal) {
    const pathWriter = new ByteWriter();
    request.writeResolvedPath(pathWriter);
    const path = decoder.decode(pathWriter.written);

    let query = "";
    if (request.hasQueryParameters) {
      const queryWriter = new ByteWriter();
      const written = request.writeQueryString(queryWriter);
      if (written > 0) {
        query = "?" + decoder.decode(queryWriter.written);
      }
    }

    const headers = new Headers();
    request.writeHeaders((name, value) => headers.append(name, value));

    // Cookies compose into a single `Cookie` header value, exactly as a real transport would. Mirror
    // the path/query write contract: writeCookies streams the cookie pairs into a ByteWriter.
    if (request.hasCookieParameters) {
      const cookieWriter = new ByteWriter();
      const written = request.writeCookies(cookieWriter);
      if (written > 0) {
        headers.append("Cookie", decoder.decode(cookieWriter.written));
      }
    }

    let url = this.baseUrl + path + query;

    // Apply auth exactly as AbstractApiTransport.send does: once per logical send, over the composed
    // wire request, after build and before dispatch. The provider mutates headers (and may rewrite the
    // URL, e.g. an apiKey in the query), so read `url` back from the wire object afterwards.
    if (this.authenticationProvider !== undefined) {
      const wire = { method: methodName(request.method), url, headers, body, signal: _signal };
      await this.authenticationProvider.authenticate(wire, _signal ?? new AbortController().signal);
      url = wire.url;
    }

    this.captured = {
      method: methodName(request.method),
      url,
      headers,
      body: await materializeBody(body, _signal),
    };

    // Return the canned response via the generated response factory.
    const responseBytes = this.canned.bytes;
    const responseStream = new ReadableStream({
      start(controller) {
        controller.enqueue(responseBytes);
        controller.close();
      },
    });
    // Case-insensitive header lookup over the canned headers (mirrors a real ResponseHeaders).
    const cannedHeaders = this.canned.headers ?? {};
    const responseHeaders = {
      tryGet(name) {
        const lower = name.toLowerCase();
        for (const key of Object.keys(cannedHeaders)) {
          if (key.toLowerCase() === lower) {
            return cannedHeaders[key];
          }
        }

        return undefined;
      },
    };

    return factory.create({
      statusCode: this.canned.status,
      body: responseStream,
      contentType: this.canned.contentType,
      headers: responseHeaders,
      transport: this,
    });
  }

  async [Symbol.asyncDispose]() {
    /* nothing to dispose */
  }
}

// OperationMethod (a const enum-like) -> the wire verb. The generated request carries the numeric
// enum value; map the small set used by these recipes.
export function methodName(method) {
  // OperationMethod: Get=0, Put=1, Post=2, Delete=3, ... (see contracts/operation-method.ts).
  return ["GET", "PUT", "POST", "DELETE", "OPTIONS", "HEAD", "PATCH", "TRACE", "QUERY"][method] ?? String(method);
}

// A "writer" body (multipart) is materialized by driving its write callback into a ByteWriter sink —
// exactly as a real transport does — so the captured body exposes the framed `content` bytes. Other
// body kinds are captured verbatim.
export async function materializeBody(body, signal) {
  if (body !== undefined && body.kind === "writer") {
    const w = new ByteWriter();
    const sink = {
      write(b) {
        w.writeBytes(b);
      },
      async flush() {
        /* buffered */
      },
    };
    await body.write(sink, signal ?? new AbortController().signal);
    return { kind: "writer", contentType: body.contentType, content: w.written.slice() };
  }

  return body ?? { kind: "none" };
}
