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

  it('JSON mode is full fidelity and guarded; schema-less editors are JSON-only', async () => {
    make();
    el.shadowRoot.querySelector('.m-json').click();
    const ta = el.shadowRoot.querySelector('.payload');
    ok(ta.value.includes('"extra": "kept"'), 'JSON view shows everything');
    let emitted = 0;
    el.addEventListener('payload-changed', () => emitted++);
    ta.value = '{ broken';
    ta.dispatchEvent(new Event('input', { bubbles: true }));
    equal(emitted, 0, 'broken JSON never emits');
    ok(ta.classList.contains('invalid'));

    el.remove();
    make({ schema: null, value: { a: 1 } });
    ok(el.shadowRoot.querySelector('.modes').hidden, 'no toggle without a schema');
    ok(el.shadowRoot.querySelector('.payload'), 'JSON editor serves');
  });
});
