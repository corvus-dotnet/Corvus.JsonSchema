// Tier 3 — <arazzo-design-surface> rendering + interaction against the designer fixture: the §6.2
// projection renders as nodes/edges/defaults, selection and breakpoints round-trip as events, the
// debug overlay applies state classes, and the pointer state machine drags nodes and draws edges
// without ever touching document-level APIs.
import { projectWorkflow } from '../../src/workflow-graph.js';
import { designerFixture } from '../../demo/designer-fixture.js';
import '../../src/components/design-surface.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

describe('<arazzo-design-surface>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(workflowId = 'place-order') {
    el = document.createElement('arazzo-design-surface');
    el.style.cssText = 'display:block;width:900px;height:600px;';
    mount(el);
    el.graph = projectWorkflow(designerFixture, workflowId);
    return el;
  }

  const node = (id) => el.shadowRoot.querySelector(`.node[data-id="${id}"]`);
  const pointer = (type, target, opts = {}) =>
    target.dispatchEvent(new PointerEvent(type, { bubbles: true, composed: true, pointerId: 1, button: 0, ...opts }));
  const centerOf = (id) => {
    const box = node(id).querySelector('.card').getBoundingClientRect();
    return { clientX: box.left + box.width / 2, clientY: box.top + box.height / 2 };
  };

  it('renders one node per step plus start/end pseudo-nodes, kind classes, chips, defaults card', () => {
    make();
    equal(el.shadowRoot.querySelectorAll('.node').length, 7, 'five steps + start + end');
    ok(node('validate-order').classList.contains('kind-operation'), 'operation kind');
    ok(node('await-confirmation').classList.contains('kind-channel'), 'channel kind');
    const start = node('#start');
    const end = node('#end');
    ok(start.classList.contains('pseudo') && start.querySelector('circle.card'), 'start pseudo circle');
    ok(end.classList.contains('pseudo') && end.querySelector('circle.card'), 'end pseudo circle');
    ok(start.textContent.includes('3 inputs'), 'start badges the workflow inputs');
    ok(end.textContent.includes('1 output'), 'end badges the workflow outputs');
    ok(!start.querySelector('.port-success') && !start.querySelector('.port-failure') && !start.querySelector('.bp')
      && start.querySelector('.port-entry'), 'start carries ONLY the entry port — no action ports or breakpoint');
    ok(!end.querySelector('.port') && !end.querySelector('.bp'), 'the end terminal has no ports or breakpoints');
    ok(el.shadowRoot.querySelector('.edge[data-id="end:capture-payment:done:success"]'), 'end action is an edge to the terminal');
    ok(node('authorize-payment').textContent.includes('↻'), 'retry chip');
    ok(node('validate-order').textContent.includes('⌁ defaults'), 'ghost defaults chip on inheriting step');
    ok(!node('authorize-payment').textContent.includes('⌁ defaults'), 'no ghost chip when overridden');
    const defaults = el.shadowRoot.querySelector('.defaults');
    ok(defaults, 'workflow-defaults card rendered');
    ok(defaults.textContent.includes('give-up'), 'defaults card lists the inherited action');
  });

  it('renders sequence, failure, and reusable edges from the projection', () => {
    make();
    equal(el.shadowRoot.querySelectorAll('.edge-seq').length, 4, 'entry + three step sequence edges (ends elide the rest)');
    const failures = [...el.shadowRoot.querySelectorAll('.edge-failure')];
    equal(failures.length, 2, 'decline goto + reusable escalate');
    ok(failures.some((e) => e.classList.contains('reusable')), 'reusable action edge is marked');
    ok(el.shadowRoot.textContent.includes('$statusCode == 402'), 'criteria summary on the edge label');
    const endEdge = el.shadowRoot.querySelector('.edge[data-id="end:capture-payment:done:success"]');
    equal(endEdge.querySelector('.elabel').textContent, 'always', 'unconditional action edges say so');
    ok(endEdge.querySelector('.elabel').classList.contains('ghost'), '…in the ghost style');
    ok(!el.shadowRoot.querySelector('.edge-seq .elabel'), 'sequence edges stay unlabelled');
    // Both failure edges share from→to (decline + escalate): the parallel fan must separate them.
    const [d1, d2] = failures.map((e) => e.querySelector('.line').getAttribute('d'));
    ok(d1 !== d2, 'parallel edges take distinct paths');
  });

  it('renders a sub-workflow node and its kind badge', () => {
    make('order-with-compensation');
    ok(node('run-order').classList.contains('kind-workflow'), 'workflow-bound step');
    equal(node('run-order').querySelector('.kbadge-text').textContent, 'WF');
  });

  it('click selects a node and emits selection-changed; background click clears', async () => {
    make();
    const svg = el.shadowRoot.querySelector('svg');
    const p = centerOf('validate-order');
    const done = nextEvent(el, 'selection-changed');
    // Synthetic events must be dispatched on the deepest element (real events retarget; dispatch doesn't).
    pointer('pointerdown', node('validate-order').querySelector('.card'), p);
    pointer('pointerup', svg, p);
    const e = await done;
    equal(e.detail.selection.type, 'node');
    equal(e.detail.selection.id, 'validate-order');
    ok(node('validate-order').classList.contains('selected'), 'selected class applied');

    const cleared = nextEvent(el, 'selection-changed');
    pointer('pointerdown', svg, { clientX: 20, clientY: 20 });
    pointer('pointerup', svg, { clientX: 20, clientY: 20 });
    equal((await cleared).detail.selection, null, 'background click clears selection');
  });

  it('breakpoint dot toggles and emits', async () => {
    make();
    const svg = el.shadowRoot.querySelector('svg');
    const dot = node('authorize-payment').querySelector('.bp');
    const box = dot.getBoundingClientRect();
    const p = { clientX: box.left + box.width / 2, clientY: box.top + box.height / 2 };
    const done = nextEvent(el, 'breakpoint-toggled');
    pointer('pointerdown', dot, p);
    pointer('pointerup', svg, p);
    const e = await done;
    equal(e.detail.stepId, 'authorize-payment');
    equal(e.detail.enabled, true);
    ok(node('authorize-payment').classList.contains('bp-on'), 'breakpoint class applied');
    ok([...el.breakpoints].includes('authorize-payment'), 'breakpoints property updated');
  });

  it('debugState applies overlay classes and lit edges', () => {
    make();
    el.debugState = {
      active: 'manual-review',
      steps: { 'validate-order': 'done-success', 'authorize-payment': 'done-failure', 'await-confirmation': 'skipped' },
      edges: ['seq:validate-order'],
    };
    ok(el.shadowRoot.querySelector('svg').classList.contains('debugging'));
    ok(node('manual-review').classList.contains('st-active'), 'active step pulses');
    ok(node('validate-order').classList.contains('st-done-success'));
    ok(node('authorize-payment').classList.contains('st-done-failure'));
    ok(node('await-confirmation').classList.contains('st-skipped'));
    ok(el.shadowRoot.querySelector('.edge[data-id="seq:validate-order"]').classList.contains('lit'), 'edge lit');
    el.debugState = null;
    ok(!el.shadowRoot.querySelector('svg').classList.contains('debugging'), 'overlay clears');
  });

  it('dragging a node moves it and emits layout-changed with the override', async () => {
    make();
    const svg = el.shadowRoot.querySelector('svg');
    const start = centerOf('validate-order');
    const before = node('validate-order').getAttribute('transform');
    const done = nextEvent(el, 'layout-changed');
    pointer('pointerdown', node('validate-order').querySelector('.card'), start);
    pointer('pointermove', svg, { clientX: start.clientX + 80, clientY: start.clientY + 40 });
    pointer('pointerup', svg, { clientX: start.clientX + 80, clientY: start.clientY + 40 });
    const e = await done;
    ok(e.detail.overrides['validate-order'], 'override recorded for the dragged node');
    ok(node('validate-order').getAttribute('transform') !== before, 'node transform updated');
  });

  it('readonly blocks dragging but still allows selection', async () => {
    make();
    el.setAttribute('readonly', '');
    const svg = el.shadowRoot.querySelector('svg');
    const start = centerOf('validate-order');
    const before = node('validate-order').getAttribute('transform');
    const selected = nextEvent(el, 'selection-changed');
    pointer('pointerdown', node('validate-order').querySelector('.card'), start);
    pointer('pointermove', svg, { clientX: start.clientX + 80, clientY: start.clientY + 40 });
    pointer('pointerup', svg, { clientX: start.clientX + 80, clientY: start.clientY + 40 });
    equal(node('validate-order').getAttribute('transform'), before, 'node did not move');
    equal((await selected).detail.selection?.id, 'validate-order', 'still selectable');
  });

  it('drawing from a success port onto another node emits edge-created', async () => {
    make();
    const svg = el.shadowRoot.querySelector('svg');
    const port = node('validate-order').querySelector('.port-success');
    const portBox = port.getBoundingClientRect();
    const target = centerOf('manual-review');
    const done = nextEvent(el, 'edge-created');
    pointer('pointerdown', port, { clientX: portBox.left + 2, clientY: portBox.top + 2 });
    pointer('pointermove', svg, target);
    const rubber = el.shadowRoot.querySelector('.rubber');
    ok(!rubber.hasAttribute('hidden'), 'rubber band is VISIBLE while drawing (attribute, not the HTML-only property)');
    ok(rubber.classList.contains('rubber-success'), 'rubber band coloured by the edge kind');
    await waitFor(() => node('manual-review').classList.contains('link-target'));
    pointer('pointerup', svg, target);
    const e = await done;
    equal(e.detail.from, 'validate-order');
    equal(e.detail.to, 'manual-review');
    equal(e.detail.kind, 'success');
    ok(rubber.hasAttribute('hidden'), 'rubber band hidden again');
  });

  it('a goto to another workflow renders an exit chip', () => {
    const doc = structuredClone(designerFixture);
    doc.workflows[0].steps[1].onSuccess = [{ name: 'handoff', type: 'goto', workflowId: 'order-with-compensation' }];
    el = document.createElement('arazzo-design-surface');
    el.style.cssText = 'display:block;width:900px;height:600px;';
    mount(el);
    el.graph = projectWorkflow(doc, 'place-order');
    const chip = el.shadowRoot.querySelector('.exit-chip');
    ok(chip, 'exit chip rendered');
    ok(chip.textContent.includes('order-with-compensation'));
  });

  it('accepts an operation drop and reports it with world coordinates', async () => {
    el = make();
    const svg = el.shadowRoot.querySelector('svg');
    const transfer = new DataTransfer();
    transfer.setData('application/x-arazzo-operation', JSON.stringify({
      sourceName: 'payments',
      operation: { kind: 'openapi', operationId: 'refundPayment', method: 'POST', path: '/refunds' },
    }));

    const over = new DragEvent('dragover', { dataTransfer: transfer, bubbles: true, cancelable: true });
    svg.dispatchEvent(over);
    ok(over.defaultPrevented, 'the surface advertises itself as a drop target for our MIME type');

    const dropped = nextEvent(el, 'operation-dropped');
    svg.dispatchEvent(new DragEvent('drop', { dataTransfer: transfer, bubbles: true, cancelable: true, clientX: 200, clientY: 150 }));
    const e = await dropped;
    equal(e.detail.sourceName, 'payments');
    equal(e.detail.operation.operationId, 'refundPayment');
    ok(Number.isFinite(e.detail.position.x) && Number.isFinite(e.detail.position.y), 'drop point mapped to world coords');
  });

  it('ignores operation drops when readonly', async () => {
    el = make();
    el.setAttribute('readonly', '');
    const svg = el.shadowRoot.querySelector('svg');
    const transfer = new DataTransfer();
    transfer.setData('application/x-arazzo-operation', JSON.stringify({ sourceName: 's', operation: { operationId: 'x' } }));
    let fired = false;
    el.addEventListener('operation-dropped', () => { fired = true; });
    const over = new DragEvent('dragover', { dataTransfer: transfer, bubbles: true, cancelable: true });
    svg.dispatchEvent(over);
    svg.dispatchEvent(new DragEvent('drop', { dataTransfer: transfer, bubbles: true, cancelable: true, clientX: 100, clientY: 100 }));
    await new Promise((r) => setTimeout(r, 30));
    ok(!over.defaultPrevented && !fired, 'readonly surfaces reject the gesture entirely');
  });

  it('dragging from the start entry port onto a step emits entry-changed', async () => {
    el = make();
    const svg = el.shadowRoot.querySelector('svg');
    const port = node('#start').querySelector('.port-entry');
    ok(port, 'the start node renders an entry port');
    const portBox = port.getBoundingClientRect();
    const target = centerOf('capture-payment');
    const done = nextEvent(el, 'entry-changed');
    pointer('pointerdown', port, { clientX: portBox.left + 2, clientY: portBox.top + 2 });
    pointer('pointermove', svg, target);
    ok(el.shadowRoot.querySelector('.rubber').classList.contains('rubber-entry'), 'rubber band coloured as the entry edge');
    pointer('pointerup', svg, target);
    equal((await done).detail.stepId, 'capture-payment');
  });

  it('an entry drag onto a pseudo node emits nothing', async () => {
    el = make();
    const svg = el.shadowRoot.querySelector('svg');
    const port = node('#start').querySelector('.port-entry');
    const portBox = port.getBoundingClientRect();
    let fired = false;
    el.addEventListener('entry-changed', () => { fired = true; });
    const target = centerOf('#end');
    pointer('pointerdown', port, { clientX: portBox.left + 2, clientY: portBox.top + 2 });
    pointer('pointermove', svg, target);
    pointer('pointerup', svg, target);
    await new Promise((r) => setTimeout(r, 30));
    ok(!fired, 'start → end is not a meaningful entry');
  });

  it('a selected action edge grows a handle; dragging it onto another node emits edge-retargeted', async () => {
    make();
    // manual-review-on-decline: authorize-payment --402--> manual-review (a goto action edge).
    el.selection = { type: 'edge', id: 'goto:authorize-payment:manual-review-on-decline:failure' };
    const handle = el.shadowRoot.querySelector('.retarget');
    ok(handle, 'the selected action edge shows its retarget handle');

    const svg = el.shadowRoot.querySelector('svg');
    const handleBox = handle.getBoundingClientRect();
    const target = centerOf('capture-payment');
    const done = nextEvent(el, 'edge-retargeted');
    pointer('pointerdown', handle, { clientX: handleBox.left + 3, clientY: handleBox.top + 3 });
    pointer('pointermove', svg, target);
    ok(!el.shadowRoot.querySelector('.rubber').hasAttribute('hidden'), 'the rubber band follows the drag');
    await waitFor(() => node('capture-payment').classList.contains('link-target'));
    pointer('pointerup', svg, target);
    const e = await done;
    equal(e.detail.actionName, 'manual-review-on-decline');
    equal(e.detail.from, 'authorize-payment');
    equal(e.detail.to, 'capture-payment');

    // A sequence edge gets no handle: it IS the step order.
    el.selection = { type: 'edge', id: 'seq:validate-order' };
    ok(!el.shadowRoot.querySelector('.retarget'), 'sequence edges cannot be retargeted');
  });
});
