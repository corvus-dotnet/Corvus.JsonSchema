// What the generator emits for ARRAYS / TUPLES (design §5.3). Type-checked under --strict.
// Three distinct array shapes the core classifies, plus fixed-size numeric (tensor).

// plain homogeneous array: { items: { type: string } }
export type Tags = readonly string[];

// pure tuple: { prefixItems: [string, number, boolean], items: false }  (no extra items)
export type Triple = readonly [string, number, boolean];

// prefix tuple + tail: { prefixItems: [string], items: { type: number } } -> variadic tuple
export type Labelled = readonly [string, ...number[]];

// fixed-size numeric array, 1-D tensor: { type: array, items: number, minItems: 3, maxItems: 3 }
export type Vec3 = readonly [number, number, number];

// MULTI-DIMENSION (design §5.3: "nested tuples for multi-dimension"; ArrayDimension() > 1). A 3x3
// matrix is a fixed tuple of fixed rows; element access is m[row][col], typed `number` all the way down.
export type Mat3 = readonly [Vec3, Vec3, Vec3];

// 2x3 rectangular tensor — dimensions need not be square.
export type Mat2x3 = readonly [readonly [number, number, number], readonly [number, number, number]];

// NUMERIC-LEAF tensor view (design §5.3: optional Float64Array view for numeric hot paths in Model A/C).
// The flat numeric buffer is exposed as a typed array; index access is monomorphic.
export type Tensor = Float64Array;

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

// multi-dimensional access: m[row][col] is typed number through both dimensions.
export function trace(m: Mat3): number {
  return m[0][0] + m[1][1] + m[2][2];
}
export function matVec(m: Mat3, v: Vec3): Vec3 {
  return [
    m[0][0] * v[0] + m[0][1] * v[1] + m[0][2] * v[2],
    m[1][0] * v[0] + m[1][1] * v[1] + m[1][2] * v[2],
    m[2][0] * v[0] + m[2][1] * v[1] + m[2][2] * v[2],
  ];
}
// numeric-leaf tensor: read + length via the typed-array view (monomorphic numeric loop).
export function tensorSum(t: Tensor): number {
  let s = 0;
  for (let i = 0; i < t.length; i++) { s += t[i]; }
  return s;
}

const tags: Tags = ["a", "b", "c"];

// @ts-expect-error -- Triple has arity 3; a 2-tuple is not assignable
const badArity: Triple = ["a", 1];
// @ts-expect-error -- element 1 of Triple must be a number
const badElem: Triple = ["a", "b", true];
// @ts-expect-error -- Vec3 requires exactly 3 elements
const badVec: Vec3 = [1, 2];
// @ts-expect-error -- a Mat3 row must itself be a Vec3 (arity 3); a 2-element row is rejected
const badMat: Mat3 = [[1, 2], [3, 4, 5], [6, 7, 8]];

export const demoArray: ReadonlyArray<unknown> = [
  firstTag(["a", 1, true]),
  dot([1, 2, 3], [4, 5, 6]),
  head(["x", 1, 2, 3]),
  trace([[1, 0, 0], [0, 2, 0], [0, 0, 3]]),
  matVec([[1, 0, 0], [0, 1, 0], [0, 0, 1]], [4, 5, 6]),
  tensorSum(Float64Array.of(1, 2, 3)),
  tags,
  badArity,
  badElem,
  badVec,
  badMat,
];
