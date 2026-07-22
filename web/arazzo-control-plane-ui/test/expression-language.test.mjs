// Unit tests for the expression tokenizer + completions (src/expression-language.js), driving the
// CM6-shaped StreamParser with a fake stream so no browser is needed.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { expressionStreamParser, completionsFor, resolveAgainstFrame, EXPRESSION_ROOTS, stepCompletionContext } from '../src/expression-language.js';

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

test('completions: $response exposes body + header, each with its grammar continuation', () => {
  const r = complete('$response.');
  // A response has only a body and headers (no query / path — those are request surfaces).
  assert.deepEqual(r.options.map((o) => o.label), ['body', 'header']);
  // The body is addressed by a JSON Pointer and headers by name, so the completion applies the right
  // continuation for each: the author never has to guess the separator.
  assert.equal(r.options.find((o) => o.label === 'body').apply, 'body#/');
  assert.equal(r.options.find((o) => o.label === 'header').apply, 'header.');
  assert.equal(complete('receipt'), null);
  assert.equal(complete('$bogus.'), null);
});

test('completions: $request adds query + path; $message exposes payload + header', () => {
  assert.deepEqual(complete('$request.').options.map((o) => o.label), ['body', 'header', 'query', 'path']);
  assert.deepEqual(complete('$request.').options.map((o) => o.apply), ['body#/', 'header.', 'query.', 'path.']);
  assert.deepEqual(complete('$message.').options.map((o) => o.label), ['payload', 'header']);
  assert.deepEqual(complete('$message.').options.map((o) => o.apply), ['payload#/', 'header.']);
});

test('completions: mid-text caret completes the fragment under it', () => {
  const text = '$inputs.or == 1';
  const r = completionsFor(text, '$inputs.or'.length, CTX);
  assert.deepEqual(r.options.map((o) => o.label), ['orderId']);
  assert.equal(r.from, '$inputs.'.length);
});

const SCHEMA_CTX = {
  inputs: {
    type: 'object',
    properties: {
      orderId: { type: 'string' },
      customer: { type: 'object', properties: { email: { type: 'string', format: 'email' } } },
    },
  },
  response: {
    body: {
      type: 'object',
      properties: {
        status: { type: 'string' },
        authorizationId: { type: 'string' },
        card: { type: 'object', properties: { last4: { type: 'string' }, brand: { type: 'string' } } },
        attempts: { type: 'array', items: { type: 'object', properties: { at: { type: 'string', format: 'date-time' } } } },
      },
    },
  },
  message: { payload: { type: 'object', properties: { confirmed: { type: 'boolean' } } } },
  steps: {
    'authorize-payment': {
      outputSchemas: { receipt: { type: 'object', properties: { id: { type: 'string' }, total: { type: 'number' } } } },
    },
  },
};

const scomplete = (text) => completionsFor(text, text.length, SCHEMA_CTX);

test('pointer completions: response body top-level properties with type details', () => {
  const r = scomplete('$response.body#/');
  assert.deepEqual(r.options.map((o) => o.label), ['status', 'authorizationId', 'card', 'attempts']);
  assert.equal(r.options.find((o) => o.label === 'card').detail, 'object');
});

test('pointer completions: nested descent, prefix filter, and from position', () => {
  const r = scomplete('$response.body#/card/la');
  assert.deepEqual(r.options.map((o) => o.label), ['last4']);
  assert.equal(r.from, '$response.body#/card/'.length);
});

test('pointer completions: array items by index, unknown paths yield null', () => {
  assert.deepEqual(scomplete('$response.body#/attempts/0/').options.map((o) => o.label), ['at']);
  assert.equal(scomplete('$response.body#/nope/'), null);
  assert.equal(scomplete('$response.body#x'), null, 'a pointer must start with /');
});

test('pointer completions: message payload and step output schemas', () => {
  assert.deepEqual(scomplete('$message.payload#/').options.map((o) => o.label), ['confirmed']);
  assert.deepEqual(
    scomplete('$steps.authorize-payment.outputs.receipt#/').options.map((o) => o.label),
    ['id', 'total'],
  );
});

test('a dotted tail into the body / payload is refused; descent is a JSON Pointer', () => {
  // `$response.body.status` is not a valid Arazzo expression — the runtime reads it as a literal, not a
  // descent into the body — so the completion offers nothing after the dot rather than leading the author
  // into an invalid expression. Property completion happens on the `#/pointer` form (tested above).
  assert.equal(scomplete('$response.body.'), null);
  assert.equal(scomplete('$response.body.card.'), null);
  assert.equal(scomplete('$response.body.attempts.0.'), null);
  assert.equal(scomplete('$message.payload.'), null);
  // A header surface descends by name, but with no header schema in the context there is nothing to offer.
  assert.equal(scomplete('$response.header.'), null);
});

test('inputs as a JSON Schema: names + details at the first level, pointer descent below', () => {
  const first = scomplete('$inputs.');
  assert.deepEqual(first.options.map((o) => o.label), ['orderId', 'customer']);
  assert.equal(first.options.find((o) => o.label === 'customer').detail, 'object');
  assert.deepEqual(scomplete('$inputs.customer#/').options.map((o) => o.label), ['email']);
  assert.equal(scomplete('$inputs.customer#/').options[0].detail, 'string (email)');
});

