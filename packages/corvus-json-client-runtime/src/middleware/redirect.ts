import type { RequestBody } from "../contracts/api-transport.js";
import type { WireRequest, WireResponse } from "../contracts/wire.js";
import type { Handler } from "./handler.js";

/** The redirect status codes this handler follows. */
const REDIRECT_STATUS = new Set<number>([301, 302, 303, 307, 308]);

/**
 * Options for {@link redirectHandler}.
 */
export interface RedirectOptions {
  /** The maximum number of redirect hops to follow before returning the last 3xx response. Defaults to `20`. */
  readonly maxRedirects?: number;
}

/**
 * A wire-level handler that follows HTTP redirects — the analogue of the browser/curl redirect policy.
 * It is most useful with the {@link NodeApiTransport}, which never auto-follows; with `fetch` the
 * platform usually follows redirects itself (use `redirect: "manual"` to hand control to this handler
 * on server runtimes).
 *
 * Method/body rewriting follows the common convention:
 * - `303 See Other` always becomes a bodyless `GET`.
 * - `301`/`302` on a `POST` downgrades to a bodyless `GET` (browser/curl default).
 * - `307`/`308` preserve the method and body. A `stream` body has already been consumed by the first
 *   hop, so a 307/308 that needs to replay it cannot be followed reliably — use `bytes`/`writer` bodies
 *   when redirects on a non-idempotent method are expected.
 *
 * On a cross-origin redirect the `Authorization` and `Cookie` headers are stripped (they must not leak
 * to a different origin).
 * @param options The redirect policy options.
 * @returns The handler.
 */
export function redirectHandler(options?: RedirectOptions): Handler {
  const maxRedirects = options?.maxRedirects ?? 20;

  return async (request, next): Promise<WireResponse> => {
    let current = request;
    let res = await next(current);
    let hops = 0;

    while (REDIRECT_STATUS.has(res.statusCode) && hops < maxRedirects) {
      const location = res.headers.get("Location");
      if (location === null) {
        break;
      }

      const target = new URL(location, current.url);

      // Drain + release the response we are leaving before issuing the next hop.
      await res.body?.cancel().catch(() => {});
      await res.dispose?.();

      const headers = new Headers(current.headers);

      let method = current.method;
      let body: RequestBody = current.body;
      const downgradeToGet =
        res.statusCode === 303 ||
        ((res.statusCode === 301 || res.statusCode === 302) && current.method === "POST");

      if (downgradeToGet) {
        method = "GET";
        body = { kind: "none" };
        headers.delete("Content-Type");
        headers.delete("Content-Length");
      }

      // Strip credentials when the origin changes.
      if (target.origin !== new URL(current.url).origin) {
        headers.delete("Authorization");
        headers.delete("Cookie");
      }

      const nextRequest: WireRequest = {
        method,
        url: target.toString(),
        headers,
        body,
        signal: current.signal,
      };

      current = nextRequest;
      res = await next(current);
      hops++;
    }

    return res;
  };
}
