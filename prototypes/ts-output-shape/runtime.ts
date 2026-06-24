// Stand-in for the @corvus/json-runtime support library that generated code imports.
// Only the type-level + tiny helpers needed to type-check the output-shape demos.

export function assertNever(x: never): never {
  throw new Error("unhandled union branch: " + JSON.stringify(x));
}

// Nominal (branded) types: a phantom unique-symbol key makes the brand un-spoofable
// and zero-cost at runtime. Used for string/number formats (uuid, date-time, ...).
declare const brand: unique symbol;
export type Brand<T, B extends string> = T & { readonly [brand]: B };

// The validator result (a generated validator returns undefined on success).
export interface Failure {
  readonly path: string;
  readonly keyword: string;
  readonly detail?: string;
}

export class FormatError extends Error {}

// ---- Mutation runtime (design §5.7): immer-style `produce` over a typed `Draft<T>`. -------------
// The recipe assigns to a deeply-mutable draft; the recorded change-set is RFC 6902 JSON Patch and
// (in Model C) lowers to a structural-sharing byte patch. Zero third-party dependency: a tiny Proxy
// change-recorder over a structural clone. (Mechanism proven in prototypes/rmw-scanner/produce.mjs.)

export type Draft<T> =
  T extends ReadonlyArray<infer E> ? Draft<E>[] :
  T extends object ? { -readonly [K in keyof T]: Draft<T[K]> } :
  T;

// A document handle. In Model C this wraps the original bytes + index; here, the read view.
export interface JsonDocument<T> {
  readonly value: T;
}

export interface JsonPatchOp {
  readonly op: "replace";
  readonly path: string;
  readonly value: unknown;
}

const escPtr = (k: string | symbol): string => String(k).replace(/~/g, "~0").replace(/\//g, "~1");

// Run the recipe over a recording draft; return the new value + the RFC 6902 change-set.
export function recordChanges<T>(value: T, recipe: (draft: Draft<T>) => void): { next: T; patches: JsonPatchOp[] } {
  const next = structuredClone(value);
  const patches: JsonPatchOp[] = [];
  const wrap = (obj: object, ptr: string): object =>
    new Proxy(obj, {
      get(o: Record<string | symbol, unknown>, k) {
        const v = o[k];
        return v !== null && typeof v === "object" ? wrap(v as object, ptr + "/" + escPtr(k)) : v;
      },
      set(o: Record<string | symbol, unknown>, k, val) {
        o[k] = val;
        patches.push({ op: "replace", path: ptr + "/" + escPtr(k), value: val });
        return true;
      },
    });
  recipe(wrap(next as object, "") as Draft<T>);
  return { next, patches };
}

// idiomatic immer-style produce: "mutate" the draft, get a new immutable document.
export function produce<T>(doc: JsonDocument<T>, recipe: (draft: Draft<T>) => void): JsonDocument<T> {
  return { value: recordChanges(doc.value, recipe).next };
}
