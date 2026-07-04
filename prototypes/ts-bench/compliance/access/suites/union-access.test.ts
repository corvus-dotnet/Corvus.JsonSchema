// PROVIDER REAL-OUTPUT test for the union surface the provider now emits (§5.2): the union type alias,
// per-member guards (the V5 TryGetAs{Branch} analog) and matchX (the V5 Match() analog). Validate with
// the emitted oneOf validator, then dispatch + read the narrowed branch through the emitted surface.
// Run after generating union.json into out-union/:
//   Codegen (union.json -> out-union/), transpile, and run are all driven by ../run-access.sh.
import Root_outuniongeneratedjs, { Circle, Rectangle, Shape } from "./out-union/generated.js";

let pass = 0;
let fail = 0;
function eq<T>(label: string, got: T, want: T): void {
  if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
  else { fail++; console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`); }
}

const circ: unknown = { kind: "circle", radius: 2 };
const rect: unknown = { kind: "rectangle", width: 3, height: 4 };
if (!Root_outuniongeneratedjs.evaluate(circ)) { throw new Error("circle should validate"); }
if (!Root_outuniongeneratedjs.evaluate(rect)) { throw new Error("rectangle should validate"); }
const s1 = circ as Shape;
const s2 = rect as Shape;

// per-member guards
eq("Circle.is(circle)", Circle.is(s1), true);
eq("Circle.is(rectangle)", Circle.is(s2), false);
eq("Rectangle.is(rectangle)", Rectangle.is(s2), true);

// match() dispatch + typed branch access
const m = { circle: (c: { radius: number }) => `c${c.radius}`, rectangle: (r: { width: number }) => `r${r.width}` };
eq("Shape.match -> circle branch", Shape.match(s1, m), "c2");
eq("Shape.match -> rectangle branch", Shape.match(s2, m), "r3");

// narrowing gives typed access to the branch members
if (Circle.is(s1)) { eq("narrowed circle.radius", s1.radius, 2); }
if (Rectangle.is(s2)) { eq("narrowed rectangle.height", s2.height, 4); }

// the oneOf validator rejects a non-member
eq("non-member rejected", Root_outuniongeneratedjs.evaluate({ kind: "triangle" }), false);

console.log(`union-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`union-access: ${fail} failed`); }
console.log("OK — provider emits union type + guards + match; dispatch and narrowed access work.");
