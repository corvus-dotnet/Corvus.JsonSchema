/// <reference types="node" />
import * as http from "node:http";
import * as https from "node:https";
import { Buffer } from "node:buffer";
import { Readable } from "node:stream";

import type { WireRequest, WireResponse } from "../contracts/wire.js";
import type { ByteSink } from "../serializers/buffer-writer.js";
import { ByteWriter } from "../serializers/buffer-writer.js";
import { AbstractApiTransport, type AbstractApiTransportOptions } from "./abstract-transport.js";

/**
 * Construction options for {@link NodeApiTransport}.
 */
export interface NodeApiTransportOptions extends AbstractApiTransportOptions {
  /** An optional `http.Agent` (connection pooling / keep-alive) for plain-HTTP requests. */
  readonly httpAgent?: http.Agent;

  /** An optional `https.Agent` for HTTPS requests. */
  readonly httpsAgent?: https.Agent;
}

/**
 * A {@link AbstractApiTransport} backed by `node:http`/`node:https`. It NEVER auto-follows redirects
 * (the Node HTTP stack does not), so the runtime's `redirectHandler` is fully in control here — unlike
 * `fetch`, where the platform may follow redirects before a handler sees them. This is the only entry
 * in the package that imports `node:*`; it is reachable solely through the `./node` export.
 */
export class NodeApiTransport extends AbstractApiTransport {
  private readonly httpAgent?: http.Agent;
  private readonly httpsAgent?: https.Agent;

  /**
   * Initializes a new Node transport.
   * @param options The construction options.
   */
  public constructor(options: NodeApiTransportOptions) {
    super(options);
    if (options.httpAgent !== undefined) {
      this.httpAgent = options.httpAgent;
    }

    if (options.httpsAgent !== undefined) {
      this.httpsAgent = options.httpsAgent;
    }
  }

  /** @inheritdoc */
  protected dispatch(wire: WireRequest): Promise<WireResponse> {
    return new Promise<WireResponse>((resolve, reject) => {
      const url = new URL(wire.url);
      const isHttps = url.protocol === "https:";
      const lib = isHttps ? https : http;
      const agent = isHttps ? this.httpsAgent : this.httpAgent;

      const options: http.RequestOptions = {
        method: wire.method,
        headers: toNodeHeaders(wire.headers),
        path: url.pathname + url.search,
        host: url.hostname,
        ...(url.port !== "" ? { port: url.port } : {}),
        ...(agent !== undefined ? { agent } : {}),
      };

      const req = lib.request(url, options, (res) => {
        resolve({
          statusCode: res.statusCode ?? 0,
          headers: fromNodeHeaders(res.headers),
          // Readable.toWeb returns a generic ReadableStream; the byte-typed view is the structural
          // truth at the Node boundary (Node yields Uint8Array chunks for an HTTP body).
          body: Readable.toWeb(res) as unknown as ReadableStream<Uint8Array>,
          dispose: () => {
            // Free the socket only if the body was never consumed (a safety net for an abandoned body);
            // a fully read response is already released for keep-alive.
            if (!res.complete) {
              res.destroy();
              req.destroy();
            }
          },
        });
      });

      req.on("error", reject);

      // Propagate the caller's abort signal to the underlying request.
      if (wire.signal.aborted) {
        req.destroy(abortError(wire.signal));
      } else {
        wire.signal.addEventListener("abort", () => req.destroy(abortError(wire.signal)), { once: true });
      }

      switch (wire.body.kind) {
        case "none":
          req.end();
          break;
        case "bytes":
          req.end(Buffer.from(wire.body.content));
          break;
        case "stream":
          Readable.fromWeb(wire.body.content as unknown as Parameters<typeof Readable.fromWeb>[0]).pipe(req);
          break;
        case "writer": {
          const writer = new ByteWriter();
          const sink: ByteSink = {
            write(bytes: Uint8Array): void {
              writer.writeBytes(bytes);
            },
            async flush(): Promise<void> {
              // Buffered: the bytes are sent in one req.end below.
            },
          };
          // A rejection in the async writer must reject the dispatch promise + tear down the request.
          void Promise.resolve(wire.body.write(sink, wire.signal)).then(
            () => {
              req.end(Buffer.from(writer.written));
            },
            (err: unknown) => {
              req.destroy(err instanceof Error ? err : new Error(String(err)));
              reject(err);
            },
          );
          break;
        }
      }
    });
  }

  /** @inheritdoc */
  public override async [Symbol.asyncDispose](): Promise<void> {
    // Only injected agents are used; this transport creates none, so there is nothing to destroy.
  }
}

/** Builds the abort reason as an `Error` (Node's `req.destroy` wants an `Error`, not an arbitrary reason). */
function abortError(signal: AbortSignal): Error {
  const reason: unknown = signal.reason;
  return reason instanceof Error ? reason : new DOMException("The operation was aborted.", "AbortError");
}

/** Flattens a WHATWG {@link Headers} into the plain record `node:http` expects. */
function toNodeHeaders(headers: Headers): Record<string, string> {
  const result: Record<string, string> = {};
  headers.forEach((value, name) => {
    result[name] = value;
  });
  return result;
}

/** Converts Node's `IncomingHttpHeaders` into a WHATWG {@link Headers}, preserving multi-valued headers. */
function fromNodeHeaders(headers: http.IncomingHttpHeaders): Headers {
  const result = new Headers();
  for (const [name, value] of Object.entries(headers)) {
    if (value === undefined) {
      continue;
    }

    if (Array.isArray(value)) {
      // `set-cookie` (and any other array-valued header) appends one entry per value.
      for (const item of value) {
        result.append(name, item);
      }
    } else {
      result.append(name, value);
    }
  }

  return result;
}
