/**
 * OpenAPI 3.x parameter-style serializers — the TypeScript analogue of the C#
 * `CodeEmitHelpers.EmitPathParamWrite` / `EmitQueryParamWrite` / `EmitHeaderParamWrite` /
 * `EmitCookieParamWrite` behavior. A generated `writeResolvedPath` / `writeQueryString` /
 * `writeHeaders` / `writeCookies` method calls one of the four `write*Param` entry points per
 * parameter, passing the parameter's style + explode; the entry point dispatches on the value shape
 * (scalar / array / object) and the style, mirroring the C# emitter byte-for-byte in structure.
 *
 * Encoding: the C# emitter mirrors the OpenAPI style STRUCTURE (prefixes, separators, the deepObject
 * `name[key]` brackets, the explode behaviour) but is uneven about percent-encoding data values inside
 * arrays/objects. This runtime keeps the identical structure while applying correct, consistent
 * encoding for the URI-borne locations:
 * - `path` / `query`: every data value (and object key) is percent-encoded; the structural delimiters
 *   (`,` `.` `;` `=` and the pre-encoded `%20` / `%7C` / `%5B` / `%5D`) are written literally.
 * - `header`: values travel in the header field, not the URI, so they are NOT percent-encoded.
 * - `cookie`: follows the C# cookie behaviour — a `form` scalar is encoded, a `cookie`-style (RFC 6265,
 *   OpenAPI 3.2) scalar and all array/object element values are written raw.
 *
 * Values arrive as the idiomatic TS shape a generated model interface produces: a scalar
 * (string/number/boolean), an array of scalars, or an object whose values are scalars.
 */

import type { ByteWriter } from "./buffer-writer.js";
import { encodeData, encodeAllowReserved } from "./percent-encoding.js";

/** A scalar parameter value. */
export type StyleScalar = string | number | boolean;

/** An object parameter value (a record of scalar values). */
export type StyleObject = { readonly [key: string]: StyleScalar };

/** A parameter value: a scalar, an array of scalars, or an object of scalars. */
export type StyleValue = StyleScalar | readonly StyleScalar[] | StyleObject;

/** The serialization style for a `path` parameter. */
export type PathStyle = "simple" | "label" | "matrix";

/** The serialization style for a `query` parameter. */
export type QueryStyle = "form" | "spaceDelimited" | "pipeDelimited" | "deepObject";

/** The serialization style for a `cookie` parameter. */
export type CookieStyle = "form" | "cookie";

/** Renders a scalar to its unencoded string form (the value a serializer then percent-encodes). */
function scalarText(value: StyleScalar): string {
  return typeof value === "string" ? value : String(value);
}

/**
 * Whether a value is an array. A custom guard (rather than `Array.isArray` directly) is needed so the
 * false branch narrows `readonly StyleScalar[]` out of {@link StyleValue} — `Array.isArray`'s built-in
 * guard is `arg is any[]`, which does not remove a readonly array from a union.
 */
function isArrayValue(value: StyleValue): value is readonly StyleScalar[] {
  return Array.isArray(value);
}

/** Whether a value is an object (record), as opposed to a scalar or array. */
function isObject(value: StyleValue): value is StyleObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Percent-encodes a value, honouring `allowReserved` (OpenAPI: leave reserved characters unescaped). */
function encodeValue(text: string, allowReserved: boolean): string {
  return allowReserved ? encodeAllowReserved(text) : encodeData(text);
}

// ── Path parameters (writeResolvedPath) ───────────────────────────────────────

/**
 * Writes a `path` parameter into the resolved path — mirrors the C# `EmitPathParamWrite`.
 *
 * Scalars are prefixed per style (`label` ⇒ `.`, `matrix` ⇒ `;name=`, `simple` ⇒ none) then the
 * percent-encoded value. Arrays and objects use the style/explode-specific prefix + separator +
 * key/value separator. Nothing is written for an empty array/object beyond the (possibly empty) prefix.
 * @param w The byte writer the resolved path is composed into.
 * @param name The parameter name (used by the `matrix` style and object keys).
 * @param value The scalar, array, or object value.
 * @param style The path serialization style.
 * @param explode Whether the parameter is exploded.
 * @param allowReserved Whether reserved characters are left unescaped (OpenAPI `allowReserved`).
 */
