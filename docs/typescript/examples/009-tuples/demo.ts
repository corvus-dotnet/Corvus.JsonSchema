// Recipe 009 — Tuples.
import { type Point3D, evaluateRoot, buildPoint3D } from "./generated.js";
const dec = new TextDecoder();
const bytes = buildPoint3D({ coord: [1, 2, 3] });
console.log("valid:    ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
const p = JSON.parse(dec.decode(bytes)) as Point3D;
const [x, y, z] = p.coord; // typed readonly [number, number, number]
console.log("x,y,z:    ", x, y, z);
console.log("too few:  ", evaluateRoot({ coord: [1, 2] }));       // false — minItems 3
console.log("too many: ", evaluateRoot({ coord: [1, 2, 3, 4] })); // false — items: false
