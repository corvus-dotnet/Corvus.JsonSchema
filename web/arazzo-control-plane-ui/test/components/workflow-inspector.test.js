// Tier 3 — <arazzo-workflow-inspector>: workflow fields + inputs schema guard + the defaults
// layer (workflow-level action lists) + outputs; focus-section highlights the requested section.
import '../../src/components/workflow-inspector.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

const WORKFLOW = {
  workflowId: 'place-order',
  summary: 'The happy path.',
  inputs: { type: 'object', properties: { orderId: { type: 'string' } } },
  failureActions: [{ name: 'give-up', type: 'end' }],
  steps: [{ stepId: 'validate-order', operationId: 'validateOrder' }],
  outputs: { receiptId: '$steps.capture-payment.outputs.receiptId' },
};

describe('<arazzo-workflow-inspector>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(value = WORKFLOW) {
    el = document.createElement('arazzo-workflow-inspector');
    el.stepIds = ['validate-order'];
    mount(el);
    el.value = value;
    return el;
  }

  it('renders summary, inputs schema, the defaults layer, and outputs', () => {
    make();
    equal(el.shadowRoot.querySelector('.summary').value, 'The happy path.');
    ok(el.shadowRoot.querySelector('.inputs').value.includes('"orderId"'));
    ok(el.shadowRoot.querySelector('.wfailure summary').textContent.includes('give-up'));
    equal(Object.keys(el.shadowRoot.querySelector('arazzo-outputs-editor').value)[0], 'receiptId');
  });

  it('guards the inputs schema: invalid JSON never emits', () => {
    make();
    const inputs = el.shadowRoot.querySelector('.inputs');
    let emitted = 0;
    el.addEventListener('workflow-changed', () => emitted++);
    inputs.value = '{ nope';
    inputs.dispatchEvent(new Event('input', { bubbles: true }));
    equal(emitted, 0);
    ok(inputs.classList.contains('invalid'));
    inputs.value = '{ "type": "object" }';
    inputs.dispatchEvent(new Event('input', { bubbles: true }));
    equal(emitted, 1);
  });

  it('adding a workflow failure action emits the defaults layer', async () => {
    make({ ...WORKFLOW, failureActions: undefined });
    const changed = nextEvent(el, 'workflow-changed');
    el.shadowRoot.querySelector('.wfailure .add-action').click();
    const w = (await changed).detail.workflow;
    equal(w.failureActions.length, 1);
  });

  it('focus-section highlights and can change after render', () => {
    make();
    el.setAttribute('focus-section', 'outputs');
    const focused = el.shadowRoot.querySelector('h3.focused');
    equal(focused.dataset.section, 'outputs');
    el.setAttribute('focus-section', 'inputs');
    equal(el.shadowRoot.querySelector('h3.focused').dataset.section, 'inputs');
  });
});
