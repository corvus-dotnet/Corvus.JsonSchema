// Tier 3 — <arazzo-debug-tray>: renders a SimulationTrace, cursor movement is client-side
// time-travel, and frameAt projects the design surface's debugState shape.
import '../../src/components/debug-tray.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

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

  function make(trace = TRACE) {
    el = document.createElement('arazzo-debug-tray');
    mount(el);
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

    // Invalid payload JSON refuses locally; nothing escapes the tray.
    el.shadowRoot.querySelector('.inj-payload').value = '{nope';
    let leaked = false;
    el.addEventListener('trigger-injected', () => { leaked = true; }, { once: true });
    el.shadowRoot.querySelector('.inj-go').click();
    ok(!leaked, 'an unparseable payload does not emit');

    el.shadowRoot.querySelector('.inj-payload').value = '{"approved": true}';
    const injected = nextEvent(el, 'trigger-injected');
    el.shadowRoot.querySelector('.inj-go').click();
    const e = await injected;
    equal(e.detail.channel, 'adoption/confirmed');
    equal(e.detail.correlationId, 'p-42');
    equal(e.detail.payload.approved, true);
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
    equal((await focused).detail.workflowId, 'place-order', 'descending focuses the sub-workflow');
    ok(el.shadowRoot.textContent.includes('get-pet'), 'the nested steps render');
    ok(el.shadowRoot.querySelector('.crumb'), 'the breadcrumb shows where you are');

    const refocused = nextEvent(el, 'workflow-focus');
    el.shadowRoot.querySelector('.crumb .up').click();
    equal((await refocused).detail.workflowId, null, "stepping out restores the run's own workflow");
    ok(el.shadowRoot.textContent.includes('run-order'), 'the parent trace is back');
    ok(!el.shadowRoot.querySelector('.crumb'), 'no breadcrumb at the top level');
  });
});
