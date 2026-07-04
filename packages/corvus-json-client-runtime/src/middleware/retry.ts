import type { WireResponse } from "../contracts/wire.js";
import type { Handler } from "./handler.js";

/**
 * Options for {@link retryHandler}.
 */
export interface RetryOptions {
  /** The maximum number of retries after the initial attempt. Defaults to `3`. */
  readonly maxRetries?: number;

  /** The base backoff delay in milliseconds. Defaults to `500`. */
  readonly baseDelayMs?: number;

  /** The maximum backoff delay in milliseconds (the jitter ceiling). Defaults to `30000`. */
  readonly maxDelayMs?: number;

  /** The HTTP status codes that trigger a retry. Defaults to `[429, 502, 503, 504]`. */
  readonly retryableStatusCodes?: readonly number[];

  /** Whether to honour a `Retry-After` response header when present. Defaults to `true`. */
  readonly respectRetryAfter?: boolean;
}

/**
 * A wire-level handler that retries failed requests with full-jitter exponential backoff — the analogue
 * of a resilience policy (Polly / Kiota's retry handler).
 *
 * A body is only replayed when it is safe to re-send: `none`, `bytes`, and `writer` bodies are
 * deterministic and re-emitted on each attempt, but a `stream` body has already been consumed by the
 * first attempt and is NOT retried (the first response is returned as-is). When a retryable response is
 * discarded, its body is drained so the connection is released before the next attempt.
 * @param options The retry policy options.
 * @returns The handler.
 */
export function retryHandler(options?: RetryOptions): Handler {
  const maxRetries = options?.maxRetries ?? 3;
  const baseDelayMs = options?.baseDelayMs ?? 500;
  const maxDelayMs = options?.maxDelayMs ?? 30000;
  const retryableStatusCodes = options?.retryableStatusCodes ?? [429, 502, 503, 504];
  const respectRetryAfter = options?.respectRetryAfter ?? true;

  return async (request, next): Promise<WireResponse> => {
    for (let attempt = 0; ; attempt++) {
      const res = await next(request);

      const retryable = retryableStatusCodes.includes(res.statusCode);
      // A consumed stream cannot be replayed; return the first response unchanged.
      const replayable = request.body.kind !== "stream";
      if (attempt >= maxRetries || !retryable || !replayable) {
        return res;
      }

      // Drain + release the discarded response before backing off.
      await res.body?.cancel().catch(() => {});
      await res.dispose?.();

      const retryAfterMs = respectRetryAfter ? parseRetryAfter(res.headers.get("Retry-After")) : null;
      const delayMs =
        retryAfterMs ?? Math.random() * Math.min(maxDelayMs, baseDelayMs * 2 ** attempt);

      await delay(delayMs, request.signal);
    }
  };
}

/**
 * Parses a `Retry-After` header value to milliseconds — accepts both an integer-seconds delay and an
 * HTTP-date. Returns `null` when the header is absent or unparseable.
 */
function parseRetryAfter(value: string | null): number | null {
  if (value === null) {
    return null;
  }

  const trimmed = value.trim();
  if (/^\d+$/.test(trimmed)) {
    return Number(trimmed) * 1000;
  }

  const date = Date.parse(trimmed);
  if (!Number.isNaN(date)) {
    return Math.max(0, date - Date.now());
  }

  return null;
}

/** A promise that resolves after `ms`, or rejects if `signal` aborts first; cleans up its timer + listener. */
function delay(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    if (signal.aborted) {
      reject(signal.reason instanceof Error ? signal.reason : new DOMException("Aborted", "AbortError"));
      return;
    }

    const onAbort = (): void => {
      clearTimeout(timer);
      reject(signal.reason instanceof Error ? signal.reason : new DOMException("Aborted", "AbortError"));
    };

    const timer = setTimeout(() => {
      signal.removeEventListener("abort", onAbort);
      resolve();
    }, ms);

    signal.addEventListener("abort", onAbort, { once: true });
  });
}
