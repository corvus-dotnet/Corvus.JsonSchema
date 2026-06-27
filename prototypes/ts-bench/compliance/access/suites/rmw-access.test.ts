// PROVIDER REAL-OUTPUT test for the Model C byte-RMW surface (§13.4): the provider's emitted
// produce<T>Bytes splices only changed top-level members into the source bytes, copying the rest
// through verbatim. Edge cases: retained-over-mutated, deep nested values, value-skipping vs
// name-like content, string escapes, multibyte, pretty-print, prefix names, source-order, type
// changes, special chars, multi-edit, no-op, undefined, throw-on-missing, BOM.
//
// Run after generating rmw.json into out-rmw/:
//   dotnet run --project TsProviderSpike.csproj -c Debug -- rmw.json out-rmw
//   <ts-bench tsc> out-rmw/generated.ts out-rmw/corvus-runtime.ts rmw-access.test.ts spike-globals.d.ts \
//       --outDir prov-test-rmw --strict --target es2022 --module esnext --moduleResolution bundler --lib es2022,dom
//   node prov-test-rmw/rmw-access.test.js
import { patchSample, buildSample, type Sample } from "./out-rmw/generated.js";

let pass = 0, fail = 0;
const enc = new TextEncoder();
const dec = new TextDecoder();

function ok(label: string, cond: boolean): void {
  if (cond) { pass++; } else { fail++; console.log(`FAIL ${label}`); }
}
function eq(label: string, got: unknown, want: unknown): void {
  ok(label + ` (got ${JSON.stringify(got)})`, JSON.stringify(got) === JSON.stringify(want));
}
function rt(src: string, changes: Partial<Sample>): string {
  return dec.decode(patchSample(enc.encode(src), changes));
}
function throws(label: string, fn: () => void): void {
  try { fn(); fail++; console.log(`FAIL ${label}: expected throw`); } catch { pass++; }
}

const rich = JSON.stringify({
  id: "日本語-id", seq: 1, ratio: 0.5, active: true, note: "メモ書き",
  address: { street: "1 Main", city: "東京", zip: "100" },
  tags: ["alpha", "β", "γ"], a: 10, ab: 20, "x-special": "v",
});

// 1) retained over mutated: change one scalar, everything else byte-identical (incl. multibyte/nested/array)
{
  const out = rt(rich, { seq: 99 });
  const p = JSON.parse(out);
  const want = JSON.parse(rich); want.seq = 99;
  eq("1 seq changed", p.seq, 99);
  eq("1 all-others retained (value)", p, want);
  ok("1 byte-retain nested object", out.includes('"address":{"street":"1 Main","city":"東京","zip":"100"}'));
  ok("1 byte-retain array", out.includes('"tags":["alpha","β","γ"]'));
  ok("1 byte-retain multibyte id (before edit)", out.includes('"id":"日本語-id"'));
  ok("1 byte-retain multibyte note (after edit)", out.includes('"note":"メモ書き"'));
}

// 2) deep nested value replaced; sibling subtree retained
{
  const out = rt(rich, { address: { street: "new", city: "NYC", zip: "999" } });
  const p = JSON.parse(out);
  eq("2 address replaced", p.address, { street: "new", city: "NYC", zip: "999" });
  eq("2 tags retained", p.tags, ["alpha", "β", "γ"]);
  ok("2 byte-retain tags", out.includes('"tags":["alpha","β","γ"]'));
}

// 3) a value that CONTAINS name-like content must not be matched as a top-level member
{
  const tricky = JSON.stringify({ id: "x", seq: 1, note: '{"seq": 999, "active": false, "a": 1}', a: 5, ab: 6 });
  const out = rt(tricky, { seq: 7, a: 50 });
  const p = JSON.parse(out);
  eq("3 real seq changed", p.seq, 7);
  eq("3 real a changed", p.a, 50);
  eq("3 note (with inner key names) untouched", p.note, '{"seq": 999, "active": false, "a": 1}');
  eq("3 ab untouched", p.ab, 6);
}

