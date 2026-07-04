import type { ApiRequest } from "../contracts/api-request.js";
import type { ApiResponse, ResponseContext, ResponseFactory, ResponseHeaders } from "../contracts/api-response.js";
import type { ApiTransport, RequestBody } from "../contracts/api-transport.js";
import { OperationMethod } from "../contracts/operation-method.js";
import type { WireRequest, WireResponse } from "../contracts/wire.js";
import type { AuthenticationProvider } from "../auth/authentication-provider.js";
import { compose, type Handler } from "../middleware/handler.js";
import { ByteWriter } from "../serializers/buffer-writer.js";

/** A signal that never aborts — the default when a caller passes no abort signal. */
const NEVER_SIGNAL: AbortSignal = new AbortController().signal;

/** Decodes UTF-8 bytes written by the request's `write*` methods into the strings the wire layer needs. */
const decoder = new TextDecoder();

/**
 * Construction options shared by every concrete {@link AbstractApiTransport}.
 */
export interface AbstractApiTransportOptions {
  /**
   * The server base URL (the OpenAPI server URI). It may carry a path prefix (e.g.
   * `https://api.example.com/v1`); the per-operation path template is appended to it verbatim, so the
   * prefix is always preserved. A trailing `/` is stripped once at construction.
   */
  readonly baseUrl: string | URL;

  /** An optional authentication provider, run once per logical `send` after the request is built. */
  readonly authenticationProvider?: AuthenticationProvider;

  /** The wire-level middleware handlers, outermost first (retry, redirect, timeout, …). */
  readonly handlers?: readonly Handler[];
}

/**
 * The shared spine of the byte-native transports — the TypeScript analogue of the C#
 * `HttpClientTransport`. It builds the wire request synchronously (mirroring the C# consume-by-`in`
 * guarantee: the request's `write*` methods run before any await, so two concurrent `send` calls in
 * single-threaded JS never interleave their build phase and can safely share the reusable
 * {@link ByteWriter}s), runs auth once, threads the request through the composed handler chain, and
 * decodes the response via the supplied {@link ResponseFactory}. Concrete subclasses supply only the
 * platform {@link dispatch}.
 */
export abstract class AbstractApiTransport implements ApiTransport {
  /** The normalized base URL (trailing slash stripped). */
  protected readonly baseUrl: string;

  /** The authentication provider, or `undefined` for unauthenticated requests. */
  protected readonly authenticationProvider?: AuthenticationProvider;

  /** The frozen handler chain, outermost first. */
  protected readonly handlers: readonly Handler[];

  /** A reusable writer for the URI path + query. Safe to share: build-wire contains no await. */
  private readonly uriWriter = new ByteWriter(512);

  /** A reusable writer for the cookie header value. Safe to share: build-wire contains no await. */
  private readonly cookieWriter = new ByteWriter(256);

  /**
   * Initializes the transport from the shared options.
   * @param options The construction options.
   */
  protected constructor(options: AbstractApiTransportOptions) {
    this.baseUrl = String(options.baseUrl).replace(/\/$/, "");
    if (options.authenticationProvider !== undefined) {
      this.authenticationProvider = options.authenticationProvider;
    }

    this.handlers = Object.freeze(options.handlers ? [...options.handlers] : []);
  }

  /**
   * Dispatches the byte request over the platform's HTTP stack and returns the wire response. The only
   * member a concrete transport must implement.
   * @param request The byte-level request snapshot.
   * @returns The wire response.
   */
  protected abstract dispatch(request: WireRequest): Promise<WireResponse>;

  /** @inheritdoc */
  public async send<TResponse extends ApiResponse>(
    request: ApiRequest,
    factory: ResponseFactory<TResponse>,
    body?: RequestBody,
    signal?: AbortSignal,
  ): Promise<TResponse> {
    // Build the wire request synchronously, before any await — the JS analogue of the C# consume-by-`in`
    // guarantee. This is the only point at which the reusable ByteWriters are touched.
    const wire = this.buildWireRequest(request, body ?? { kind: "none" }, signal ?? NEVER_SIGNAL);

    // Auth runs once per logical send, after build + before dispatch, so a retry never replays a stale
    // credential.
    await this.authenticationProvider?.authenticate(wire, wire.signal);

    const run = compose(this.handlers, (req) => this.dispatch(req));
    const wireResponse = await run(wire);

    const context: ResponseContext = {
      statusCode: wireResponse.statusCode,
      body: wireResponse.body,
      contentType: mediaType(wireResponse.headers),
      headers: headersAdapter(wireResponse.headers),
      transport: this,
      ...(signal !== undefined ? { signal } : {}),
    };

    try {
      return await factory.create(context);
    } catch (e) {
      // The factory rejected before it could consume the body — free the unconsumed wire response. On
      // the success path the factory buffers the body, which releases the connection, so we never
      // force-dispose there (keep-alive must work).
      await wireResponse.dispose?.();
      throw e;
    }
  }

