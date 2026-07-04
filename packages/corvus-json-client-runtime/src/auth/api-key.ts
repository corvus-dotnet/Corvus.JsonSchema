import { encodeData } from "../serializers/percent-encoding.js";
import type { AuthenticationProvider } from "./authentication-provider.js";

/** Where an API key is carried (OpenAPI `apiKey` scheme `in`). */
export type ApiKeyLocation = "header" | "query" | "cookie";

/**
 * API-key authentication (OpenAPI `apiKey`) — mirrors the C# `ApiKeyAuthenticationProvider`. Sets a header,
 * appends a percent-encoded query parameter (RFC 3986, matching `Uri.EscapeDataString`), or adds a cookie,
 * depending on {@link ApiKeyLocation}.
 * @param value The API key.
 * @param parameterName The header / query / cookie name.
 * @param location Where to place the key.
 * @returns The authentication provider.
 */
export function apiKey(value: string, parameterName: string, location: ApiKeyLocation): AuthenticationProvider {
  return {
    authenticate(request): void {
      switch (location) {
        case "header":
          request.headers.set(parameterName, value);
          break;
        case "query": {
          const separator = request.url.includes("?") ? "&" : "?";
          request.url += `${separator}${encodeData(parameterName)}=${encodeData(value)}`;
          break;
        }
        case "cookie": {
          const existing = request.headers.get("Cookie");
          const cookie = `${parameterName}=${value}`;
          request.headers.set("Cookie", existing ? `${existing}; ${cookie}` : cookie);
          break;
        }
      }
    },
  };
}
