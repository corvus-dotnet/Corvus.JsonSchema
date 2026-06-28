// Runnable demo for recipe 002 — Validation.
import { Email, Registration } from "./generated.js";

const dec = new TextDecoder();

// Constraints (minLength / pattern / minimum / multipleOf / format) are enforced by Registration.evaluate, not by
// the type: the interface is the SHAPE, the evaluator is the CONSTRAINT authority.
const bytes = Registration.build({
  username: "ada_lovelace",
  age: 36,
  email: Email.as("ada@example.com"),
  score: 4.5,
});
console.log("valid:          ", Registration.evaluate(JSON.parse(dec.decode(bytes)))); // true

// Each constraint rejection is just `false` — no thrown error, no error-object graph.
console.log("short username: ", Registration.evaluate({ username: "ab", age: 36, email: "a@b.com" })); // minLength 3
console.log("bad pattern:    ", Registration.evaluate({ username: "Ada", age: 36, email: "a@b.com" })); // ^[a-z][a-z0-9_]*$
console.log("under age:      ", Registration.evaluate({ username: "ada", age: 17, email: "a@b.com" })); // minimum 18
console.log("score step:     ", Registration.evaluate({ username: "ada", age: 36, email: "a@b.com", score: 0.3 })); // multipleOf 0.5

// A `format` brand validates eagerly at construction (it throws) — distinct from whole-document evaluation.
try {
  Email.as("not-an-email");
} catch (e) {
  console.log("Email.as threw:  ", (e as Error).message);
}
