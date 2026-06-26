// Recipe 015 — Maps (additionalProperties).
import { type Scores, evaluateRoot } from "./generated.js";
const enc = new TextEncoder();
const dec = new TextDecoder();
// additionalProperties:{number} -> an index-signature map { [key: string]: number }. An open map is just a
// plain object, so there is no build*; serialise it directly.
const map: Scores = { ada: 9.5, alan: 8 };
const bytes = enc.encode(JSON.stringify(map));
console.log("valid:   ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
const scores = JSON.parse(dec.decode(bytes)) as Scores;
console.log("ada:     ", scores.ada);
for (const [name, score] of Object.entries(scores)) console.log(`   ${name} = ${score}`);
console.log("negative:", evaluateRoot({ ada: -1 })); // false — minimum 0 on the values
