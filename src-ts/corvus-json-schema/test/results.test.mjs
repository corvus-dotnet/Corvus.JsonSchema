// Results collection: the expectations of the C# evaluator's ResultsTests and ResultPathTests, plus worked examples
// traced through the C# collecting path (row order, locations, messages, levels).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { collectAnnotations, compile, JsonSchemaResultsCollector, ResultsLevel } from '../dist/index.js';

function dump(validator, instance, level) {
  const collector = JsonSchemaResultsCollector.create(level);
  validator.evaluate(instance, collector);
  return collector.results
    .map((r) => `${r.isMatch ? 'match' : 'fail'}|${r.schemaEvaluationLocation}|${r.evaluationLocation}|${r.documentEvaluationLocation}|${r.message}`)
    .join('\n');
}

const PERSON = {
  $schema: 'https://json-schema.org/draft/2020-12/schema',
  type: 'object',
  title: 'Person',
  properties: {
    name: { type: 'string', minLength: 1, description: 'The name' },
    age: { type: 'integer', minimum: 0 },
  },
  required: ['name'],
  additionalProperties: false,
};

test('flag and collecting evaluation agree at every level', () => {
  const v = compile(PERSON);
  for (const [instance, expected] of [
    [{ name: 'a', age: 3 }, true],
    [{ name: '', age: 3 }, false],
    [{ age: 3 }, false],
    [{ name: 'a', extra: 1 }, false],
    [{ name: 'a', age: -1 }, false],
    [[], false],
  ]) {
    assert.equal(v(instance), expected);
    for (const level of [ResultsLevel.Basic, ResultsLevel.Detailed, ResultsLevel.Verbose]) {
      assert.equal(v.evaluate(instance, JsonSchemaResultsCollector.create(level)), expected);
    }
  }
});

test('basic results report failing keywords with locations', () => {
  const collector = JsonSchemaResultsCollector.create(ResultsLevel.Basic);
  assert.equal(compile(PERSON).evaluate({ name: '', age: -1 }, collector), false);
  const failures = collector.results.filter((r) => !r.isMatch);
  assert.ok(failures.some((f) => f.evaluationLocation.endsWith('/minLength') && f.documentEvaluationLocation === '/name'));
  assert.ok(failures.some((f) => f.evaluationLocation.endsWith('/minimum') && f.documentEvaluationLocation === '/age'));
  assert.ok(failures.some((f) => f.schemaEvaluationLocation === '/properties/name'));
  assert.ok(collector.results.every((r) => r.message === ''));
});

test('verbose annotations are produced', () => {
  const collector = JsonSchemaResultsCollector.create(ResultsLevel.Verbose);
  assert.equal(compile(PERSON).evaluate({ name: 'a' }, collector), true);
  const annotations = collectAnnotations(collector);
  assert.deepEqual(annotations[''].title, { '#': 'Person' });
  assert.deepEqual(annotations['/name'].description, { '#/properties/name': 'The name' });
});

test('verbose output follows the C# row order (worked example 7.2)', () => {
  assert.equal(
    dump(compile(PERSON), { name: 'a' }, ResultsLevel.Verbose),
    [
      "match|/properties/name|/properties/name|/name|The value was expected to match the subschema.",
      'match|/properties/name|/properties/name/description|/name|"The name"',
      "match|/properties/name/minLength|/properties/name/minLength|/name|Expected the length of the value to be greater than or equal to '1'",
      "match|/properties/name/type|/properties/name/type|/name|The value was expected to be of type 'string'",
      'match||||The value was expected to match the subschema.',
      'match||/title||"Person"',
      "match|/required|/required|/name|Required property present 'name'",
      "match|/type|/type||The value was expected to be of type 'object'",
    ].join('\n'),
  );
});

