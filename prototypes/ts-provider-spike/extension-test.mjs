// Proves the EXTENSIBILITY seam: the base provider has no `multipleOf` handler, so its validator
// ignores the constraint; registering the custom TsMultipleOfHandler (dispatched by the core
// registry) makes the SAME schema's validator enforce it — no provider change.
import { evaluateRoot as base } from "./out/generated.js";
import { evaluateRoot as ext } from "./out/generated-ext.js";

let pass = 0, fail = 0;
const check = (label, got, want) => { got === want ? pass++ : (fail++, console.log(`FAIL ${label}: ${got} != ${want}`)); };

const violatesMultipleOf = { name: "A", address: { postcode: "00000" }, price: 0.3 }; // 0.3 not a multiple of 0.5
const ok = { name: "A", address: { postcode: "00000" }, price: 0.5 };

check("base ignores multipleOf (price 0.3 accepted)", base(violatesMultipleOf), true);
check("extension enforces multipleOf (price 0.3 rejected)", ext(violatesMultipleOf), false);
check("base accepts price 0.5", base(ok), true);
check("extension accepts price 0.5", ext(ok), true);

console.log(`\n${pass} passed, ${fail} failed`);
if (fail > 0) { process.exit(1); }
console.log("OK — custom keyword handler plugged in via RegisterValidationHandlers; the core registry dispatched to it and the emitted validator changed.");
