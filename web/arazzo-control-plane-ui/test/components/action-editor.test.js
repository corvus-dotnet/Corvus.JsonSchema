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
    equal(el.shadowRoot.querySelector('.step').value, 'step:manual-review');
    equal(el.shadowRoot.querySelector('arazzo-criteria-editor').value.length, 1);
  });

  it('success actions do not offer retry', () => {
    make({ kind: 'success', value: { name: 'done', type: 'end' } });
    const types = [...el.shadowRoot.querySelectorAll('.atype option')].map((o) => o.value);
    equal(types.join(','), 'end,goto');
  });

  it('switching goto → retry keeps the target as retry-from; end prunes it', async () => {
    make();
    const typeSel = el.shadowRoot.querySelector('.atype');
    const changed = nextEvent(el, 'action-changed');
    typeSel.value = 'retry';
    typeSel.dispatchEvent(new Event('change', { bubbles: true }));
    const a1 = (await changed).detail.action;
    equal(a1.type, 'retry');
    equal(a1.stepId, 'manual-review', 'the target survives as the retry-from step (schema allows it)');
    ok(!el.shadowRoot.querySelector('.retry').hidden);

    const ended = nextEvent(el, 'action-changed');
    const typeSel2 = el.shadowRoot.querySelector('.atype');
    typeSel2.value = 'end';
    typeSel2.dispatchEvent(new Event('change', { bubbles: true }));
    equal((await ended).detail.action.stepId, undefined, 'end prunes the target');

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

  it('goto can target another workflow, and switching back prunes workflowId', async () => {
    el = document.createElement('arazzo-action-editor');
    el.kind = 'failure';
    el.stepIds = ['step-a'];
    el.workflowIds = ['refund-order'];
    mount(el);
    el.value = { name: 'jump', type: 'goto', stepId: 'step-a' };

    const select = el.shadowRoot.querySelector('.step');
    let changed = nextEvent(el, 'action-changed');
    select.value = 'workflow:refund-order';
    select.dispatchEvent(new Event('change'));
    let action = (await changed).detail.action;
    equal(action.workflowId, 'refund-order');
    equal(action.stepId, undefined, 'a workflow target replaces the step target');

    changed = nextEvent(el, 'action-changed');
    select.value = 'step:step-a';
    select.dispatchEvent(new Event('change'));
    action = (await changed).detail.action;
    equal(action.stepId, 'step-a');
    equal(action.workflowId, undefined);
  });

  it('retry offers an optional target: blank re-runs this step, a step re-runs from there', async () => {
    el = document.createElement('arazzo-action-editor');
    el.kind = 'failure';
    el.stepIds = ['step-a'];
    mount(el);
    el.value = { name: 'again', type: 'retry', retryAfter: 5, retryLimit: 2, stepId: 'step-a' };

    ok(!el.shadowRoot.querySelector('.target').hidden, 'retry shows the optional target');
    const select = el.shadowRoot.querySelector('.step');
    equal(select.value, 'step:step-a', 'the existing retry-from target renders');

    const changed = nextEvent(el, 'action-changed');
    select.value = '';
    select.dispatchEvent(new Event('change'));
    const action = (await changed).detail.action;
    equal(action.stepId, undefined, 'blank means re-run this step');
    equal(action.retryAfter, 5, 'retry settings survive');
  });

});