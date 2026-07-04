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

  it('renders one node per step with kind classes, chips, and the defaults card', () => {
    make();
    equal(el.shadowRoot.querySelectorAll('.node').length, 5, 'five steps');
    ok(node('validate-order').classList.contains('kind-operation'), 'operation kind');
    ok(node('await-confirmation').classList.contains('kind-channel'), 'channel kind');
    ok(node('capture-payment').textContent.includes('■ end'), 'end marker chip');
    ok(node('authorize-payment').textContent.includes('↻'), 'retry chip');
    ok(node('validate-order').textContent.includes('⌁ defaults'), 'ghost defaults chip on inheriting step');
    ok(!node('authorize-payment').textContent.includes('⌁ defaults'), 'no ghost chip when overridden');
    const defaults = el.shadowRoot.querySelector('.defaults');
    ok(defaults, 'workflow-defaults card rendered');
    ok(defaults.textContent.includes('give-up'), 'defaults card lists the inherited action');
  });

  it('renders sequence, failure, and reusable edges from the projection', () => {
    make();
    equal(el.shadowRoot.querySelectorAll('.edge-seq').length, 3, 'three sequence edges (end elides the last)');
    const failures = [...el.shadowRoot.querySelectorAll('.edge-failure')];
    equal(failures.length, 2, 'decline goto + reusable escalate');
    ok(failures.some((e) => e.classList.contains('reusable')), 'reusable action edge is marked');
    ok(el.shadowRoot.textContent.includes('$statusCode == 402'), 'criteria summary on the edge label');
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
    await waitFor(() => node('manual-review').classList.contains('link-target'));
    pointer('pointerup', svg, target);
    const e = await done;
    equal(e.detail.from, 'validate-order');
    equal(e.detail.to, 'manual-review');
    equal(e.detail.kind, 'success');
    ok(el.shadowRoot.querySelector('.rubber').hidden, 'rubber band hidden again');
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
});
