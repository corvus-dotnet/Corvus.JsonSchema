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

  it('a parallel edge keeps its label stacking offset after a node move (label-offset bug 4)', () => {
    const doc = structuredClone(designerFixture);
    doc.workflows[0].steps[0].onSuccess = [
      { name: 'jump', type: 'goto', stepId: 'authorize-payment', criteria: [{ condition: '$statusCode == 200' }] },
      { name: 'jump', type: 'goto', stepId: 'authorize-payment', criteria: [{ condition: '$statusCode == 202' }] },
    ];
    el = document.createElement('arazzo-design-surface');
    el.style.cssText = 'display:block;width:900px;height:600px;';
    mount(el);
    el.graph = projectWorkflow(doc, 'place-order');

    // The second of two parallel edges carries order 2 → _buildEdge stacks its label +12 below the
    // edge midpoint so the labels do not overlap. _moveNode must preserve that offset when it re-paths.
    const order2 = el.graph.edges.find((e) => e.from === 'validate-order' && e.to === 'authorize-payment' && (e.order ?? 1) === 2);
    ok(order2, 'the second parallel goto exists with order 2');
    const offsetFromMid = () =>
      Number(el.shadowRoot.querySelector(`.edge[data-id="${order2.id}"] .elabel`).getAttribute('y')) - el._edgeMid(order2).y;
    ok(Math.abs(offsetFromMid() - 12) < 0.5, 'the order-2 label starts +12 below its edge midpoint (_buildEdge)');

    // Dragging validate-order re-paths the touching edges through _moveNode.
    el._positions['validate-order'] = { ...el._positions['validate-order'], x: el._positions['validate-order'].x + 40, y: el._positions['validate-order'].y + 40 };
    el._moveNode('validate-order');
    ok(Math.abs(offsetFromMid() - 12) < 0.5, 'the order-2 label keeps its +12 stacking offset after the move — not collapsed onto the midpoint');
  });

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

  it('diffState applies classification classes, the defaults card, and adornments', () => {
    make();
    el.diffState = {
      nodes: { 'validate-order': 'changed', 'manual-review': 'added' },
      edges: { 'seq:validate-order': 'added' },
      defaults: 'changed',
      notes: { 'validate-order': 'was checkOrder' },
    };
    ok(el.shadowRoot.querySelector('svg').classList.contains('diffing'));
    ok(node('validate-order').classList.contains('df-changed'));
    ok(node('manual-review').classList.contains('df-added'));
    ok(el.shadowRoot.querySelector('.edge[data-id="seq:validate-order"]').classList.contains('df-added'), 'edge classified');
    ok(el.shadowRoot.querySelector('.defaults').classList.contains('df-changed'), 'defaults card painted');
    const badge = el.shadowRoot.querySelector('.df-badge');
    ok(badge, 'a corner badge renders');
    equal(getComputedStyle(badge).pointerEvents, 'none', 'adornments never eat pointer events');
    equal(getComputedStyle(el.shadowRoot.querySelector('.df-halo')).pointerEvents, 'none');
    ok(el.shadowRoot.querySelector('.df-note'), 'a rename note chip renders');
  });

  it('diffState clears on null and survives a .graph reassignment', () => {
    make();
    el.diffState = { nodes: { 'validate-order': 'added' } };
    ok(node('validate-order').classList.contains('df-added'));
    el.graph = projectWorkflow(designerFixture, 'place-order'); // re-render -> _applyDiff re-runs
    ok(node('validate-order').classList.contains('df-added'), 'paint survives a graph reassignment');
    el.diffState = null;
    ok(!el.shadowRoot.querySelector('svg').classList.contains('diffing'), 'null clears the overlay');
    ok(!node('validate-order').classList.contains('df-added'));
    ok(!el.shadowRoot.querySelector('.df-badge'), 'adornments removed');
  });

  it('debug wins over diff, order-independently', () => {
    make();
    el.diffState = { nodes: { 'validate-order': 'changed' } };
    el.debugState = { steps: { 'validate-order': 'done-success' } }; // set AFTER diff
    ok(!node('validate-order').classList.contains('df-changed'), 'debug clears the diff paint');
    ok(!el.shadowRoot.querySelector('svg').classList.contains('diffing'));
    el.debugState = null;
    ok(node('validate-order').classList.contains('df-changed'), 'clearing debug restores the diff paint');
  });

  it('diff colours resolve through --arazzo-diff-* then the status-token fallback', () => {
    make();
    el.diffState = { nodes: { 'validate-order': 'changed' } };
    const rectStroke = () => getComputedStyle(node('validate-order').querySelector('rect.card')).stroke;
    el.style.setProperty('--arazzo-status-suspended', 'rgb(4, 5, 6)'); // tier 2: the status fallback
    equal(rectStroke(), 'rgb(4, 5, 6)', 'df-changed falls back to the status token');
    el.style.setProperty('--arazzo-diff-changed', 'rgb(1, 2, 3)'); // tier 1: the dedicated token wins
    equal(rectStroke(), 'rgb(1, 2, 3)', 'the dedicated --arazzo-diff-changed token wins');
  });

  it('view get returns a copy and set is silent (no event)', async () => {
    make();
    let fired = false;
    el.addEventListener('view-changed', () => { fired = true; });
    el.view = { tx: 12, ty: 34, k: 2 };
    const v = el.view;
    equal(v.tx, 12); equal(v.ty, 34); equal(v.k, 2);
    v.tx = 999; // mutating the copy must not affect the surface
    equal(el.view.tx, 12, 'get returns a copy');
    await Promise.resolve();
    ok(!fired, 'a programmatic set emits nothing (slice E adds only the gesture event)');
  });

  it('centerOn pans (at the current zoom) so the node lands at the viewport centre', () => {
    make();
    const box = el.shadowRoot.querySelector('svg').getBoundingClientRect();
    el.centerOn('validate-order');
    const v = el.view;
    const pos = el.positions['validate-order'];
    const cx = v.tx + (pos.x + 105) * v.k; // NODE_WIDTH/2
    const cy = v.ty + (pos.y + 37) * v.k;  // NODE_HEIGHT/2
    ok(Math.abs(cx - box.width / 2) < 1, 'node centre lands at viewport centre x');
    ok(Math.abs(cy - box.height / 2) < 1, 'node centre lands at viewport centre y');
  });

  it('view-changed fires from a user pan gesture, not from fit()/centerOn/a programmatic set', () => {
    make();
    const svg = el.shadowRoot.querySelector('svg');
    let events = 0;
    el.addEventListener('view-changed', () => { events += 1; });
    el.view = { tx: 12, ty: 12, k: 1 };
    el.fit();
    el.centerOn('validate-order');
    equal(events, 0, 'programmatic set / fit / centerOn stay silent');
    pointer('pointerdown', svg, { clientX: 20, clientY: 20 });
    pointer('pointermove', svg, { clientX: 90, clientY: 70 });
    pointer('pointerup', svg, { clientX: 90, clientY: 70 });
    ok(events > 0, 'a pan gesture emits view-changed');
  });

  it('overlay ghost mode: df-ghost composes with classification and the diff-overlay svg class (§4.7)', () => {
    make();
    el.diffState = {
      nodes: { 'validate-order': 'changed', 'manual-review': 'removed' },
      edges: { 'seq:validate-order': 'removed' },
      ghosts: { nodes: ['manual-review'], edges: ['seq:validate-order'] },
      overlay: true,
    };
    const svg = el.shadowRoot.querySelector('svg');
    ok(svg.classList.contains('diff-overlay'), 'overlay adds the diff-overlay svg class');
    const ghostNode = node('manual-review');
    ok(ghostNode.classList.contains('df-removed') && ghostNode.classList.contains('df-ghost'), 'a ghost keeps its class AND gains df-ghost');
    ok(!node('validate-order').classList.contains('df-ghost'), 'a solid (base) element is not a ghost');
    const ghostEdge = el.shadowRoot.querySelector('.edge[data-id="seq:validate-order"]');
    ok(ghostEdge.classList.contains('df-ghost'), 'a ghost edge gains df-ghost');
    equal(getComputedStyle(ghostNode).opacity, '0.5', 'ghosts render translucent');
    el.diffState = { nodes: { 'validate-order': 'changed' } }; // a non-overlay diff clears the overlay class
    ok(!svg.classList.contains('diff-overlay'), 'diff-overlay clears when overlay is not set');
    ok(!node('validate-order').classList.contains('df-ghost'));
  });
});

