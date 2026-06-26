// Recipe 004 — Open and Closed objects.
import { evaluateRoot, buildStrictPoint } from "./generated.js";
const dec = new TextDecoder();
const bytes = buildStrictPoint({ x: 1, y: 2 });
console.log("valid:      ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
// unevaluatedProperties:false -> a CLOSED type: an unknown property is rejected.
console.log("extra prop: ", evaluateRoot({ x: 1, y: 2, z: 3 })); // false — z is unevaluated
console.log("missing y:  ", evaluateRoot({ x: 1 }));             // false — y required
