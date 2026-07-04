import type { WireRequest } from "../contracts/wire.js";

/**
 * Mutates a wire request to add authentication — the TypeScript analogue of the C#
 * `IHttpAuthenticationProvider` (a request mutator). The transport runs it once per logical `send`,
 * after the request is built but before it is dispatched, so a retry never replays a stale credential.
 */
export interface AuthenticationProvider {
  /**
   * Applies authentication to the request (sets a header, appends a query parameter, or adds a cookie).
   * @param request The wire request to mutate.
   * @param signal The request's abort signal (e.g. for an async token fetch).
   */
  authenticate(request: WireRequest, signal: AbortSignal): Promise<void> | void;
}