// 4) string escapes in a value before the edited member (closing-quote / backslash parity)
{
  const esc = JSON.stringify({ seq: 1, note: 'he said "hi" \\ and \\" bye', a: 5 });
  const out = rt(esc, { a: 50 });
  const p = JSON.parse(out);
  eq("4 a changed past escaped string", p.a, 50);
  eq("4 escaped note retained", p.note, 'he said "hi" \\ and \\" bye');
  eq("4 seq retained", p.seq, 1);
}

// 6) pretty-printed source (whitespace between tokens)
{
  const pretty = JSON.stringify(JSON.parse(rich), null, 2);
  const out = dec.decode(patchSample(enc.encode(pretty), { seq: 99 }));
  eq("6 pretty seq changed", JSON.parse(out).seq, 99);
  eq("6 pretty address retained", JSON.parse(out).address, { street: "1 Main", city: "東京", zip: "100" });
}

// 7) prefix property names: changing `a` must not touch `ab`, and vice versa
{
  const oa = JSON.parse(rt(rich, { a: 111 }));
  eq("7 a changed", oa.a, 111); eq("7 ab untouched", oa.ab, 20);
  const ob = JSON.parse(rt(rich, { ab: 222 }));
  eq("7 ab changed", ob.ab, 222); eq("7 a untouched", ob.a, 10);
}

// 8) source key order != schema (alphabetical) order: locate by name
{
  const reordered = JSON.stringify({ "x-special": "v", ab: 20, a: 10, tags: ["t"], note: "n", active: true, ratio: 0.5, seq: 1, id: "i" });
  const p = JSON.parse(rt(reordered, { seq: 99, id: "newid" }));
  eq("8 reordered seq", p.seq, 99); eq("8 reordered id", p.id, "newid"); eq("8 reordered ab retained", p.ab, 20);
}

// 9) type change (consumer deliberately bypasses the typed surface)
{
  const o1 = JSON.parse(dec.decode(patchSample(enc.encode(rich), { seq: "now-a-string" } as unknown as Partial<Sample>)));
  eq("9 seq int->string", o1.seq, "now-a-string");
  const o2 = JSON.parse(dec.decode(patchSample(enc.encode(rich), { active: null } as unknown as Partial<Sample>)));
  eq("9 active bool->null", o2.active, null);
}

// 10) special chars in the new value -> JSON.stringify escaping round-trips
{
  const tricky = 'has "quotes", \\ backslash, \n newline, and 日本語';
  eq("10 special-char note", JSON.parse(rt(rich, { note: tricky })).note, tricky);
}

// 11) multiple simultaneous edits
{
  const p = JSON.parse(rt(rich, { seq: 99, ratio: 9.9, note: "changed", a: 1 }));
  ok("11 multi-edit", p.seq === 99 && p.ratio === 9.9 && p.note === "changed" && p.a === 1 && p.ab === 20 && p.active === true);
}

// 12) no changes -> source returned byte-identical
{
  ok("12 no-change byte-identical", rt(rich, {}) === rich);
}

// 13) explicit undefined is a no-op, not a delete
{
  ok("13 undefined skipped byte-identical", rt(rich, { seq: undefined }) === rich);
}

// 14) upsert: setting a member absent from the source ADDS it (set = replace-or-add)
{
  const noSeq = JSON.stringify({ id: "x", a: 5 });
  const p = JSON.parse(dec.decode(patchSample(enc.encode(noSeq), { seq: 5 })));
  ok("14 upsert adds absent member", p.seq === 5 && p.id === "x" && p.a === 5);
}

