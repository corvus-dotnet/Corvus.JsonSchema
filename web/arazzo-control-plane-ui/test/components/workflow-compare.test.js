// Tier 3 — <arazzo-workflow-compare>: the side-by-side dialog with the visual-diff overlay.
import '../../src/components/workflow-compare.js';
import { ArazzoExpressionInput } from '../../src/components/expression-input.js';
import { ok, equal, waitFor, mount } from './helpers.js';

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

  const overlay = () => el.shadowRoot.querySelector('.side-overlay arazzo-design-surface');
  const ovNodeCls = (id) => overlay().shadowRoot.querySelector(`.node[data-id="${id}"]`)?.classList;

  it('Overlay mode renders ONE ghost surface: base solid, the other side translucent', () => {
    const l = base();
    l.workflows[0].steps.push({ stepId: 'e', operationId: 'op.e' }); // present only on the left → removed
    const r = base();
    r.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' }); // present only on the right → added
    open(l, r);
    el.shadowRoot.querySelector('.mode[data-mode="overlay"]').click();
    ok(overlay(), 'a single overlay surface is rendered');
    ok(!el.shadowRoot.querySelector('.side-left arazzo-design-surface'), 'the two side-by-side surfaces are gone');
    ok(overlay().shadowRoot.querySelector('svg').classList.contains('diff-overlay'), 'the overlay svg class is on');
    ok(ovNodeCls('e').contains('df-removed') && ovNodeCls('e').contains('df-ghost'), 'the removed left step is a ghost');
    ok(ovNodeCls('d').contains('df-added') && !ovNodeCls('d').contains('df-ghost'), 'the added right step is solid (base = right)');
    ok(el.shadowRoot.querySelector('.sync').hidden, 'view sync is hidden on a single surface');
  });

  it('the change list selects on the single surface in overlay mode', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' });
    open(l, r);
    el.shadowRoot.querySelector('.mode[data-mode="overlay"]').click();
    const added = [...el.shadowRoot.querySelectorAll('.cl-item')].find((b) => b.textContent.trim() === '+ d');
    added.click();
    equal(overlay().selection?.id, 'd', 'the added step selects on the overlay surface');
    ok(added.classList.contains('current'));
  });

  it('switching Overlay → Side by side restores the two surfaces', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r);
    el.shadowRoot.querySelector('.mode[data-mode="overlay"]').click();
    ok(overlay(), 'in overlay');
    el.shadowRoot.querySelector('.mode[data-mode="side"]').click();
    ok(surface('left') && surface('right'), 'both side-by-side surfaces return');
    ok(!el.shadowRoot.querySelector('.side-overlay'), 'the overlay surface is gone');
    ok(!el.shadowRoot.querySelector('.sync').hidden, 'sync returns');
  });

  it('the mode switch shows all three modes when there is a diff', () => {
    open(base(), base());
    ok(!el.shadowRoot.querySelector('.modes').hidden, 'the mode switch is shown when there is a diff to compare');
    const modes = [...el.shadowRoot.querySelectorAll('.mode')].map((b) => b.dataset.mode);
    ok(modes.includes('side') && modes.includes('overlay') && modes.includes('text'), 'Side by side · Overlay · Text');
    ok(!el.shadowRoot.querySelector('.mode[data-mode="text"]').disabled, 'Text is enabled (CM loads lazily on switch)');
  });

  it('the mode switch is hidden when diff is off', () => {
    open(base(), base(), { diff: false });
    ok(el.shadowRoot.querySelector('.modes').hidden);
  });

  it('Text mode renders a read-only CodeMirror MergeView with change chunks', async function () {
    this.timeout(20000);
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r);
    el.shadowRoot.querySelector('.mode[data-mode="text"]').click();
    await ArazzoExpressionInput.loadCm(); // vendored bundle — no network
    const host = el.shadowRoot.querySelector('.textmerge');
    await waitFor(() => host.querySelector('.cm-mergeView'), 15000);
    equal(host.querySelectorAll('.cm-editor').length, 2, 'two panes (a and b)');
    await waitFor(() => host.querySelector('.cm-changedLine, .cm-changedText'), 5000);
    ok(host.querySelector('.cm-changedLine, .cm-changedText'), 'a change chunk is highlighted');
    const contents = [...host.querySelectorAll('.cm-content')];
    ok(contents.length === 2 && contents.every((c) => c.getAttribute('contenteditable') === 'false'),
      'both panes are read-only (no merge target)');
  });

  it('Text mode is disabled with an explanatory title when CodeMirror cannot load', async () => {
    const savedCm = ArazzoExpressionInput._cm;
    const savedLoader = ArazzoExpressionInput.cmLoader;
    ArazzoExpressionInput._cm = null;
    ArazzoExpressionInput.cmLoader = () => Promise.reject(new Error('offline'));
    try {
      const l = base();
      const r = base();
      r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
      open(l, r);
      el.shadowRoot.querySelector('.mode[data-mode="text"]').click();
      const host = el.shadowRoot.querySelector('.textmerge');
      await waitFor(() => host.querySelector('.tm-unavailable'), 5000);
      const btn = el.shadowRoot.querySelector('.mode[data-mode="text"]');
      ok(btn.disabled, 'Text is disabled when CM fails');
      ok(/unavailable/i.test(btn.title), 'with an explanatory title');
      ok(!host.querySelector('.cm-mergeView'), 'no merge editor');
    } finally {
      ArazzoExpressionInput._cm = savedCm;
      ArazzoExpressionInput.cmLoader = savedLoader;
    }
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
