/**
 * Response-header value parsers — the inverse of the request-side {@link writeHeaderParam} serializer.
 * OpenAPI response headers always use the `simple` style: a scalar is the raw value; an array is
 * comma-joined (explode has no effect on the separator); an object is `key,value,key,value`
 * (explode `false`) or `key=value,key=value` (explode `true`). These turn a raw header string back
 * into the typed value a generated response getter exposes.
 *
 * The C# generator parses header values into the schema's model type via zero-alloc builders; the
 * TypeScript runtime returns plain values (`string` / `number` / `boolean` / arrays / records), which
 * is the idiomatic shape for a header getter. (A divergence noted for the C#↔TS parity task.)
 */

/** Returns a string-typed header value verbatim. */
export function parseHeaderString(raw: string): string {
  return raw;
}

/** Parses a numeric header value. */
export function parseHeaderNumber(raw: string): number {
  return Number(raw);
}

/** Parses a boolean header value (`"true"` ⇒ `true`, anything else ⇒ `false`). */
export function parseHeaderBoolean(raw: string): boolean {
  return raw === "true";
}

/**
 * Parses a `simple`-style array header — comma-separated elements, each parsed by `parseElement`.
 * An empty header value yields an empty array.
 */
export function parseHeaderArray<T>(raw: string, parseElement: (element: string) => T): T[] {
  if (raw.length === 0) {
    return [];
  }

  return raw.split(",").map((element) => parseElement(element.trim()));
}

/**
 * Parses a `simple`-style object header into a string-valued record. With `explode` the parts are
 * `key=value`; without, they are flat `key,value,key,value` pairs.
 */
export function parseHeaderObject(raw: string, explode: boolean): Record<string, string> {
  const out: Record<string, string> = {};
  if (raw.length === 0) {
    return out;
  }

  const parts = raw.split(",");
  if (explode) {
    for (const part of parts) {
      const eq = part.indexOf("=");
      if (eq >= 0) {
        out[part.slice(0, eq).trim()] = part.slice(eq + 1).trim();
      }
    }
  } else {
    for (let i = 0; i + 1 < parts.length; i += 2) {
      out[parts[i].trim()] = parts[i + 1].trim();
    }
  }

  return out;
}