// 15) BOM-prefixed source: BOM copied through, member still located
{
  const body = enc.encode(rich);
  const bom = new Uint8Array(body.length + 3);
  bom.set([0xef, 0xbb, 0xbf], 0); bom.set(body, 3);
  const outBytes = patchSample(bom, { seq: 99 });
  ok("15 BOM preserved", outBytes[0] === 0xef && outBytes[1] === 0xbb && outBytes[2] === 0xbf);
  eq("15 BOM seq changed", JSON.parse(dec.decode(outBytes)).seq, 99);
}

// 16) replacement with large / structural size deltas: retained-around members (before AND after) intact
{
  const grow = "X".repeat(500);
  const og = JSON.parse(rt(rich, { note: grow }));
  eq("16 grow note (+500B)", og.note, grow); eq("16 grow seq retained", og.seq, 1); eq("16 grow tags retained", og.tags, ["alpha", "β", "γ"]);
  // object -> scalar (large shrink) in the middle: siblings before/after stay correct
  const shrink = JSON.parse(dec.decode(patchSample(enc.encode(rich), { address: 42 } as unknown as Partial<Sample>)));
  eq("16 address object->scalar", shrink.address, 42); eq("16 id retained (before)", shrink.id, "日本語-id"); eq("16 tags retained (after)", shrink.tags, ["alpha", "β", "γ"]);
  // scalar -> object (grow in the middle)
  const grow2 = JSON.parse(dec.decode(patchSample(enc.encode(rich), { seq: { nested: [1, 2] } } as unknown as Partial<Sample>)));
  eq("16 seq scalar->object", grow2.seq, { nested: [1, 2] }); eq("16 ratio retained", grow2.ratio, 0.5); eq("16 address retained", grow2.address, { street: "1 Main", city: "東京", zip: "100" });
}

// --- removal (the produce*Bytes `removals` param) ---
const rtRem = (src: string, ch: Partial<Sample>, rm: ReadonlyArray<keyof Sample>): string =>
  dec.decode(patchSample(enc.encode(src), ch, rm));

// 17) remove single member (first / middle / last in source order)
{
  const noId = JSON.parse(rtRem(rich, {}, ["id"]));
  ok("17 remove id (first)", noId.id === undefined && noId.seq === 1 && noId.tags !== undefined);
  const noActive = JSON.parse(rtRem(rich, {}, ["active"]));
  ok("17 remove active (middle)", noActive.active === undefined && noActive.seq === 1 && noActive.note === "メモ書き");
  const noX = JSON.parse(rtRem(rich, {}, ["x-special"]));
  ok("17 remove x-special (last)", noX["x-special"] === undefined && noX.ab === 20);
}
// 18) remove + replace in one call
{
  const p = JSON.parse(rtRem(rich, { seq: 99 }, ["ratio"]));
  ok("18 remove ratio + replace seq", p.seq === 99 && p.ratio === undefined && p.id === "日本語-id");
}
// 19) remove multiple: adjacent (source order) and non-adjacent
{
  const p1 = JSON.parse(rtRem(rich, {}, ["seq", "ratio"]));
  ok("19 remove seq,ratio (adjacent)", p1.seq === undefined && p1.ratio === undefined && p1.id === "日本語-id" && p1.active === true);
  const p2 = JSON.parse(rtRem(rich, {}, ["id", "tags"]));
  ok("19 remove id,tags (non-adjacent)", p2.id === undefined && p2.tags === undefined && p2.seq === 1 && p2.a === 10);
}
// 20) remove a nested-object member
{
  const p = JSON.parse(rtRem(rich, {}, ["address"]));
  ok("20 remove nested-object member", p.address === undefined && p.tags !== undefined && p.note === "メモ書き");
}
// 21) throw on removing a member absent from the source
{
  const noSeq = JSON.stringify({ id: "x", a: 5 });
  throws("21 throw remove missing", () => patchSample(enc.encode(noSeq), {}, ["seq"]));
}
// 22) removal under pretty-print
{
  const pretty = JSON.stringify(JSON.parse(rich), null, 2);
  const p = JSON.parse(dec.decode(patchSample(enc.encode(pretty), {}, ["active"])));
  ok("22 pretty remove active", p.active === undefined && p.seq === 1 && JSON.stringify(p.address) === JSON.stringify({ street: "1 Main", city: "東京", zip: "100" }));
}
// 23) byte-retention of complex siblings after a removal
{
  const out = rtRem(rich, {}, ["seq"]);
  ok("23 byte-retain address after removal", out.includes('"address":{"street":"1 Main","city":"東京","zip":"100"}'));
  ok("23 byte-retain tags after removal", out.includes('"tags":["alpha","β","γ"]'));
}