export function writePathParam(
  w: ByteWriter,
  name: string,
  value: StyleValue,
  style: PathStyle,
  explode: boolean,
  allowReserved = false,
): void {
  if (isArrayValue(value)) {
    writePathArray(w, name, value, style, explode, allowReserved);
    return;
  }

  if (isObject(value)) {
    writePathObject(w, name, value, style, explode, allowReserved);
    return;
  }

  // Scalar: style prefix, then the encoded value.
  writePathPrefix(w, name, style);
  w.writeAscii(encodeValue(scalarText(value), allowReserved));
}

/** Writes the scalar/leading prefix for a path parameter (`label` ⇒ `.`, `matrix` ⇒ `;name=`). */
function writePathPrefix(w: ByteWriter, name: string, style: PathStyle): void {
  if (style === "label") {
    w.writeAscii(".");
  } else if (style === "matrix") {
    w.writeAscii(";");
    w.writeAscii(encodeData(name));
    w.writeAscii("=");
  }
}

/** Writes a path array per style + explode — mirrors the C# `EmitPathArrayWrite`. */
function writePathArray(
  w: ByteWriter,
  name: string,
  value: readonly StyleScalar[],
  style: PathStyle,
  explode: boolean,
  allowReserved: boolean,
): void {
  // prefix: label ⇒ ".", matrix & !explode ⇒ ";name=", matrix & explode ⇒ ";", simple ⇒ "".
  // separator: label & explode ⇒ ".", matrix & explode ⇒ ";name=", else ⇒ ",".
  const encodedName = encodeData(name);
  if (style === "label") {
    w.writeAscii(".");
  } else if (style === "matrix" && !explode) {
    w.writeAscii(";");
    w.writeAscii(encodedName);
    w.writeAscii("=");
  } else if (style === "matrix") {
    w.writeAscii(";");
  }

  // matrix & explode: the first element also needs the "name=" prefix.
  if (style === "matrix" && explode) {
    w.writeAscii(encodedName);
    w.writeAscii("=");
  }

  const separator =
    style === "label" && explode
      ? "."
      : style === "matrix" && explode
        ? `;${encodedName}=`
        : ",";

  let first = true;
  for (const item of value) {
    if (!first) {
      w.writeAscii(separator);
    }

    w.writeAscii(encodeValue(scalarText(item), allowReserved));
    first = false;
  }
}

/** Writes a path object per style + explode — mirrors the C# `EmitPathObjectWrite`. */
function writePathObject(
  w: ByteWriter,
  name: string,
  value: StyleObject,
  style: PathStyle,
  explode: boolean,
  allowReserved: boolean,
): void {
  // prefix: label ⇒ ".", matrix & !explode ⇒ ";name=", matrix & explode ⇒ ";", simple ⇒ "".
  // separator: label & explode ⇒ ".", matrix & explode ⇒ ";", else ⇒ ",".
  // key/value separator: explode ⇒ "=", else ⇒ ",".
  const encodedName = encodeData(name);
  if (style === "label") {
    w.writeAscii(".");
  } else if (style === "matrix" && !explode) {
    w.writeAscii(";");
    w.writeAscii(encodedName);
    w.writeAscii("=");
  } else if (style === "matrix") {
    w.writeAscii(";");
  }

  const separator =
    style === "label" && explode ? "." : style === "matrix" && explode ? ";" : ",";
  const kvSeparator = explode ? "=" : ",";

  let first = true;
  for (const key of Object.keys(value)) {
    if (!first) {
      w.writeAscii(separator);
    }

    w.writeAscii(encodeData(key));
    w.writeAscii(kvSeparator);
    w.writeAscii(encodeValue(scalarText(value[key]), allowReserved));
    first = false;
  }
}

// ── Query parameters (writeQueryString) ───────────────────────────────────────

/**
 * Writes a `query` parameter into the query string (without a leading `?`) — mirrors the C#
 * `EmitQueryParamWrite`. The caller passes `first=true` for the first parameter written; this helper
 * emits the leading `&` for every pair after the first, so the caller writes no `?`/`&` itself.
 * @param w The byte writer the query string is composed into.
 * @param name The query parameter name.
 * @param value The scalar, array, or object value.
 * @param style The query serialization style.
 * @param explode Whether the parameter is exploded.
 * @param allowReserved Whether reserved characters are left unescaped (OpenAPI `allowReserved`).
 * @param first Whether nothing has yet been written to the query string.
 * @returns The number of `name=value` pairs written (0 ⇒ the caller's `first` is unchanged).
 */
