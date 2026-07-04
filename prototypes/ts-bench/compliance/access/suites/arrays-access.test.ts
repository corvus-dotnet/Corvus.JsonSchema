// PROVIDER REAL-OUTPUT access test for the array/tuple/tensor/map types the provider now emits (§5.3).
// Validate with the emitted validator, then consume the value through the emitted typed surface:
//   - plain `readonly T[]`, pure tuple `readonly [A,B,C]`, prefix+tail `readonly [A,...T[]]`
//   - multi-dimensional tensor `readonly (readonly number[])[]`
//   - pure map `Readonly<Record<string, T>>`
// Codegen (arrays.json -> out-arrays/), transpile, and run are all driven by ../run-access.sh.
import Root_outarraysgeneratedjs, { Container, Scores } from "./out-arrays/generated.js";

let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}

const raw: unknown = {
  tags: ["a", "b"],
  triple: ["x", 7, true],
  labelled: ["h", 1, 2, 3],
  matrix: [[1, 2], [3, 4]],
  scores: { math: 90, cs: 100 },
};
if (!Root_outarraysgeneratedjs.evaluate(raw)) { throw new Error("expected the container instance to be valid"); }
const c = raw as Container;

// plain homogeneous array
eq("plain array element", c.tags[0], "a");
eq("plain array length", c.tags.length, 2);

// pure tuple — typed per position
eq("tuple[0] (string)", c.triple[0], "x");
eq("tuple[1] (number)", c.triple[1], 7);
eq("tuple[2] (boolean)", c.triple[2], true);

// prefix + variadic tail
eq("variadic tuple head", c.labelled?.[0], "h");
eq("variadic tuple tail element", c.labelled?.[2], 2);

// multi-dimensional tensor: m[row][col]
eq("matrix[0][1]", c.matrix?.[0][1], 2);
eq("matrix[1][0]", c.matrix?.[1][0], 3);

// pure map / dictionary (Record alias)
const s: Scores | undefined = c.scores;
eq("map value present", s?.["cs"], 100);
eq("map value absent -> undefined", s?.["nope"], undefined);

console.log(`arrays-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`arrays-access: ${fail} failed`); }
console.log("OK — provider emits typed arrays/tuples/tensors/maps; typed access works.");
