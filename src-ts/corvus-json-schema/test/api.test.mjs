import assert from 'node:assert/strict';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { compile, Dialect, generateModule, SchemaCompilationError, SchemaEvaluationDepthError } from '../dist/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const runtimeUrl = pathToFileURL(path.resolve(here, '../dist/runtime.js')).href;
const collectingUrl = pathToFileURL(path.resolve(here, '../dist/collecting.js')).href;

async function importModule(source) {
  return import('data:text/javascript,' + encodeURIComponent(source));
}

test('compile accepts JSON text and parsed schemas', () => {
  const fromText = compile('{"type":"string","minLength":2}');
  const fromValue = compile({ type: 'string', minLength: 2 });
  for (const v of [fromText, fromValue]) {
    assert.equal(v('ab'), true);
    assert.equal(v('a'), false);
    assert.equal(v(12), false);
  }
});

test('generateModule emits a standalone module equivalent to compile', async () => {
  const schema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    type: 'object',
    properties: { id: { type: 'integer', minimum: 1 }, tags: { type: 'array', items: { type: 'string' }, uniqueItems: true } },
    required: ['id'],
    unevaluatedProperties: false,
  };
  const mod = await importModule(generateModule(schema, { runtimeImport: runtimeUrl, collectingImport: collectingUrl }));
  const direct = compile(schema);
  const cases = [
    { id: 1 },
    { id: 1, tags: ['a', 'b'] },
    { id: 1, tags: ['a', 'a'] },
    { id: 0 },
    { id: 1, extra: true },
    {},
    'x',
  ];
  for (const c of cases) {
    assert.equal(mod.default(c), direct(c), JSON.stringify(c));
    assert.equal(mod.validate(c), direct(c), JSON.stringify(c));
  }
});

test('generateModule keeps the dynamic scope', async () => {
  const schema = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://example.com/tree',
    $dynamicAnchor: 'node',
    type: 'object',
    properties: { data: true, children: { type: 'array', items: { $dynamicRef: '#node' } } },
  };
  const strict = {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $id: 'https://example.com/strict-tree',
    $dynamicAnchor: 'node',
    $ref: 'tree',
    unevaluatedProperties: false,
  };
  const resolveDocument = (uri) => (uri === 'https://example.com/tree' ? schema : undefined);
  const mod = await importModule(generateModule(strict, { runtimeImport: runtimeUrl, collectingImport: collectingUrl, resolveDocument }));
  const direct = compile(strict, { resolveDocument });
  const ok = { children: [{ data: 1, children: [] }] };
  const bad = { children: [{ daat: 1 }] };
  assert.equal(direct(ok), true);
  assert.equal(direct(bad), false);
  assert.equal(mod.default(ok), true);
  assert.equal(mod.default(bad), false);
});

test('custom formats are asserted when format assertion is on', () => {
  const v = compile({ type: 'string', format: 'even-length' }, { assertFormat: true, formats: { 'even-length': (s) => s.length % 2 === 0 } });
  assert.equal(v('ab'), true);
  assert.equal(v('abc'), false);
  assert.throws(() => generateModule({ format: 'even-length' }, { assertFormat: true, formats: { 'even-length': () => true } }), SchemaCompilationError);
});

test('format is an annotation by default and asserted on request', () => {
  assert.equal(compile({ format: 'ipv4' })('not an address'), true);
  assert.equal(compile({ format: 'ipv4' }, { assertFormat: true })('not an address'), false);
  assert.equal(compile({ format: 'ipv4' }, { assertFormat: true })('10.0.0.1'), true);
});

test('entryPoint evaluates from a subschema', () => {
  const schema = { $defs: { positive: { type: 'number', exclusiveMinimum: 0 } }, type: 'string' };
  const v = compile(schema, { entryPoint: '#/$defs/positive' });
  assert.equal(v(3), true);
  assert.equal(v(-3), false);
  assert.equal(v('s'), false);
});

test('defaultDialect applies to schemas without $schema', () => {
  const schema = { items: [{ type: 'string' }], additionalItems: false };
  assert.equal(compile(schema, { defaultDialect: Dialect.Draft7 })(['a', 'b']), false);
  // In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies.
  assert.equal(compile(schema)(['a', 'b']), true);
});

test('unresolvable references fail compilation', () => {
  assert.throws(() => compile({ $ref: 'https://example.com/missing.json' }), SchemaCompilationError);
});

test('in-place recursion beyond maxDepth throws', () => {
  const v = compile({ $defs: { loop: { allOf: [{ $ref: '#/$defs/loop' }] } }, $ref: '#/$defs/loop' }, { maxDepth: 16 });
  assert.throws(() => v(1), SchemaEvaluationDepthError);
});