test('matching keywords carry their message in verbose output', () => {
  const schema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    type: ['integer', 'array'],
    uniqueItems: true,
    properties: { n: { type: 'integer' } },
  };
  const rows = (instance) => {
    const c = JsonSchemaResultsCollector.create(ResultsLevel.Verbose);
    const valid = compile(schema).evaluate(instance, c);
    return { valid, rows: c.results };
  };
  let r = rows([1, 2]);
  assert.ok(r.valid);
  assert.ok(r.rows.some((x) => x.isMatch && x.evaluationLocation === '/type' && x.message === 'The value was expected to be of type \'["array", "integer"]\''));
  assert.ok(r.rows.some((x) => x.isMatch && x.evaluationLocation === '/uniqueItems' && x.message.length > 0));
  r = rows([1, 1]);
  assert.ok(!r.valid);
  assert.ok(r.rows.some((x) => !x.isMatch && x.evaluationLocation === '/uniqueItems' && x.message.length > 0));
  const c = JsonSchemaResultsCollector.create(ResultsLevel.Verbose);
  assert.ok(compile({ type: 'integer' }).evaluate(3, c));
  assert.ok(c.results.some((x) => x.isMatch && x.evaluationLocation === '/type' && x.message === "The value was expected to be of type 'integer'"));
});

const REFS = {
  $schema: 'https://json-schema.org/draft/2020-12/schema',
  $defs: {
    fooId: { type: 'integer', minimum: 0 },
    holder: { type: 'object', properties: { fooId: { $ref: '#/$defs/fooId' } } },
    viaRef: { $ref: '#/$defs/fooId' },
  },
};

test('an entry point reports its own schema location', () => {
  assert.equal(
    dump(compile(REFS, { entryPoint: '#/$defs/fooId' }), 'notAnInteger', ResultsLevel.Detailed),
    ['fail|/$defs/fooId|||The value was expected to match the subschema.', "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'"].join('\n'),
  );
});

test('a pure $ref property is elided with $ref in the evaluation path', () => {
  assert.equal(
    dump(compile(REFS, { entryPoint: '#/$defs/holder' }), { fooId: 'notAnInteger' }, ResultsLevel.Detailed),
    [
      'fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the subschema.',
      "fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of type 'integer'",
      'fail|/$defs/holder|||The value was expected to match the subschema.',
    ].join('\n'),
  );
});

test('a pure $ref root reports against its target', () => {
  assert.equal(
    dump(compile(REFS, { entryPoint: '#/$defs/viaRef' }), 'notAnInteger', ResultsLevel.Detailed),
    ['fail|/$defs/fooId|||The value was expected to match the subschema.', "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'"].join('\n'),
  );
});

test('a required failure carries the property name', () => {
  const out = dump(compile({ type: 'object', required: ['name'] }), {}, ResultsLevel.Detailed);
  assert.equal(out, ['fail||||The value was expected to match the subschema.', "fail|/required|/required|/name|Required property not present 'name'"].join('\n'));
});

const EXAMPLE = {
  type: 'object',
  properties: { a: { type: 'string' } },
  required: ['b'],
  anyOf: [{ required: ['a'] }, { minProperties: 5 }],
};

test('detailed output keeps failures only, with messages (worked example 7.1)', () => {
  const expected = [
    'fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.',
    "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
    'fail||||The value was expected to match the subschema.',
    "fail|/required|/required|/b|Required property not present 'b'",
  ];
  assert.equal(dump(compile(EXAMPLE), { a: 1 }, ResultsLevel.Detailed), expected.join('\n'));
  assert.equal(dump(compile(EXAMPLE), { a: 1 }, ResultsLevel.Basic), expected.map((l) => l.slice(0, l.lastIndexOf('|') + 1)).join('\n'));
});

test('verbose output reverses a context\'s own rows after its summary (worked example 7.1)', () => {
  assert.equal(
    dump(compile(EXAMPLE), { a: 1 }, ResultsLevel.Verbose),
    [
      'fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.',
      "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
      'match|/anyOf/0|/anyOf/0||The value was expected to match the subschema.',
      "match|/anyOf/0/required|/anyOf/0/required|/a|Required property present 'a'",
      'fail||||The value was expected to match the subschema.',
      'match|/anyOf|/anyOf||The value matched at least one subschema.',
      "fail|/required|/required|/b|Required property not present 'b'",
      "match|/type|/type||The value was expected to be of type 'object'",
    ].join('\n'),
  );
});

test('not subtrees and boolean schemas (worked example 7.3)', () => {
  assert.equal(
    dump(compile({ not: { type: 'string' } }), 'x', ResultsLevel.Detailed),
    ['fail||||The value was expected to match the subschema.', 'fail|/not|/not||The value matched the subschema in a not composition, which means the evaluation was not a match.'].join('\n'),
  );
  assert.equal(dump(compile(false), 1, ResultsLevel.Detailed), ['fail||||The value was expected to match the subschema.', 'fail||||'].join('\n'));
});

