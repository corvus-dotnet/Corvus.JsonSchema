// Recipe 004 — Open and Closed objects.
import { StrictPoint } from "./generated.js";
const dec = new TextDecoder();
const bytes = StrictPoint.build({ x: 1, y: 2 });
console.log("valid:      ", StrictPoint.evaluate(JSON.parse(dec.decode(bytes)))); // true
// unevaluatedProperties:false -> a CLOSED type: an unknown property is rejected.
console.log("extra prop: ", StrictPoint.evaluate({ x: 1, y: 2, z: 3 })); // false — z is unevaluated
console.log("missing y:  ", StrictPoint.evaluate({ x: 1 }));             // false — y required