test('numbers are compared exactly for multipleOf', () => {
  const v = compile({ multipleOf: 0.01 });
  assert.equal(v(0.07), true);
  assert.equal(v(19.99), true);
  assert.equal(v(0.075), false);
  assert.equal(compile({ multipleOf: 0.0001 })(0.0075), true);
});

test('property names that shadow Object.prototype are looked up as own properties', () => {
  const v = compile({ required: ['constructor', 'toString'], properties: JSON.parse('{"__proto__":{"type":"string"}}') });
  assert.equal(v({}), false);
  assert.equal(v(JSON.parse('{"constructor":1,"toString":2}')), true);
  assert.equal(v(JSON.parse('{"constructor":1,"toString":2,"__proto__":3}')), false);
  assert.equal(v(JSON.parse('{"constructor":1,"toString":2,"__proto__":"x"}')), true);
});

test('structurally identical subschemas share one generated function', () => {
  const leaf = { type: 'object', properties: { a: { type: 'string' }, b: { type: 'integer' } }, required: ['a'] };
  const v = compile({ type: 'object', properties: { x: leaf, y: structuredClone(leaf), z: structuredClone(leaf) } });
  const functions = v.source.match(/^(?:const \w+ = \()?function /gm) ?? [];
  assert.equal(functions.length, 2);
});

test('pattern fast paths agree with RegExp', async () => {
  const { compileClassSequence } = await import('../dist/codegen.js');
  const patterns = [
    '^[1-5](?:[0-9]{2}|XX)$',
    '^[a-zA-Z0-9._-]+$',
    '^\\d{4}-\\d{2}-\\d{2}$',
    '^[A-Z]',
    '^[a-z]{2,3}$',
    '^(ab|cd)',
    '^[a-c]{2}[0-9]*$',
    '^x-',
    '^\\w+$',
    '^(a|b)c{2}$',
  ];
  const alphabet = ['a', 'b', 'c', 'd', 'x', 'X', 'A', 'Z', '-', '.', '_', '0', '1', '2', '5', '9', ' ', 'é', '😀'];
  let seed = 12345;
  const random = () => ((seed = (Math.imul(seed, 1103515245) + 12345) >>> 0) / 2 ** 32);
  for (const p of patterns) {
    const re = new RegExp(p, 'u');
    const src = compileClassSequence(p);
    const viaCompile = compile({ pattern: p });
    const matcher = src === undefined ? undefined : new Function(`return ${src}`)();
    for (let n = 0; n < 3000; n++) {
      const length = Math.floor(random() * 7);
      let s = '';
      for (let k = 0; k < length; k++) s += alphabet[Math.floor(random() * alphabet.length)];
      if (matcher) assert.equal(matcher(s), re.test(s), `${p} on ${JSON.stringify(s)}`);
      assert.equal(viaCompile(s), re.test(s), `${p} (compiled) on ${JSON.stringify(s)}`);
    }
  }
});

test('standalone modules collect the same results as compile', async () => {
  const { JsonSchemaResultsCollector, ResultsLevel } = await import('../dist/index.js');
  const schema = { type: 'object', properties: { a: { type: 'string', title: 'A' } }, required: ['b'] };
  const mod = await importModule(generateModule(schema, { runtimeImport: runtimeUrl, collectingImport: collectingUrl }));
  const direct = compile(schema);
  for (const level of [ResultsLevel.Basic, ResultsLevel.Detailed, ResultsLevel.Verbose]) {
    const a = JsonSchemaResultsCollector.create(level);
    const b = JsonSchemaResultsCollector.create(level);
    assert.equal(mod.evaluate({ a: 1 }, a), direct.evaluate({ a: 1 }, b));
    assert.deepEqual(a.results, b.results);
  }
  assert.equal(generateModule(schema, { collecting: false }).includes('evaluate'), false);
});

test('one schema object used at two locations keeps both locations', async () => {
  const { JsonSchemaResultsCollector, ResultsLevel } = await import('../dist/index.js');
  const leaf = { type: 'string' };
  const v = compile({ properties: { a: leaf, b: leaf } });
  assert.equal(v({ a: 'x', b: 1 }), false);
  const c = JsonSchemaResultsCollector.create(ResultsLevel.Detailed);
  v.evaluate({ a: 'x', b: 1 }, c);
  assert.ok(c.results.some((r) => r.schemaEvaluationLocation === '/properties/b/type' && r.documentEvaluationLocation === '/b'));
});