  /**
   * Releases resources owned by this transport. The default is a no-op; concrete transports override
   * when they own a connection pool or agent.
   */
  public async [Symbol.asyncDispose](): Promise<void> {
    // No owned resources by default.
  }

  /**
   * Builds the byte-level wire request from a generated {@link ApiRequest} — mirrors the C#
   * `BuildHttpRequest`/`BuildUri`/`MapMethod`. Runs entirely synchronously: the reusable writers are
   * reset at entry and consumed before the method returns, and a fresh {@link Headers} is allocated per
   * call so the snapshot never aliases shared state.
   * @param request The composed per-operation request.
   * @param body The request body descriptor.
   * @param signal The abort signal for this request.
   * @returns A fresh wire request snapshot.
   */
  private buildWireRequest(request: ApiRequest, body: RequestBody, signal: AbortSignal): WireRequest {
    const url = this.buildUrl(request);
    const headers = new Headers();

    if (request.hasHeaderParameters) {
      request.writeHeaders((name, value) => headers.append(name, value));
    }

    if (request.hasCookieParameters) {
      this.cookieWriter.reset();
      const cookieBytes = request.writeCookies(this.cookieWriter);
      if (cookieBytes > 0) {
        headers.set("Cookie", decoder.decode(this.cookieWriter.written));
      }
    }

    // Content-Type comes from the body descriptor (writeHeaders is for header PARAMS only), so the
    // middleware + dispatch see it on the headers.
    if (body.kind !== "none") {
      headers.set("Content-Type", body.contentType);
    }

    return {
      method: mapMethod(request),
      url,
      headers,
      body,
      signal,
    };
  }

  /**
   * Composes the absolute request URL by STRING CONCATENATION — never `new URL(path, base)`, which would
   * drop a base path prefix. The base URL has its trailing slash already stripped; the resolved path (or
   * the template) begins with `/`. Mirrors the C# `BuildUri`: write the `?` optimistically and drop it
   * when the query string is empty.
   * @param request The composed per-operation request.
   * @returns The absolute request URL.
   */
  private buildUrl(request: ApiRequest): string {
    const writer = this.uriWriter;
    writer.reset();

    if (request.hasPathParameters) {
      request.writeResolvedPath(writer);
    } else {
      writer.writeUtf8(request.pathTemplate);
    }

    const path = decoder.decode(writer.written);

    if (request.hasQueryParameters) {
      writer.reset();
      const queryBytes = request.writeQueryString(writer);
      if (queryBytes > 0) {
        return `${this.baseUrl}${path}?${decoder.decode(writer.written)}`;
      }
    }

    return `${this.baseUrl}${path}`;
  }
}

/**
 * Maps a generated request's {@link OperationMethod} to the wire verb — mirrors the C# `MapMethod`.
 * `Custom` resolves to the request's `customMethodName`; `Query` becomes `QUERY`; the AsyncAPI
 * `Publish`/`Subscribe` pair has no HTTP mapping and throws.
 * @param request The composed per-operation request.
 * @returns The uppercase wire verb.
 */
function mapMethod(request: ApiRequest): string {
  switch (request.method) {
    case OperationMethod.Get:
      return "GET";
    case OperationMethod.Put:
      return "PUT";
    case OperationMethod.Post:
      return "POST";
    case OperationMethod.Delete:
      return "DELETE";
    case OperationMethod.Options:
      return "OPTIONS";
    case OperationMethod.Head:
      return "HEAD";
    case OperationMethod.Patch:
      return "PATCH";
    case OperationMethod.Trace:
      return "TRACE";
    case OperationMethod.Query:
      return "QUERY";
    case OperationMethod.Custom: {
      const name = request.customMethodName;
      if (name === undefined || name.length === 0) {
        throw new Error("A Custom operation method requires a non-empty customMethodName.");
      }

      return name;
    }
    case OperationMethod.Publish:
    case OperationMethod.Subscribe:
      throw new Error(
        `The AsyncAPI operation method ${OperationMethod[request.method]} has no HTTP wire mapping.`,
      );
    default:
      throw new Error(`Unknown operation method: ${String(request.method)}.`);
  }
}

/**
 * Extracts the media type from a `Content-Type` header (strips any `;charset=...` and other parameters).
 * @param headers The headers to read.
 * @returns The lower-level media type, or `null` when the header is absent.
 */
export function mediaType(headers: Headers): string | null {
  const value = headers.get("Content-Type");
  if (value === null) {
    return null;
  }

  const semicolon = value.indexOf(";");
  const media = semicolon === -1 ? value : value.slice(0, semicolon);
  return media.trim();
}

/** Adapts a WHATWG {@link Headers} to the case-insensitive {@link ResponseHeaders} contract. */
function headersAdapter(headers: Headers): ResponseHeaders {
  return {
    tryGet(name: string): string | undefined {
      // Headers.get is case-insensitive and comma-joins repeated values (RFC 9110 §5.3).
      return headers.get(name) ?? undefined;
    },
  };
}
