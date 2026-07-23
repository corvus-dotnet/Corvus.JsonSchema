// Tier 3 — <arazzo-payload-editor>: schema-driven structure with expression-capable leaves,
// schema-typed coercion, unknown-key preservation, and the guarded JSON view.
import '../../src/components/payload-editor.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

const SCHEMA = {
  type: 'object',
  properties: {
    orderId: { type: 'string' },
    amount: { type: 'number' },
    capture: { type: 'boolean' },
    card: { type: 'object', properties: { number: { type: 'string' } } },
  },
};

describe('<arazzo-payload-editor>', () => {
  let el;
  afterEach(() => el?.remove());

  function make({ schema = SCHEMA, value } = {}) {
    el = document.createElement('arazzo-payload-editor');
    el.schema = schema;
    mount(el);
    el.value = value ?? { orderId: '$inputs.orderId', amount: '$inputs.amount', extra: 'kept' };
    return el;
  }

  const leafFor = (name) => [...el.shadowRoot.querySelectorAll('.field')]
    .find((f) => f.querySelector('label').textContent.startsWith(name))
    ?.querySelector('arazzo-expression-input');

  it('renders a field per schema property with type chips, nested objects as fieldsets', () => {
    make();
    ok(leafFor('orderId'), 'string leaf');
    ok(leafFor('amount'), 'number leaf');
    ok(el.shadowRoot.querySelector('.fieldset .fname').textContent === 'card', 'nested object fieldset');
    ok(leafFor('amount').value === '$inputs.amount', 'expression value shown verbatim in a number field');
    equal(el.shadowRoot.querySelector('.modes').hidden, false, 'Form|JSON toggle');
  });

  it('seats the Form area in a delimited box, mirroring the JSON text editor', () => {
    make();
    const fields = el.shadowRoot.querySelector('.fields');
    ok(fields, 'the Form root container renders');
    const border = getComputedStyle(fields).borderTopWidth;
    ok(border && border !== '0px', `the form area is delimited by a border (got ${border})`);
  });

  it('coerces literals to the schema type; expressions stay strings; empty unsets', async () => {
    make();
    let payload;
    el.addEventListener('payload-changed', (e) => { payload = e.detail.payload; });

    leafFor('amount').dispatchEvent(new CustomEvent('value-changed', { detail: { value: '42' } }));
    equal(payload.amount, 42, 'number literal coerced');
    leafFor('amount').dispatchEvent(new CustomEvent('value-changed', { detail: { value: '$steps.a.outputs.total' } }));
    equal(payload.amount, '$steps.a.outputs.total', 'expression preserved as a string');
    leafFor('capture').dispatchEvent(new CustomEvent('value-changed', { detail: { value: 'true' } }));
    equal(payload.capture, true, 'boolean literal coerced');
    leafFor('orderId').dispatchEvent(new CustomEvent('value-changed', { detail: { value: '' } }));
    equal('orderId' in payload, false, 'emptied leaf unsets the key');
    equal(payload.extra, 'kept', 'unknown keys the schema does not describe are preserved');
  });

  it('nested leaves write through their path', () => {
    make();
    let payload;
    el.addEventListener('payload-changed', (e) => { payload = e.detail.payload; });
    el.shadowRoot.querySelector('.fieldset arazzo-expression-input')
      .dispatchEvent(new CustomEvent('value-changed', { detail: { value: '$inputs.cardNumber' } }));
    equal(payload.card.number, '$inputs.cardNumber');
  });

  it('JSON mode is a syntax-highlighted, guarded editor; schema-less editors are JSON-only', async () => {
    make();
    el.shadowRoot.querySelector('.modes').shadowRoot.querySelector('button[data-value="json"]').click();
    const ed = el.shadowRoot.querySelector('.payload');
    equal(ed.tagName.toLowerCase(), 'arazzo-text-editor', 'JSON mode uses the syntax-highlighted editor');
    ok(ed.value.includes('"extra": "kept"'), 'JSON view shows everything');

    let emitted = 0;
    let last;
    el.addEventListener('payload-changed', (e) => { emitted++; last = e.detail.payload; });
    // The editor's value/text-changed contract: an unparseable buffer is guarded (never emitted).
    ed.dispatchEvent(new CustomEvent('text-changed', { detail: { text: '{ broken' }, bubbles: true, composed: true }));
    equal(emitted, 0, 'broken JSON never emits');
    // A parseable buffer commits.
    ed.dispatchEvent(new CustomEvent('text-changed', { detail: { text: '{"amount": 5}' }, bubbles: true, composed: true }));
    equal(emitted, 1, 'valid JSON commits');
    equal(last.amount, 5);
    // A blank buffer clears the payload.
    ed.dispatchEvent(new CustomEvent('text-changed', { detail: { text: '   ' }, bubbles: true, composed: true }));
    equal(last, undefined, 'a blank buffer clears the payload');

    el.remove();
    make({ schema: null, value: { a: 1 } });
    ok(el.shadowRoot.querySelector('.modes').hidden, 'no toggle without a schema');
    equal(el.shadowRoot.querySelector('.payload')?.tagName.toLowerCase(), 'arazzo-text-editor', 'JSON editor serves');
  });

  it('marks a leaf invalid while its literal can never satisfy the schema type', async () => {
    make();
    const capture = leafFor('capture');
    capture.dispatchEvent(new CustomEvent('value-changed', { detail: { value: 'tru' } }));
    ok(capture.classList.contains('invalid'), '"tru" on a boolean leaf shows invalid while typing');
    ok(capture.title.includes('neither a boolean'), 'the title says why');

    capture.dispatchEvent(new CustomEvent('value-changed', { detail: { value: 'true' } }));
    ok(!capture.classList.contains('invalid'), 'finishing the word clears it');

    capture.dispatchEvent(new CustomEvent('value-changed', { detail: { value: '$inputs.capture' } }));
    ok(!capture.classList.contains('invalid'), 'expressions are always fine');

    const amount = leafFor('amount');
    amount.dispatchEvent(new CustomEvent('value-changed', { detail: { value: '12x' } }));
    ok(amount.classList.contains('invalid'), 'a non-number literal on a number leaf shows invalid');
  });
});
