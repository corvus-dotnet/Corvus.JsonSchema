// Runnable demo for recipe 002 — Validation.
import { type Registration, evaluateRoot, buildRegistration, asEmail } from "./generated.js";

const dec = new TextDecoder();

// Constraints (minLength / pattern / minimum / multipleOf / format) are enforced by evaluateRoot, not by
// the type: the interface is the SHAPE, the evaluator is the CONSTRAINT authority.
const bytes = buildRegistration({
  username: "ada_lovelace",
  age: 36,
  email: asEmail("ada@example.com"),
  score: 4.5,
});
console.log("valid:          ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true

// Each constraint rejection is just `false` — no thrown error, no error-object graph.
console.log("short username: ", evaluateRoot({ username: "ab", age: 36, email: "a@b.com" })); // minLength 3
console.log("bad pattern:    ", evaluateRoot({ username: "Ada", age: 36, email: "a@b.com" })); // ^[a-z][a-z0-9_]*$
console.log("under age:      ", evaluateRoot({ username: "ada", age: 17, email: "a@b.com" })); // minimum 18
console.log("score step:     ", evaluateRoot({ username: "ada", age: 36, email: "a@b.com", score: 0.3 })); // multipleOf 0.5

// A `format` brand validates eagerly at construction (it throws) — distinct from whole-document evaluation.
try {
  asEmail("not-an-email");
} catch (e) {
  console.log("asEmail threw:  ", (e as Error).message);
}
