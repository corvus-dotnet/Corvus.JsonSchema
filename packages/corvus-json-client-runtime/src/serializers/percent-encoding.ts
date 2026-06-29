/**
 * RFC 3986 percent-encoding for OpenAPI parameter serialization — the TypeScript analogue of the C#
 * `Utf8Uri.TryEscapeDataString` / `TryEscapeUri` used by the generated path/query/header/cookie writers.
 *
 * `encodeURIComponent` is deliberately NOT used: it leaves `!'()*` unescaped and cannot express the
 * `allowReserved` distinction OpenAPI needs. These functions encode at the UTF-8 byte level (so any
 * non-ASCII code point becomes its `%XX` byte sequence) and uppercase the hex, matching the C# escaper.
 */

const HEX = "0123456789ABCDEF";
const encoder = new TextEncoder();

// RFC 3986 unreserved set: ALPHA / DIGIT / "-" / "." / "_" / "~". Never percent-encoded.
const UNRESERVED = buildSet("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~");

// RFC 3986 reserved set: gen-delims (":" "/" "?" "#" "[" "]" "@") + sub-delims
// ("!" "$" "&" "'" "(" ")" "*" "+" "," ";" "="). Left intact only when allowReserved is true.
const RESERVED = buildSet(":/?#[]@!$&'()*+,;=");

function buildSet(chars: string): Uint8Array {
  const set = new Uint8Array(128);
  for (let i = 0; i < chars.length; i++) {
    set[chars.charCodeAt(i)] = 1;
  }

  return set;
}

function encode(value: string, allowReserved: boolean): string {
  const bytes = encoder.encode(value);
  let out = "";
  for (let i = 0; i < bytes.length; i++) {
    const b = bytes[i];
    if (b < 0x80 && (UNRESERVED[b] === 1 || (allowReserved && RESERVED[b] === 1))) {
      out += String.fromCharCode(b);
    } else {
      out += "%" + HEX[b >> 4] + HEX[b & 0x0f];
    }
  }

  return out;
}

/**
 * Percent-encodes a value for the "data" set: everything outside the RFC 3986 unreserved set is
 * escaped (a space becomes `%20`, not `+`). This is the default for path and query parameter values.
 * @param value The value to encode.
 * @returns The percent-encoded ASCII string.
 */
export function encodeData(value: string): string {
  return encode(value, false);
}

/**
 * Percent-encodes a value but leaves the RFC 3986 reserved set intact — the OpenAPI `allowReserved`
 * behavior for query parameters.
 * @param value The value to encode.
 * @returns The percent-encoded ASCII string.
 */
export function encodeAllowReserved(value: string): string {
  return encode(value, true);
}
