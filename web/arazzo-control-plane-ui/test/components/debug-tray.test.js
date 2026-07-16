// Tier 3 — <arazzo-debug-tray>: renders a SimulationTrace, cursor movement is client-side
// time-travel, and frameAt projects the design surface's debugState shape.
import '../../src/components/debug-tray.js';
import { ok, equal, nextEvent, mount } from './helpers.js';
import { projectWorkflow } from '../../src/workflow-graph.js';
import { designerFixture } from '../../demo/designer-fixture.js';

// The lit-edge ids frameAt mints MUST be a subset of the ids the projection mints, or the taken
// edge never lights on the surface (design §10 — the pre-existing id-drift bugs slice 0 fixes).
const projectedEdgeIds = (doc, workflowId) => new Set(projectWorkflow(doc, workflowId).edges.map((e) => e.id));

const TRACE = {
  outcome: 'completed',
  stepsExecuted: 2,
  outputs: { name: 'Fido' },
  steps: [
    {
      stepId: 'get-pet', status: 'completed', attempt: 0,
      requests: [{ method: 'get', path: '/pets/42', status: 200, requestBody: { include: ['photos'] }, responseBody: { name: 'Fido' } }],
      successCriteria: [{ condition: '$statusCode == 200', satisfied: true }],
      actionTaken: { type: 'fallThrough' },
      outputs: { petName: 'Fido' },
    },
    {
      stepId: 'adopt-pet', status: 'completed', attempt: 0,
      requests: [{ method: 'post', path: '/pets/42/adopt', status: 200 }],
      actionTaken: { type: 'end', name: 'done' },
    },
  ],
};