test('step output names fall back to the outputSchemas keys when no list is given', () => {
  const r = scomplete('$steps.authorize-payment.outputs.');
  assert.deepEqual(r.options.map((o) => o.label), ['receipt']);
});

// ---- stepCompletionContext: a step's OWN operation drives its response/request completions ---------

const props = (ctx, expr) => {
  const r = completionsFor(expr, expr.length, ctx);
  return r ? r.options.map((o) => o.label) : null;
};

test('stepCompletionContext: the step gets ITS operation body, and the base body is dropped', () => {
  // The base carries a stale response.body (a workflow-wide stand-in); it must NOT leak into a step.
  const base = { inputs: { type: 'object', properties: { orderId: { type: 'string' } } }, response: { body: { type: 'object', properties: { STALE: {} } } } };
  const op = {
    responses: { 200: { schema: { type: 'object', properties: { validated: { type: 'boolean' } } } }, 402: { schema: {} } },
    request: { schema: { type: 'object', properties: { amount: { type: 'number' } } } },
  };
  const ctx = stepCompletionContext(base, op, { operationId: 'op' });
  assert.deepEqual(props(ctx, '$inputs.'), ['orderId'], 'the workflow-wide roots carry through');
  assert.deepEqual(props(ctx, '$response.body#/'), ['validated'], "THIS step's response body, not the stale base");
  assert.deepEqual(props(ctx, '$request.body#/'), ['amount']);
});

test('stepCompletionContext: prefers the 2xx response, else the first documented status', () => {
  const twoXX = stepCompletionContext({}, { responses: { 404: { schema: { type: 'object', properties: { err: {} } } }, 201: { schema: { type: 'object', properties: { id: {} } } } } }, {});
  assert.deepEqual(props(twoXX, '$response.body#/'), ['id']);
  const noSuccess = stepCompletionContext({}, { responses: { 404: { schema: { type: 'object', properties: { err: {} } } } } }, {});
  assert.deepEqual(props(noSuccess, '$response.body#/'), ['err']);
});

test('stepCompletionContext: a channel step surfaces its payload, and has no $response body', () => {
  const ctx = stepCompletionContext({}, { request: { schema: { type: 'object', properties: { confirmed: { type: 'boolean' } } } } }, { channelPath: '/channels/x' });
  assert.deepEqual(props(ctx, '$message.payload#/'), ['confirmed']);
  assert.equal(props(ctx, '$response.body#/'), null, 'a message step exposes no response body');
});

test('stepCompletionContext: no operation drops the per-step surfaces (base response never leaks)', () => {
  const ctx = stepCompletionContext({ inputs: { type: 'object', properties: { a: {} } }, response: { body: { type: 'object', properties: { x: {} } } } }, undefined, {});
  assert.deepEqual(props(ctx, '$inputs.'), ['a']);
  assert.equal(ctx.response, undefined);
  assert.equal(props(ctx, '$response.body#/'), null);
});

// ---- resolveAgainstFrame: the paused-context expression console (§3.3) ----------------------------

const FRAME = {
  inputs: { petId: 'p-1', customer: { email: 'a@b.c' } },
  outputs: { adopted: true },
  steps: { 'get-pet': { outputs: { pet: { name: 'Fido', tags: ['calm', 'small'] } } } },
  current: { statusCode: 200, responseBody: { name: 'Fido', status: 'available' } },
};

test('resolves inputs, outputs, step outputs, and the current exchange from a frame', () => {
  assert.deepEqual(resolveAgainstFrame('$inputs.petId', FRAME), { found: true, value: 'p-1' });
  assert.deepEqual(resolveAgainstFrame('$inputs.customer.email', FRAME), { found: true, value: 'a@b.c' });
  assert.deepEqual(resolveAgainstFrame('$outputs.adopted', FRAME), { found: true, value: true });
  assert.deepEqual(resolveAgainstFrame('$steps.get-pet.outputs.pet.name', FRAME), { found: true, value: 'Fido' });
  assert.deepEqual(resolveAgainstFrame('$statusCode', FRAME), { found: true, value: 200 });
  assert.deepEqual(resolveAgainstFrame('$response.body', FRAME).value.status, 'available');
});

test('descends a #/json/pointer suffix, RFC 6901 escapes and array indexes included', () => {
  assert.deepEqual(resolveAgainstFrame('$steps.get-pet.outputs.pet#/tags/1', FRAME), { found: true, value: 'small' });
  assert.deepEqual(resolveAgainstFrame('$response.body#/name', FRAME), { found: true, value: 'Fido' });
  assert.equal(resolveAgainstFrame('$response.body#/missing', FRAME).found, false);
});

test('unexecuted steps and unrecorded roots explain themselves instead of resolving', () => {
  const notRun = resolveAgainstFrame('$steps.confirm.outputs.x', FRAME);
  assert.equal(notRun.found, false);
  assert.match(notRun.reason, /has not executed/);
  assert.equal(resolveAgainstFrame('$url', FRAME).found, false);
  assert.equal(resolveAgainstFrame('nonsense', FRAME).found, false);
});
