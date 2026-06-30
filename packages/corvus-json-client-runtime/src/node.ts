/**
 * Node-only entry of `@endjin/corvus-json-client-runtime` (the `./node` export).
 *
 * It re-exports the entire neutral surface, then adds the {@link NodeApiTransport} — the ONLY part of
 * the package that imports `node:*`. Keeping it behind this separate entry guarantees the Node HTTP
 * stack never leaks into a browser/edge bundle that imports only the package root.
 */

export * from "./index.js";
export { NodeApiTransport } from "./transports/node-transport.js";
export type { NodeApiTransportOptions } from "./transports/node-transport.js";
