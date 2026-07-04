// Recipe 009 — Tuples.
import { Point3D } from "./generated.js";
const dec = new TextDecoder();
const bytes = Point3D.build({ coord: [1, 2, 3] });
console.log("valid:    ", Point3D.evaluate(bytes)); // true
const p = Point3D.parse(bytes);
const [x, y, z] = p.coord; // typed readonly [number, number, number]
console.log("x,y,z:    ", x, y, z);
console.log("too few:  ", Point3D.evaluate({ coord: [1, 2] }));       // false — minItems 3
console.log("too many: ", Point3D.evaluate({ coord: [1, 2, 3, 4] })); // false — items: false
