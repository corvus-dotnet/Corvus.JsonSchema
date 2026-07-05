// Tier 3 — <arazzo-step-inspector>: binding kinds render and switching prunes (the schema's
// oneOf), parameters/outputs/criteria/actions round-trip, payload JSON is guarded, and the
// localize-defaults affordance copies the inherited layer onto the step.
import '../../src/components/step-inspector.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

const STEP = {
  stepId: 'authorize-payment',
  description: 'Authorize the card.',
  operationId: 'authorizePayment',
  parameters: [{ name: 'orderId', in: 'path', value: '$inputs.orderId' }],
  requestBody: { contentType: 'application/json', payload: { amount: '$inputs.amount' } },
  successCriteria: [{ condition: '$statusCode == 201' }],
  onFailure: [{ name: 'retry-throttled', type: 'retry', retryAfter: 5, retryLimit: 3 }],
  outputs: { authorizationId: '$response.body#/authorizationId' },
};

describe('<arazzo-step-inspector>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(value = STEP, defaults = { successActions: [], failureActions: [] }) {
    el = document.createElement('arazzo-step-inspector');
    el.stepIds = ['validate-order', 'authorize-payment', 'manual-review'];
    el.workflowIds = ['refund'];
    el.workflowDefaults = defaults;
    mount(el);
    el.value = value;
    return el;
  }

  it('renders the operation binding, parameters, body, criteria, actions, outputs', () => {
    make();
    equal(el.shadowRoot.querySelector('.kind').value, 'operationId');
    equal(el.shadowRoot.querySelector('.binding .bval').value, 'authorizePayment');
    equal(el.shadowRoot.querySelector('.prow .pname').value, 'orderId');
    equal(el.shadowRoot.querySelector('.ctype').value, 'application/json');
    const payloadEd = el.shadowRoot.querySelector('arazzo-payload-editor');
    ok(payloadEd.shadowRoot.querySelector('.payload').value.includes('$inputs.amount'), 'JSON mode without a schema');
    equal(el.shadowRoot.querySelector('arazzo-criteria-editor').value.length, 1);
    ok(el.shadowRoot.querySelector('.onfailure summary').textContent.includes('retry-throttled'));
    equal(Object.keys(el.shadowRoot.querySelector('arazzo-outputs-editor').value)[0], 'authorizationId');
  });

  it('switching the binding kind prunes the other binding fields (oneOf)', async () => {
    make();
    const kind = el.shadowRoot.querySelector('.kind');
    const changed = nextEvent(el, 'step-changed');
    kind.value = 'channelPath';
    kind.dispatchEvent(new Event('change', { bubbles: true }));
    const step = (await changed).detail.step;
    equal(step.operationId, undefined, 'operationId pruned');
    equal(step.action, 'receive', 'channel defaults to receive');
    ok(el.shadowRoot.querySelector('.chaction'), 'channel fields rendered');
  });

  it('guards the payload: invalid JSON never emits, valid JSON does', async () => {
    make();
    const payload = el.shadowRoot.querySelector('arazzo-payload-editor').shadowRoot.querySelector('.payload');
    let emitted = 0;
    el.addEventListener('step-changed', () => emitted++);
    payload.value = '{ broken';
    payload.dispatchEvent(new Event('input', { bubbles: true }));
    equal(emitted, 0, 'no emit while unparseable');
    ok(payload.classList.contains('invalid'));
    payload.value = '{ "amount": 12 }';
    payload.dispatchEvent(new Event('input', { bubbles: true }));
    equal(emitted, 1);
    ok(!payload.classList.contains('invalid'));
  });

  it('with an operation schema the payload is a typed form (leaves are expression inputs)', async () => {
    make({ stepId: 'x', operationId: 'op', requestBody: { payload: { amount: '$inputs.amount' } } });
    el.value = { stepId: 'x', operationId: 'op', requestBody: { payload: { amount: '$inputs.amount' } } };
    el.operationRequest = {
      contentType: 'application/json',
      schema: { type: 'object', properties: { orderId: { type: 'string' }, amount: { type: 'number' } } },
    };
    el.value = el.value; // rebuild with the schema in place
    const pe = el.shadowRoot.querySelector('arazzo-payload-editor');
    ok(!pe.shadowRoot.querySelector('.modes').hidden, 'Form|JSON toggle offered');
    const leaves = pe.shadowRoot.querySelectorAll('arazzo-expression-input');
    equal(leaves.length, 2, 'a leaf per schema property');

    // A literal typed into the number field coerces; the expression string is preserved.
    const changed = nextEvent(el, 'step-changed');
    leaves[1].dispatchEvent(new CustomEvent('value-changed', { detail: { value: '42' } }));
    const step = (await changed).detail.step;
    equal(step.requestBody.payload.amount, 42, 'literal coerced to the schema type');
  });

  it('localize copies the inherited defaults onto the step (only when inheriting)', async () => {
    make({ stepId: 'validate-order', operationId: 'validateOrder' },
      { successActions: [], failureActions: [{ name: 'give-up', type: 'end' }] });
    const btn = el.shadowRoot.querySelector('.localize');
    ok(btn, 'localize offered to an inheriting step');
    const changed = nextEvent(el, 'step-changed');
    btn.click();
    const step = (await changed).detail.step;
    equal(step.onFailure[0].name, 'give-up', 'defaults copied locally');
    ok(!el.shadowRoot.querySelector('.localize'), 'affordance gone once localized');
  });

  it('templates criteria from the operation responses (fills empty, appends missing)', async () => {
    make({ stepId: 'x', operationId: 'op' });
    el.operationResponses = ['201', '402', '429'];
    const btn = el.shadowRoot.querySelector('.template');
    ok(btn, 'template affordance offered when responses are known');
    const changed = nextEvent(el, 'step-changed');
    btn.click();
    const step = (await changed).detail.step;
    equal(step.successCriteria[0].condition, '$statusCode == 201');
    equal(step.onFailure.map((a) => a.name).join(','), 'on-402,on-429,unexpected-failure');
    equal(step.onFailure.at(-1).criteria, undefined, 'the fallback is criteria-less');

    // Re-templating never duplicates and never overwrites existing criteria.
    const again = nextEvent(el, 'step-changed');
    el.shadowRoot.querySelector('.template').click();
    const step2 = (await again).detail.step;
    equal(step2.onFailure.length, 3, 'no duplicate failure actions');
  });

  it('builds the request body from the operation schema (only when the payload is empty)', async () => {
    make({ stepId: 'x', operationId: 'op' });
    el.operationRequest = {
      contentType: 'application/json',
      schema: { type: 'object', properties: { orderId: { type: 'string' }, amount: { type: 'number' } } },
    };
    const btn = el.shadowRoot.querySelector('.body-template');
    ok(btn, 'body-skeleton affordance offered');
    const changed = nextEvent(el, 'step-changed');
    btn.click();
    const step = (await changed).detail.step;
    equal(step.requestBody.contentType, 'application/json');
    ok(step.requestBody.payload.orderId === '' && step.requestBody.payload.amount === 0, 'skeleton stubbed');
    ok(!el.shadowRoot.querySelector('.body-template'), 'affordance gone once a payload exists');
  });

  it('templating with an existing catch-all inserts above it and adds no second fallback', async () => {
    make({
      stepId: 'x',
      operationId: 'op',
      onFailure: [{ name: 'give-up-here', type: 'end' }], // an existing criteria-less catch-all
    });
    el.operationResponses = ['200', '404'];
    const changed = nextEvent(el, 'step-changed');
    el.shadowRoot.querySelector('.template').click();
    const step = (await changed).detail.step;
    equal(step.onFailure.map((a) => a.name).join(','), 'on-404,give-up-here',
      'criteria’d template above the catch-all; unexpected-failure skipped');
  });

  it('reorders actions with catch-alls pinned last', async () => {
    make({
      stepId: 'x',
      operationId: 'op',
      onFailure: [
        { name: 'a', type: 'end', criteria: [{ condition: '$statusCode == 401' }] },
        { name: 'b', type: 'end', criteria: [{ condition: '$statusCode == 402' }] },
        { name: 'fallback', type: 'end' },
      ],
    });
    const rows = el.shadowRoot.querySelectorAll('.onfailure details');
    ok(rows[2].querySelector('summary').textContent.includes('catch-all'), 'catch-all labelled');
    ok(!rows[2].querySelector('.move'), 'catch-all has no reorder controls');
    const [up, down] = rows[0].querySelectorAll('.move');
    ok(up.disabled, 'first action cannot move up');
    ok(!down.disabled, 'can move down within the criteria’d section');
    equal(rows[1].querySelectorAll('.move')[1].disabled, true, 'cannot move below the pinned catch-all');

    const changed = nextEvent(el, 'step-changed');
    down.click();
    const step = (await changed).detail.step;
    equal(step.onFailure.map((a) => a.name).join(','), 'b,a,fallback', 'swap emitted');
  });

  it('an action that loses its criteria becomes a catch-all and moves to the end', async () => {
    make({
      stepId: 'x',
      operationId: 'op',
      onFailure: [
        { name: 'was-criteriad', type: 'end', criteria: [{ condition: '$statusCode == 401' }] },
        { name: 'other', type: 'end', criteria: [{ condition: '$statusCode == 402' }] },
      ],
    });
    const firstEditor = el.shadowRoot.querySelector('.onfailure details arazzo-action-editor');
    const criteria = firstEditor.shadowRoot.querySelector('arazzo-criteria-editor');
    const changed = nextEvent(el, 'step-changed');
    criteria.shadowRoot.querySelector('.row .close').click(); // remove its only criterion
    const step = (await changed).detail.step;
    equal(step.onFailure.map((a) => a.name).join(','), 'other,was-criteriad', 'new catch-all pinned last');
  });

  it('adding a parameter emits and focuses the new row', async () => {
    make();
    const changed = nextEvent(el, 'step-changed');
    el.shadowRoot.querySelector('.addp').click();
    equal((await changed).detail.step.parameters.length, 2);
  });
  it('dependsOn shows only ACTUAL dependencies; adds via the select in step order; prunes empty', async () => {
    make();
    // A step with no dependsOn asserts nothing — no pills, an explicit none-hint, an add-select.
    equal(el.shadowRoot.querySelectorAll('.dependson .chip').length, 0, 'no dependency pills without dependsOn');
    ok(el.shadowRoot.querySelector('.dependson .hint').textContent.includes('none'), 'the empty state says so');

    const add = (id) => {
      const select = el.shadowRoot.querySelector('.dependson .add-dep');
      select.value = id;
      select.dispatchEvent(new Event('change'));
    };

    let changed = nextEvent(el, 'step-changed');
    add('manual-review');
    let step = (await changed).detail.step;
    ok(step.dependsOn.includes('manual-review'));

    changed = nextEvent(el, 'step-changed');
    add('validate-order');
    step = (await changed).detail.step;
    equal(step.dependsOn[0], 'validate-order', 'dependencies keep step order, not add order');
    equal(el.shadowRoot.querySelectorAll('.dependson .chip').length, 2, 'both render as pills');

    changed = nextEvent(el, 'step-changed');
    [...el.shadowRoot.querySelectorAll('.dependson .chip')].find((c) => c.textContent.includes('validate-order')).click();
    changed = nextEvent(el, 'step-changed');
    el.shadowRoot.querySelector('.dependson .chip').click();
    step = (await changed).detail.step;
    equal(step.dependsOn, undefined, 'removing the last pill prunes the property');
  });

  it('replacements add, edit, and prune with the request body', async () => {
    make({ stepId: 'x', operationId: 'op' });
    el.shadowRoot.querySelector('.addr').click();
    const target = el.shadowRoot.querySelector('.rrow .rtarget');
    target.value = '/card/number';
    const changed = nextEvent(el, 'step-changed');
    target.dispatchEvent(new Event('input'));
    const step = (await changed).detail.step;
    equal(step.requestBody.replacements[0].target, '/card/number');

    const pruned = nextEvent(el, 'step-changed');
    el.shadowRoot.querySelector('.rrow .rdel').click();
    const after = (await pruned).detail.step;
    equal(after.requestBody, undefined, 'removing the last replacement prunes the empty request body');
  });

});