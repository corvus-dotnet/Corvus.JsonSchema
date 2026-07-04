import type { WireRequest, WireResponse } from "../contracts/wire.js";
import type { ByteSink } from "../serializers/buffer-writer.js";
import { ByteWriter } from "../serializers/buffer-writer.js";
import { AbstractApiTransport, type AbstractApiTransportOptions } from "./abstract-transport.js";

/**
 * Construction options for {@link FetchApiTransport}.
 */
export interface FetchApiTransportOptions extends AbstractApiTransportOptions {
  /** The `fetch` implementation to use. Defaults to the global `fetch`. */
  readonly fetchImpl?: typeof fetch;

  /** The redirect policy passed to `fetch`. Defaults to `"follow"`. */
  readonly redirect?: RequestRedirect;
}

/**
 * A {@link AbstractApiTransport} backed by the WHATWG `fetch` API — the portable transport that runs
 * unchanged in a browser, edge runtime, Deno, Bun, or Node (via the global `fetch`). It defers
 * connection pooling, keep-alive, and (by default) redirect-following to the platform.
 *
 * To drive redirects through the runtime's `redirectHandler` instead, construct with
 * `redirect: "manual"`. On server runtimes (undici/Deno) that exposes the 3xx response so the handler
 * can act on it; in browsers `redirect: "manual"` yields an opaque redirect, so there prefer the
 * default `redirect: "follow"` and no redirect handler.
 */
export class FetchApiTransport extends AbstractApiTransport {
  private readonly fetchImpl: typeof fetch;
  private readonly redirect: RequestRedirect;

  /**
   * Initializes a new fetch transport.
   * @param options The construction options.
   */
  public constructor(options: FetchApiTransportOptions) {
    super(options);
    this.fetchImpl = options.fetchImpl ?? globalThis.fetch.bind(globalThis);
    this.redirect = options.redirect ?? "follow";
  }

  /** @inheritdoc */
  protected async dispatch(wire: WireRequest): Promise<WireResponse> {
    // `duplex` is not yet on lib.dom's RequestInit (needed for a streaming request body), so widen it
    // locally; the buffered Uint8Array body is a valid BodyInit at runtime, but the es2023 generic
    // Uint8Array type does not structurally match lib.dom's BodyInit, so it is assigned as one here.
    const init: RequestInit & { duplex?: "half" } = {
      method: wire.method,
      headers: wire.headers,
      signal: wire.signal,
      redirect: this.redirect,
    };

    switch (wire.body.kind) {
      case "none":
        // No body — leave init.body undefined.
        break;
      case "bytes":
        init.body = wire.body.content as unknown as BodyInit;
        break;
      case "stream":
        init.body = wire.body.content as unknown as BodyInit;
        // Required by fetch for a streaming request body.
        init.duplex = "half";
        break;
      case "writer": {
        // v1 buffers the writer body; a future refinement can stream it zero-copy with duplex:"half".
        const writer = new ByteWriter();
        const sink: ByteSink = {
          write(bytes: Uint8Array): void {
            writer.writeBytes(bytes);
          },
          async flush(): Promise<void> {
            // Buffered: nothing to flush downstream until the body is materialized below.
          },
        };
        await wire.body.write(sink, wire.signal);
        init.body = writer.written as unknown as BodyInit;
        break;
      }
    }

    const response = await this.fetchImpl(wire.url, init);
    return {
      statusCode: response.status,
      headers: response.headers,
      body: response.body,
    };
  }

  /** @inheritdoc */
  public override async [Symbol.asyncDispose](): Promise<void> {
    // The platform owns connection pooling; nothing to dispose.
  }
}
