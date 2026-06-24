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