export function writeQueryParam(
  w: ByteWriter,
  name: string,
  value: StyleValue,
  style: QueryStyle,
  explode: boolean,
  allowReserved: boolean,
  first: boolean,
): number {
  if (isArrayValue(value)) {
    return writeQueryArray(w, name, value, style, explode, allowReserved, first);
  }

  if (isObject(value)) {
    return writeQueryObject(w, name, value, style, explode, allowReserved, first);
  }

  // Scalar (form): name=value.
  if (!first) {
    w.writeAscii("&");
  }

  w.writeAscii(encodeData(name));
  w.writeAscii("=");
  w.writeAscii(encodeValue(scalarText(value), allowReserved));
  return 1;
}

/** Writes a query array per style + explode — mirrors the C# `EmitQueryArrayWrite`. */
function writeQueryArray(
  w: ByteWriter,
  name: string,
  value: readonly StyleScalar[],
  style: QueryStyle,
  explode: boolean,
  allowReserved: boolean,
  first: boolean,
): number {
  if (value.length === 0) {
    return 0;
  }

  const encodedName = encodeData(name);

  if (style === "form" && explode) {
    // One name=value pair per element.
    let pairs = 0;
    for (const item of value) {
      if (!first || pairs > 0) {
        w.writeAscii("&");
      }

      w.writeAscii(encodedName);
      w.writeAscii("=");
      w.writeAscii(encodeValue(scalarText(item), allowReserved));
      pairs++;
    }

    return pairs;
  }

  // Non-exploded: name=v1<sep>v2<sep>v3. The separator is a literal pre-encoded delimiter.
  const itemSep = style === "spaceDelimited" ? "%20" : style === "pipeDelimited" ? "%7C" : ",";

  if (!first) {
    w.writeAscii("&");
  }

  w.writeAscii(encodedName);
  w.writeAscii("=");
  let firstItem = true;
  for (const item of value) {
    if (!firstItem) {
      w.writeAscii(itemSep);
    }

    w.writeAscii(encodeValue(scalarText(item), allowReserved));
    firstItem = false;
  }

  return 1;
}

/** Writes a query object per style + explode — mirrors the C# `EmitQueryObjectWrite`. */
function writeQueryObject(
  w: ByteWriter,
  name: string,
  value: StyleObject,
  style: QueryStyle,
  explode: boolean,
  allowReserved: boolean,
  first: boolean,
): number {
  const keys = Object.keys(value);
  if (keys.length === 0) {
    return 0;
  }

  const encodedName = encodeData(name);

  if (style === "deepObject") {
    // deepObject: name[key]=value, brackets percent-encoded to %5B / %5D.
    let pairs = 0;
    for (const key of keys) {
      if (!first || pairs > 0) {
        w.writeAscii("&");
      }

      w.writeAscii(encodedName);
      w.writeAscii("%5B");
      w.writeAscii(encodeData(key));
      w.writeAscii("%5D=");
      w.writeAscii(encodeValue(scalarText(value[key]), allowReserved));
      pairs++;
    }

    return pairs;
  }

  if (explode) {
    // form/spaceDelimited/pipeDelimited + explode: key=value pairs.
    let pairs = 0;
    for (const key of keys) {
      if (!first || pairs > 0) {
        w.writeAscii("&");
      }

      w.writeAscii(encodeData(key));
      w.writeAscii("=");
      w.writeAscii(encodeValue(scalarText(value[key]), allowReserved));
      pairs++;
    }

    return pairs;
  }

  // Non-exploded: name=key1<sep>val1<sep>key2<sep>val2, the kv separator a literal pre-encoded delimiter.
  const kvSep = style === "spaceDelimited" ? "%20" : style === "pipeDelimited" ? "%7C" : ",";

  if (!first) {
    w.writeAscii("&");
  }

  w.writeAscii(encodedName);
  w.writeAscii("=");
  let firstProp = true;
  for (const key of keys) {
    if (!firstProp) {
      w.writeAscii(kvSep);
    }

    w.writeAscii(encodeData(key));
    w.writeAscii(kvSep);
    w.writeAscii(encodeValue(scalarText(value[key]), allowReserved));
    firstProp = false;
  }

  return 1;
}

// ── Header parameters (writeHeaders) ──────────────────────────────────────────

/**
 * Returns a `header` parameter value in `simple` style (the only style for headers) — mirrors the C#
 * `EmitHeaderParamWrite`. Header values are NOT percent-encoded.
 * @param value The scalar, array, or object value.
 * @param explode Whether the parameter is exploded (affects only the object key/value separator).
 * @returns The header field value string.
 */
