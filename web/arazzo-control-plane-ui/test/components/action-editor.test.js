// Tier 3 — <arazzo-action-editor>: the action shape round-trips (name/type/target/retry),
// type switches prune fields the new type does not carry, and embedded criteria propagate.
import '../../src/components/action-editor.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

describe('<arazzo-action-editor>', () => {
  let el;
  afterEach(() => el?.remove());

  function make({ kind = 'failure', value, stepIds = ['manual-review', 'refund-payment'] } = {}) {
    el = document.createElement('arazzo-action-editor');
    el.kind = kind;
    el.stepIds = stepIds;
    mount(el);
    el.value = value ?? {
      name: 'manual-review-on-decline',
      type: 'goto',
      stepId: 'manual-review',
      criteria: [{ condition: '$statusCode == 402' }],
    };
    return el;
  }

  it('renders the goto action: name, type options per kind, target, criteria', () => {
    make();
    equal(el.shadowRoot.querySelector('.name').value, 'manual-review-on-decline');
    const types = [...el.shadowRoot.querySelectorAll('.atype option')].map((o) => o.value);
    equal(types.join(','), 'end,goto,retry', 'failure actions offer retry');
    equal(el.shadowRoot.querySelector('.atype').value, 'goto');
    ok(!el.shadowRoot.querySelector('.target').hidden);
    equal(el.shadowRoot.querySelector('.step').value, 'manual-review');
    equal(el.shadowRoot.querySelector('arazzo-criteria-editor').value.length, 1);
  });

  it('success actions do not offer retry', () => {
    make({ kind: 'success', value: { name: 'done', type: 'end' } });
    const types = [...el.shadowRoot.querySelectorAll('.atype option')].map((o) => o.value);
    equal(types.join(','), 'end,goto');
  });

  it('switching goto → retry prunes the target and emits retry fields', async () => {
    make();
    const typeSel = el.shadowRoot.querySelector('.atype');
    const changed = nextEvent(el, 'action-changed');
    typeSel.value = 'retry';
    typeSel.dispatchEvent(new Event('change', { bubbles: true }));
    const a1 = (await changed).detail.action;
    equal(a1.type, 'retry');
    equal(a1.stepId, undefined, 'goto target pruned');
    ok(!el.shadowRoot.querySelector('.retry').hidden);

    const after = nextEvent(el, 'action-changed');
    const rafter = el.shadowRoot.querySelector('.rafter');
    rafter.value = '5';
    rafter.dispatchEvent(new Event('input', { bubbles: true }));
    equal((await after).detail.action.retryAfter, 5);
  });

  it('renaming emits, and embedded criteria edits land on the action', async () => {
    make();
    const name = el.shadowRoot.querySelector('.name');
    const renamed = nextEvent(el, 'action-changed');
    name.value = 'decline-review';
    name.dispatchEvent(new Event('input', { bubbles: true }));
    equal((await renamed).detail.action.name, 'decline-review');

    const criteria = el.shadowRoot.querySelector('arazzo-criteria-editor');
    const changed = nextEvent(el, 'action-changed');
    criteria.shadowRoot.querySelector('.add').click(); // adds an empty criterion row
    const a = (await changed).detail.action;
    equal(a.criteria.length, 2, 'criteria list propagated onto the action');
  });
});
