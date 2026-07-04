import type { ApiTransport } from "./api-transport.js";
import type { ValidationMode } from "./validation-mode.js";

/**
 * Case-insensitive read access to the response headers — the analogue of the C# `IResponseHeaders`,
 * decoupling a generated response from any concrete HTTP-response type.
 */
export interface ResponseHeaders {
  /** Returns the (comma-joined) value of a header, or `undefined` when absent. */
  tryGet(name: string): string | undefined;
}

/**
 * A generated per-operation response. It decomposes and validates the response bytes via the model
 * companions. Mirrors the C# `IApiResponse<TSelf>`; `AsyncDisposable` ties any underlying stream/socket
 * lifetime to the response.
 */
export interface ApiResponse extends AsyncDisposable {
  /** The HTTP status code. */
  readonly statusCode: number;

  /** Whether the status is a 2xx success. */
  readonly isSuccess: boolean;

  /** Validates the active response body against its schema; throws on failure. */
  validate(mode?: ValidationMode): void;
}

/**
 * Everything a {@link ResponseFactory} needs to build a typed response from the wire result — the
 * argument bundle replacing the C# `IApiResponse.CreateAsync` parameter list.
 */
export interface ResponseContext {
  /** The HTTP status code. */
  readonly statusCode: number;

  /** The response body stream (for streaming/SSE/NDJSON or large bodies); `null` when empty. */
  readonly body: ReadableStream<Uint8Array> | null;

  /** The response `Content-Type` media type, or `null` when absent. */
  readonly contentType: string | null;

  /** The response headers. */
  readonly headers: ResponseHeaders;

  /** The transport, threaded back so generated link-followers can issue subsequent requests. */
  readonly transport: ApiTransport;

  /** The caller's abort signal, propagated to streaming reads. */
  readonly signal?: AbortSignal;
}

/**
 * Builds a typed response from the wire result — a generated response module exports one of these
 * (the TypeScript analogue of the C# `static abstract CreateAsync`).
 */
export interface ResponseFactory<TResponse extends ApiResponse> {
  /** Creates the typed response from the wire context. */
  create(context: ResponseContext): Promise<TResponse>;
}
