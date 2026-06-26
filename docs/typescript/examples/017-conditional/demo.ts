// Recipe 017 — Conditional schemas (if / then / else).
import { evaluateRoot, buildPayment } from "./generated.js";
const dec = new TextDecoder();
// if method=="card" then cardNumber is required. The TYPE is the base shape; the rule is evaluator-enforced.
console.log("cash:            ", evaluateRoot(JSON.parse(dec.decode(buildPayment({ method: "cash" }))))); // true
console.log("card + number:   ", evaluateRoot({ method: "card", cardNumber: "4111 1111 1111 1111" }));     // true
console.log("card, no number: ", evaluateRoot({ method: "card" }));                                        // false
