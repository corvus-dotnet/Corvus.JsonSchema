/**
 * OpenAPI 3.x parameter-style serializers — the TypeScript analogue of the C#
 * `CodeEmitHelpers.EmitPathParamWrite` / `EmitQueryParamWrite` / `EmitHeaderParamWrite` behavior. The
 * generated `writeResolvedPath` / `writeQueryString` / `writeHeaders` methods call these helpers
 * rather than inlining the per-style byte writing.
 *
 * Phase 1 scope: `simple` (path + header) and `form` (query) styles, for scalar and array values.
 * Label/matrix/spaceDelimited/pipeDelimited/deepObject/cookie styles and object-typed values are out
 * of scope and are not handled here (they land in a later phase). Values arrive as the idiomatic TS
 * shape the model interface produces: a scalar (string/number/boolean) or an array of scalars.
 */

import type { ByteWriter } from "./buffer-writer.js";
import { encodeData, encodeAllowReserved } from "./percent-encoding.js";

/** A scalar parameter value. */
export type StyleScalar = string | number | boolean;

/** A parameter value: a scalar or an array of scalars. */
export type StyleValue = StyleScalar | readonly StyleScalar[];

/** Renders a scalar to its unencoded string form (the value a serializer then percent-encodes). */
function scalarText(value: StyleScalar): string {
  return typeof value === "string" ? value : String(value);
}

/**
 * Writes a path parameter value in `style=simple` form (the default for path parameters).
 *
 * For a scalar the percent-encoded value is written directly (`blue` ⇒ `blue`, a space ⇒ `%20`). For
 * an array the (percent-encoded) elements are joined with `,` regardless of `explode` — exploded and
 * non-exploded `simple` arrays are identical in the path. An empty array writes nothing.
 * @param w The byte writer the resolved path is composed into.
 * @param value The scalar or array value.
 * @param _explode Whether the parameter is exploded (no effect for `simple`; accepted for symmetry).
 */
export function writePathSimple(w: ByteWriter, value: StyleValue, _explode: boolean): void {
  if (Array.isArray(value)) {
    let first = true;
    for (const item of value) {
      if (!first) {
        w.writeAscii(",");
      }

      w.writeAscii(encodeData(scalarText(item as StyleScalar)));
      first = false;
    }

    return;
  }

  w.writeAscii(encodeData(scalarText(value as StyleScalar)));
}

/**
 * Writes a query parameter in `style=form` form (the default for query parameters), composing the
 * `name=value` pair(s) and the `?`/`&` separators byte-natively.
 *
 * - Scalar: `name=value` (explode has no effect).
 * - Array, `explode=false`: `name=v1,v2,v3` (comma-joined, single pair).
 * - Array, `explode=true`: `name=v1&name=v2&name=v3` (one pair per element).
 *
 * An empty string scalar writes `name=` (the `=` is always emitted for a present scalar). An empty
 * array writes nothing and returns 0. The caller passes `first=true` for the first parameter written
 * to the query string; this helper emits the leading separator (`&`) for every pair after the first,
 * so the caller does not write any `?`/`&` itself — it only tracks whether anything has been written.
 * @param w The byte writer the query string is composed into (without a leading `?`).
 * @param name The query parameter name (written verbatim; assumed already URL-safe).
 * @param value The scalar or array value.
 * @param explode Whether the parameter is exploded.
 * @param allowReserved Whether reserved characters are left unescaped (OpenAPI `allowReserved`).
 * @param first Whether nothing has yet been written to the query string.
 * @returns The number of `name=value` pairs written (0 ⇒ the caller's `first` is unchanged).
 */
export function writeQueryForm(
  w: ByteWriter,
  name: string,
  value: StyleValue,
  explode: boolean,
  allowReserved: boolean,
  first: boolean,
): number {
  const encode = allowReserved ? encodeAllowReserved : encodeData;

  if (Array.isArray(value)) {
    if (value.length === 0) {
      return 0;
    }

    if (explode) {
      // One name=value pair per element.
      let pairs = 0;
      for (const item of value) {
        if (!first || pairs > 0) {
          w.writeAscii("&");
        }

        w.writeAscii(name);
        w.writeAscii("=");
        w.writeAscii(encode(scalarText(item as StyleScalar)));
        pairs++;
      }

      return pairs;
    }

    // Single name=v1,v2,v3 pair. The "," separator is written literally (it is the form-style array
    // delimiter, not part of a value), matching the C# `EmitQueryArrayWrite`; only the element values
    // are percent-encoded.
    if (!first) {
      w.writeAscii("&");
    }

    w.writeAscii(name);
    w.writeAscii("=");
    let firstItem = true;
    for (const item of value) {
      if (!firstItem) {
        w.writeAscii(",");
      }

      w.writeAscii(encode(scalarText(item as StyleScalar)));
      firstItem = false;
    }

    return 1;
  }

  // Scalar: name=value (always emits the "=", so an empty string yields "name=").
  if (!first) {
    w.writeAscii("&");
  }

  w.writeAscii(name);
  w.writeAscii("=");
  w.writeAscii(encode(scalarText(value as StyleScalar)));
  return 1;
}

/**
 * Returns a header parameter value in `style=simple` form (the only style for header parameters).
 *
 * Header values are NOT percent-encoded (they travel in the header field, not the URI). A scalar
 * returns its string form; an array returns the comma-joined elements regardless of `explode`.
 * @param value The scalar or array value.
 * @param _explode Whether the parameter is exploded (no effect for `simple`; accepted for symmetry).
 * @returns The header field value string.
 */
export function writeHeaderSimple(value: StyleValue, _explode: boolean): string {
  if (Array.isArray(value)) {
    return value.map((item) => scalarText(item as StyleScalar)).join(",");
  }

  return scalarText(value as StyleScalar);
}