describe('<arazzo-debug-tray>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(trace = TRACE, configure) {
    el = document.createElement('arazzo-debug-tray');
    mount(el);
    configure?.(el); // schema feeds land before the trace renders
    el.trace = trace;
    return el;
  }

  it('renders the trace with the cursor at the end and the outcome chip', () => {
    make();
    equal(el.cursor, 2, 'assigning a trace lands on the final state');
    ok(el.shadowRoot.textContent.includes('✓ completed'));
    equal(el.shadowRoot.querySelectorAll('.step').length, 2);
    ok(el.shadowRoot.textContent.includes('workflow outputs'), 'final frame shows the workflow outputs');
  });

  it('the context pane shows what the step SENT — the resolved request body', () => {
    make();
    el.cursor = 1;
    ok(el.shadowRoot.textContent.includes('as sent'), 'the exchanges section says what it shows');
    ok(el.shadowRoot.querySelector('td.sent'), 'the sent-body line renders');
    ok(el.shadowRoot.textContent.includes('photos'), 'with the resolved payload');
  });

  it('scrubbing is pure cursor movement with context per frame', async () => {
    make();
    const changed = nextEvent(el, 'cursor-changed');
    el.shadowRoot.querySelector('.step[data-index="0"]').click();
    equal((await changed).detail.index, 1);
    ok(el.shadowRoot.textContent.includes('truth table'), 'the inspected step shows its criteria');
    ok(el.shadowRoot.textContent.includes('$statusCode == 200'));
    ok(el.shadowRoot.textContent.includes('petName'), 'and its outputs');
  });

  it('frameAt projects the surface debugState: done steps, taken edges, active step', () => {
    make();
    const mid = el.frameAt(1);
    equal(mid.active, 'adopt-pet', 'frame k is "about to run step k"');
    equal(mid.steps['get-pet'], 'done-success');
    ok(mid.edges.includes('seq:#start') && mid.edges.includes('seq:get-pet'), 'fall-through lights the sequence edge');

    const final = el.frameAt(2);
    equal(final.active, null);
    ok(final.edges.includes('end:adopt-pet:done:success'), 'the end action lights its edge');
    equal(final.steps['#end'], 'done-success');
  });

  it('a failed criterion renders as failure and colours the taken edge', () => {
    make({
      outcome: 'completed', stepsExecuted: 1, steps: [{
        stepId: 'a', status: 'completed', attempt: 0,
        successCriteria: [{ condition: '$statusCode == 200', satisfied: false }],
        actionTaken: { type: 'goto', name: 'recover', target: 'b' },
      }],
    });
    const frame = el.frameAt(1);
    equal(frame.steps.a, 'done-failure');
    ok(frame.edges.includes('goto:a:recover:failure'));
  });

  // ── slice 0 regressions: frameAt ids must be a subset of the projected edge ids (design §10) ──

  it('an UNNAMED goto lights its projected edge (id-drift bug 1)', () => {
    const doc = structuredClone(designerFixture);
    doc.workflows[0].steps[0].onSuccess = [{ type: 'goto', stepId: 'authorize-payment' }]; // no name
    const projected = projectedEdgeIds(doc, 'place-order');
    make({
      outcome: 'completed', stepsExecuted: 1, steps: [{
        stepId: 'validate-order', status: 'completed', attempt: 0,
        successCriteria: [{ condition: '$statusCode == 200', satisfied: true }],
        actionTaken: { type: 'goto', target: 'authorize-payment' }, // no name — projection falls back to the target id
      }],
    });
    const frame = el.frameAt(1);
    for (const id of frame.edges) ok(projected.has(id), `frame edge ${id} is not in the projected graph`);
    ok(frame.edges.includes('goto:validate-order:authorize-payment:success'), 'the unnamed goto lights the projected edge');
  });

  it('a cross-workflow (exit-chip) goto lights its projected edge (id-drift bug 2)', () => {
    const doc = structuredClone(designerFixture);
    doc.workflows[0].steps[0].onSuccess = [{ name: 'handoff', type: 'goto', workflowId: 'order-with-compensation' }];
    const projected = projectedEdgeIds(doc, 'place-order');
    make({
      outcome: 'completed', stepsExecuted: 1, steps: [{
        stepId: 'validate-order', status: 'completed', attempt: 0,
        successCriteria: [{ condition: '$statusCode == 200', satisfied: true }],
        actionTaken: { type: 'goto', name: 'handoff', target: 'order-with-compensation' },
      }],
    });
    const frame = el.frameAt(1);
    for (const id of frame.edges) ok(projected.has(id), `frame edge ${id} is not in the projected graph`);
    ok(frame.edges.includes('goto:validate-order:handoff:success'), 'the exit-chip goto lights the projected edge');
  });

  it('a faulted step with no failed criterion lights its onFailure edge (id-drift bug 3)', () => {
    const doc = structuredClone(designerFixture);
    doc.workflows[0].steps[0].onFailure = [{ name: 'bail', type: 'end' }];
    const projected = projectedEdgeIds(doc, 'place-order');
    make({
      outcome: 'faulted', stepsExecuted: 1, steps: [{
        stepId: 'validate-order', status: 'faulted', attempt: 0,
        // no successCriteria → failedCriteria is false, yet the step FAULTED: the taken action is on the failure path
        actionTaken: { type: 'end', name: 'bail' },
      }],
    });
    const frame = el.frameAt(1);
    equal(frame.steps['validate-order'], 'done-failure', 'the faulted step is marked failed');
    for (const id of frame.edges) ok(projected.has(id), `frame edge ${id} is not in the projected graph`);
    ok(frame.edges.includes('end:validate-order:bail:failure'), 'the onFailure end lights the failure edge');
  });

  it('step-requested fires only past the end of the trace', async () => {
    make();
    el.cursor = 1;
    let requested = false;
    el.addEventListener('step-requested', () => { requested = true; });
    el.shadowRoot.querySelector('.next').click(); // 1 → 2 (client-side)
    equal(el.cursor, 2);
    ok(!requested);
    const req = nextEvent(el, 'step-requested');
    el.shadowRoot.querySelector('.next').click(); // at the end: ask the host to replay further
    await req;
  });

  it('a suspended message wait offers trigger injection and emits the session trigger', async () => {
    make({
      outcome: 'suspended',
      stepsExecuted: 1,
      steps: [TRACE.steps[0]],
      wait: { kind: 'message', channel: 'adoption/confirmed', correlationId: 'p-42' },
    });
    ok(el.shadowRoot.textContent.includes('waiting on'), 'the wait renders');
    const channel = el.shadowRoot.querySelector('.inj-channel');
    equal(channel.value, 'adoption/confirmed', 'the channel prefills from the wait');
    equal(el.shadowRoot.querySelector('.inj-corr').value, 'p-42', 'the correlation id prefills too');

    // The payload edits through the typed value editor (free-form JSON here — no channel schema
    // was fed). Invalid JSON refuses locally; nothing escapes the tray.
    const payloadEditor = el.shadowRoot.querySelector('.inj-payload');
    equal(payloadEditor.tagName.toLowerCase(), 'arazzo-value-editor', 'the inject payload is the standard typed editor');
    const jsonArea = payloadEditor.shadowRoot.querySelector('textarea');
    jsonArea.value = '{nope';
    jsonArea.dispatchEvent(new Event('input'));
    let leaked = false;
    el.addEventListener('trigger-injected', () => { leaked = true; }, { once: true });
    el.shadowRoot.querySelector('.inj-go').click();
    ok(!leaked, 'an unparseable payload does not emit');

    jsonArea.value = '{"approved": true}';
    jsonArea.dispatchEvent(new Event('input'));
    const injected = nextEvent(el, 'trigger-injected');
    el.shadowRoot.querySelector('.inj-go').click();
    const e = await injected;
    equal(e.detail.channel, 'adoption/confirmed');
    equal(e.detail.correlationId, 'p-42');
    equal(e.detail.payload.approved, true);
  });

  it('a typed channel schema gives the inject payload a structured form', () => {
    make({
      outcome: 'suspended',
      stepsExecuted: 1,
      steps: [TRACE.steps[0]],
      wait: { kind: 'message', channel: 'adoption/confirmed' },
    }, (tray) => {
      tray.channelSchemas = { 'adoption/confirmed': { type: 'object', properties: { approved: { type: 'boolean' } } } };
    });
    const payloadEditor = el.shadowRoot.querySelector('.inj-payload');
    ok(payloadEditor.shadowRoot.textContent.includes('approved'), 'the message schema drives the form');
  });

  it('step over opens a typed inline editor in the context pane and emits the override', async () => {
    make(TRACE, (tray) => {
      tray.stepSchemas = { 'get-pet': { outputs: { petName: { type: 'string' } } } };
    });
    el.cursor = 1;
    el.shadowRoot.querySelector('.override').click();
    const form = el.shadowRoot.querySelector('.ovr-form');
    ok(!form.hidden, 'the inline editor opens under the button');
    ok(form.textContent.includes('typed by'), 'and says it is typed');
    const overridden = nextEvent(el, 'output-override');
    el.shadowRoot.querySelector('.ovr-apply').click();
    equal((await overridden).detail.stepId, el.trace.steps[0].stepId);
  });

  it('a completed trace offers no injection form', () => {
    make();
    ok(!el.shadowRoot.querySelector('.inject'), 'inject only appears on a suspended message wait');
  });

  it('steps into a sub-workflow trace and back out, telling the host which workflow to show', async () => {
    make({
      outcome: 'completed',
      stepsExecuted: 1,
      steps: [{
        stepId: 'run-order', status: 'completed', attempt: 0,
        actionTaken: { type: 'end', name: 'done' },
        subTrace: { workflowId: 'place-order', outcome: 'completed', stepsExecuted: 1, steps: [TRACE.steps[0]] },
      }],
    });

    const into = el.shadowRoot.querySelector('[data-into]');
    ok(into, 'a sub-workflow step offers ⤵');
    const focused = nextEvent(el, 'workflow-focus');
    into.click();
    const descended = await focused;
    equal(descended.detail.workflowId, 'place-order', 'descending focuses the sub-workflow');
    equal(JSON.stringify(descended.detail.path), '["run-order"]', 'the focus event carries the descent STEP path (§3.5 breakpoint composition)');
    ok(el.shadowRoot.textContent.includes('get-pet'), 'the nested steps render');
    ok(el.shadowRoot.querySelector('.crumb'), 'the breadcrumb shows where you are');

    const refocused = nextEvent(el, 'workflow-focus');
    el.shadowRoot.querySelector('.crumb .up').click();
    const ascended = await refocused;
    equal(ascended.detail.workflowId, null, "stepping out restores the run's own workflow");
    equal(JSON.stringify(ascended.detail.path), '[]', 'the path empties back at the root');
    ok(el.shadowRoot.textContent.includes('run-order'), 'the parent trace is back');
    ok(!el.shadowRoot.querySelector('.crumb'), 'no breadcrumb at the top level');
  });

  it('an in-progress sub-workflow parent renders paused, never a green tick (§3.5)', () => {
    make({
      outcome: 'paused',
      pausedBefore: 'call-child/get-pet',
      stepsExecuted: 1,
      steps: [{
        stepId: 'call-child',
        status: 'paused',
        attempt: 0,
        subTrace: { workflowId: 'place-order', outcome: 'paused', pausedBefore: 'get-pet', stepsExecuted: 0, steps: [] },
      }],
    });

    const row = el.shadowRoot.querySelector('.step');
    ok(row.textContent.includes('⏸'), 'the paused parent shows the pause glyph');
    ok(!row.querySelector('.ok'), 'no green tick for an in-progress parent');
    ok(el.shadowRoot.textContent.includes('call-child/get-pet'), 'the scoped pausedBefore path renders');

    // The canvas frame: the paused parent is the ACTIVE (pulsing) node, not a done one.
    const frame = el.frameAt(el.length);
    equal(frame.active, 'call-child', 'the paused parent pulses as active');
    equal(frame.steps['call-child'], undefined, 'it is not marked done');
  });

  it('a suspended sub-workflow parent renders the timer glyph and stays active (§3.5)', () => {
    make({
      outcome: 'suspended',
      stepsExecuted: 1,
      wait: { kind: 'timer', dueAt: '2020-01-01T00:00:05Z' },
      steps: [{
        stepId: 'call-child',
        status: 'suspended',
        attempt: 0,
        subTrace: { workflowId: 'place-order', outcome: 'suspended', stepsExecuted: 1, steps: [TRACE.steps[0]] },
      }],
    });

    const row = el.shadowRoot.querySelector('.step');
    ok(row.textContent.includes('⏱'), 'the suspended parent shows the timer glyph');
    const frame = el.frameAt(el.length);
    equal(frame.active, 'call-child', 'the suspended parent pulses as active');
  });
});

