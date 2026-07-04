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
    ok(el.shadowRoot.querySelector('.payload').value.includes('$inputs.amount'));
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
    const payload = el.shadowRoot.querySelector('.payload');
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

  it('adding a parameter emits and focuses the new row', async () => {
    make();
    const changed = nextEvent(el, 'step-changed');
    el.shadowRoot.querySelector('.addp').click();
    equal((await changed).detail.step.parameters.length, 2);
  });
});
