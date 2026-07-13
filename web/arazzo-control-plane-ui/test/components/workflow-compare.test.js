// Tier 3 — <arazzo-workflow-compare>: the side-by-side dialog with the visual-diff overlay.
import '../../src/components/workflow-compare.js';
import { ok, equal, mount } from './helpers.js';

const base = () => ({
  arazzo: '1.1.0',
  workflows: [{
    workflowId: 'w',
    steps: [
      { stepId: 'a', operationId: 'op.a' },
      { stepId: 'b', operationId: 'op.b', onSuccess: [{ name: 'go', type: 'goto', stepId: 'c' }] },
      { stepId: 'c', operationId: 'op.c' },
    ],
  }],
});

describe('<arazzo-workflow-compare>', () => {
  let el;
  afterEach(() => el?.remove());

  function open(left, right, opts = {}) {
    el = document.createElement('arazzo-workflow-compare');
    mount(el);
    el.open({ left: { label: 'Left', document: left }, right: { label: 'Right', document: right }, workflowId: 'w', ...opts });
    return el;
  }
  const surface = (which) => el.shadowRoot.querySelector(`.side-${which} arazzo-design-surface`);
  const nodeCls = (which, id) => surface(which).shadowRoot.querySelector(`.node[data-id="${id}"]`)?.classList;
  const legend = () => el.shadowRoot.querySelector('.legend').textContent;

  it('paints a changed step on both sides and shows a legend count', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r);
    ok(legend().includes('1 changed'), `legend shows the change (${legend()})`);
    ok(surface('left').shadowRoot.querySelector('svg').classList.contains('diffing'), 'the overlay is on');
    ok(nodeCls('left', 'a').contains('df-changed'), 'painted on the left');
    ok(nodeCls('right', 'a').contains('df-changed'), 'painted on the right');
  });

  it('paints added on the right and removed on the left', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' }); // added
    const l2 = base();
    l2.workflows[0].steps.push({ stepId: 'e', operationId: 'op.e' }); // present only on the left → removed
    open(l2, r);
    ok(nodeCls('right', 'd').contains('df-added'), 'added step painted on the right');
    ok(nodeCls('left', 'e').contains('df-removed'), 'removed step painted on the left');
    ok(legend().includes('added') && legend().includes('removed'));
  });

  it('a rename presents on both sides (changed) with the counts', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[1].stepId = 'bx'; // same binding op.b, same content → renamed b→bx
    open(l, r);
    ok(legend().includes('1 renamed'), `legend shows the rename (${legend()})`);
    ok(nodeCls('left', 'b').contains('df-changed'));
    ok(nodeCls('right', 'bx').contains('df-changed'));
  });

  it('"Highlight changes" toggles the overlay off (legend stays)', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r);
    const hl = el.shadowRoot.querySelector('.hl');
    ok(!hl.hidden, 'the toggle is shown when diffing');
    hl.click();
    equal(hl.getAttribute('aria-pressed'), 'false');
    ok(!surface('left').shadowRoot.querySelector('svg').classList.contains('diffing'), 'overlay cleared');
    ok(!nodeCls('left', 'a').contains('df-changed'), 'paint cleared on toggle off');
    ok(legend().includes('1 changed'), 'the legend stays');
    hl.click();
    ok(nodeCls('left', 'a').contains('df-changed'), 'toggling back repaints');
  });

  it('the shared union layout aligns matched steps across the split', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' }); // an exclusive step shifts the right solo layout
    open(l, r);
    for (const id of ['a', 'b', 'c']) {
      ok(surface('left').positions[id], `left has ${id}`);
      equal(JSON.stringify(surface('left').positions[id]), JSON.stringify(surface('right').positions[id]), `${id} is level on both sides`);
    }
  });

  it('an identical pair reports no differences and paints nothing', () => {
    open(base(), base());
    equal(legend(), 'No differences in this workflow');
    ok(!nodeCls('left', 'a').contains('df-changed') && !nodeCls('left', 'a').contains('df-added') && !nodeCls('left', 'a').contains('df-removed'));
  });

  it('diff:false reproduces the plain side-by-side (no legend, no toggle, no overlay)', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r, { diff: false });
    equal(legend(), '');
    ok(el.shadowRoot.querySelector('.hl').hidden, 'no Highlight toggle without diff');
    ok(!surface('left').shadowRoot.querySelector('svg').classList.contains('diffing'), 'no overlay');
  });

  it('the change list groups entries; clicking a changed step selects it on both sides', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r);
    ok(el.shadowRoot.querySelector('.cl-title').textContent.startsWith('Changes ('));
    ok([...el.shadowRoot.querySelectorAll('.cl-group')].some((g) => g.textContent === 'Steps'), 'a Steps group');
    const item = [...el.shadowRoot.querySelectorAll('.cl-item')].find((b) => b.textContent.trim().startsWith('Δ a'));
    ok(item, 'the changed step is listed');
    item.click();
    equal(surface('left').selection?.id, 'a', 'selected on the left');
    equal(surface('right').selection?.id, 'a', 'selected on the right');
    ok(item.classList.contains('current'), 'the clicked entry is marked current');
  });

  it('an added step selects on the right only; a removed step on the left only', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' });
    open(l, r);
    const added = [...el.shadowRoot.querySelectorAll('.cl-item')].find((b) => b.textContent.trim() === '+ d');
    ok(added, 'the added step is listed');
    added.click();
    equal(surface('right').selection?.id, 'd');
    equal(surface('left').selection, null, 'the left has no added node to select');
  });

  it('Prev / Next cycle through the entries and wrap deterministically', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    r.workflows[0].steps[2].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r);
    const n = el.shadowRoot.querySelectorAll('.cl-item').length;
    ok(n >= 2, 'several entries');
    const next = el.shadowRoot.querySelector('.cl-next');
    next.click();
    const first = el.shadowRoot.querySelector('.cl-item.current');
    ok(first, 'Next selects the first entry');
    for (let i = 0; i < n; i++) next.click(); // a full cycle returns to the same entry
    ok(el.shadowRoot.querySelector('.cl-item.current') === first, 'wraps around to the same entry');
  });

  it('the change list collapses', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r);
    el.shadowRoot.querySelector('.cl-collapse').click();
    ok(el.shadowRoot.querySelector('.grid').classList.contains('cl-collapsed'));
  });

  it('syncs one side view onto the other; the "Sync views" toggle stops it', () => {
    open(base(), base());
    const left = surface('left');
    const right = surface('right');
    left.dispatchEvent(new CustomEvent('view-changed', { detail: { tx: 5, ty: 6, k: 1.2 }, bubbles: true, composed: true }));
    equal(right.view.tx, 5); equal(right.view.ty, 6); equal(right.view.k, 1.2);
    right.dispatchEvent(new CustomEvent('view-changed', { detail: { tx: 7, ty: 8, k: 0.9 }, bubbles: true, composed: true }));
    equal(left.view.tx, 7, 'mirrors both directions');
    el.shadowRoot.querySelector('.sync').click(); // sync off
    right.view = { tx: 0, ty: 0, k: 1 };
    left.dispatchEvent(new CustomEvent('view-changed', { detail: { tx: 99, ty: 99, k: 2 }, bubbles: true, composed: true }));
    equal(right.view.tx, 0, 'no mirror after the toggle turns sync off');
  });
});
