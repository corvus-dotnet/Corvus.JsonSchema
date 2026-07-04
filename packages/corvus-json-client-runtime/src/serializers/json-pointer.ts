/**
 * RFC 6901 JSON Pointer evaluation — resolves the `#/…` fragment of an OpenAPI runtime expression
 * (`$response.body#/id`, `$request.body#/user/name`) against a parsed JSON value. Generated link
 * followers use this to extract a bound parameter value from a decoded response body.
 *
 * An empty pointer (`""`) returns the whole value. A missing or non-traversable segment yields
 * `undefined` rather than throwing, so an absent body field surfaces as an absent parameter.
 */
export function getByPointer(root: unknown, pointer: string): unknown {
  if (pointer.length === 0) {
    return root;
  }

  let current: unknown = root;
  for (const rawSegment of pointer.split("/").slice(1)) {
    if (current === null || typeof current !== "object") {
      return undefined;
    }

    // RFC 6901 unescaping: ~1 -> "/", ~0 -> "~" (apply ~1 first).
    const segment = rawSegment.replace(/~1/g, "/").replace(/~0/g, "~");
    current = (current as Record<string, unknown>)[segment];
  }

  return current;
}