// --- upsert / add (the `changes` param now sets = replace-or-add) ---
// 24) add members absent from the source
{
  const partial = JSON.stringify({ id: "x", seq: 1 });
  const p = JSON.parse(dec.decode(patchSample(enc.encode(partial), { note: "added", active: true })));
  ok("24 add absent props", p.note === "added" && p.active === true && p.id === "x" && p.seq === 1);
}
// 25) replace existing + add absent in one call
{
  const partial = JSON.stringify({ id: "x", seq: 1 });
  const p = JSON.parse(dec.decode(patchSample(enc.encode(partial), { seq: 99, note: "new" })));
  ok("25 replace + add", p.seq === 99 && p.note === "new" && p.id === "x");
}
// 26) add appends at end; multibyte sibling retained byte-wise
{
  const noNote = JSON.stringify({ id: "日本語-id", seq: 1, tags: ["alpha", "β"] });
  const out = dec.decode(patchSample(enc.encode(noNote), { note: "メモ" }));
  const p = JSON.parse(out);
  ok("26 add appended", p.note === "メモ" && JSON.stringify(p.tags) === JSON.stringify(["alpha", "β"]));
  ok("26 byte-retain id before add", out.includes('"id":"日本語-id"'));
}
// 27) add to an empty object
{
  const p = JSON.parse(dec.decode(patchSample(enc.encode("{}"), { seq: 5 })));
  ok("27 add to empty", p.seq === 5);
}
// 28) add + remove in one call
{
  const src = JSON.stringify({ id: "x", seq: 1, ratio: 0.5 });
  const p = JSON.parse(dec.decode(patchSample(enc.encode(src), { note: "n" }, ["ratio"])));
  ok("28 add note + remove ratio", p.note === "n" && p.ratio === undefined && p.id === "x" && p.seq === 1);
}
// 29) add complex (nested object + array) values
{
  const src = JSON.stringify({ id: "x" });
  const p = JSON.parse(dec.decode(patchSample(enc.encode(src), { address: { street: "S", city: "C", zip: "Z" }, tags: ["t1", "t2"] })));
  ok("29 add complex values", JSON.stringify(p.address) === JSON.stringify({ street: "S", city: "C", zip: "Z" }) && JSON.stringify(p.tags) === JSON.stringify(["t1", "t2"]) && p.id === "x");
}
// 30) pretty-printed add (assert parsed)
{
  const pretty = JSON.stringify({ id: "x", seq: 1 }, null, 2);
  const p = JSON.parse(dec.decode(patchSample(enc.encode(pretty), { note: "n" })));
  ok("30 pretty add", p.note === "n" && p.id === "x" && p.seq === 1);
}

// --- overlay via patch (the loose apply was just patch; real apply is allOf-only) ---
// 31) patch as overlay: replace existing + add new, leave the rest
{
  const base = JSON.stringify({ id: "x", seq: 1, ratio: 0.5 });
  const p = JSON.parse(dec.decode(patchSample(enc.encode(base), { seq: 99, note: "added" })));
  ok("31 patch overlay", p.seq === 99 && p.note === "added" && p.ratio === 0.5 && p.id === "x");
}

