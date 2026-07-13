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
    ok('orderId' in (el.shadowRoot.querySelector('arazzo-schema-editor.inputs').value.properties || {}), 'inputs schema loaded into the editor');
    ok(el.shadowRoot.querySelector('.wfailure summary').textContent.includes('give-up'));
    equal(Object.keys(el.shadowRoot.querySelector('arazzo-outputs-editor').value)[0], 'receiptId');
  });

  it('the inputs schema editor drives workflow.inputs; a JSON-tier blank deletes it', () => {
    make();
    const ed = el.shadowRoot.querySelector('arazzo-schema-editor.inputs');
    let last = null;
    el.addEventListener('workflow-changed', (e) => { last = e.detail.workflow; });
    ed.dispatchEvent(new CustomEvent('schema-changed', { detail: { schema: { type: 'object', properties: { x: { type: 'string' } } } }, bubbles: true, composed: true }));
    ok(last?.inputs?.properties?.x, 'a schema-changed updates workflow.inputs');
    ed.dispatchEvent(new CustomEvent('schema-changed', { detail: { schema: undefined }, bubbles: true, composed: true }));
    ok(!('inputs' in last), 'schema:undefined (a JSON-tier blank) deletes workflow.inputs');
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

  it('workflow parameters add, edit, and prune', async () => {
    el = make();
    el.shadowRoot.querySelector('.addwp').click();
    const name = el.shadowRoot.querySelector('.wparams .wpname');
    name.value = 'tenant';
    let changed = nextEvent(el, 'workflow-changed');
    name.dispatchEvent(new Event('input'));
    let wf = (await changed).detail.workflow;
    equal(wf.parameters[0].name, 'tenant');

    const where = el.shadowRoot.querySelector('.wparams .wpin');
    where.value = 'header';
    changed = nextEvent(el, 'workflow-changed');
    where.dispatchEvent(new Event('change'));
    wf = (await changed).detail.workflow;
    equal(wf.parameters[0].in, 'header');

    changed = nextEvent(el, 'workflow-changed');
    el.shadowRoot.querySelector('.wparams .wpdel').click();
    wf = (await changed).detail.workflow;
    equal(wf.parameters, undefined, 'empty prunes the property');
  });

  it('workflow dependsOn shows only actual dependencies and adds via the select', async () => {
    el = make();
    el.workflowIds = ['first', 'second'];
    el.value = el.value; // rebuild with the ids
    equal(el.shadowRoot.querySelectorAll('.wdependson .chip').length, 0, 'nothing asserted without dependsOn');
    ok(el.shadowRoot.querySelector('.wdependson .hint').textContent.includes('none'), 'explicit empty state');

    const select = el.shadowRoot.querySelector('.wdependson .add-dep');
    select.value = 'second';
    const changed = nextEvent(el, 'workflow-changed');
    select.dispatchEvent(new Event('change'));
    const wf = (await changed).detail.workflow;
    equal(wf.dependsOn[0], 'second');
    equal(el.shadowRoot.querySelectorAll('.wdependson .chip').length, 1, 'the real dependency renders as a pill');
  });


  it('the only-filter scopes the form to the named sections (canvas anchors)', async () => {
    el = make();
    el.only = ['inputs'];
    el.value = el.value; // rebuild
    ok(el.shadowRoot.querySelector('.inputs'), 'the inputs editor renders');
    ok(!el.shadowRoot.querySelector('.wouts'), 'no outputs section on the start anchor');
    ok(!el.shadowRoot.querySelector('.summary'), 'no summary fields either');

    // Scoped edits still round-trip the WHOLE workflow object.
    const ed = el.shadowRoot.querySelector('arazzo-schema-editor.inputs');
    const changed = nextEvent(el, 'workflow-changed');
    ed.dispatchEvent(new CustomEvent('schema-changed', { detail: { schema: { type: 'object' } }, bubbles: true, composed: true }));
    const wf = (await changed).detail.workflow;
    equal(wf.inputs.type, 'object');
    ok(wf.workflowId, 'the untouched rest of the workflow survives the scoped edit');

    el.only = ['outputs'];
    ok(el.shadowRoot.querySelector('.wouts'), 'the end anchor shows outputs');
    ok(!el.shadowRoot.querySelector('.inputs'), 'and no inputs');

    el.only = null;
    ok(el.shadowRoot.querySelector('.inputs') && el.shadowRoot.querySelector('.wouts'), 'null restores the full form');
  });
});