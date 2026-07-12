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
});
