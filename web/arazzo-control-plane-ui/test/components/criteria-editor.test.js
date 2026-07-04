// Tier 3 — <arazzo-criteria-editor>: rows reflect the criterion shapes (type/version/context),
// edits mutate and emit without rebuilding (focus survives typing), add/remove round-trip.
import '../../src/components/criteria-editor.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

const CRITERIA = [
  { condition: '$statusCode == 201' },
  { context: '$response.body', type: 'jsonpath', condition: '$[?@.status == "authorized"]' },
];

describe('<arazzo-criteria-editor>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(value = CRITERIA) {
    el = document.createElement('arazzo-criteria-editor');
    mount(el);
    el.value = value;
    return el;
  }

  it('renders one row per criterion with type, version, and context visibility', () => {
    make();
    const rows = el.shadowRoot.querySelectorAll('.row');
    equal(rows.length, 2);
    equal(rows[0].querySelector('.type').value, 'simple');
    ok(rows[0].querySelector('.ctx').hidden, 'simple rows hide the context');
    ok(rows[0].querySelector('.version').hidden, 'simple rows hide the version');
    equal(rows[1].querySelector('.type').value, 'jsonpath');
    ok(!rows[1].querySelector('.ctx').hidden, 'typed rows show the context');
    equal(rows[1].querySelector('.version').value, 'draft-goessner-dispatch-jsonpath-00');
    equal(rows[1].querySelector('.ctx-input').value, '$response.body');
  });

  it('editing a condition emits criteria-changed without rebuilding the row', async () => {
    make();
    const cond = el.shadowRoot.querySelectorAll('.row .cond')[0];
    await waitFor(() => !cond.usingFallback);
    const rowBefore = el.shadowRoot.querySelectorAll('.row')[0];
    const changed = nextEvent(el, 'criteria-changed');
    cond.value = '$statusCode == 200';
    const e = await changed;
    equal(e.detail.criteria[0].condition, '$statusCode == 200');
    equal(el.shadowRoot.querySelectorAll('.row')[0], rowBefore, 'row not rebuilt mid-edit');
  });

  it('switching simple → jsonpath shows version + context and emits the typed shape', async () => {
    make();
    const type = el.shadowRoot.querySelectorAll('.row .type')[0];
    const changed = nextEvent(el, 'criteria-changed');
    type.value = 'jsonpath';
    type.dispatchEvent(new Event('change', { bubbles: true }));
    const e = await changed;
    equal(e.detail.criteria[0].type, 'jsonpath');
    ok(!el.shadowRoot.querySelectorAll('.row')[0].querySelector('.ctx').hidden);
  });

  it('add and remove rows round-trip', async () => {
    make([]);
    ok(el.shadowRoot.querySelector('.empty').textContent.includes('always'), 'empty state names the semantics');
    const added = nextEvent(el, 'criteria-changed');
    el.shadowRoot.querySelector('.add').click();
    equal((await added).detail.criteria.length, 1);
    const removed = nextEvent(el, 'criteria-changed');
    el.shadowRoot.querySelector('.row .close').click();
    equal((await removed).detail.criteria.length, 0);
    ok(el.shadowRoot.querySelector('.empty'), 'back to the empty state');
  });
});
