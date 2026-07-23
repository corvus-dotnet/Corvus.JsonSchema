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
    payload.dispatchEvent(new CustomEvent('text-changed', { detail: { text: '{ broken' }, bubbles: true, composed: true }));
    equal(emitted, 0, 'no emit while unparseable');
    ok(!payload.shadowRoot.querySelector('.err').hidden, 'the parse problem shows under the editor');
    payload.dispatchEvent(new CustomEvent('text-changed', { detail: { text: '{ "amount": 12 }' }, bubbles: true, composed: true }));
    equal(emitted, 1);
    ok(payload.shadowRoot.querySelector('.err').hidden, 'the problem clears once it parses');
  });

  it('hides the request body section when the operation takes no body (e.g. GET)', async () => {
    // A step bound to a body-less operation, with no request body of its own.
    make({ stepId: 'get-order', operationId: 'getOrder', parameters: [{ name: 'orderId', in: 'path', value: '$inputs.orderId' }] });
    ok(!el.shadowRoot.querySelector('.ctype'), 'no contentType field');
    ok(!el.shadowRoot.querySelector('.payload-slot'), 'no payload slot');
    ok(![...el.shadowRoot.querySelectorAll('h3')].some((h) => h.textContent === 'request body'), 'no request body heading');

    // Binding an operation that DOES take a body brings the section back.
    el.operationRequest = { schema: { type: 'object', properties: { note: { type: 'string' } } } };
    el.value = el.value; // rebuild with the request schema in place
    ok(el.shadowRoot.querySelector('.payload-slot'), 'the section returns once the operation takes a body');
    ok([...el.shadowRoot.querySelectorAll('h3')].some((h) => h.textContent === 'request body'), 'the heading returns');
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
    // The operation takes a body, so the request body section (with its replacements) shows.
    el.operationRequest = { schema: { type: 'object', properties: { card: { type: 'object', properties: { number: { type: 'string' } } } } } };
    el.shadowRoot.querySelector('.addr').click();
    const target = el.shadowRoot.querySelector('.rrow .rtarget');
    // A replacement target is a JSON Pointer, not a runtime expression.
    equal(target.validator('/card/number').valid, true, 'a JSON Pointer is a valid target');
    equal(target.validator('$inputs.x').valid, false, 'a runtime expression is not a valid target');
    const changed = nextEvent(el, 'step-changed');
    target.dispatchEvent(new CustomEvent('value-changed', { detail: { value: '/card/number' }, bubbles: true, composed: true }));
    const step = (await changed).detail.step;
    equal(step.requestBody.replacements[0].target, '/card/number');

    const pruned = nextEvent(el, 'step-changed');
    el.shadowRoot.querySelector('.rrow .rdel').click();
    const after = (await pruned).detail.step;
    equal(after.requestBody, undefined, 'removing the last replacement prunes the empty request body');
  });


  it('an object-schema parameter edits structurally; scalar literals coerce to the declared type', async () => {
    make({
      stepId: 'x', operationId: 'op',
      parameters: [
        { name: 'checks', in: 'query', value: { inventory: true } },
        { name: 'limit', in: 'query', value: 25 },
      ],
    });
    el.operationParameters = [
      { name: 'checks', in: 'query', schema: { type: 'object', properties: { inventory: { type: 'boolean' }, priceTolerance: { type: 'number' } } } },
      { name: 'limit', in: 'query', schema: { type: 'integer' } },
    ];

    const structured = el.shadowRoot.querySelector('.prow arazzo-payload-editor');
    ok(structured, 'the object parameter gets the structured payload editor');
    const fixed = el.shadowRoot.querySelector('.prow .pfixed');
    ok(fixed && fixed.textContent.includes('checks'), 'a DECLARED parameter is a fixed row — the binding names it');
    ok(!el.shadowRoot.querySelector('.prow:first-of-type .pname'), 'no editable name field on declared rows');
    ok(structured.shadowRoot.textContent.includes('priceTolerance'), 'fields come from the declared schema');

    const scalar = [...el.shadowRoot.querySelectorAll('.prow arazzo-expression-input')].at(-1);
    ok(scalar, 'the scalar parameter stays a single expression input');
    const changed = nextEvent(el, 'step-changed');
    scalar.dispatchEvent(new CustomEvent('value-changed', { detail: { value: '50' }, bubbles: true, composed: true }));
    equal((await changed).detail.step.parameters[1].value, 50, 'a literal coerces to the declared integer');

    const exprChanged = nextEvent(el, 'step-changed');
    scalar.dispatchEvent(new CustomEvent('value-changed', { detail: { value: '$inputs.limit' }, bubbles: true, composed: true }));
    equal((await exprChanged).detail.step.parameters[1].value, '$inputs.limit', 'expressions always stay strings');
  });

  it('replacement targets autocomplete from the request schema; values follow the target type', async () => {
    make({
      stepId: 'x', operationId: 'op',
      requestBody: { payload: { card: { number: '' } }, replacements: [{ target: '/card', value: '' }, { target: '/amount', value: '' }] },
    });
    el.operationRequest = {
      contentType: 'application/json',
      schema: { type: 'object', properties: {
        amount: { type: 'number' },
        card: { type: 'object', properties: { number: { type: 'string' }, cvv: { type: 'string' } } },
      } },
    };

    const targetInput = el.shadowRoot.querySelector('.rtarget');
    equal(targetInput.tagName.toLowerCase(), 'arazzo-expression-input', 'the target is the SAME CM6 input as every other editor');
    const options = (targetInput.staticCompletions ?? []).map((o) => o.label);
    ok(options.includes('/card') && options.includes('/card/number') && options.includes('/amount'), 'paths walk the schema');
    ok((targetInput.staticCompletions ?? []).some((o) => o.label === '/amount' && o.detail === 'number'), 'completions carry the schema type');

    const rows = [...el.shadowRoot.querySelectorAll('.rrow')];
    ok(rows[0].querySelector('arazzo-payload-editor'), 'an object target edits structurally, constrained to the sub-schema');
    ok(rows[0].querySelector('arazzo-payload-editor').shadowRoot.textContent.includes('cvv'), 'the sub-schema fields render');

    const scalar = rows[1].querySelector('.rvalue arazzo-expression-input');
    ok(scalar, 'a scalar target stays an expression input');
    const changed = nextEvent(el, 'step-changed');
    scalar.dispatchEvent(new CustomEvent('value-changed', { detail: { value: '19.99' }, bubbles: true, composed: true }));
    equal((await changed).detail.step.requestBody.replacements[1].value, 19.99, 'a literal coerces to the target number');

    const exprChanged = nextEvent(el, 'step-changed');
    scalar.dispatchEvent(new CustomEvent('value-changed', { detail: { value: '$inputs.amount' }, bubbles: true, composed: true }));
    equal((await exprChanged).detail.step.requestBody.replacements[1].value, '$inputs.amount', 'expressions stay strings');
  });

  it('shared actions edit all instances in place; locals promote to shared; localize diverges', async () => {
    make({
      stepId: 'x', operationId: 'op',
      onFailure: [
        { reference: '$components.failureActions.escalate' },
        { name: 'give-up-here', type: 'end' },
      ],
    });
    el.components = { failureActions: { escalate: { name: 'escalate', type: 'goto', stepId: 'review', criteria: [{ condition: '$statusCode == 500' }] } } };

    // ✎ opens the two-choice menu; "for all instances" edits the SHARED action in place.
    const wrap = el.shadowRoot.querySelector('.reusable-wrap');
    ok(wrap.textContent.includes('shared'), 'the row says what it is');
    wrap.querySelector('.edit').click();
    const menu = wrap.querySelector('.edit-menu');
    ok(!menu.hidden, 'the edit menu opens');
    equal(menu.querySelector('.edit-all').textContent, 'for all instances');
    equal(menu.querySelector('.edit-one').textContent, 'just this instance');
    menu.querySelector('.edit-all').click();
    const sharedEditor = wrap.querySelector('arazzo-action-editor');
    ok(sharedEditor, 'the shared action edits in place');
    const componentChanged = nextEvent(el, 'component-changed');
    sharedEditor.dispatchEvent(new CustomEvent('action-changed', {
      detail: { action: { name: 'escalate', type: 'goto', stepId: 'review', criteria: [{ condition: '$statusCode == 503' }] } },
      bubbles: true, composed: true,
    }));
    const evt = await componentChanged;
    equal(evt.detail.kind, 'failureActions');
    equal(evt.detail.action.criteria[0].condition, '$statusCode == 503', 'the edit targets the SHARED action, all references follow');

    // "just this instance" is the localize path (proven by the menu's presence above).

    // Make shared: a local action promotes into the library and becomes a reference.
    const promote = [...el.shadowRoot.querySelectorAll('.promote')].at(-1);
    ok(promote, 'local actions offer Make shared');
    const changed = nextEvent(el, 'step-changed');
    promote.click();
    const step = (await changed).detail.step;
    equal(step.onFailure[1].reference, '$components.failureActions.give-up-here', 'the local entry became a reference');
  });
});
