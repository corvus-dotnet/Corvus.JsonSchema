/**
 * `application/x-www-form-urlencoded` request-body serializer — the TypeScript analogue of the C#
 * `FormUrlEncodedSerializer`. Per OpenAPI §4.8.14.4 a form-urlencoded body is the object's properties
 * serialized as `name=value` pairs joined by `&`, exactly like `query` parameters; this reuses the
 * parameter-style serializer ({@link writeQueryParam}) per property, so arrays/objects and the
 * form/spaceDelimited/pipeDelimited/deepObject styles + `explode` behave identically to query params.
 *
 * Per-property overrides come from the OpenAPI Encoding Object (`style` / `explode` / `allowReserved`);
 * the default is `style: "form"` with `explode: true` (the OAS default for `form`). A `null` property
 * value writes `name=` (an empty value); an `undefined` property is omitted.
 */

import { ByteWriter } from "./buffer-writer.js";
import { encodeData } from "./percent-encoding.js";
import { writeQueryParam, type QueryStyle, type StyleValue } from "./style.js";

/** A form-urlencoded property value: a parameter value, or `null` for an explicit empty value. */
export type FormValue = StyleValue | null;

/** A form-urlencoded body: a record of property values (an `undefined` property is omitted). */
export type FormBody = { readonly [property: string]: FormValue | undefined };

/** Per-property encoding overrides (the OpenAPI Encoding Object), keyed by property name. */
export interface FormPropertyEncoding {
  /** The serialization style for array/object values. Defaults to `"form"`. */
  readonly style?: QueryStyle;

  /** Whether arrays/objects explode into separate pairs. Defaults to `true` for `form`, else `false`. */
  readonly explode?: boolean;

  /** Whether reserved characters are left unescaped. Defaults to `false`. */
  readonly allowReserved?: boolean;
}

/** Per-property encoding overrides keyed by property name. */
export type FormEncodings = { readonly [property: string]: FormPropertyEncoding };

/**
 * Serializes a form-urlencoded body into a byte writer — mirrors the C# `FormUrlEncodedSerializer`.
 * @param w The byte writer the body is composed into.
 * @param value The body object (its properties become the form fields).
 * @param encodings Optional per-property encoding overrides.
 */
export function writeFormUrlEncoded(w: ByteWriter, value: FormBody, encodings?: FormEncodings): void {
  let first = true;
  for (const name of Object.keys(value)) {
    const propValue = value[name];
    if (propValue === undefined) {
      continue;
    }

    const enc = encodings?.[name];
    const style: QueryStyle = enc?.style ?? "form";
    const explode = enc?.explode ?? style === "form";
    const allowReserved = enc?.allowReserved ?? false;

    if (propValue === null) {
      // A null value writes `name=` (an empty value).
      if (!first) {
        w.writeAscii("&");
      }

      w.writeAscii(encodeData(name));
      w.writeAscii("=");
      first = false;
      continue;
    }

    const pairs = writeQueryParam(w, name, propValue, style, explode, allowReserved, first);
    if (pairs > 0) {
      first = false;
    }
  }
}

/**
 * Serializes a form-urlencoded body to a fresh `Uint8Array` (the `bytes` request body a transport sends).
 * @param value The body object.
 * @param encodings Optional per-property encoding overrides.
 * @returns The UTF-8 form-urlencoded body bytes.
 */
export function formUrlEncodedBytes(value: FormBody, encodings?: FormEncodings): Uint8Array {
  const w = new ByteWriter();
  writeFormUrlEncoded(w, value, encodings);
  // `written` is a view over the reusable buffer; copy it out so the returned body owns its bytes.
  return w.written.slice();
}
