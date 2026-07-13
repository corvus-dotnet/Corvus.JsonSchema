// Tier 1 — the schema-editor shape logic (schema-authoring.js) + the model-diff order/rename seam. node --test.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  classifyNode, unrenderedKeywords, addProperty, renameProperty, reorderProperty, removeProperty,
  setRequired, setType, constraintsDroppedOnType, setCombinerKind, addVariant, removeVariant, reorderVariant,
  setConstraint, emissionCheck,
} from '../src/schema-authoring.js';
import { diff } from '../src/workflow-document-model.js';

// ── classification ────────────────────────────────────────────────────────────────────────────────
test('classifyNode: plain typed nodes are renderable', () => {
  assert.equal(classifyNode({ type: 'string' }).kind, 'renderable');
  assert.equal(classifyNode({ type: 'object', properties: {} }).kind, 'renderable');
  assert.equal(classifyNode({}).kind, 'renderable'); // typeless blank — editor picks a type
});

test('classifyNode: combiners report their kind', () => {
  assert.deepEqual(classifyNode({ oneOf: [] }), { kind: 'combiner', combiner: 'oneOf' });
  assert.deepEqual(classifyNode({ anyOf: [] }), { kind: 'combiner', combiner: 'anyOf' });
  assert.deepEqual(classifyNode({ allOf: [] }), { kind: 'combiner', combiner: 'allOf' });
});

test('classifyNode: §3.3 constructs are advanced, with the trigger', () => {
  for (const k of ['not', '$ref', 'patternProperties', 'prefixItems', '$defs', 'dependentSchemas', 'if']) {
    const c = classifyNode({ [k]: {} });
    assert.equal(c.kind, 'advanced', `${k} ⇒ advanced`);
    assert.equal(c.advancedBy, k);
  }
  assert.equal(classifyNode({ type: ['string', 'null'] }).advancedBy, 'type[]');
  assert.equal(classifyNode({ oneOf: [], anyOf: [] }).kind, 'advanced'); // multiple combiners at once
});

test('classifyNode: a boolean schema is its own kind', () => {
  assert.equal(classifyNode(true).kind, 'boolean-schema');
  assert.equal(classifyNode(false).kind, 'boolean-schema');
});

test('unrenderedKeywords surfaces only the non-rendered present keywords', () => {
  const kw = unrenderedKeywords({ type: 'string', deprecated: true, examples: ['x'], 'x-vendor': 1, pattern: '^a' });
  assert.deepEqual(kw.sort(), ['deprecated', 'examples', 'x-vendor']);
});

// ── property mutations (key order + required) ───────────────────────────────────────────────────────
test('addProperty appends without disturbing existing order', () => {
  const s = { type: 'object', properties: { a: {}, b: {} } };
  addProperty(s, 'c', { type: 'integer' });
  assert.deepEqual(Object.keys(s.properties), ['a', 'b', 'c']);
});

test('renameProperty preserves position AND rewrites required', () => {
  const s = { type: 'object', properties: { a: { type: 'string' }, b: {}, c: {} }, required: ['a', 'c'] };
  renameProperty(s, 'a', 'aa');
  assert.deepEqual(Object.keys(s.properties), ['aa', 'b', 'c'], 'renamed key stays in place');
  assert.deepEqual(s.required, ['aa', 'c'], 'requiredness follows the rename');
});

test('reorderProperty moves a key within the object', () => {
  const s = { properties: { a: {}, b: {}, c: {} } };
  reorderProperty(s, 'c', 0);
  assert.deepEqual(Object.keys(s.properties), ['c', 'a', 'b']);
});

test('removeProperty drops the key and its requiredness; last-required deletes the array', () => {
  const s = { properties: { a: {}, b: {} }, required: ['a'] };
  removeProperty(s, 'a');
  assert.deepEqual(Object.keys(s.properties), ['b']);
  assert.equal(s.required, undefined, 'emptied required array is deleted');
});

test('setRequired toggles the parent array and deletes it when empty', () => {
  const s = { properties: { a: {} } };
  setRequired(s, 'a', true);
  assert.deepEqual(s.required, ['a']);
  setRequired(s, 'a', false);
  assert.equal(s.required, undefined);
});

// ── type & combiner conversions ─────────────────────────────────────────────────────────────────────
test('constraintsDroppedOnType lists what a type change would lose (no mutation)', () => {
  const s = { type: 'string', pattern: '^a', minLength: 2, title: 'X' };
  assert.deepEqual(constraintsDroppedOnType(s, 'integer').sort(), ['minLength', 'pattern']);
  assert.equal(s.pattern, '^a', 'the check does not mutate');
});

test('setType drops inapplicable constraints and keeps neutral ones', () => {
  const s = { type: 'string', pattern: '^a', title: 'X', description: 'd', default: 'z' };
  const dropped = setType(s, 'integer');
  assert.deepEqual(dropped, ['pattern']);
  assert.equal(s.type, 'integer');
  assert.equal(s.pattern, undefined);
  assert.equal(s.title, 'X'); assert.equal(s.description, 'd'); assert.equal(s.default, 'z');
});

test('type → combiner seeds variant 1 with the current schema (lossless)', () => {
  const s = { type: 'string', format: 'email', title: 'Contact' };
  setType(s, 'oneOf');
  assert.ok(Array.isArray(s.oneOf) && s.oneOf.length === 1);
  assert.deepEqual(s.oneOf[0], { type: 'string', format: 'email' }, 'variant 1 carries the old constraints');
  assert.equal(s.title, 'Contact', 'the combiner node keeps its title');
  assert.equal(s.type, undefined);
});