describe('<arazzo-debug-tray> live-pause halo (frameAt at the end of a paused trace)', () => {
  let el;
  afterEach(() => el?.remove());

  function makeWith(trace) {
    el = document.createElement('arazzo-debug-tray');
    mount(el);
    el.trace = trace;
    return el;
  }

  const pausedTrace = (outcome) => ({
    outcome,
    stepsExecuted: 1,
    pausedBefore: 'adopt-pet',
    steps: [{
      stepId: 'get-pet', status: 'completed', attempt: 0,
      requests: [{ method: 'get', path: '/pets/42', status: 200 }],
      actionTaken: { type: 'fallThrough' },
    }],
  });

  it('a breakpoint pause pulses the step the run is paused BEFORE — live, not only when scrubbing back', () => {
    makeWith(pausedTrace('paused'));
    equal(el.cursor, el.length, 'a fresh trace lands at the end');
    equal(el.frameAt(el.length).active, 'adopt-pet', 'active = trace.pausedBefore at the live pause');
  });

  it('a budget (single-step) pause pulses the paused-before step too', () => {
    makeWith(pausedTrace('budgetExhausted'));
    equal(el.frameAt(el.length).active, 'adopt-pet');
  });

  it('a scoped pausedBefore haloes its ROOT segment on the root canvas', () => {
    makeWith({ ...pausedTrace('paused'), pausedBefore: 'call-child/get-pet' });
    equal(el.frameAt(el.length).active, 'call-child', 'the sub-workflow step is the active node at root level');
  });
});
