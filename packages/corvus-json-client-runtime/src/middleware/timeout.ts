import type { WireResponse } from "../contracts/wire.js";
import type { Handler } from "./handler.js";

/**
 * Options for {@link timeoutHandler}.
 */
export interface TimeoutOptions {
  /** The per-attempt timeout in milliseconds. */
  readonly timeoutMs: number;
}

/**
 * A wire-level handler that enforces a per-attempt timeout. It creates a fresh timeout signal for each
 * downstream call and combines it with the caller's signal, so a request aborts when EITHER the caller
 * cancels OR the timeout elapses. Sitting INNER to {@link retryHandler}, the timeout is applied per
 * attempt rather than across the whole retry sequence.
 * @param options The timeout options.
 * @returns The handler.
 */
export function timeoutHandler(options: TimeoutOptions): Handler {
  return (request, next): Promise<WireResponse> => {
    const timeout = AbortSignal.timeout(options.timeoutMs);
    const signal = AbortSignal.any([request.signal, timeout]);
    return next({ ...request, signal });
  };
}