test('combiner → type takes variant 1', () => {
  const s = { oneOf: [{ type: 'integer', minimum: 0 }, { type: 'string' }], title: 'T' };
  setType(s, 'integer');
  assert.equal(s.type, 'integer');
  assert.equal(s.minimum, 0);
  assert.equal(s.oneOf, undefined);
  assert.equal(s.title, 'T');
});

test('setCombinerKind swaps the keyword and keeps variants', () => {
  const s = { anyOf: [{ type: 'string' }, { type: 'integer' }] };
  setCombinerKind(s, 'oneOf');
  assert.equal(s.anyOf, undefined);
  assert.deepEqual(s.oneOf, [{ type: 'string' }, { type: 'integer' }]);
});

test('add/remove/reorder variant', () => {
  const s = { oneOf: [{ type: 'string' }] };
  addVariant(s, { type: 'integer' });
  assert.equal(s.oneOf.length, 2);
  reorderVariant(s, 1, 0);
  assert.deepEqual(s.oneOf[0], { type: 'integer' });
  removeVariant(s, 0);
  assert.deepEqual(s.oneOf, [{ type: 'string' }]);
});

test('setConstraint deletes the key on a blank value rather than writing null', () => {
  const s = { type: 'string', pattern: '^a' };
  setConstraint(s, 'pattern', '');
  assert.ok(!('pattern' in s));
  setConstraint(s, 'minLength', 3);
  assert.equal(s.minLength, 3);
});

// ── emission check ──────────────────────────────────────────────────────────────────────────────────
test('emissionCheck: renderable trees and normalizable combiners pass', () => {
  assert.ok(emissionCheck({ type: 'object', properties: { a: { type: 'string' }, b: { oneOf: [{ type: 'integer' }, { type: 'null' }] } } }));
  assert.ok(emissionCheck({ allOf: [{ properties: { a: {} } }, { properties: { b: {} } }] }), 'simple allOf renders');
  assert.ok(emissionCheck(true), 'boolean schema is fine');
});

test('emissionCheck: a non-simple allOf (raw fallback) is a violation', () => {
  assert.ok(!emissionCheck({ allOf: [{ properties: { id: { type: 'string' } } }, { properties: { id: { type: 'integer' } } }] }));
});

// ── the model-diff order/rename seam (mandatory — without it these edits evaporate) ──────────────────
test('model diff: a pure property reorder emits one whole-object set op (not zero ops)', () => {
  const before = { inputs: { type: 'object', properties: { a: { type: 'string' }, b: { type: 'integer' } } } };
  const after = { inputs: { type: 'object', properties: { b: { type: 'integer' }, a: { type: 'string' } } } };
  const ops = diff(before, after);
  const set = ops.find((o) => o.kind === 'set' && o.path.join('/') === 'inputs/properties');
  assert.ok(set, 'a set op is emitted for the reordered object');
  assert.deepEqual(Object.keys(set.value), ['b', 'a'], 're-applying the op reproduces the new order');
});

test('model diff: a property rename emits a whole-object set (position preserved, not remove+append)', () => {
  const before = { inputs: { properties: { a: { type: 'string' }, b: { type: 'integer' } } } };
  const after = { inputs: { properties: { aa: { type: 'string' }, b: { type: 'integer' } } } };
  const ops = diff(before, after);
  const set = ops.find((o) => o.kind === 'set' && o.path.join('/') === 'inputs/properties');
  assert.ok(set, 'the rename is one whole-object set');
  assert.deepEqual(Object.keys(set.value), ['aa', 'b'], 'aa stays in a-s position, not re-appended at the end');
  assert.ok(!ops.some((o) => o.kind === 'remove'), 'no stray remove that would drop the value');
});

test('model diff: an ordinary value edit still diffs per-key (no coarsening)', () => {
  const before = { inputs: { properties: { a: { type: 'string' }, b: { type: 'integer' } } } };
  const after = { inputs: { properties: { a: { type: 'string', minLength: 2 }, b: { type: 'integer' } } } };
  const ops = diff(before, after);
  assert.ok(ops.some((o) => o.path.join('/').startsWith('inputs/properties/a')), 'the edit localises to property a');
  assert.ok(!ops.some((o) => o.path.join('/') === 'inputs/properties'), 'not a whole-object set');
});

// ── round-trip guarantee (the heart of the slice) ─────────────────────────────────────────────────────
test('round-trip: a visual edit on one node leaves every other byte equal', () => {
  const rich = {
    type: 'object',
    $comment: 'keep me',
    'x-vendor': { flags: [1, 2] },
    $defs: { Money: { type: 'number' } },
    properties: {
      a: { type: 'string', deprecated: true, examples: ['x'], pattern: '^a' },
      poly: { oneOf: [{ type: 'string' }, { type: 'null' }] },
      nested: { type: 'object', properties: { z: { type: 'integer' } }, patternProperties: { '^x': {} } },
    },
    required: ['a'],
  };
  const clone = structuredClone(rich);
  renameProperty(clone, 'a', 'aa'); // touch ONE renderable node
  // Everything unrelated is byte-identical (keys, order, values).
  assert.equal(JSON.stringify(clone.$comment), JSON.stringify(rich.$comment));
  assert.equal(JSON.stringify(clone['x-vendor']), JSON.stringify(rich['x-vendor']));
  assert.equal(JSON.stringify(clone.$defs), JSON.stringify(rich.$defs));
  assert.equal(JSON.stringify(clone.properties.poly), JSON.stringify(rich.properties.poly));
  assert.equal(JSON.stringify(clone.properties.nested), JSON.stringify(rich.properties.nested));
  // The edit itself landed, requiredness followed, position preserved.
  assert.deepEqual(Object.keys(clone.properties), ['aa', 'poly', 'nested']);
  assert.deepEqual(clone.required, ['aa']);
});
