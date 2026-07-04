import type { RequestBody } from "./api-transport.js";

/**
 * The byte-level request a transport's middleware pipeline operates on — the snapshot the transport
 * builds synchronously from an {@link ApiRequest} before any async I/O, then threads through the
 * handler chain. Uses the WHATWG `Headers` type so the same value works in both the fetch and Node
 * transports.
 */
export interface WireRequest {
  /** The resolved HTTP verb (including custom verbs and `QUERY`). */
  method: string;

  /** The absolute request URL (base URL + resolved path + query). */
  url: string;

  /** The request headers. */
  headers: Headers;

  /** The request body. */
  body: RequestBody;

  /** The abort signal for this request. */
  signal: AbortSignal;
}

/**
 * The byte-level response a transport's middleware pipeline returns — the wire result the concrete
 * transport produces and the handlers may inspect or rewrite.
 */
export interface WireResponse {
  /** The HTTP status code. */
  statusCode: number;

  /** The response headers. */
  headers: Headers;

  /** The response body stream, or `null` when empty. */
  body: ReadableStream<Uint8Array> | null;

  /** An optional disposal hook (the Node transport frees the socket; fetch leaves this undefined). */
  dispose?: () => Promise<void> | void;
}