describe('<arazzo-design-surface> edge routing (§6.3 corridor/lane pass)', () => {
  let el;
  afterEach(() => el?.remove());

  // A chain a→b→c→d, a band-skipping goto a→d, a ghost skip b→d, and two departures from b.
  const routedGraph = {
    nodes: [
      { id: 'a', kind: 'op', label: 'a', sublabel: '' },
      { id: 'b', kind: 'op', label: 'b', sublabel: '' },
      { id: 'c', kind: 'op', label: 'c', sublabel: '' },
      { id: 'd', kind: 'op', label: 'd', sublabel: '' },
    ],
    edges: [
      { id: 'e-ab', from: 'a', to: 'b', kind: 'seq' },
      { id: 'e-bc', from: 'b', to: 'c', kind: 'seq' },
      { id: 'e-cd', from: 'c', to: 'd', kind: 'seq' },
      { id: 'skip-ad', from: 'a', to: 'd', kind: 'failure' },
      { id: 'ghost:skip-bd', from: 'b', to: 'd', kind: 'seq', ghost: true },
      { id: 'loop-ca', from: 'c', to: 'a', kind: 'failure' },
      { id: 'loop-db', from: 'd', to: 'b', kind: 'failure' },
    ],
    defaults: { successActions: [], failureActions: [] },
  };

  function make() {
    el = document.createElement('arazzo-design-surface');
    el.style.cssText = 'display:block;width:900px;height:600px;';
    mount(el);
    el.graph = routedGraph;
    return el;
  }

  const path = (id) => el.shadowRoot.querySelector(`.edge[data-id="${id}"] .line`).getAttribute('d');

  it('no two rendered edge paths coincide (the overlay pile-up bug)', () => {
    make();
    const ds = routedGraph.edges.map((e) => path(e.id));
    equal(new Set(ds).size, ds.length, 'every edge draws a distinct path');
  });

  it('a band-skipping edge takes waypoints beside the column instead of a straight line through it', () => {
    make();
    // The skip path is threaded through ≥2 corridor waypoints: 3+ cubic segments vs the plain 1.
    const segments = (path('skip-ad').match(/C /g) || []).length;
    ok(segments >= 3, `skip edge threads waypoints (got ${segments} cubic segments)`);
    equal((path('e-ab').match(/C /g) || []).length, 1, 'adjacent edges keep the single cubic');
  });

  it('two edges leaving one node depart from different points on its border', () => {
    make();
    const start = (d) => d.match(/^M ([-\d.]+) ([-\d.]+)/).slice(1).map(Number);
    const [x1] = start(path('e-bc'));
    const [x2] = start(path('ghost:skip-bd'));
    ok(x1 !== x2, `departures spread along the border (${x1} vs ${x2})`);
  });

  it('overlapping upward loops render on distinct right-side lanes', () => {
    make();
    // Each loop path carries its vertical lane as an L segment; the two lane x's differ.
    const laneX = (d) => Number(d.match(/L ([-\d.]+) /)[1]);
    ok(laneX(path('loop-ca')) !== laneX(path('loop-db')), 'lanes differ');
  });

  it('routing follows a node move (the moved band re-routes, no stale lanes)', () => {
    make();
    const before = path('skip-ad');
    // Move c far to the right (the _moveNode path a drag takes): the corridor beside c relocates,
    // so the skip edge crossing c's band must re-route — not keep its stale lane.
    el._positions.c = { ...el._positions.c, x: el._positions.c.x + 260 };
    el._moveNode('c');
    ok(path('skip-ad') !== before, 'the skip edge re-routed after the move');
  });
});
