// Unit tests for the expression tokenizer + completions (src/expression-language.js), driving the
// CM6-shaped StreamParser with a fake stream so no browser is needed.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { expressionStreamParser, completionsFor, EXPRESSION_ROOTS } from '../src/expression-language.js';

class FakeStream {
  constructor(s) { this.string = s; this.pos = 0; }
  eol() { return this.pos >= this.string.length; }
  next() { return this.pos < this.string.length ? this.string[this.pos++] : undefined; }
  peek() { return this.string[this.pos]; }
  eatSpace() {
    const start = this.pos;
    while (/\s/.test(this.string[this.pos] || '')) this.pos++;
    return this.pos > start;
  }
  match(pattern, consume = true) {
    if (typeof pattern === 'string') {
      if (!this.string.startsWith(pattern, this.pos)) return null;
      if (consume) this.pos += pattern.length;
      return pattern;
    }
    const m = pattern.exec(this.string.slice(this.pos));
    if (!m || m.index !== 0) return null;
    if (consume) this.pos += m[0].length;
    return m;
  }
}

/** Drive the parser the way CM6 does: one token at a time, recording [text, style]. */
function tokenizeAll(text) {
  const stream = new FakeStream(text);
  const state = expressionStreamParser.startState();
  const tokens = [];
  while (!stream.eol()) {
    const start = stream.pos;
    const style = expressionStreamParser.token(stream, state);
    assert.ok(stream.pos > start, `parser must always make progress at ${start} in '${text}'`);
    if (style) tokens.push([text.slice(start, stream.pos), style]);
  }
  return tokens;
}

test('simple criterion: root, operator, number', () => {
  assert.deepEqual(tokenizeAll('$statusCode == 402'), [
    ['$statusCode', 'exprRoot'],
    ['==', 'exprOperator'],
    ['402', 'exprNumber'],
  ]);
});

test('expression with a JSON Pointer tail', () => {
  assert.deepEqual(tokenizeAll('$response.body#/receipt/id'), [
    ['$response', 'exprRoot'],
    ['.', 'exprPunct'],
    ['body', 'exprProperty'],
    ['#/receipt/id', 'exprPointer'],
  ]);
});

test('JSONPath filter: bare root, filter bracket, current node, string', () => {
  assert.deepEqual(tokenizeAll('$[?@.status == "authorized"]'), [
    ['$', 'exprRoot'],
    ['[?', 'exprKeyword'],
    ['@', 'exprRoot'],
    ['.', 'exprPunct'],
    ['status', 'exprProperty'],
    ['==', 'exprOperator'],
    ['"authorized"', 'exprString'],
    [']', 'exprBracket'],
  ]);
});

test('step output paths keep hyphenated ids as properties', () => {
  const tokens = tokenizeAll('$steps.authorize-payment.outputs.authorizationId');
  assert.deepEqual(tokens[0], ['$steps', 'exprRoot']);
  assert.deepEqual(tokens[2], ['authorize-payment', 'exprProperty']);
  assert.deepEqual(tokens[4], ['outputs', 'exprProperty']);
  assert.deepEqual(tokens[6], ['authorizationId', 'exprProperty']);
});

test('unknown $root is flagged, known ones are not', () => {
  assert.equal(tokenizeAll('$bogus')[0][1], 'exprBadRoot');
  for (const root of EXPRESSION_ROOTS) {
    assert.equal(tokenizeAll(root)[0][1], 'exprRoot', root);
  }
});

test('literals, logical operators, functions, escapes', () => {
  assert.deepEqual(tokenizeAll('true && !false'), [
    ['true', 'exprAtom'],
    ['&&', 'exprOperator'],
    ['!', 'exprOperator'],
    ['false', 'exprAtom'],
  ]);
  const fn = tokenizeAll('length(@.parts) >= 2');
  assert.deepEqual(fn[0], ['length', 'exprFunction']);
  const esc = tokenizeAll('"a\\"b"');
  assert.deepEqual(esc, [['"a\\"b"', 'exprString']]);
  assert.deepEqual(tokenizeAll('..items')[0], ['..', 'exprKeyword']);
});

const CTX = {
  inputs: ['orderId', 'amount', 'customerEmail'],
  outputs: ['receiptId'],
  steps: {
    'validate-order': { outputs: ['validated'], summary: 'Check the order' },
    'authorize-payment': { outputs: ['authorizationId'] },
  },
};

const complete = (text, ctx = CTX) => completionsFor(text, text.length, ctx);

test('completions: roots at $, filtered by prefix', () => {
  const all = complete('$');
  assert.deepEqual(all.options.map((o) => o.label), EXPRESSION_ROOTS);
  const st = complete('$st');
  assert.deepEqual(st.options.map((o) => o.label), ['$statusCode', '$steps']);
  assert.equal(st.from, 0);
});

test('completions: $inputs.<name> from the schema context', () => {
  const r = complete('$inputs.');
  assert.deepEqual(r.options.map((o) => o.label), ['orderId', 'amount', 'customerEmail']);
  assert.equal(r.from, '$inputs.'.length);
  assert.deepEqual(complete('$inputs.or').options.map((o) => o.label), ['orderId']);
});

test('completions: step ids, then outputs, then output names', () => {
  assert.deepEqual(complete('$steps.').options.map((o) => o.label), ['validate-order', 'authorize-payment']);
  assert.equal(complete('$steps.').options[0].detail, 'Check the order');
  assert.deepEqual(complete('$steps.authorize-payment.').options.map((o) => o.label), ['outputs']);
  assert.deepEqual(
    complete('$steps.authorize-payment.outputs.').options.map((o) => o.label),
    ['authorizationId'],
  );
});

test('completions: $response parts; none for plain words or unknown roots', () => {
  assert.deepEqual(complete('$response.').options.map((o) => o.label), ['body', 'header', 'query', 'path']);
  assert.equal(complete('receipt'), null);
  assert.equal(complete('$bogus.'), null);
});

test('completions: mid-text caret completes the fragment under it', () => {
  const text = '$inputs.or == 1';
  const r = completionsFor(text, '$inputs.or'.length, CTX);
  assert.deepEqual(r.options.map((o) => o.label), ['orderId']);
  assert.equal(r.from, '$inputs.'.length);
});
