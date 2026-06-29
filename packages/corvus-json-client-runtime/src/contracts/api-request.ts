import type { ByteWriter } from "../serializers/buffer-writer.js";
import type { OperationMethod } from "./operation-method.js";
import type { ValidationMode } from "./validation-mode.js";

/**
 * Receives header name/value pairs during request composition — the closure-capturing TypeScript
 * analogue of the C# `HeaderCallback<TState>` delegate (the state is captured by the closure, so no
 * explicit state parameter is needed).
 */
export type HeaderSink = (name: string, value: string) => void;

/**
 * A generated per-operation request. It composes the wire request byte-natively: the transport calls
 * the `write*` methods synchronously to build the path, query, headers, and cookies before any async
 * I/O (the TypeScript analogue of the C# `IApiRequest<TSelf>` contract, where the request is consumed
 * by `in` reference before the first await).
 */
export interface ApiRequest {
  /** The operation's HTTP method. */
  readonly method: OperationMethod;

  /** The unresolved path template, e.g. `/pets/{petId}`. */
  readonly pathTemplate: string;

  /** The custom method name when {@link method} is {@link OperationMethod.Custom} (OpenAPI 3.2). */
  readonly customMethodName?: string;

  /** Whether the operation has any path parameters (gate flag, avoids a needless resolve). */
  readonly hasPathParameters: boolean;

  /** Whether the operation has any query parameters. */
  readonly hasQueryParameters: boolean;

  /** Whether the operation has any header parameters. */
  readonly hasHeaderParameters: boolean;

  /** Whether the operation has any cookie parameters. */
  readonly hasCookieParameters: boolean;

  /** Writes the resolved path (template with path parameters substituted and percent-encoded). */
  writeResolvedPath(writer: ByteWriter): void;

  /** Writes the query string (without a leading `?`); returns the number of bytes written (0 ⇒ omit). */
  writeQueryString(writer: ByteWriter): number;

  /** Emits each header (name + serialized value) to the sink. */
  writeHeaders(sink: HeaderSink): void;

  /** Writes the cookie header value (`name=value; ...`); returns the bytes written (0 ⇒ no Cookie). */
  writeCookies(writer: ByteWriter): number;

  /** Validates the request parameters against their schemas; throws on failure. */
  validate(mode?: ValidationMode): void;
}