test('a valid instance at detailed level yields only the passing root row', () => {
  assert.equal(dump(compile(PERSON), { name: 'a' }, ResultsLevel.Detailed), 'match||||');
});

test('propertyNames keeps the object location and adds a failure row per name', () => {
  assert.equal(
    dump(compile({ propertyNames: { maxLength: 2 } }), { abc: 1 }, ResultsLevel.Detailed),
    [
      'fail|/propertyNames|/propertyNames||The value was expected to match the subschema.',
      "fail|/propertyNames/maxLength|/propertyNames/maxLength||Expected the length of the value to be less than or equal to '2'",
      'fail||||The value was expected to match the subschema.',
      'fail|/propertyNames|/propertyNames||The property name did not match the schema.',
    ].join('\n'),
  );
});

test('draft 4 exclusive bounds report under exclusiveMaximum with the maximum', () => {
  const v = compile({ $schema: 'http://json-schema.org/draft-04/schema#', maximum: 3, exclusiveMaximum: true });
  assert.equal(dump(v, 3, ResultsLevel.Detailed), ['fail||||The value was expected to match the subschema.', "fail|/exclusiveMaximum|/exclusiveMaximum||The value was expected to be less than '3'"].join('\n'));
});

test('failing anyOf branches are discarded; unevaluatedProperties has no message', () => {
  const v = compile({ anyOf: [{ properties: { a: true } }, { required: ['zz'] }], unevaluatedProperties: false });
  assert.equal(
    dump(v, { a: 1, b: 2 }, ResultsLevel.Detailed),
    [
      'fail|/unevaluatedProperties|/unevaluatedProperties|/b|The value was expected to match the subschema.',
      'fail|/unevaluatedProperties|/unevaluatedProperties|/b|',
      'fail||||The value was expected to match the subschema.',
      'fail|/unevaluatedProperties|/unevaluatedProperties||',
    ].join('\n'),
  );
});

test('a collector accumulates across evaluations', () => {
  const v = compile(PERSON);
  const c = JsonSchemaResultsCollector.create(ResultsLevel.Detailed);
  v.evaluate({ age: 'x' }, c);
  const first = c.resultCount;
  assert.ok(first > 0);
  v.evaluate({ age: 'x' }, c);
  assert.equal(c.resultCount, 2 * first);
});

test('dependencies reports under its own name in every dialect', () => {
  const v = compile({ $schema: 'https://json-schema.org/draft/2020-12/schema', dependencies: { a: ['b'], c: { required: ['d'] } } });
  const out = dump(v, { a: 1, c: 1 }, ResultsLevel.Detailed);
  assert.equal(
    out,
    [
      'fail|/dependencies/c|/dependencies/c||The value was expected to match the subschema.',
      "fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present 'd'",
      'fail||||The value was expected to match the subschema.',
      "fail|/dependencies|/dependencies|/c|The value did match the schema applied because it contained the property 'c'",
      "fail|/dependencies|/dependencies|/b|Required property not present 'b'",
    ].join('\n'),
  );
  const modern = compile({ dependentRequired: { a: ['b'] }, dependentSchemas: { c: { required: ['d'] } } });
  const rows = dump(modern, { a: 1, c: 1 }, ResultsLevel.Detailed);
  assert.ok(rows.includes("fail|/dependentRequired|/dependentRequired|/b|Required property not present 'b'"));
  assert.ok(rows.includes('fail|/dependentSchemas/c|/dependentSchemas/c||'));
});

test('a statically resolved $dynamicRef hop is named $dynamicRef in the evaluation path', () => {
  const v = compile({
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    properties: { p: { $dynamicRef: '#/$defs/n' } },
    $defs: { n: { type: 'integer' } },
  });
  assert.equal(
    dump(v, { p: 'x' }, ResultsLevel.Detailed),
    [
      'fail|/$defs/n|/properties/p/$dynamicRef|/p|The value was expected to match the subschema.',
      "fail|/$defs/n/type|/properties/p/$dynamicRef/type|/p|The value was expected to be of type 'integer'",
      'fail||||The value was expected to match the subschema.',
    ].join('\n'),
  );
});