export function writeHeaderParam(value: StyleValue, explode: boolean): string {
  if (isArrayValue(value)) {
    // simple arrays are comma-joined regardless of explode.
    return value.map((item) => scalarText(item)).join(",");
  }

  if (isObject(value)) {
    // explode=false ⇒ key,value,key,value; explode=true ⇒ key=value,key=value.
    const kvSep = explode ? "=" : ",";
    return Object.keys(value)
      .map((key) => `${key}${kvSep}${scalarText(value[key])}`)
      .join(",");
  }

  return scalarText(value);
}

// ── Cookie parameters (writeCookies) ──────────────────────────────────────────

/**
 * Writes a `cookie` parameter into the Cookie header value — mirrors the C# `EmitCookieParamWrite`.
 * Pairs are separated by `; `. A `form` scalar value is percent-encoded; a `cookie`-style (RFC 6265,
 * OpenAPI 3.2) scalar and all array/object element values are written raw, matching the C# emitter.
 * @param w The byte writer the Cookie header value is composed into.
 * @param name The cookie name.
 * @param value The scalar, array, or object value.
 * @param style The cookie serialization style (`form` default, or `cookie` for OpenAPI 3.2).
 * @param explode Whether the parameter is exploded.
 * @param first Whether nothing has yet been written to the Cookie value.
 * @returns The number of pairs written (0 ⇒ the caller's `first` is unchanged).
 */
export function writeCookieParam(
  w: ByteWriter,
  name: string,
  value: StyleValue,
  style: CookieStyle,
  explode: boolean,
  first: boolean,
): number {
  if (isArrayValue(value)) {
    return writeCookieArray(w, name, value, explode, first);
  }

  if (isObject(value)) {
    return writeCookieObject(w, name, value, explode, first);
  }

  // Scalar: name=value, "; " separator. form ⇒ encode; cookie-style ⇒ raw.
  if (!first) {
    w.writeAscii("; ");
  }

  w.writeAscii(name);
  w.writeAscii("=");
  const text = scalarText(value);
  w.writeAscii(style === "cookie" ? text : encodeData(text));
  return 1;
}

/** Writes a cookie array (form style) — mirrors the C# `EmitCookieArrayWrite`. Elements are raw. */
function writeCookieArray(
  w: ByteWriter,
  name: string,
  value: readonly StyleScalar[],
  explode: boolean,
  first: boolean,
): number {
  if (value.length === 0) {
    return 0;
  }

  if (explode) {
    // form + explode: name=val1; name=val2; name=val3.
    let pairs = 0;
    for (const item of value) {
      if (!first || pairs > 0) {
        w.writeAscii("; ");
      }

      w.writeAscii(name);
      w.writeAscii("=");
      w.writeAscii(scalarText(item));
      pairs++;
    }

    return pairs;
  }

  // form + !explode: name=val1,val2,val3.
  if (!first) {
    w.writeAscii("; ");
  }

  w.writeAscii(name);
  w.writeAscii("=");
  let firstItem = true;
  for (const item of value) {
    if (!firstItem) {
      w.writeAscii(",");
    }

    w.writeAscii(scalarText(item));
    firstItem = false;
  }

  return 1;
}

/** Writes a cookie object (form style) — mirrors the C# `EmitCookieObjectWrite`. Values are raw. */
function writeCookieObject(
  w: ByteWriter,
  name: string,
  value: StyleObject,
  explode: boolean,
  first: boolean,
): number {
  const keys = Object.keys(value);
  if (keys.length === 0) {
    return 0;
  }

  if (explode) {
    // form + explode: key1=val1; key2=val2.
    let pairs = 0;
    for (const key of keys) {
      if (!first || pairs > 0) {
        w.writeAscii("; ");
      }

      w.writeAscii(key);
      w.writeAscii("=");
      w.writeAscii(scalarText(value[key]));
      pairs++;
    }

    return pairs;
  }

  // form + !explode: name=key1,val1,key2,val2.
  if (!first) {
    w.writeAscii("; ");
  }

  w.writeAscii(name);
  w.writeAscii("=");
  let firstProp = true;
  for (const key of keys) {
    if (!firstProp) {
      w.writeAscii(",");
    }

    w.writeAscii(key);
    w.writeAscii(",");
    w.writeAscii(scalarText(value[key]));
    firstProp = false;
  }

  return 1;
}
