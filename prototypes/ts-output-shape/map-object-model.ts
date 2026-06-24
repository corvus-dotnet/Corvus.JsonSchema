// What the generator emits for MAP / DICTIONARY objects (design §5.3). Type-checked under --strict.
// (additionalProperties / patternProperties / unevaluatedProperties with a fallback value type.)

// pure map: { type: object, additionalProperties: { type: number } }
export type Scores = Readonly<Record<string, number>>;

// declared properties + additionalProperties: { type: string }
//   -> interface with an index signature (declared props must be compatible with it)
export interface Config {
  readonly name: string;
  readonly [key: string]: string;
}

// patternProperties can't be expressed as a key constraint in the TS TYPE — the index signature
// stays broad and the regex-keyed value schema is enforced by the VALIDATOR (design §5.4). A typed
// `entries()`/`get(key)` runtime helper is the ergonomic accessor (vs C#'s TryGetProperty).
export function getScore(s: Scores, key: string): number | undefined {
  return s[key];
}

const cfg: Config = { name: "app", featureX: "on" };

// @ts-expect-error -- additionalProperties is `number`, so a string value is rejected
const badScore: Scores = { a: "x" };
// @ts-expect-error -- the index signature requires every property (incl. `name`) to be a string
const badConfig: Config = { name: 1, other: "y" };

export const demoMap: ReadonlyArray<unknown> = [
  getScore({ a: 1, b: 2 }, "a"),
  cfg.name,
  cfg.featureX,
  badScore,
  badConfig,
];
