import type { ByteSink } from "../serializers/buffer-writer.js";
import type { ApiRequest } from "./api-request.js";
import type { ApiResponse, ResponseFactory } from "./api-response.js";

/**
 * The request body, in one of four shapes — the union collapses the four C# `IApiTransport.SendAsync`
 * overloads into one. The C# typed-JSON overload becomes {@link RequestBody.kind} `"bytes"` because a
 * generated model's `build(props)` already produces the canonical UTF-8 JSON bytes; the `"writer"` case
 * is the zero-copy form/multipart analogue of the C# `Func<Stream, …, ValueTask>` body writer.
 */
export type RequestBody =
  | { readonly kind: "none" }
  | { readonly kind: "bytes"; readonly content: Uint8Array; readonly contentType: string }
  | { readonly kind: "stream"; readonly content: ReadableStream<Uint8Array>; readonly contentType: string }
  | {
      readonly kind: "writer";
      readonly write: (sink: ByteSink, signal: AbortSignal) => Promise<void>;
      readonly contentType: string;
    };

/**
 * The pluggable, byte-native HTTP transport a generated client depends on — the TypeScript analogue of
 * the C# `IApiTransport`. A generated client builds an {@link ApiRequest} + a {@link RequestBody},
 * hands them here, and the transport returns the typed response via the supplied {@link ResponseFactory}.
 * Concrete transports (fetch, Node) wrap the platform's HTTP stack; the API surface is platform-neutral.
 */
export interface ApiTransport extends AsyncDisposable {
  /**
   * Sends a request and decodes the response.
   * @param request The composed per-operation request.
   * @param factory The factory that builds the typed response from the wire result.
   * @param body The request body; defaults to no body.
   * @param signal An optional abort signal (replaces the C# `CancellationToken`).
   * @returns The typed response.
   */
  send<TResponse extends ApiResponse>(
    request: ApiRequest,
    factory: ResponseFactory<TResponse>,
    body?: RequestBody,
    signal?: AbortSignal,
  ): Promise<TResponse>;
}
