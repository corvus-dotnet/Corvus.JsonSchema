// Unit tests for the response-derived criteria templates (src/criteria-templates.js).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { templatesFromResponses } from '../src/operation-templates.js';

test('a single success code templates an exact status criterion', () => {
  const t = templatesFromResponses(['201', '402']);
  assert.deepEqual(t.successCriteria, [{ condition: '$statusCode == 201' }]);
});

test('several success codes (or a 2XX wildcard) template the range form', () => {
  assert.deepEqual(
    templatesFromResponses(['200', '201']).successCriteria,
    [{ condition: '$statusCode >= 200 && $statusCode < 300' }],
  );
  assert.deepEqual(
    templatesFromResponses(['2XX']).successCriteria,
    [{ condition: '$statusCode >= 200 && $statusCode < 300' }],
  );
});

test('documented error codes each template a criteria-ed failure action', () => {
  const t = templatesFromResponses(['201', '402', '429']);
  const names = t.failureActions.map((a) => a.name);
  assert.deepEqual(names, ['on-402', 'on-429', 'unexpected-failure']);
  assert.deepEqual(t.failureActions[0].criteria, [{ condition: '$statusCode == 402' }]);
});

test('wildcard error ranges become range conditions', () => {
  const t = templatesFromResponses(['200', '5XX']);
  assert.deepEqual(t.failureActions[0], {
    name: 'on-5xx',
    type: 'end',
    criteria: [{ condition: '$statusCode >= 500 && $statusCode < 600' }],
  });
});

test('a documented default becomes the catch-all; without one, unexpected-failure is added', () => {
  const withDefault = templatesFromResponses({ 200: {}, 400: {}, default: {} });
  assert.equal(withDefault.failureActions.at(-1).name, 'on-default');
  assert.equal(withDefault.failureActions.at(-1).criteria, undefined, 'catch-all has no criteria');

  const without = templatesFromResponses(['200', '400']);
  assert.equal(without.failureActions.at(-1).name, 'unexpected-failure');
  assert.equal(without.failureActions.at(-1).criteria, undefined);
});

test('accepts an OpenAPI responses map shape; nothing in, null out', () => {
  const t = templatesFromResponses({ 201: { description: 'created' }, 429: {} });
  assert.equal(t.successCriteria[0].condition, '$statusCode == 201');
  assert.equal(templatesFromResponses([]), null);
  assert.equal(templatesFromResponses(undefined), null);
});

import { payloadSkeletonFromSchema } from '../src/operation-templates.js';

test('payload skeleton: objects recurse, primitives stub, default/const/enum honoured', () => {
  const skeleton = payloadSkeletonFromSchema({
    type: 'object',
    properties: {
      orderId: { type: 'string' },
      amount: { type: 'number' },
      capture: { type: 'boolean', default: true },
      currency: { enum: ['GBP', 'USD'] },
      kind: { const: 'card' },
      card: { type: 'object', properties: { number: { type: 'string' } } },
      lines: { type: 'array', items: { type: 'object', properties: { sku: { type: 'string' }, qty: { type: 'integer' } } } },
    },
  });
  assert.deepEqual(skeleton, {
    orderId: '',
    amount: 0,
    capture: true,
    currency: 'GBP',
    kind: 'card',
    card: { number: '' },
    lines: [{ sku: '', qty: 0 }],
  });
});

test('payload skeleton: no schema → undefined; cyclic depth capped', () => {
  assert.equal(payloadSkeletonFromSchema(undefined), undefined);
  const cyclic = { type: 'object', properties: {} };
  cyclic.properties.self = cyclic;
  const out = payloadSkeletonFromSchema(cyclic); // terminates
  assert.equal(typeof out, 'object');
});
