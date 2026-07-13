// Tier 3 — <arazzo-workflow-compare>: the side-by-side dialog with the visual-diff overlay.
import '../../src/components/workflow-compare.js';
import { ArazzoExpressionInput } from '../../src/components/expression-input.js';
import { ok, equal, waitFor, nextEvent, mount } from './helpers.js';

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

  // A comparison whose LEFT side is the working-copy merge target (as the git panel opens it).
  function openMerge(left, right, extra = {}) {
    el = document.createElement('arazzo-workflow-compare');
    mount(el);
    el.open({ left: { label: 'Working copy', document: left, mergeTarget: true }, right: { label: 'Commit', document: right }, workflowId: 'w', ...extra });
    return el;
  }
  const rows = () => [...el.shadowRoot.querySelectorAll('.cl-item')];
  const rowFor = (match) => rows().find((r) => match(r.querySelector('.cl-sel').textContent.trim()));
  const takeOf = (row) => row.querySelector('.cl-take');

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

  // ── Interactive merge (§6.4) ──────────────────────────────────────────────────────────────────
  it('exposes no Take/Keep verbs without a merge target', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    open(l, r); // no mergeTarget
    equal(el.shadowRoot.querySelectorAll('.cl-take').length, 0, 'no Take buttons');
    equal(el.shadowRoot.querySelectorAll('.cl-keep').length, 0, 'no Keep buttons');
  });

  it('Take on an added step emits insert-step with the other side’s step + our index', async () => {
    const l = base();
    const r = base();
    r.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' });
    openMerge(l, r);
    const accepted = nextEvent(el, 'change-accepted');
    takeOf(rowFor((t) => t === '+ d')).click();
    const { apply } = (await accepted).detail;
    equal(apply.kind, 'insert-step');
    equal(apply.step.stepId, 'd');
    equal(apply.index, 3, 'after c (our last common predecessor)');
  });

  it('Take on a removed step emits remove-step', async () => {
    const l = base();
    l.workflows[0].steps.push({ stepId: 'e', operationId: 'op.e' });
    const r = base();
    openMerge(l, r);
    const accepted = nextEvent(el, 'change-accepted');
    takeOf(rowFor((t) => t === '− e')).click();
    const { apply } = (await accepted).detail;
    equal(apply.kind, 'remove-step');
    equal(apply.stepId, 'e');
  });

  it('Take on a changed step emits replace-step with the other side’s content', async () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    openMerge(l, r);
    const accepted = nextEvent(el, 'change-accepted');
    takeOf(rowFor((t) => t.startsWith('Δ a'))).click();
    const { apply } = (await accepted).detail;
    equal(apply.kind, 'replace-step');
    equal(apply.stepId, 'a');
    ok(apply.step.successCriteria, 'carries the new content');
  });

  it('Take on a renamed step emits replace-step carrying the new stepId', async () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[1].stepId = 'bx'; // op.b binding preserved → rename b→bx
    openMerge(l, r);
    const accepted = nextEvent(el, 'change-accepted');
    takeOf(rowFor((t) => t.includes('b → bx'))).click();
    const { apply } = (await accepted).detail;
    equal(apply.kind, 'replace-step');
    equal(apply.stepId, 'b');
    equal(apply.step.stepId, 'bx', 'the model handles the id change as remove+insert');
  });

  it('Take on a moved step emits move-step with a target index', async () => {
    const l = base();
    const r = base();
    r.workflows[0].steps = [r.workflows[0].steps[1], r.workflows[0].steps[0], r.workflows[0].steps[2]]; // b,a,c
    openMerge(l, r);
    const accepted = nextEvent(el, 'change-accepted');
    takeOf(rowFor((t) => t.startsWith('moved'))).click();
    const { apply } = (await accepted).detail;
    equal(apply.kind, 'move-step');
    ok(typeof apply.index === 'number');
  });

  it('Take on an added action edge emits insert-action scoped to that action', async () => {
    const l = base(); // step a has no onSuccess
    const r = base();
    r.workflows[0].steps[0].onSuccess = [{ name: 'retry', type: 'goto', stepId: 'b' }];
    openMerge(l, r);
    const accepted = nextEvent(el, 'change-accepted');
    takeOf(rowFor((t) => t.includes('edge a → b') && t.includes('retry'))).click(); // the success goto, not the removed seq edge
    const { apply } = (await accepted).detail;
    equal(apply.kind, 'insert-action');
    equal(apply.stepId, 'a');
    equal(apply.list, 'onSuccess');
    equal(apply.action.name, 'retry');
    equal(apply.index, 0, 'a’s empty onSuccess list');
  });

  it('Take on a changed action edge emits replace-action at the raw index', async () => {
    const l = base(); // b.onSuccess = [{name:'go', goto c}]
    const r = base();
    r.workflows[0].steps[1].onSuccess = [{ name: 'go', type: 'goto', stepId: 'c', criteria: [{ condition: '$statusCode == 200' }] }];
    openMerge(l, r);
    const accepted = nextEvent(el, 'change-accepted');
    takeOf(rowFor((t) => t.includes('edge b → c') && t.startsWith('Δ'))).click();
    const { apply } = (await accepted).detail;
    equal(apply.kind, 'replace-action');
    equal(apply.stepId, 'b');
    equal(apply.index, 0);
    ok(apply.action.criteria, 'carries the new action body');
  });

  it('seq-edge flow entries route rather than Take', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' }); // adds a seq edge c → d
    openMerge(l, r);
    const seqRow = rowFor((t) => t.includes('seq edge') && t.includes('d'));
    ok(seqRow, 'a seq-edge entry exists');
    ok(!takeOf(seqRow), 'no Take on a seq edge');
    ok(seqRow.querySelector('.cl-route'), 'a route hint is shown instead');
  });

  it('Take locks every verb until refresh; refresh re-enables them', async () => {
    const l = base();
    const r = base();
    r.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' });
    openMerge(l, r);
    const accepted = nextEvent(el, 'change-accepted');
    takeOf(rowFor((t) => t === '+ d')).click();
    await accepted;
    ok([...el.shadowRoot.querySelectorAll('.cl-take')].every((b) => b.disabled), 'all Takes disabled after a Take');
    // The host applied the step; recompute against the merged document.
    const merged = base();
    merged.workflows[0].steps.push({ stepId: 'd', operationId: 'op.d' });
    el.refresh({ left: { document: merged } });
    ok(![...el.shadowRoot.querySelectorAll('.cl-take')].some((b) => b.disabled), 'verbs re-enabled after refresh');
  });

  it('Keep dims an entry and survives refresh (stable key)', () => {
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    r.workflows[0].steps[2].successCriteria = [{ condition: '$statusCode == 200' }]; // a second change survives
    openMerge(l, r);
    const row = rowFor((t) => t.startsWith('Δ a'));
    row.querySelector('.cl-keep').click();
    ok(row.classList.contains('reviewed'), 'the kept entry dims');
    el.refresh({ left: { document: base() }, right: { document: r } }); // recompute (a still changed)
    ok(rowFor((t) => t.startsWith('Δ a')).classList.contains('reviewed'), 'still reviewed after refresh');
  });

  it('Text merge: the merge-target pane is editable, the other read-only, with an Apply button', async function () {
    this.timeout(20000);
    const l = base();
    const r = base();
    r.workflows[0].steps[0].successCriteria = [{ condition: '$statusCode == 200' }];
    openMerge(l, r);
    el.shadowRoot.querySelector('.mode[data-mode="text"]').click();
    await ArazzoExpressionInput.loadCm();
    const host = el.shadowRoot.querySelector('.textmerge');
    await waitFor(() => host.querySelector('.cm-mergeView'), 15000);
    ok(host.querySelector('.tm-apply'), 'an Apply button is present with a merge target');
    const contents = [...host.querySelectorAll('.cm-content')];
    equal(contents.filter((c) => c.getAttribute('contenteditable') === 'true').length, 1, 'exactly one editable pane');
    equal(contents.filter((c) => c.getAttribute('contenteditable') === 'false').length, 1, 'the other is read-only');
  });
});
