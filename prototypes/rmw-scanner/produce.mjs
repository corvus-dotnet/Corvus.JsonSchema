// Mutation API spike (design §5.7): the idiomatic immer-style `produce(doc, recipe)`.
// Proves: (1) write "mutations" on a draft, get a new value + the original is untouched;
//         (2) the recipe yields a change-set that IS RFC 6902 JSON Patch; and
//         (3) that change-set lowers to a Model C structural-sharing BYTE patch (unchanged
//             bytes copied verbatim) — reusing the proven scanner/applyEditsBytes.
// Zero third-party dependency: we don't need immer's object structural sharing because the
// sharing happens at the byte level (Model C), so a tiny Proxy change-recorder is enough.
// Run: node produce.mjs

import { scanTargets } from './scanner.mjs';
import { applyEditsBytes, makeTargets } from './rmw.mjs';

const enc = new TextEncoder();
const dec = new TextDecoder();

function escPtr(k) { return String(k).replace(/~/g, '~0').replace(/\//g, '~1'); }

// immer-style produce: run the recipe over a recording draft; return { next, patches }.
function produce(base, recipe) {
  const next = structuredClone(base);
  const patches = [];
  const wrap = (obj, ptr) =>
    new Proxy(obj, {
      get(o, k) {
        const v = o[k];
        return v !== null && typeof v === 'object' ? wrap(v, ptr + '/' + escPtr(k)) : v;
      },
      set(o, k, val) {
        o[k] = val;
        patches.push({ op: 'replace', path: ptr + '/' + escPtr(k), value: val });
        return true;
      },
    });
  recipe(wrap(next, ''));
  return { next, patches };
}

// Lower the TOP-LEVEL replace patches to a Model C byte patch over the original bytes.
// (Nested paths need the §13.3 lazy-descent index; the change-set already captures them.)
function applyTopLevelToBytes(originalBytes, patches) {
  const top = patches.filter((p) => p.path.indexOf('/', 1) === -1);
  const names = top.map((p) => p.path.slice(1));
  const contents = top.map((p) => enc.encode(JSON.stringify(p.value)));
  const targets = makeTargets(names, contents);
  scanTargets(originalBytes, targets);
  const edits = targets.map((t) => ({ offset: t.vbs, length: t.vbe - t.vbs, content: t.content }));
  return applyEditsBytes(originalBytes, edits);
}

// ---- demo ----
let pass = 0, fail = 0;
const eq = (label, got, want) => {
  const ok = JSON.stringify(got) === JSON.stringify(want);
  ok ? pass++ : (fail++, console.log(`FAIL ${label}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`));
};

const base = {
  name: 'Ada',
  age: 30,
  address: { street: '1 Main St', city: 'Anytown' },
  active: true,
};
const baseBytes = enc.encode(JSON.stringify(base));
const baseSnapshot = JSON.stringify(base);

// idiomatic: "mutate" the draft, get a new immutable value
const { next, patches } = produce(base, (d) => {
  d.age = 31;                 // top-level scalar
  d.name = 'Ada Lovelace';    // top-level scalar
  d.address.city = 'London';  // NESTED scalar (typed in mutation-model.ts)
});

eq('original untouched', JSON.stringify(base), baseSnapshot);
eq('next.age', next.age, 31);
eq('next.name', next.name, 'Ada Lovelace');
eq('next.address.city', next.address.city, 'London');
eq('next.active unchanged', next.active, true);

// the recorded change-set IS RFC 6902 JSON Patch
eq('patches (RFC 6902)', patches, [
  { op: 'replace', path: '/age', value: 31 },
  { op: 'replace', path: '/name', value: 'Ada Lovelace' },
  { op: 'replace', path: '/address/city', value: 'London' },
]);

// ... and the top-level subset lowers to a Model C byte patch (unchanged bytes copied verbatim)
const newBytes = applyTopLevelToBytes(baseBytes, patches);
const reparsed = JSON.parse(dec.decode(newBytes));
eq('byte patch: age', reparsed.age, 31);
eq('byte patch: name', reparsed.name, 'Ada Lovelace');
eq('byte patch: address unchanged (verbatim copy)', reparsed.address, { street: '1 Main St', city: 'Anytown' });
eq('byte patch: active unchanged', reparsed.active, true);
eq('original bytes untouched', dec.decode(baseBytes), baseSnapshot);

console.log(`\npatches = ${JSON.stringify(patches)}`);
console.log(`${pass} passed, ${fail} failed`);
if (fail > 0) { process.exit(1); }
console.log('OK — immer-style produce -> change-set (RFC 6902) -> Model C byte patch.');
