/**
 * Public, platform-neutral surface of `@endjin/corvus-json-client-runtime`.
 *
 * This entry carries the transport contracts, byte-buffer primitives, the OpenAPI parameter-style and
 * percent-encoding serializers, the auth providers, the composable middleware (retry / redirect /
 * timeout), and the portable {@link FetchApiTransport} — everything that runs unchanged in a browser,
 * edge runtime, Deno, Bun, or Node. The {@link NodeApiTransport} (the only part of the package that
 * imports `node:*`) lives behind the separate `./node` entry so it never leaks into a browser bundle.
 */

export { OperationMethod } from "./contracts/operation-method.js";
export { ValidationMode } from "./contracts/validation-mode.js";
export type { ApiRequest, HeaderSink } from "./contracts/api-request.js";
export type { ApiResponse, ResponseContext, ResponseFactory, ResponseHeaders } from "./contracts/api-response.js";
export type { ApiTransport, RequestBody } from "./contracts/api-transport.js";
export type { WireRequest, WireResponse } from "./contracts/wire.js";
export { ByteWriter } from "./serializers/buffer-writer.js";
export type { ByteSink } from "./serializers/buffer-writer.js";
export { encodeData, encodeAllowReserved } from "./serializers/percent-encoding.js";
export { writePathParam, writeQueryParam, writeHeaderParam, writeCookieParam } from "./serializers/style.js";
export type {
  StyleScalar,
  StyleObject,
  StyleValue,
  PathStyle,
  QueryStyle,
  CookieStyle,
} from "./serializers/style.js";
export { writeFormUrlEncoded, formUrlEncodedBytes } from "./serializers/form-urlencoded.js";
export type {
  FormValue,
  FormBody,
  FormPropertyEncoding,
  FormEncodings,
} from "./serializers/form-urlencoded.js";
export {
  parseHeaderString,
  parseHeaderNumber,
  parseHeaderBoolean,
  parseHeaderArray,
  parseHeaderObject,
} from "./serializers/header.js";
export { getByPointer } from "./serializers/json-pointer.js";
export { multipartFormData, multipartMixed } from "./serializers/multipart.js";
export type {
  MultipartBinaryPart,
  MultipartScalar,
  MultipartFieldValue,
  MultipartFormFields,
  MultipartFormOptions,
  MultipartMixedPart,
  MultipartMixedOptions,
} from "./serializers/multipart.js";
export type { AuthenticationProvider } from "./auth/authentication-provider.js";
export { bearerToken } from "./auth/bearer.js";
export type { TokenFactory } from "./auth/bearer.js";
export { apiKey } from "./auth/api-key.js";
export type { ApiKeyLocation } from "./auth/api-key.js";
export { basicAuth } from "./auth/basic.js";
export { compose } from "./middleware/handler.js";
export type { Handler, Next } from "./middleware/handler.js";
export { retryHandler } from "./middleware/retry.js";
export type { RetryOptions } from "./middleware/retry.js";
export { redirectHandler } from "./middleware/redirect.js";
export type { RedirectOptions } from "./middleware/redirect.js";
export { timeoutHandler } from "./middleware/timeout.js";
export type { TimeoutOptions } from "./middleware/timeout.js";
export { defaultHandlers } from "./middleware/defaults.js";
export type { DefaultHandlersOptions } from "./middleware/defaults.js";
export { AbstractApiTransport } from "./transports/abstract-transport.js";
export type { AbstractApiTransportOptions } from "./transports/abstract-transport.js";
export { FetchApiTransport } from "./transports/fetch-transport.js";
export type { FetchApiTransportOptions } from "./transports/fetch-transport.js";
