import type { WireRequest, WireResponse } from "../contracts/wire.js";

/** Invokes the next handler in the pipeline (the innermost {@link Next} is the transport's network send). */
export type Next = (request: WireRequest) => Promise<WireResponse>;

/**
 * A wire-level middleware handler — the analogue of an ASP.NET `DelegatingHandler` / a Kiota middleware.
 * It receives the byte request and the rest of the chain, and may inspect, rewrite, retry, or short-circuit.
 * This flat, composable shape is the ONLY place wire concerns (retry, redirect, timeout, and later
 * compression/observability/chaos) live; new handlers slot in with no contract change.
 */
export type Handler = (request: WireRequest, next: Next) => Promise<WireResponse>;

/**
 * Composes a handler chain outer-to-inner: `handlers[0]` is the outermost wrapper and `terminal` (the
 * transport's actual network send) is the innermost call.
 * @param handlers The handlers, outermost first.
 * @param terminal The innermost call (the platform dispatch).
 * @returns A single {@link Next} that runs the whole chain.
 */
export function compose(handlers: readonly Handler[], terminal: Next): Next {
  return handlers.reduceRight<Next>(
    (next, handler) => (request) => handler(request, next),
    terminal,
  );
}
