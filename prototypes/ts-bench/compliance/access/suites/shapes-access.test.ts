// Tuple/list array-ops test for the reshaped patch `arrays` surface (strongly typed per element).
//   dotnet run --project TsProviderSpike.csproj -c Debug -- shapes.json out-shapes
//   <ts-bench tsc> out-shapes/generated.ts out-shapes/corvus-runtime.ts shapes-access.test.ts spike-globals.d.ts \
//       --outDir prov-test-shapes --strict --target es2022 --module esnext --moduleResolution bundler --lib es2022,dom
//   node prov-test-shapes/shapes-access.test.js
import { patchShapes } from "./out-shapes/generated.js";

const enc = new TextEncoder();
const dec = new TextDecoder();
let pass = 0, fail = 0;
function ok(label: string, cond: boolean): void { if (cond) { pass++; } else { fail++; console.log("FAIL " + label); } }

// list T[] -- object-keyed set + append + removeAt (indices against original positions)
{
  const src = JSON.stringify({ id: "x", tags: ["a", "b", "c"] });
  const p = JSON.parse(dec.decode(patchShapes(enc.encode(src), {}, undefined, { tags: { set: { 1: "B" }, append: ["d"], removeAt: [0] } })));
  ok("list set/append/removeAt", JSON.stringify(p.tags) === JSON.stringify(["B", "c", "d"]));
}
// list -- insert (object-keyed: insert elements before index)
{
  const src = JSON.stringify({ id: "x", tags: ["a", "b"] });
  const p = JSON.parse(dec.decode(patchShapes(enc.encode(src), {}, undefined, { tags: { insert: { 1: ["X", "Y"] } } })));
  ok("list insert", JSON.stringify(p.tags) === JSON.stringify(["a", "X", "Y", "b"]));
}
// pure tuple [number, number] -- positional set, value typed per position; no append/insert/remove
{
  const src = JSON.stringify({ id: "x", coord: [1.5, 2.5] });
  const p = JSON.parse(dec.decode(patchShapes(enc.encode(src), {}, undefined, { coord: { set: { 0: 9.9 } } })));
  ok("tuple set position 0", JSON.stringify(p.coord) === JSON.stringify([9.9, 2.5]));
}
// prefix tuple [string, ...number[]] -- set the typed prefix + edit the rest (rest-relative indices)
{
  const src = JSON.stringify({ id: "x", header: ["title", 1, 2, 3] });
  const p = JSON.parse(dec.decode(patchShapes(enc.encode(src), {}, undefined, { header: { set: { 0: "TITLE" }, rest: { append: [4], removeAt: [0] } } })));
  ok("prefix tuple set + rest", JSON.stringify(p.header) === JSON.stringify(["TITLE", 2, 3, 4]));
}
// removals = optional keys only -- deleting "tags" (optional) is fine; ["id"] (required) is a COMPILE error
{
  const src = JSON.stringify({ id: "x", tags: ["a"], coord: [1, 2] });
  const p = JSON.parse(dec.decode(patchShapes(enc.encode(src), {}, ["tags"])));
  ok("remove optional member", p.tags === undefined && p.id === "x" && JSON.stringify(p.coord) === JSON.stringify([1, 2]));
}

console.log(`shapes-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`shapes-access: ${fail} failed`); }
console.log("OK -- reshaped arrays: list object-keyed set/insert, pure-tuple positional set, prefix-tuple set+rest, optional-only removals.");
