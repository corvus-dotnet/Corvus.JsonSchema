// PROVIDER REAL-OUTPUT test for the union surface the provider now emits (§5.2): the union type alias,
// per-member guards (the V5 TryGetAs{Branch} analog) and matchX (the V5 Match() analog). Validate with
// the emitted oneOf validator, then dispatch + read the narrowed branch through the emitted surface.
// Run after generating union.json into out-union/:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- union.json out-union
//   npx -y -p typescript tsc out-union/generated.ts out-union/corvus-runtime.ts union-access.test.ts spike-globals.d.ts \
//       --outDir prov-test-union --strict --target es2022 --module esnext --moduleResolution bundler
//   node prov-test-union/union-access.test.js
import { evaluateRoot, matchShape, isCircle, isRectangle, type Shape } from "./out-union/generated.js";

let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}

const circ: unknown = { kind: "circle", radius: 2 };
const rect: unknown = { kind: "rectangle", width: 3, height: 4 };
if (!evaluateRoot(circ)) { throw new Error("circle should validate"); }
if (!evaluateRoot(rect)) { throw new Error("rectangle should validate"); }
const s1 = circ as Shape;
const s2 = rect as Shape;

// per-member guards
eq("isCircle(circle)", isCircle(s1), true);
eq("isCircle(rectangle)", isCircle(s2), false);
eq("isRectangle(rectangle)", isRectangle(s2), true);

// match() dispatch + typed branch access
const m = { circle: (c: { radius: number }) => `c${c.radius}`, rectangle: (r: { width: number }) => `r${r.width}` };
eq("matchShape -> circle branch", matchShape(s1, m), "c2");
eq("matchShape -> rectangle branch", matchShape(s2, m), "r3");

// narrowing gives typed access to the branch members
if (isCircle(s1)) { eq("narrowed circle.radius", s1.radius, 2); }
if (isRectangle(s2)) { eq("narrowed rectangle.height", s2.height, 4); }

// the oneOf validator rejects a non-member
eq("non-member rejected", evaluateRoot({ kind: "triangle" }), false);

console.log(`union-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`union-access: ${fail} failed`); }
console.log("OK — provider emits union type + guards + match; dispatch and narrowed access work.");