// --- build (bytes from typed props at the native serialiser floor; canonical write is a separate opt-in) ---
// 32) build round-trips and equals native JSON.stringify(props) (caller key order, NOT canonicalised)
{
  const props = { id: "x", seq: 1, active: true };
  const out = dec.decode(buildSample(props));
  const p = JSON.parse(out);
  ok("32 build round-trips", p.id === "x" && p.seq === 1 && p.active === true);
  ok("32 build = JSON.stringify (caller order)", out === JSON.stringify(props));
}
// 33) build empty -> {}
{
  ok("33 build empty", dec.decode(buildSample({})) === "{}");
}
// 34) build with complex (nested object + array) values
{
  const p = JSON.parse(dec.decode(buildSample({ id: "x", address: { street: "S", city: "C", zip: "Z" }, tags: ["t"] })));
  ok("34 build complex", p.id === "x" && JSON.stringify(p.address) === JSON.stringify({ street: "S", city: "C", zip: "Z" }) && JSON.stringify(p.tags) === JSON.stringify(["t"]));
}
// 35) build then RMW: construct, then produce over the built bytes
{
  const built = buildSample({ id: "x", seq: 1 });
  const p = JSON.parse(dec.decode(patchSample(built, { seq: 99, note: "n" })));
  ok("35 build then produce", p.id === "x" && p.seq === 99 && p.note === "n");
}

// --- nested array integration (the 4th `arrays` param: element-wise ops on array members) ---
// 36) nested append (addRange), surrounding object preserved byte-wise
{
  const out = dec.decode(patchSample(enc.encode(rich), {}, undefined, { tags: { append: ["δ"] } }));
  const p = JSON.parse(out);
  ok("36 nested append", JSON.stringify(p.tags) === JSON.stringify(["alpha", "β", "γ", "δ"]) && p.seq === 1);
  ok("36 byte-retain sibling object", out.includes('"address":{"street":"1 Main","city":"東京","zip":"100"}'));
  ok("36 byte-retain unchanged elements", out.includes('"alpha","β","γ"'));
}
// 37) nested insert / set / removeAt (object-keyed)
{
  const ins = JSON.parse(dec.decode(patchSample(enc.encode(rich), {}, undefined, { tags: { insert: { 1: ["X"] } } })));
  ok("37 nested insert", JSON.stringify(ins.tags) === JSON.stringify(["alpha", "X", "β", "γ"]));
  const setd = JSON.parse(dec.decode(patchSample(enc.encode(rich), {}, undefined, { tags: { set: { 0: "ZERO" } } })));
  ok("37 nested set", JSON.stringify(setd.tags) === JSON.stringify(["ZERO", "β", "γ"]));
  const remd = JSON.parse(dec.decode(patchSample(enc.encode(rich), {}, undefined, { tags: { removeAt: [1] } })));
  ok("37 nested removeAt", JSON.stringify(remd.tags) === JSON.stringify(["alpha", "γ"]));
}
// 38) nested array op + scalar replace + member removal, all in one call
{
  const out = dec.decode(patchSample(enc.encode(rich), { seq: 99 }, ["ratio"], { tags: { append: ["new"] } }));
  const p = JSON.parse(out);
  ok("38 combined nested+scalar+remove", p.seq === 99 && p.ratio === undefined && JSON.stringify(p.tags) === JSON.stringify(["alpha", "β", "γ", "new"]));
}
// 39) nested addRange; multibyte sibling preserved byte-wise
{
  const out = dec.decode(patchSample(enc.encode(rich), {}, undefined, { tags: { append: ["d", "e"] } }));
  const p = JSON.parse(out);
  ok("39 nested addRange", JSON.stringify(p.tags) === JSON.stringify(["alpha", "β", "γ", "d", "e"]));
  ok("39 byte-retain multibyte sibling", out.includes('"note":"メモ書き"'));
}

console.log(`rmw-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`rmw-access: ${fail} failed`); }
console.log("OK -- byte-RMW produce*Bytes: edges (retain/nested/escapes/multibyte/prefix/order/types/BOM) all hold.");
