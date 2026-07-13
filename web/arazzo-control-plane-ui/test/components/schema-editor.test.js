// Tier 3 — <arazzo-schema-editor>: the typed JSON Schema authoring form (schema-form design §3).
import '../../src/components/schema-editor.js';
import { ok, equal, waitFor, nextEvent, mount } from './helpers.js';

describe('<arazzo-schema-editor>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(schema) {
    el = document.createElement('arazzo-schema-editor');
    mount(el);
    el.value = schema;
    return el;
  }
  const rows = () => el.shadowRoot.querySelectorAll('.node');
  const names = () => [...el.shadowRoot.querySelectorAll('.rowline .name')];

  it('renders a property row per object property, with type + required reflected', () => {
    make({ type: 'object', properties: { amount: { type: 'number' }, note: { type: 'string' } }, required: ['amount'] });
    const ns = names().map((n) => n.value);
    ok(ns.includes('amount') && ns.includes('note'), 'a row per property');
    const amountRow = names().find((n) => n.value === 'amount').closest('.node');
    equal(amountRow.querySelector('select.type').value, 'number');
    equal(amountRow.querySelector('.req').getAttribute('aria-pressed'), 'true', 'amount is required');
  });

  it('renaming a property emits schema-changed preserving position + requiredness', async () => {
    make({ type: 'object', properties: { a: { type: 'string' }, b: { type: 'integer' } }, required: ['a'] });
    const changed = nextEvent(el, 'schema-changed');
    const input = names().find((n) => n.value === 'a');
    input.value = 'alpha';
    input.dispatchEvent(new Event('change'));
    const { schema } = (await changed).detail;
    equal(Object.keys(schema.properties).join(','), 'alpha,b', 'renamed in place');
    ok(schema.required.includes('alpha'), 'requiredness followed the rename');
  });

  it('Add property appends a new row and emits', async () => {
    make({ type: 'object', properties: { a: { type: 'string' } } });
    const changed = nextEvent(el, 'schema-changed');
    el.shadowRoot.querySelector('.add').click();
    const { schema } = (await changed).detail;
    equal(Object.keys(schema.properties).length, 2);
  });

  it('the required toggle round-trips through the parent required array', async () => {
    make({ type: 'object', properties: { a: { type: 'string' } } });
    const changed = nextEvent(el, 'schema-changed');
    const req = el.shadowRoot.querySelector('.req');
    req.click();
    equal((await changed).detail.schema.required.join(','), 'a');
  });

  it('changing a scalar type with no droppable constraints applies without a confirm', async () => {
    make({ type: 'object', properties: { a: { type: 'string' } } });
    const changed = nextEvent(el, 'schema-changed');
    const sel = names().find((n) => n.value === 'a').closest('.node').querySelector('select.type');
    sel.value = 'integer';
    sel.dispatchEvent(new Event('change'));
    equal((await changed).detail.schema.properties.a.type, 'integer');
  });

  it('the Form | JSON toggle round-trips the schema', async () => {
    make({ type: 'object', properties: { a: { type: 'string', 'x-keep': 1 } } });
    el.shadowRoot.querySelector('.t-json').click();
    const ta = el.shadowRoot.querySelector('textarea.json');
    ok(ta && !el.shadowRoot.querySelector('.json').hidden, 'JSON tier shown');
    ok(ta.value.includes('x-keep'), 'raw JSON exposes the unrendered keyword');
    el.shadowRoot.querySelector('.t-form').click();
    ok(!el.shadowRoot.querySelector('.form').hidden, 'back to Form');
    ok(names().some((n) => n.value === 'a'), 're-derives rows');
  });

  it('a combiner node shows a kind select and one card per variant with a label input', () => {
    make({ oneOf: [{ type: 'string', title: 'Text' }, { type: 'integer', title: 'Count' }] });
    const root = el.shadowRoot.querySelector('.node');
    equal(root.querySelector('select.type').value, 'oneOf', 'the type select reflects the combiner kind');
    const cards = el.shadowRoot.querySelectorAll('.variant');
    equal(cards.length, 2, 'a card per variant');
    ok([...cards].map((c) => c.querySelector('.vlabel').value).includes('Text'), 'variant label from title');
    ok(cards[0].querySelector('select.type'), 'each card nests a full node editor');
  });

  it('switching the combiner kind keeps the variants', async () => {
    make({ anyOf: [{ type: 'string' }, { type: 'integer' }] });
    const changed = nextEvent(el, 'schema-changed');
    const sel = el.shadowRoot.querySelector('.node > .rowline select.type');
    sel.value = 'oneOf';
    sel.dispatchEvent(new Event('change'));
    const { schema } = (await changed).detail;
    ok(!schema.anyOf && schema.oneOf?.length === 2, 'anyOf → oneOf, variants intact');
  });

  it('Add variant grows the combiner', async () => {
    make({ oneOf: [{ type: 'string' }] });
    const changed = nextEvent(el, 'schema-changed');
    el.shadowRoot.querySelector('.add').click();
    equal((await changed).detail.schema.oneOf.length, 2);
  });

  it('an advanced node summarizes and never mutates on form edits', () => {
    const original = { type: 'object', properties: { a: { $ref: '#/$defs/X', description: 'ref' } }, $defs: { X: { type: 'string' } } };
    make(structuredClone(original));
    // The root has $defs → advanced. Its summary lists the construct; there is no property row for `a`.
    const adv = el.shadowRoot.querySelector('.advanced');
    ok(adv && /\$defs/.test(adv.textContent), 'advanced summary names the construct');
    ok(adv.querySelector('.iconbtn'), 'offers edit as JSON');
    // No form control rewrote anything.
    equal(JSON.stringify(el.value), JSON.stringify(original), 'the schema is untouched by rendering');
  });

  it('a boolean schema renders a read-only switch, editable only in JSON', () => {
    make(false);
    ok(/reject all/.test(el.shadowRoot.querySelector('.advanced').textContent));
    equal(el.value, false, 'preserved');
  });

  it('ghost rows surface a required name with no property', () => {
    make({ type: 'object', properties: { a: { type: 'string' } }, required: ['a', 'missing'] });
    const ghost = el.shadowRoot.querySelector('.ghost');
    ok(ghost && /missing/.test(ghost.textContent), 'the orphan required name is flagged');
  });

  it('authors enum chips (typed by the row type) and a const in more…', async () => {
    make({ type: 'object', properties: { status: { type: 'string' }, code: { type: 'integer' } } });
    // Open the status row's more… and add two enum values.
    const statusRow = names().find((n) => n.value === 'status').closest('.node');
    statusRow.querySelector('details.more').open = true;
    const enumAdd = [...statusRow.querySelectorAll('.more-body input')].find((i) => i.placeholder?.includes('add value'));
    const addValue = async (v) => { const c = nextEvent(el, 'schema-changed'); enumAdd.value = v; enumAdd.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' })); return (await c).detail.schema; };
    await addValue('pending');
    const schema = await addValue('settled');
    equal(JSON.stringify(schema.properties.status.enum), JSON.stringify(['pending', 'settled']), 'string enum values stay strings');
    // A numeric const on the integer row coerces to a number.
    const codeRow = names().find((n) => n.value === 'code').closest('.node');
    codeRow.querySelector('details.more').open = true;
    const constInput = [...codeRow.querySelectorAll('.more-body label')].find((l) => l.textContent === 'const').nextElementSibling;
    const changed = nextEvent(el, 'schema-changed');
    constInput.value = '7';
    constInput.dispatchEvent(new Event('input'));
    equal((await changed).detail.schema.properties.code.const, 7, 'const typed by the integer row');
  });

  it('references a library schema from the type menu and detaches by inlining the target (§6)', async () => {
    make({ type: 'object', properties: { a: { type: 'string' } } });
    el.library = { Address: { type: 'object', properties: { city: { type: 'string' } } } };
    // Shared library types LEAD the type menu (the encouraged default), not a separate picker.
    const aType = names().find((n) => n.value === 'a').closest('.node').querySelector('select.type');
    ok([...aType.querySelectorAll('optgroup[label="Shared types"] option')].some((o) => o.value === 'ref:Address'), 'shared types lead the type menu');
    const changed = nextEvent(el, 'schema-changed');
    aType.value = 'ref:Address'; aType.dispatchEvent(new Event('change'));
    equal((await changed).detail.schema.properties.a.$ref, '#/components/inputs/Address');
    const refRow = [...el.shadowRoot.querySelectorAll('.node')].find((n) => n.textContent.includes('#/components/inputs/Address'));
    ok(refRow, 'renders a reference row');
    const detach = [...refRow.querySelectorAll('.iconbtn')].find((b) => b.textContent === 'detach');
    const changed2 = nextEvent(el, 'schema-changed');
    detach.click();
    const s2 = (await changed2).detail.schema;
    equal(s2.properties.a.type, 'object', 'detach inlines the target');
    ok('city' in s2.properties.a.properties, 'the inlined copy carries the target content');
  });

  it('New shared type… extracts the node into the library and references it (§6)', async () => {
    make({ type: 'object', properties: { addr: { type: 'object', properties: { city: { type: 'string' } } } } });
    const addrType = names().find((n) => n.value === 'addr').closest('.node').querySelector('select.type');
    ok([...addrType.querySelectorAll('option')].some((o) => o.value === 'new-ref'), 'the New shared type… action is offered');
    const created = nextEvent(el, 'library-create');
    const changed = nextEvent(el, 'schema-changed');
    addrType.value = 'new-ref'; addrType.dispatchEvent(new Event('change'));
    const ev = (await created).detail;
    ok(ev.name && ev.schema.properties.city, 'library-create carries the extracted schema');
    equal((await changed).detail.schema.properties.addr.$ref, `#/components/inputs/${ev.name}`, 'the node now references the new shared type');
  });

  it('a dangling library reference renders a problem row', () => {
    make({ type: 'object', properties: { a: { $ref: '#/components/inputs/Missing' } } });
    el.library = { Present: { type: 'object' } };
    ok([...el.shadowRoot.querySelectorAll('.ghost')].some((g) => /not found/.test(g.textContent)), 'a dangling target is flagged');
  });

  it('the preview renders value-editor from the authored schema (union for oneOf)', async () => {
    make({ type: 'object', properties: { pick: { oneOf: [{ type: 'string' }, { type: 'integer' }] } } });
    el.shadowRoot.querySelector('.preview').open = true;
    el.shadowRoot.querySelector('.preview').dispatchEvent(new Event('toggle'));
    const ve = el.shadowRoot.querySelector('.preview arazzo-value-editor');
    const union = await waitFor(() => ve.shadowRoot.querySelector('.union select'));
    ok(union, 'the authored oneOf previews as a union chooser');
  });
});
