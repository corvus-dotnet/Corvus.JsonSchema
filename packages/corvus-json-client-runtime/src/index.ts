/**
 * Public, platform-neutral surface of `@endjin/corvus-json-client-runtime`.
 *
 * This entry carries the transport contracts, serializers, middleware, auth providers, and the fetch
 * transport — everything that runs unchanged in a browser, edge runtime, Deno, Bun, or Node. The Node
 * transport (which imports `node:*` / `undici`) lives behind the separate `./node` entry so it never
 * leaks into a browser bundle.
 *
 * NOTE: this is the package foundation (Workstream B). The contracts and byte-buffer primitives are in
 * place; the serializers, middleware pipeline, transports, and auth providers land in subsequent
 * increments and will be re-exported here.
 */

export { OperationMethod } from "./contracts/operation-method.js";
export { ValidationMode } from "./contracts/validation-mode.js";
export type { ApiRequest, HeaderSink } from "./contracts/api-request.js";
export type { ApiResponse, ResponseContext, ResponseFactory, ResponseHeaders } from "./contracts/api-response.js";
export type { ApiTransport, RequestBody } from "./contracts/api-transport.js";
export type { WireRequest, WireResponse } from "./contracts/wire.js";
export { ByteWriter } from "./serializers/buffer-writer.js";
export type { ByteSink } from "./serializers/buffer-writer.js";
