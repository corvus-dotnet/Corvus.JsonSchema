// Proves the EXTENSIBILITY seam: the base provider has no `format` handler (format is annotation-only),
// so its validator ignores `format`; registering the custom TsFormatExtensionHandler (dispatched by the
// core registry) makes the SAME schema's validator enforce it -- no provider change.
import { evaluateRoot as base } from "./out/generated.js";
import { evaluateRoot as ext } from "./out/generated-ext.js";

let pass = 0, fail = 0;
const check = (label, got, want) => { got === want ? pass++ : (fail++, console.log(`FAIL ${label}: ${got} != ${want}`)); };

const badEmail = { name: "A", address: { postcode: "00000" }, contact: "not-an-email" };
const okEmail = { name: "A", address: { postcode: "00000" }, contact: "a@b.com" };

check("base ignores format (bad email accepted)", base(badEmail), true);
check("extension enforces format=email (bad email rejected)", ext(badEmail), false);
check("base accepts valid email", base(okEmail), true);
check("extension accepts valid email", ext(okEmail), true);

console.log(`\n${pass} passed, ${fail} failed`);
if (fail > 0) { process.exit(1); }
console.log("OK — custom format handler plugged in via RegisterValidationHandlers; the core registry dispatched to it and the emitted validator changed.");
