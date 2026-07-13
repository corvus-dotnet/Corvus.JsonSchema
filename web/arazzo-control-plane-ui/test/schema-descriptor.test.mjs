// Tier 1 — the shared combiner normalizer (schema-descriptor.js). DOM-free; node --test.
// Fixtures mirror the .NET generator tests (WorkflowSchemaMetadataGeneratorTests) — keep the two in step.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeDescriptor, deepEqualUnordered } from '../src/schema-descriptor.js';

test('a plain typed schema passes through unchanged', () => {
  const s = { type: 'string', format: 'email' };
  assert.equal(normalizeDescriptor(s), s);
});

test('null and non-objects pass through', () => {
  assert.equal(normalizeDescriptor(null), null);
  assert.equal(normalizeDescriptor(undefined), undefined);
  assert.equal(normalizeDescriptor('x'), 'x');
});

test('raw oneOf becomes a union with the raw variants preserved', () => {
  const d = normalizeDescriptor({ oneOf: [{ type: 'string' }, { type: 'integer' }] });
  assert.equal(d.type, 'union');
  assert.deepEqual(d.variants, [{ type: 'string' }, { type: 'integer' }]);
  assert.equal(d.nullable, undefined);
});

test('raw anyOf becomes a union too', () => {
  const d = normalizeDescriptor({ anyOf: [{ type: 'string' }, { type: 'number' }] });
  assert.equal(d.type, 'union');
  assert.equal(d.variants.length, 2);
});

test('oneOf takes precedence over anyOf (generator read order)', () => {
  const d = normalizeDescriptor({ oneOf: [{ type: 'string' }, { type: 'boolean' }], anyOf: [{ type: 'integer' }] });
  assert.equal(d.type, 'union');
  assert.deepEqual(d.variants, [{ type: 'string' }, { type: 'boolean' }]);
});

test('a null branch collapses: X | null unwraps to a nullable X', () => {
  const d = normalizeDescriptor({ oneOf: [{ type: 'string', format: 'email' }, { type: 'null' }] });
  assert.equal(d.type, 'string');
  assert.equal(d.nullable, true);
  assert.equal(d.format, 'email');
});

test('a null branch on a real union sets nullable on the union', () => {
  const d = normalizeDescriptor({ anyOf: [{ type: 'string' }, { type: 'integer' }, { type: ['null'] }] });
  assert.equal(d.type, 'union');
  assert.equal(d.nullable, true);
  assert.equal(d.variants.length, 2);
});

test('a single non-null variant unwraps to that variant', () => {
  const d = normalizeDescriptor({ oneOf: [{ type: 'integer', minimum: 0 }] });
  assert.deepEqual(d, { type: 'integer', minimum: 0 });
});

test('all-null branches yield a null descriptor', () => {
  assert.deepEqual(normalizeDescriptor({ oneOf: [{ type: 'null' }] }), { type: 'null' });
});

test('an explicit OpenAPI discriminator is carried onto the union', () => {
  const d = normalizeDescriptor({
    oneOf: [{ type: 'object', properties: { kind: { const: 'a' } } }, { type: 'object', properties: { kind: { const: 'b' } } }],
    discriminator: { propertyName: 'kind' },
  });
  assert.equal(d.discriminator, 'kind');
});

test('a simple allOf merges properties and unions required', () => {
  const d = normalizeDescriptor({
    allOf: [
      { type: 'object', properties: { a: { type: 'string' } }, required: ['a'] },
      { type: 'object', properties: { b: { type: 'integer' } }, required: ['b'] },
    ],
  });
  assert.equal(d.type, 'object');
  assert.deepEqual(Object.keys(d.properties), ['a', 'b']);
  assert.deepEqual(d.required, ['a', 'b']);
});

test('an agreeing same-key overlap merges (order-insensitive) — reordered-keys fixture', () => {
  const d = normalizeDescriptor({
    allOf: [
      { properties: { id: { type: 'string', format: 'uuid' } } },
      { properties: { id: { format: 'uuid', type: 'string' } } }, // same schema, keys reordered
    ],
  });
  assert.ok(d && d.type === 'object', 'agreeing overlap merges despite key order');
  assert.deepEqual(Object.keys(d.properties), ['id']);
});

test('a conflicting same-key overlap falls back to raw (never a guessed merge)', () => {
  const s = {
    allOf: [
      { properties: { id: { type: 'string' } } },
      { properties: { id: { type: 'integer' } } },
    ],
  };
  assert.equal(normalizeDescriptor(s), s, 'conflict ⇒ the node passes through unchanged (raw JSON fallback)');
});

test('a non-object allOf branch falls back to raw', () => {
  const s = { allOf: [{ type: 'object', properties: { a: {} } }, { type: 'string' }] };
  assert.equal(normalizeDescriptor(s), s);
});

test('an allOf branch with a keyword beyond the mergeable set falls back to raw', () => {
  const s = { allOf: [{ type: 'object', properties: { a: {} }, minProperties: 1 }] };
  assert.equal(normalizeDescriptor(s), s);
});

test('siblings on an allOf node (title/description) survive the merge', () => {
  const d = normalizeDescriptor({
    title: 'Account',
    description: 'merged',
    allOf: [{ properties: { a: {} } }, { properties: { b: {} } }],
  });
  assert.equal(d.title, 'Account');
  assert.equal(d.description, 'merged');
});

test('an already-normalized union descriptor passes through unchanged', () => {
  const d = { type: 'union', variants: [{ type: 'string' }, { type: 'integer' }] };
  assert.equal(normalizeDescriptor(d), d);
});

test('normalizeDescriptor is deterministic (run twice)', () => {
  const s = { oneOf: [{ type: 'string' }, { type: 'null' }, { type: 'integer' }] };
  assert.deepEqual(normalizeDescriptor(s), normalizeDescriptor(s));
});

test('deepEqualUnordered ignores key order but respects array order', () => {
  assert.ok(deepEqualUnordered({ a: 1, b: 2 }, { b: 2, a: 1 }));
  assert.ok(!deepEqualUnordered([1, 2], [2, 1]));
  assert.ok(!deepEqualUnordered({ a: 1 }, { a: 1, b: 2 }));
});
