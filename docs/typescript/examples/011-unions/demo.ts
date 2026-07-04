// Runnable demo for recipe 011 — Unions (oneOf + match).
import { Circle, Rectangle, Shape } from "./generated.js";

const dec = new TextDecoder();

// `oneOf` -> a union `Shape = Circle | Rectangle`, with per-branch type guards and an exhaustive `Shape.match`.
const circle = Shape.parse(Circle.build({ kind: "circle", radius: 2 }));
console.log("valid circle: ", Shape.evaluate(circle)); // true

// Shape.match is exhaustive — TypeScript requires a branch for every member of the union.
const area = (s: Shape) =>
  Shape.match(s, {
    circle: (c) => Math.PI * c.radius * c.radius,
    rectangle: (r) => r.width * r.height,
  });
console.log("circle area:  ", area(circle).toFixed(2));

// ...or narrow with a generated guard (inside the `if`, `shape` is typed as `Circle`).
if (Circle.is(circle)) {
  console.log("radius:       ", circle.radius);
}

// A rectangle takes the other branch.
const rect = Shape.parse(Rectangle.build({ kind: "rectangle", width: 3, height: 4 }));
console.log("rect area:    ", area(rect)); // 12
