// What the generator emits for ARRAYS / TUPLES (design §5.3). Type-checked under --strict.
// Three distinct array shapes the core classifies, plus fixed-size numeric (tensor).

// plain homogeneous array: { items: { type: string } }
export type Tags = readonly string[];

// pure tuple: { prefixItems: [string, number, boolean], items: false }  (no extra items)
export type Triple = readonly [string, number, boolean];

// prefix tuple + tail: { prefixItems: [string], items: { type: number } } -> variadic tuple
export type Labelled = readonly [string, ...number[]];

// fixed-size numeric array (tensor): { type: array, items: number, minItems: 3, maxItems: 3 }
export type Vec3 = readonly [number, number, number];

// positional typing + arity are enforced by the type; the validator adds
// contains/min-maxContains/min-maxItems/uniqueItems (design §5.4).
export function firstTag(t: Triple): string {
  return t[0]; // t[0]:string, t[1]:number, t[2]:boolean
}
export function dot(a: Vec3, b: Vec3): number {
  return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
}
export function head(l: Labelled): string {
  return l[0];
}

const tags: Tags = ["a", "b", "c"];

// @ts-expect-error -- Triple has arity 3; a 2-tuple is not assignable
const badArity: Triple = ["a", 1];
// @ts-expect-error -- element 1 of Triple must be a number
const badElem: Triple = ["a", "b", true];
// @ts-expect-error -- Vec3 requires exactly 3 elements
const badVec: Vec3 = [1, 2];

export const demoArray: ReadonlyArray<unknown> = [
  firstTag(["a", 1, true]),
  dot([1, 2, 3], [4, 5, 6]),
  head(["x", 1, 2, 3]),
  tags,
  badArity,
  badElem,
  badVec,
];
