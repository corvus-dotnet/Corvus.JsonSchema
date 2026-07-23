// Tier 3 — <arazzo-value-editor>: a strongly-typed form built from a TypeDescriptor.
import '../../src/components/value-editor.js';
import { ok, equal, waitFor, mount } from './helpers.js';

describe('<arazzo-value-editor>', () => {
  let el;
  afterEach(() => el?.remove());

  it('renders a control per field type and assembles a typed object', async () => {
    el = document.createElement('arazzo-value-editor');
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
    // Two selects: the status enum, then the OPTIONAL boolean's tri-state (unset/true/false — a
    // checkbox could never return to "no value", #857).
    const [statusSel, confirmedSel] = el.shadowRoot.querySelectorAll('select');
    statusSel.value = '1'; // 'settled'
    confirmedSel.value = 'true';
    el.shadowRoot.querySelector('input[type="text"]').value = 'abc-123';

    const v = el.value;
    equal(v.amount, 42, 'number coerced');
    equal(v.status, 'settled', 'enum resolved to its value');
    equal(v.confirmed, true, 'boolean from the tri-state select');
    equal(v.paymentId, 'abc-123', 'string field');
  });

  it('an optional boolean is tri-state: seeded true, clearable back to unset, and explicit false expressible (#857)', async () => {
    el = document.createElement('arazzo-value-editor');
    el.seed = { flag: true };
    el.descriptor = { type: 'object', properties: { flag: { type: 'boolean' } } };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('select'));
    const sel = el.shadowRoot.querySelector('select');
    equal(sel.value, 'true', 'the seeded value round-trips');
    sel.value = '';
    ok(!('flag' in (el.value ?? {})), 'unset omits the field — the way back to "no default"');
    sel.value = 'false';
    equal(el.value.flag, false, 'explicit false is still expressible');
  });

  it('a REQUIRED boolean keeps the inline checkbox (a definite value has exactly two states)', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { type: 'object', properties: { flag: { type: 'boolean' } }, required: ['flag'] };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('input[type="checkbox"]'));
    el.shadowRoot.querySelector('input[type="checkbox"]').checked = true;
    equal(el.value.flag, true, 'checkbox drives the required boolean');
  });

  it('omits blank optional fields', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { type: 'object', properties: { a: { type: 'string' }, b: { type: 'number' } } };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('input'));
    el.shadowRoot.querySelector('input[type="number"]').value = '5';
    const v = el.value;
    equal(v.b, 5);
    ok(!('a' in v), 'blank optional string omitted');
  });

  it('collapses array-of-object items into an editable summary row', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = {
      type: 'object',
      properties: {
        resources: {
          type: 'array',
          items: {
            type: 'object',
            properties: { name: { type: 'string' }, kind: { type: 'string', enum: ['db', 'bucket'] } },
            required: ['name'],
          },
        },
      },
    };
    mount(el);
    const addBtn = await waitFor(() => [...el.shadowRoot.querySelectorAll('button')].find((b) => b.textContent.includes('Add item')));
    addBtn.click();

    const item = el.shadowRoot.querySelector('.item');
    ok(item, 'a collapsible item row is created');
    const body = item.querySelector('.item-body');
    ok(!body.hidden, 'a new item opens in edit mode');

    body.querySelector('input[type="text"]').value = 'primary';
    body.querySelector('select').value = '0'; // 'db'

    const edit = item.querySelector('button.edit');
    equal(edit.textContent, 'Done', 'toggle reads Done while editing');
    edit.click(); // collapse
    ok(body.hidden, 'editing collapses the body');
    equal(edit.textContent, 'Edit', 'toggle reads Edit when collapsed');
    ok(item.querySelector('.item-summary').textContent.includes('name: primary'), 'summary shows the item');

    const v = el.value;
    equal(v.resources[0].name, 'primary');
    equal(v.resources[0].kind, 'db');
  });

  it('keeps scalar array items as compact inline rows', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { type: 'object', properties: { tags: { type: 'array', items: { type: 'string' } } } };
    mount(el);
    const addBtn = await waitFor(() => [...el.shadowRoot.querySelectorAll('button')].find((b) => b.textContent.includes('Add item')));
    addBtn.click();
    ok(el.shadowRoot.querySelector('.array-row'), 'scalar items use the inline row');
    ok(!el.shadowRoot.querySelector('.item'), 'no collapsible wrapper for scalars');
    el.shadowRoot.querySelector('.array-row input[type="text"]').value = 'prod';
    equal(el.value.tags[0], 'prod');
  });

  it('renders a polymorphic union as a type chooser that reveals the chosen variant', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = {
      type: 'object',
      properties: {
        payment: {
          type: 'union',
          discriminator: 'kind',
          variants: [
            { type: 'object', title: 'Card', properties: { kind: { type: 'string', const: 'card' }, pan: { type: 'string' } } },
            { type: 'object', title: 'Bank', properties: { kind: { type: 'string', const: 'bank' }, iban: { type: 'string' } } },
          ],
        },
      },
    };
    mount(el);
    const select = await waitFor(() => el.shadowRoot.querySelector('.union select'));
    const labels = [...select.options].map((o) => o.textContent);
    ok(labels.includes('Card') && labels.includes('Bank'), 'variant labels come from titles');
    ok(el.value === undefined, 'no variant chosen yields no value');

    select.value = '0'; // Card
    select.dispatchEvent(new Event('change'));
    const slotInput = await waitFor(() => el.shadowRoot.querySelector('.union-slot input[type="text"]:not([readonly])'));
    slotInput.value = '4111';
    const v = el.value;
    equal(v.payment.pan, '4111', 'reads the chosen variant');
    equal(v.payment.kind, 'card', 'the const discriminator is auto-filled');
    ok(!('iban' in v.payment), 'only the chosen variant contributes');
  });

  it('renders a tuple as fixed positional slots', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = {
      type: 'object',
      properties: {
        point: { type: 'array', prefixItems: [{ type: 'number' }, { type: 'number' }] },
      },
    };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.tuple'));
    const nums = el.shadowRoot.querySelectorAll('.tuple input[type="number"]');
    equal(nums.length, 2, 'one slot per prefix item');
    nums[0].value = '3';
    nums[1].value = '4';
    const v = el.value;
    equal(v.point[0], 3);
    equal(v.point[1], 4);
  });

  it('renders a free-form map (additionalProperties) as key/value rows', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = {
      type: 'object',
      properties: {
        labels: { type: 'object', additionalProperties: { type: 'string' } },
      },
    };
    mount(el);
    const addBtn = await waitFor(() => [...el.shadowRoot.querySelectorAll('button')].find((b) => b.textContent.includes('Add entry')));
    addBtn.click();
    const row = el.shadowRoot.querySelector('.map-row');
    ok(row, 'a key/value row is added');
    row.querySelector('.map-key').value = 'env';
    row.querySelector('input:not(.map-key)').value = 'prod';
    equal(el.value.labels.env, 'prod', 'assembles the typed map');
  });

  it('marks required fields and shows constraint hints', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = {
      type: 'object',
      required: ['score'],
      properties: {
        score: { type: 'number', minimum: 0, maximum: 1 },
        name: { type: 'string', maxLength: 80 },
      },
    };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.field'));
    ok(el.shadowRoot.querySelector('.field.required'), 'the required field is marked');
    const hints = [...el.shadowRoot.querySelectorAll('.field .hint')].map((h) => h.textContent);
    ok(hints.some((t) => t.includes('0–1')), 'numeric range hint shown');
    ok(hints.some((t) => t.includes('80 chars')), 'string length hint shown');
  });

  it('assembles best-effort without throwing on a missing required field', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { type: 'object', required: ['score'], properties: { score: { type: 'number' }, name: { type: 'string' } } };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('input[type="text"]'));
    el.shadowRoot.querySelector('input[type="text"]').value = 'x';
    const v = el.value; // must not throw even though required `score` is blank
    equal(v.name, 'x');
    ok(!('score' in v), 'blank required field is simply omitted (server reports conformance)');
  });

  it('shows live, inline per-field validation errors from the validator', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { type: 'object', properties: { score: { type: 'number' }, name: { type: 'string' } } };
    el.validator = async (value) => (value.score > 1
      ? { valid: false, errors: [{ instancePath: '/score', message: 'must be <= 1' }] }
      : { valid: true, errors: [] });
    mount(el);

    const num = await waitFor(() => el.shadowRoot.querySelector('input[type="number"]'));
    num.value = '5';
    num.dispatchEvent(new Event('input', { bubbles: true }));

    const err = await waitFor(() => {
      const e = el.shadowRoot.querySelector('.field .err');
      return e && !e.hidden ? e : null;
    });
    ok(err.textContent.includes('<= 1'), 'the error renders against the score field');

    // Correcting the value clears the inline error.
    num.value = '1';
    num.dispatchEvent(new Event('input', { bubbles: true }));
    await waitFor(() => el.shadowRoot.querySelector('.field .err')?.hidden === true);
  });

  it('falls back to a raw JSON editor when there is no typed schema', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { type: 'object', properties: {} };
    mount(el);
    const ta = await waitFor(() => el.shadowRoot.querySelector('arazzo-text-editor'));
    ta.value = '{ "manual": true }';
    equal(el.value.manual, true);
  });

  it('throws a friendly error on invalid JSON in the fallback', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = null;
    mount(el);
    const ta = await waitFor(() => el.shadowRoot.querySelector('arazzo-text-editor'));
    ta.value = '{ not json';
    let threw = false;
    try { void el.value; } catch { threw = true; }
    ok(threw, 'invalid JSON throws');
  });

  // ── slice C: boundary normalization of RAW combiner schemas (§3.2a) ─────────────────────────────
  it('normalizes a RAW oneOf property into a union type chooser', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { type: 'object', properties: { p: { oneOf: [{ type: 'string' }, { type: 'integer' }] } } };
    mount(el);
    const select = await waitFor(() => el.shadowRoot.querySelector('.union select'));
    ok(select, 'a raw oneOf renders the union select (not the raw-JSON fallback)');
  });

  it('renders a RAW simple allOf as one merged form', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = {
      allOf: [
        { type: 'object', properties: { a: { type: 'string' } }, required: ['a'] },
        { type: 'object', properties: { b: { type: 'integer' } } },
      ],
    };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('input[type="text"]'));
    ok(el.shadowRoot.querySelector('input[type="text"]'), 'property a renders');
    ok(el.shadowRoot.querySelector('input[type="number"]'), 'property b renders (merged from both branches)');
    ok(!el.shadowRoot.querySelector('arazzo-text-editor'), 'not the raw-JSON fallback');
  });

  it('renders {type:object, allOf:[…]} merged rather than swallowing it to raw (guard uses the normalized descriptor)', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { type: 'object', allOf: [{ properties: { x: { type: 'string' } } }, { properties: { y: { type: 'string' } } }] };
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('input[type="text"]'));
    equal(el.shadowRoot.querySelectorAll('input[type="text"]').length, 2, 'x and y both render');
  });

  it('falls back to raw JSON for a NON-simple allOf (never a guessed merge)', async () => {
    el = document.createElement('arazzo-value-editor');
    el.descriptor = { allOf: [{ type: 'object', properties: { id: { type: 'string' } } }, { type: 'string' }] };
    mount(el);
    const ta = await waitFor(() => el.shadowRoot.querySelector('arazzo-text-editor'));
    ok(ta, 'a non-object branch degrades to the raw-JSON editor');
  });
});
