// Runnable demo for recipe 011 — Unions (oneOf + match).
import { type Shape, evaluateRoot, buildCircle, buildRectangle, matchShape, isCircle } from "./generated.js";

const dec = new TextDecoder();

// `oneOf` -> a union `Shape = Circle | Rectangle`, with per-branch type guards and an exhaustive `matchShape`.
const circle = JSON.parse(dec.decode(buildCircle({ kind: "circle", radius: 2 }))) as Shape;
console.log("valid circle: ", evaluateRoot(circle)); // true

// matchShape is exhaustive — TypeScript requires a branch for every member of the union.
const area = (s: Shape) =>
  matchShape(s, {
    circle: (c) => Math.PI * c.radius * c.radius,
    rectangle: (r) => r.width * r.height,
  });
console.log("circle area:  ", area(circle).toFixed(2));

// ...or narrow with a generated guard (inside the `if`, `shape` is typed as `Circle`).
if (isCircle(circle)) {
  console.log("radius:       ", circle.radius);
}

// A rectangle takes the other branch.
const rect = JSON.parse(dec.decode(buildRectangle({ kind: "rectangle", width: 3, height: 4 }))) as Shape;
console.log("rect area:    ", area(rect)); // 12
