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

  // "New shared type…" (#99) now prompts for the name via an ad-hoc <arazzo-input-dialog> in document.body;
  // find it, set the name, and confirm.
  async function drivePromptDialog(value) {
    let dlg = null;
    for (let i = 0; i < 60 && !dlg; i++) { dlg = document.body.querySelector('arazzo-input-dialog'); if (!dlg) await new Promise((r) => setTimeout(r)); }
    const field = dlg.shadowRoot.querySelector('.in-field');
    if (value != null) field.value = value;
    dlg.shadowRoot.querySelector('.confirm').click();
  }

  it('renders a property row per object property, with type + required reflected', () => {
    make({ type: 'object', properties: { amount: { type: 'number' }, note: { type: 'string' } }, required: ['amount'] });
    const ns = names().map((n) => n.value);
    ok(ns.includes('amount') && ns.includes('note'), 'a row per property');
    const amountRow = names().find((n) => n.value === 'amount').closest('.node');
    equal(amountRow.querySelector('select.type').value, 'number');
    equal(amountRow.querySelector('.req').getAttribute('aria-pressed'), 'true', 'amount is required');
  });

  it('rolls up an object\'s properties section and expands it again', () => {
    make({ type: 'object', properties: { a: { type: 'string' }, b: { type: 'string' } } });
    const toggle = el.shadowRoot.querySelector('.body-toggle');
    ok(toggle, 'the properties section carries a roll-up toggle');
    ok(toggle.textContent.includes('(2)'), 'the toggle shows the property count');
    const members = el.shadowRoot.querySelector('.members');
    ok(!members.hidden, 'expanded by default');
    toggle.click();
    ok(members.hidden, 'clicking the toggle rolls the members up');
    equal(toggle.getAttribute('aria-expanded'), 'false', 'aria reflects the collapsed state');
    toggle.click();
    ok(!members.hidden, 'clicking again expands');
  });

  it('numeric format menus offer the full CTJ set (decimal/half for number, unsigned + 128-bit for integer)', () => {
    make({ type: 'object', properties: { n: { type: 'number' }, i: { type: 'integer' } } });
    const fmtOptions = (name) => {
      const node = names().find((x) => x.value === name).closest('.node');
      return [...node.querySelectorAll('.format-row select option')].map((o) => o.value);
    };
    const num = fmtOptions('n');
    ok(num.includes('decimal') && num.includes('half') && num.includes('double') && num.includes('float'), 'number offers the real formats');
    const int = fmtOptions('i');
    ok(int.includes('uint128') && int.includes('byte') && int.includes('int64') && int.includes('sbyte'), 'integer offers unsigned + 128-bit + 8-bit');
  });

  it('the root form has no fill — it is delimited by its border, like the payload form', () => {
    make({ type: 'object', properties: { a: { type: 'string' } } });
    const root = el.shadowRoot.querySelector('.form > .node');
    const bg = getComputedStyle(root).backgroundColor;
    ok(bg === 'rgba(0, 0, 0, 0)' || bg === 'transparent', `the root form is transparent (got ${bg})`);
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

  it('the Form | JSON toggle round-trips the schema through the syntax-highlighted editor', async () => {
    make({ type: 'object', properties: { a: { type: 'string', 'x-keep': 1 } } });
    const tier = el.shadowRoot.querySelector('.tier');
    tier.shadowRoot.querySelector('button[data-value="json"]').click();
    const ed = el.shadowRoot.querySelector('.json-ed');
    equal(ed.tagName.toLowerCase(), 'arazzo-text-editor', 'the JSON tier is the syntax-highlighted editor, not a bare textarea');
    ok(ed && !el.shadowRoot.querySelector('.json').hidden, 'JSON tier shown');
    ok(ed.value.includes('x-keep'), 'raw JSON exposes the unrendered keyword');
    tier.shadowRoot.querySelector('button[data-value="form"]').click();
    ok(!el.shadowRoot.querySelector('.form').hidden, 'back to Form');
    ok(names().some((n) => n.value === 'a'), 're-derives rows');
  });

  it('object members are an indented list under a "properties" toggle, not boxed cards', () => {
    make({ type: 'object', properties: { a: { type: 'string' }, b: { type: 'integer' } } });
    const toggle = el.shadowRoot.querySelector('.body-toggle');
    ok(toggle && toggle.textContent.includes('properties'), 'a "properties" toggle heads the member list');
    const child = el.shadowRoot.querySelector('.node.child');
    ok(child, 'members render as child rows');
    const cs = getComputedStyle(child);
    equal(cs.borderTopStyle, 'none', 'a member is not a boxed card (no full border)');
    ok(parseFloat(cs.borderLeftWidth) > 0, 'it hangs off the parent accent rail');
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

  it('references an external schema document from the type menu and renders its row (#94)', async () => {
    make({ type: 'object', properties: { addr: { type: 'string' } } });
    el.externalSchemas = { 'acme-types': { id: null, defs: ['Address', 'Money'] }, 'raw-doc': { id: null, defs: [] } };
    const addrType = names().find((n) => n.value === 'addr').closest('.node').querySelector('select.type');
    const externals = [...addrType.querySelectorAll('optgroup[label="External schemas"] option')].map((o) => o.value);
    ok(externals.includes('xref:schemas/acme-types#/$defs/Address'), 'each $defs entry is an option');
    ok(externals.includes('xref:schemas/raw-doc'), 'a doc with no $defs offers its root');
    const changed = nextEvent(el, 'schema-changed');
    addrType.value = 'xref:schemas/acme-types#/$defs/Address'; addrType.dispatchEvent(new Event('change'));
    equal((await changed).detail.schema.properties.addr.$ref, 'schemas/acme-types#/$defs/Address');
    const row = [...el.shadowRoot.querySelectorAll('.node')].find((n) => n.textContent.includes('schemas/acme-types#/$defs/Address'));
    ok(row, 'renders an external reference row');
    ok(!row.textContent.includes('not attached'), 'an attached document is not flagged');
  });

  it('authors the preferred $id form when the external document declares one (#94)', async () => {
    make({ type: 'object', properties: { addr: { type: 'string' } } });
    el.externalSchemas = { 'acme-types': { id: 'https://schemas.acme.example/types', defs: ['Address'] } };
    const addrType = names().find((n) => n.value === 'addr').closest('.node').querySelector('select.type');
    const externals = [...addrType.querySelectorAll('optgroup[label="External schemas"] option')].map((o) => o.value);
    ok(externals.includes('xref:https://schemas.acme.example/types#/$defs/Address'), 'the option carries the root $id, not the virtual path');
    const changed = nextEvent(el, 'schema-changed');
    addrType.value = 'xref:https://schemas.acme.example/types#/$defs/Address'; addrType.dispatchEvent(new Event('change'));
    equal((await changed).detail.schema.properties.addr.$ref, 'https://schemas.acme.example/types#/$defs/Address');
    const row = [...el.shadowRoot.querySelectorAll('.node')].find((n) => n.textContent.includes('https://schemas.acme.example/types'));
    ok(row, 'renders an external reference row for the $id form');
    ok(!row.textContent.includes('not attached'), 'a reference matching an attached $id is not flagged');
  });

  it('flags an external reference whose document is not attached (#94)', async () => {
    make({ type: 'object', properties: { addr: { $ref: 'schemas/missing-doc#/$defs/Address' }, money: { $ref: 'https://schemas.acme.example/unattached#/$defs/Money' } } });
    const row = [...el.shadowRoot.querySelectorAll('.node')].find((n) => n.textContent.includes('schemas/missing-doc'));
    ok(row, 'renders the fallback-form external reference row');
    ok(row.textContent.includes('not attached'), 'flags the missing attachment');
    const idRow = [...el.shadowRoot.querySelectorAll('.node')].find((n) => n.textContent.includes('https://schemas.acme.example/unattached'));
    ok(idRow, 'renders the $id-form external reference row');
    ok(idRow.textContent.includes('not attached'), 'flags the unmatched $id');
  });

  it('Move to shared type… extracts the node into the library and references it (§6, #101)', async () => {
    make({ type: 'object', properties: { addr: { type: 'object', properties: { city: { type: 'string' } } } } });
    const addrType = names().find((n) => n.value === 'addr').closest('.node').querySelector('select.type');
    ok([...addrType.querySelectorAll('option')].some((o) => o.value === 'move-ref'), 'the Move to shared type… action is offered');
    const created = nextEvent(el, 'library-create');
    const changed = nextEvent(el, 'schema-changed');
    addrType.value = 'move-ref'; addrType.dispatchEvent(new Event('change'));
    // #99: creating a shared type prompts for its name via the kit dialog — name it, then confirm.
    await drivePromptDialog('Address');
    const ev = (await created).detail;
    equal(ev.name, 'Address', 'library-create carries the chosen name');
    ok(ev.schema.properties.city, 'the moved type carries the extracted schema');
    equal((await changed).detail.schema.properties.addr.$ref, `#/components/inputs/${ev.name}`, 'the node now references the new shared type');
  });

  it('New shared type… creates a BLANK type, not the node content (#101)', async () => {
    make({ type: 'object', properties: { addr: { type: 'object', properties: { city: { type: 'string' } } } } });
    const addrType = names().find((n) => n.value === 'addr').closest('.node').querySelector('select.type');
    ok([...addrType.querySelectorAll('option')].some((o) => o.value === 'new-ref'), 'the New shared type… action is offered');
    const created = nextEvent(el, 'library-create');
    const changed = nextEvent(el, 'schema-changed');
    addrType.value = 'new-ref'; addrType.dispatchEvent(new Event('change'));
    await drivePromptDialog('Fresh');
    const ev = (await created).detail;
    equal(ev.name, 'Fresh');
    equal(ev.schema.type, 'object', 'a fresh empty object type');
    ok(!ev.schema.properties, 'does not inherit the node content (no city)');
    equal((await changed).detail.schema.properties.addr.$ref, '#/components/inputs/Fresh', 'the node references the new blank type');
  });

  it('format is a constrained dropdown promoted above more…, not free text (#102)', async () => {
    make({ type: 'object', properties: { when: { type: 'string' } } });
    const whenRow = names().find((n) => n.value === 'when').closest('.node');
    // format is a TOP-LEVEL field now (a .format-row), not tucked inside more…
    const control = whenRow.querySelector('.format-row select');
    ok(control, 'format renders at the top level (.format-row)');
    equal(control.tagName, 'SELECT', 'format renders as a select');
    ok([...control.options].some((o) => o.value === 'date-time'), 'offers the recognised string formats');
    ok([...control.options].some((o) => o.value === '' && /none/.test(o.textContent)), 'defaults to (none)');
    // It sits ABOVE the collapsible more… section, not within it.
    const formatRow = whenRow.querySelector(':scope > .format-row');
    const more = whenRow.querySelector(':scope > details.more');
    ok(more.compareDocumentPosition(formatRow) & Node.DOCUMENT_POSITION_PRECEDING, 'format is above more…');
    ok(![...whenRow.querySelectorAll('.more-body label')].some((l) => l.textContent === 'format'), 'not duplicated in more…');
    const changed = nextEvent(el, 'schema-changed');
    control.value = 'uuid'; control.dispatchEvent(new Event('change'));
    equal((await changed).detail.schema.properties.when.format, 'uuid', 'selecting sets the format');
  });

  it("object members render above the more… constraints", async () => {
    make({ type: 'object', properties: { a: { type: 'string' } } });
    const root = el.shadowRoot.querySelector('.form').firstElementChild;
    const add = root.querySelector('.add'); // the '+ Add property' button lives in the object body
    const more = root.querySelector(':scope > details.more');
    ok(add && more, 'both the object body and the more section are present');
    ok(more.compareDocumentPosition(add) & Node.DOCUMENT_POSITION_PRECEDING, 'the object body is above more…');
  });

  it('the typed default sits in the constraint grid, not a standout cluster', async () => {
    make({ type: 'object', properties: { a: { type: 'number' } } });
    const aRow = names().find((n) => n.value === 'a').closest('.node');
    aRow.querySelector('details.more').open = true;
    ok(!aRow.querySelector('.default-cluster'), 'no accent-edged default cluster');
    ok(aRow.querySelector('.more-body arazzo-value-editor.default-ve'), 'default is a normal more-body row');
    ok([...aRow.querySelectorAll('.more-body label')].some((l) => l.textContent === 'default'), 'default has a plain grid label');
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
