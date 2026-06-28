// Recipe 017 — Conditional schemas (if / then / else).
import { Payment } from "./generated.js";
const dec = new TextDecoder();
// if method=="card" then cardNumber is required. The TYPE is the base shape; the rule is evaluator-enforced.
console.log("cash:            ", Payment.evaluate(JSON.parse(dec.decode(Payment.build({ method: "cash" }))))); // true
console.log("card + number:   ", Payment.evaluate({ method: "card", cardNumber: "4111 1111 1111 1111" }));     // true
console.log("card, no number: ", Payment.evaluate({ method: "card" }));                                        // false
