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

  it('renders one row per criterion with type and context visibility; no version noise', () => {
    make();
    const rows = el.shadowRoot.querySelectorAll('.row');
    equal(rows.length, 2);
    equal(rows[0].querySelector('.type').value, 'simple');
    ok(rows[0].querySelector('.ctx').hidden, 'simple rows hide the context');
    ok(rows[0].querySelector('.vwrap').hidden, 'simple rows show no version control');
    equal(rows[1].querySelector('.type').value, 'jsonpath');
    ok(!rows[1].querySelector('.ctx').hidden, 'typed rows show the context');
    ok(rows[1].querySelector('.vwrap').hidden, 'jsonpath has one spec version — no dropdown');
    equal(rows[1].querySelector('.cond').getAttribute('placeholder'), '$[?@.status == "authorized"]', 'jsonpath placeholder');
    equal(rows[1].querySelector('.ctx-input').value, '$response.body');
  });

  it('xpath: never offered for new criteria, preserved + flagged when a document carries it', async () => {
    make([{ context: '$response.body', type: 'xpath', condition: '//ok' }]);
    const row = el.shadowRoot.querySelector('.row');
    ok(row.querySelector('.type option[value="xpath"]'), 'existing xpath stays selectable');
    ok(row.querySelector('.type').selectedOptions[0].textContent.includes('unsupported'), 'and is labelled unsupported');
    ok(row.querySelector('.unsupported'), 'warning shown: the runtime does not evaluate xpath');
    ok(!row.querySelector('.vwrap').hidden, 'xpath has a real version choice — labelled select');
    equal(row.querySelectorAll('.version option').length, 3);
    const changed = nextEvent(el, 'criteria-changed');
    const sel = row.querySelector('.version');
    sel.value = 'xpath-20';
    sel.dispatchEvent(new Event('change', { bubbles: true }));
    equal((await changed).detail.criteria[0].type.version, 'xpath-20', 'explicit version emits {type, version}');

    // A fresh simple row offers only the runtime-evaluated types.
    const options = [...el.shadowRoot.querySelectorAll('.row')[0].querySelector('.type').options].map((o) => o.value);
    equal(options.includes('xpath'), true, 'this row IS xpath so it keeps its option');
    make([{ condition: '$statusCode == 200' }]);
    const fresh = [...el.shadowRoot.querySelector('.row .type').options].map((o) => o.value);
    equal(fresh.join(','), 'simple,regex,jsonpath', 'new criteria cannot pick xpath');
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
    const row = el.shadowRoot.querySelectorAll('.row')[0];
    ok(!row.querySelector('.ctx').hidden);
    ok(row.querySelector('.vwrap').hidden, 'still no version dropdown for jsonpath');
    equal(row.querySelector('.cond').getAttribute('placeholder'), '$[?@.status == "authorized"]');
  });

  it('switching type clears a condition still equal to the OLD type’s example, but never authored content (#856)', async () => {
    // A seeded route carries the simple-grammar default; switching to jsonpath must not keep teaching it.
    make([{ condition: '$statusCode == 200' }]);
    const row = () => el.shadowRoot.querySelectorAll('.row')[0];
    const type = row().querySelector('.type');
    type.value = 'jsonpath';
    type.dispatchEvent(new Event('change', { bubbles: true }));
    equal(row().querySelector('.cond').value, '', 'the stale simple example is cleared');
    equal(row().querySelector('.cond').getAttribute('placeholder'), '$[?@.status == "authorized"]', 'the valid jsonpath example shows instead');

    // Authored content is the author's: it survives a type switch untouched.
    el.remove();
    make([{ condition: '$statusCode == 299' }]);
    const t2 = el.shadowRoot.querySelectorAll('.row .type')[0];
    t2.value = 'jsonpath';
    t2.dispatchEvent(new Event('change', { bubbles: true }));
    equal(el.shadowRoot.querySelectorAll('.row')[0].querySelector('.cond').value, '$statusCode == 299', 'a real condition is kept');
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
