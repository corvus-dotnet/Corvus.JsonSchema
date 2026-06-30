import type { AuthenticationProvider } from "./authentication-provider.js";

const encoder = new TextEncoder();

/** Base64-encodes a string at the UTF-8 byte level (RFC 7617), without depending on Node's Buffer. */
function base64Utf8(text: string): string {
  const bytes = encoder.encode(text);
  let binary = "";
  for (let i = 0; i < bytes.length; i++) {
    binary += String.fromCharCode(bytes[i]);
  }

  return btoa(binary);
}

/**
 * HTTP Basic authentication (OpenAPI `http`/`basic`) — mirrors the C# `BasicAuthenticationProvider`. Sets
 * `Authorization: Basic <base64(username:password)>`, computed once at construction.
 * @param username The user name.
 * @param password The password.
 * @returns The authentication provider.
 */
export function basicAuth(username: string, password: string): AuthenticationProvider {
  const header = `Basic ${base64Utf8(`${username}:${password}`)}`;
  return {
    authenticate(request): void {
      request.headers.set("Authorization", header);
    },
  };
}
