import type { AuthenticationProvider } from "./authentication-provider.js";

/** Produces a bearer token, optionally per-request (for refresh/caching). */
export type TokenFactory = (signal: AbortSignal) => Promise<string> | string;

/**
 * Bearer-token authentication (OpenAPI `http`/`bearer`, OAuth2, OIDC) — sets `Authorization: Bearer <token>`.
 * Mirrors the C# `BearerTokenAuthenticationProvider`: pass a static token, or a factory that is invoked once
 * per request so a token refreshed between sends is picked up.
 * @param tokenOrFactory The static token, or a factory producing one per request.
 * @returns The authentication provider.
 */
export function bearerToken(tokenOrFactory: string | TokenFactory): AuthenticationProvider {
  if (typeof tokenOrFactory === "string") {
    const header = `Bearer ${tokenOrFactory}`;
    return {
      authenticate(request): void {
        request.headers.set("Authorization", header);
      },
    };
  }

  return {
    async authenticate(request, signal): Promise<void> {
      request.headers.set("Authorization", `Bearer ${await tokenOrFactory(signal)}`);
    },
  };
}
