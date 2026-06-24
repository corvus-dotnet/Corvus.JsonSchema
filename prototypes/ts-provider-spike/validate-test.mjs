// Runs the generated validator (compiled from out/generated.ts) against valid/invalid instances.
// Regenerate + compile + run:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- person.json out
//   npx -y -p typescript tsc out/generated.ts --strict --target es2022 --module esnext --moduleResolution bundler --outDir out
//   node validate-test.mjs
import { evaluateRoot } from "./out/generated.js";

let pass = 0, fail = 0;
const check = (label, instance, want) => {
  const got = evaluateRoot(instance);
  got === want ? pass++ : (fail++, console.log(`FAIL ${label}: evaluateRoot(...) = ${got}, want ${want}`));
};

// valid
check("full valid", { name: "Ada", age: 30, status: "active", address: { postcode: "12345", street: "Main" } }, true);
check("minimal valid (optionals omitted)", { name: "A", address: { postcode: "00000" } }, true);

// invalid — structural
check("not an object", "hello", false);
check("missing required name", { address: { postcode: "12345" } }, false);
check("missing required address", { name: "Ada" }, false);
check("address missing required postcode", { name: "Ada", address: { street: "Main" } }, false);

// invalid — leaf constraints
check("name wrong type", { name: 123, address: { postcode: "12345" } }, false);
check("name too short (minLength 1)", { name: "", address: { postcode: "12345" } }, false);
check("age negative (minimum 0)", { name: "Ada", age: -1, address: { postcode: "12345" } }, false);
check("age not integer", { name: "Ada", age: 1.5, address: { postcode: "12345" } }, false);
check("status not in enum", { name: "Ada", status: "paused", address: { postcode: "12345" } }, false);
check("postcode fails pattern", { name: "Ada", address: { postcode: "abcde" } }, false);

console.log(`\n${pass} passed, ${fail} failed`);
if (fail > 0) { process.exit(1); }
console.log("OK — validator emitted from the core engine compiles (tsc --strict) and validates correctly.");
