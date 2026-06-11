// Tier 3 — <arazzo-patch-builder>: a strongly-typed form built from a TypeDescriptor.
import '../../src/components/patch-builder.js';
import { ok, equal, waitFor, mount } from './helpers.js';

describe('<arazzo-patch-builder>', () => {
  let el;
  afterEach(() => el?.remove());

  it('renders a control per field type and assembles a typed object', async () => {
    el = document.createElement('arazzo-patch-builder');
    el.descriptor = {
      type: 'object',
      properties: {
        amount: { type: 'number', minimum: 0 },
        status: { type: 'string', enum: ['pending', 'settled', 'failed'] },
        confirmed: { type: 'boolean' },
        paymentId: { type: 'string', format: 'uuid' },
      },
    };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('input[type="number"]'));

    el.shadowRoot.querySelector('input[type="number"]').value = '42';
    const select = el.shadowRoot.querySelector('select');
    select.value = '1'; // 'settled'
    el.shadowRoot.querySelector('input[type="checkbox"]').checked = true;
    el.shadowRoot.querySelector('input[type="text"]').value = 'abc-123';

    const v = el.value;
    equal(v.amount, 42, 'number coerced');
    equal(v.status, 'settled', 'enum resolved to its value');
    equal(v.confirmed, true, 'boolean from the checkbox');
    equal(v.paymentId, 'abc-123', 'string field');
  });

  it('omits blank optional fields', async () => {
    el = document.createElement('arazzo-patch-builder');
    el.descriptor = { type: 'object', properties: { a: { type: 'string' }, b: { type: 'number' } } };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('input'));
    el.shadowRoot.querySelector('input[type="number"]').value = '5';
    const v = el.value;
    equal(v.b, 5);
    ok(!('a' in v), 'blank optional string omitted');
  });

  it('falls back to a raw JSON editor when there is no typed schema', async () => {
    el = document.createElement('arazzo-patch-builder');
    el.descriptor = { type: 'object', properties: {} };
    mount(el);
    const ta = await waitFor(() => el.shadowRoot.querySelector('textarea'));
    ta.value = '{ "manual": true }';
    equal(el.value.manual, true);
  });

  it('throws a friendly error on invalid JSON in the fallback', async () => {
    el = document.createElement('arazzo-patch-builder');
    el.descriptor = null;
    mount(el);
    const ta = await waitFor(() => el.shadowRoot.querySelector('textarea'));
    ta.value = '{ not json';
    let threw = false;
    try { void el.value; } catch { threw = true; }
    ok(threw, 'invalid JSON throws');
  });
});
