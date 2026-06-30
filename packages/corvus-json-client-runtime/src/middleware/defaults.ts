import type { Handler } from "./handler.js";
import { redirectHandler, type RedirectOptions } from "./redirect.js";
import { retryHandler, type RetryOptions } from "./retry.js";
import { timeoutHandler, type TimeoutOptions } from "./timeout.js";

/**
 * Options for {@link defaultHandlers}.
 */
export interface DefaultHandlersOptions {
  /** The retry policy options. */
  readonly retry?: RetryOptions;

  /** The redirect policy options. */
  readonly redirect?: RedirectOptions;

  /**
   * The per-attempt timeout. When omitted a sane default of 100 seconds is applied so a request can
   * never hang forever.
   */
  readonly timeout?: TimeoutOptions;
}

/**
 * Builds the recommended handler chain in the canonical order `redirect → retry → timeout → send`:
 * the redirect handler is outermost (it re-issues a request that the retry handler then guards), the
 * retry handler is in the middle (each attempt is independently timed), and the timeout handler is
 * innermost (a per-attempt deadline closest to the network send). Pass the result as the transport's
 * `handlers`.
 * @param options The policy options for each handler.
 * @returns The composed handler array, outermost first.
 */
export function defaultHandlers(options?: DefaultHandlersOptions): Handler[] {
  return [
    redirectHandler(options?.redirect),
    retryHandler(options?.retry),
    timeoutHandler(options?.timeout ?? { timeoutMs: 100000 }),
  ];
}
