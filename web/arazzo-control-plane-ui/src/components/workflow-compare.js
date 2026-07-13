// <arazzo-workflow-compare> — a reusable SIDE-BY-SIDE comparison of any two workflow versions: two
// read-only design surfaces in one dialog (the current state vs a commit, two commits, two catalog
// versions, or any two documents the host supplies). The git pane's history browser opens it for
// "current vs commit"; the same host serves any pair.
//
// When `diff` is on (the default), it overlays a VISUAL DIFF: `diffWorkflowPair` (workflow-diff.js)
// classifies steps/edges/workflow surfaces, both sides share ONE union layout so matched steps sit
// level, each surface paints its classification via `diffState`, and the head shows a legend of live
// counts plus a "Highlight changes" toggle. A collapsible CHANGE-LIST strip (grouped Steps / Flow /
// Workflow) lists every change; clicking one selects + centres it on the side(s) that have it, and
// ‹ Prev / Next › walk them in order. `diff: false` reproduces the plain side-by-side view.
//
// A segmented mode switch (Side by side · Overlay · Text) sits in the head. OVERLAY (§4.7/§6.2) renders
// ONE surface from `buildGhostProjection`: the base version solid, the other side's exclusive elements
// as translucent ghosts (base = the merge target when one is set, else the right side). TEXT (§6.3) is a
// CodeMirror MergeView over the two documents, serialized with the same deterministic pretty-print the
// text editor uses (`serializeDocument`); both panes read-only (interactive merge is a later slice). If
// CodeMirror cannot load, Text is disabled with an explanatory title — there is no textarea fallback for a
// merge editor. The legend, "Highlight changes" toggle, and change list are common chrome across all modes.
//
//   const dlg = document.createElement('arazzo-workflow-compare');
//   host.appendChild(dlg);
//   dlg.open({
//     left:  { label: 'Working copy (current)', document: currentDoc },
//     right: { label: 'a3f1c9d — Route failures to review', document: commitDoc },
//     workflowId: 'adopt-pet',   // optional: defaults to each side's first workflow (per-side wins)
//     diff: true,                // optional (default true): compute + paint the diff overlay
//   });
//
// Methods : open({left, right, workflowId?, diff?}) · close()
// Sides   : { label: string, document: object, workflowId?: string } — a per-side workflowId wins
//           over the shared one (compare a renamed workflow across versions).

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import { projectWorkflow } from '../workflow-graph.js';
import { diffWorkflowPair, buildGhostProjection } from '../workflow-diff.js';
import { serializeDocument } from '../workflow-document-model.js';
import { ArazzoExpressionInput } from './expression-input.js';
import './design-surface.js';

const GROUPS = [['steps', 'Steps'], ['flow', 'Flow'], ['workflow', 'Workflow']];

class ArazzoWorkflowCompare extends ArazzoElement {
  /** Opens the comparison; each side projects read-only and (when diff is on) paints the overlay.
   *  A side may carry `mergeTarget: true` (at most one — the working-copy side): the change list then
   *  gains Take/Keep verbs whose Take emits `change-accepted` / `merge-text-applied` for the host to apply
   *  to its single model (§6.4). The component never mutates a document. */
  open({ left, right, workflowId, diff = true } = {}) {
    this.render();
    const leftWorkflowId = this._resolveWorkflowId(left, workflowId);
    const rightWorkflowId = this._resolveWorkflowId(right, workflowId);
    this._sides = { left, right };
    this._workflowIds = { left: leftWorkflowId, right: rightWorkflowId };
    this._mergeSide = left?.mergeTarget ? 'left' : (right?.mergeTarget ? 'right' : null); // at most one; left wins
    this._reviewed = new Set(); // Keep marks, keyed by stable entry key (survives refresh)
    this._current = -1;
    this._mode = 'side';
    this._syncViews = true;
    this._computeDiff(diff);

    this._renderStage();
    this._renderModes();
    this._renderLegend();
    this._renderChangeList();

    this.$('dialog').showModal();
    this._wireViewSync();
    // Fit after the dialog lays out. With a diff, ONE fit over the union extent is assigned to both
    // surfaces (§4.6 shared fit) so matched steps sit level; without one, each side fits itself.
    requestAnimationFrame(() => this._fitStage());
  }

  /** Recompute the diff after the host applied a Take (§6.4): swap in the updated document(s), keep the
   *  mode + reviewed set, re-enable the verbs, and repaint. The host calls this after model.update/applyText. */
  refresh({ left, right } = {}) {
    if (!this._sides) return; // never opened — nothing to recompute
    if (left) this._sides.left = { ...this._sides.left, ...left };
    if (right) this._sides.right = { ...this._sides.right, ...right };
    this._workflowIds = {
      left: this._resolveWorkflowId(this._sides.left, this._workflowIds.left),
      right: this._resolveWorkflowId(this._sides.right, this._workflowIds.right),
    };
    this._verbsLocked = false;
    this._computeDiff(this._diff != null);
    this._renderStage();
    this._renderLegend();
    this._renderChangeList();
    requestAnimationFrame(() => this._fitStage());
  }

  /** @private — (re)compute the diff, entries, and derived flags from the current sides. */
  _computeDiff(diff) {
    this._diff = diff
      ? diffWorkflowPair(this._sides.left?.document ?? {}, this._sides.right?.document ?? {},
        { leftWorkflowId: this._workflowIds.left, rightWorkflowId: this._workflowIds.right })
      : null;
    this._highlight = !!this._diff;
    this._entries = this._diff ? [...this._diff.steps, ...this._diff.flow, ...this._diff.workflow] : [];
    this._verbsLocked = false;
    // Overlay base = the merge target when one is set (§6.2), else the right side.
    this._base = this._mergeSide || 'right';
  }

  close() {
    if (this._mergeView) { this._mergeView.destroy(); this._mergeView = null; }
    this.$('dialog')?.close();
  }

  /** @private — per-side wins over the shared id, else the side's first workflow. */
  _resolveWorkflowId(side, sharedWorkflowId) {
    return side?.workflowId ?? sharedWorkflowId ?? side?.document?.workflows?.[0]?.workflowId ?? '';
  }

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: 10px; background: var(--_bg); color: inherit; padding: 0;
                 width: min(94vw, 1500px); height: 86vh; max-height: 86vh; display: grid; grid-template-rows: auto minmax(0, 1fr); }
        dialog:not([open]) { display: none; }
        dialog::backdrop { background: rgb(0 0 0 / 0.35); }
        .head { display: flex; align-items: center; gap: 10px; padding: 10px 14px; border-bottom: 1px solid var(--_border); }
        .head h2 { margin: 0; font-size: 14px; }
        .legend { margin: 0 auto 0 4px; font-size: 12px; color: var(--_muted); }
        .modes { display: inline-flex; gap: 2px; }
        .modes .mode[aria-pressed="true"] { background: var(--_surface); box-shadow: inset 0 0 0 1px var(--_border); }
        .grid { display: grid; grid-template-columns: minmax(0, 1fr); grid-template-rows: minmax(0, 1fr); min-height: 0; }
        .grid.has-diff { grid-template-columns: var(--cl-w, 260px) minmax(0, 1fr); }
        .grid:not(.has-diff) .changelist { display: none; }
        .grid.cl-collapsed { --cl-w: 44px; }
        .stage { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); grid-template-rows: minmax(0, 1fr); min-width: 0; min-height: 0; }
        .stage.is-overlay, .stage.is-text { grid-template-columns: minmax(0, 1fr); }
        .textmerge { min-width: 0; min-height: 0; display: flex; flex-direction: column; font-size: 12px; }
        .tm-bar { flex: none; display: flex; justify-content: flex-end; padding: 6px 10px; border-bottom: 1px solid var(--_border); }
        .tm-apply { font: 12px var(--_font); padding: 3px 12px; border: 1px solid var(--_border); border-radius: 6px;
                    background: var(--_accent, #3b6cf6); color: #fff; cursor: pointer; }
        .tm-apply[disabled] { opacity: 0.5; cursor: default; background: var(--_bg); color: var(--_muted); }
        .tm-view { flex: 1; min-height: 0; overflow: auto; }
        .tm-view .cm-mergeView, .tm-view .cm-mergeViewEditors, .tm-view .cm-editor { height: 100%; }
        .tm-unavailable { padding: 24px; color: var(--_muted); font-size: 13px; }
        .changelist { display: grid; grid-template-rows: auto minmax(0, 1fr); min-height: 0; border-right: 1px solid var(--_border); }
        .cl-head { display: flex; align-items: center; gap: 4px; padding: 6px 8px; border-bottom: 1px solid var(--_border); font-size: 12px; }
        .cl-title { margin-right: auto; font-weight: 600; }
        .grid.cl-collapsed .cl-title, .grid.cl-collapsed .cl-prev, .grid.cl-collapsed .cl-next, .grid.cl-collapsed .cl-body { display: none; }
        .cl-body { overflow: auto; padding: 4px 0 8px; }
        .cl-group { padding: 6px 10px 2px; font-size: 10px; text-transform: uppercase; letter-spacing: 0.05em; color: var(--_muted); }
        .cl-item { display: flex; align-items: center; gap: 4px; padding-right: 6px; }
        .cl-item:hover { background: var(--_surface); }
        .cl-item.current { background: var(--_surface); box-shadow: inset 3px 0 0 var(--_accent); }
        .cl-item.reviewed .cl-sel { opacity: 0.5; text-decoration: line-through; }
        .cl-sel { flex: 1; min-width: 0; text-align: left; border: none; background: none; color: inherit; cursor: pointer;
                  font: 12px var(--_font); padding: 3px 10px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .cl-take, .cl-keep { flex: none; font: 11px var(--_font); padding: 1px 6px; border: 1px solid var(--_border);
                             border-radius: 5px; background: var(--_bg); color: inherit; cursor: pointer; }
        .cl-take:hover:not([disabled]), .cl-keep:hover:not([disabled]) { background: var(--_surface); }
        .cl-take[disabled], .cl-keep[disabled] { opacity: 0.4; cursor: default; }
        .cl-route { flex: none; color: var(--_muted); cursor: help; padding: 0 4px; }
        .grid:not(.merging) .cl-take, .grid:not(.merging) .cl-keep, .grid:not(.merging) .cl-route { display: none; }
        .cl-mark { font-weight: 700; }
        .cl-mark.added { color: var(--arazzo-diff-added, var(--arazzo-status-completed, #2a8a4a)); }
        .cl-mark.removed { color: var(--arazzo-diff-removed, var(--arazzo-status-faulted, #d4351c)); }
        .cl-mark.changed, .cl-mark.renamed, .cl-mark.moved { color: var(--arazzo-diff-changed, var(--arazzo-status-suspended, #b07d18)); }
        .side { display: grid; grid-template-rows: auto minmax(0, 1fr); min-width: 0; min-height: 0; }
        .side + .side { border-left: 1px solid var(--_border); }
        .side-head { padding: 6px 12px; font-size: 12px; color: var(--_muted); border-bottom: 1px solid var(--_border);
                     overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .side arazzo-design-surface { display: block; width: 100%; height: 100%; min-height: 0; }
        .hl[aria-pressed="false"], .sync[aria-pressed="false"] { opacity: 0.55; text-decoration: line-through; }
        .sync[aria-pressed="true"] { background: var(--_surface); box-shadow: inset 0 0 0 1px var(--_border); }
        [hidden] { display: none !important; }
      </style>
      <dialog>
        <div class="head">
          <h2>Compare workflow versions</h2>
          <span class="legend"></span>
          <div class="modes" role="group" aria-label="Comparison mode" hidden>
            <button class="mode ghost" type="button" data-mode="side" aria-pressed="true">Side by side</button>
            <button class="mode ghost" type="button" data-mode="overlay" aria-pressed="false">Overlay</button>
            <button class="mode ghost" type="button" data-mode="text" aria-pressed="false" title="Side-by-side text merge (CodeMirror)">Text</button>
          </div>
          <button class="hl ghost" type="button" aria-pressed="true" hidden>Highlight changes</button>
          <button class="sync ghost" type="button" aria-pressed="true" title="Pan/zoom one side to scroll both">Sync views</button>
          <button class="close ghost" type="button" title="Close">✕</button>
        </div>
        <div class="grid">
          <div class="changelist">
            <div class="cl-head">
              <button class="cl-collapse ghost" type="button" aria-expanded="true" title="Collapse the change list">☰</button>
              <span class="cl-title">Changes</span>
              <button class="cl-prev ghost" type="button" title="Previous change">‹</button>
              <button class="cl-next ghost" type="button" title="Next change">›</button>
            </div>
            <div class="cl-body"></div>
          </div>
          <div class="stage"></div>
        </div>
      </dialog>`;
    this.$('.close').addEventListener('click', () => this.close());
    this.$('.hl').addEventListener('click', () => this._toggleHighlight());
    this.$('.sync').addEventListener('click', () => this._toggleSync());
    for (const b of this.$$('.mode')) b.addEventListener('click', () => this._setMode(b.dataset.mode));
    this.$('.cl-prev').addEventListener('click', () => this._cycle(-1));
    this.$('.cl-next').addEventListener('click', () => this._cycle(1));
    this.$('.cl-collapse').addEventListener('click', () => this._toggleCollapse());
    this.$('.cl-body').addEventListener('click', (e) => {
      const item = e.target.closest('.cl-item');
      if (!item) return;
      const i = Number(item.dataset.i);
      if (e.target.closest('.cl-take')) { this._take(i); return; }
      if (e.target.closest('.cl-keep')) { this._keep(i); return; }
      this._selectEntry(i);
    });
  }

  /** @private — one side: label + a read-only surface. With diff on it reuses the diff's projection,
   *  union layout, and paint (no re-projection); with diff off it projects plainly. */
  renderSide(sel, side, workflowId, which) {
    const host = this.$(sel);
    host.querySelector('.side-head').textContent = side?.label ?? '';
    host.querySelector('.side-head').title = side?.label ?? '';
    const surface = document.createElement('arazzo-design-surface');
    surface.setAttribute('readonly', '');
    if (this._diff) {
      surface.layoutEngine = () => this._diff.layout[which];
      surface.graph = which === 'left' ? this._diff.leftGraph : this._diff.rightGraph;
      surface.diffState = this._highlight ? this._diff.paint[which] : null;
    } else {
      surface.graph = projectWorkflow(side?.document ?? {}, workflowId);
    }
    host.appendChild(surface);
  }

  /** @private — (re)build the stage for the current mode. Side-by-side = two surfaces; Overlay = one ghost
   *  surface (§6.2); Text = a CodeMirror MergeView (§6.3). The change list, legend, and toggle are common
   *  chrome and are not rebuilt here. */
  _renderStage() {
    const stage = this.$('.stage');
    if (this._mergeView) { this._mergeView.destroy(); this._mergeView = null; } // free the prior CM instance
    if (this._mode === 'text' && this._diff) {
      stage.classList.remove('is-overlay');
      stage.classList.add('is-text');
      stage.innerHTML = '<div class="textmerge"></div>';
      this._renderTextMerge();
      return;
    }
    stage.classList.remove('is-text');
    const overlay = this._mode === 'overlay' && this._diff;
    stage.classList.toggle('is-overlay', !!overlay);
    if (overlay) {
      stage.innerHTML = '<div class="side side-overlay"><div class="side-head"></div></div>';
      this._renderOverlaySurface();
    } else {
      stage.innerHTML = '<div class="side side-left"><div class="side-head"></div></div><div class="side side-right"><div class="side-head"></div></div>';
      this.renderSide('.side-left', this._sides.left, this._workflowIds.left, 'left');
      this.renderSide('.side-right', this._sides.right, this._workflowIds.right, 'right');
    }
  }

  /** @private — Text mode (§6.3): a CodeMirror MergeView over both documents, serialized identically to the
   *  text editor (`serializeDocument`). Read-only without a merge target; with one, the merge-target pane is
   *  editable, gains chunk `revertControls` from the other side, and an Apply button emits the merged text
   *  (§6.4). A dirty buffer disables list Takes (text-buffer exclusivity). If CM cannot load, Text is
   *  disabled with an explanatory title — no textarea fallback for a merge editor. */
  async _renderTextMerge() {
    const host = this.$('.textmerge');
    this._mergeDirty = false;
    let cm = null;
    try { cm = await ArazzoExpressionInput.loadCm(); } catch { cm = null; }
    if (this._mode !== 'text' || !host.isConnected) return; // a mode switch raced the async load
    if (!cm?.merge?.MergeView) {
      const btn = this.$('.mode[data-mode="text"]');
      if (btn) { btn.disabled = true; btn.title = 'Text merge is unavailable — CodeMirror could not load.'; }
      host.innerHTML = '<div class="tm-unavailable">Text merge is unavailable in this environment.</div>';
      return;
    }
    const merging = !!this._mergeSide;
    host.innerHTML = merging
      ? '<div class="tm-bar"><button class="tm-apply" type="button" disabled>Apply merge</button></div><div class="tm-view"></div>'
      : '<div class="tm-view"></div>';
    if (merging) this.$('.tm-apply').addEventListener('click', () => this._applyTextMerge());
    const { merge, state, view, langJson } = cm;
    const readonlyExts = [state.EditorState.readOnly.of(true), view.EditorView.editable.of(false)];
    const dirtyWatch = view.EditorView.updateListener.of((u) => { if (u.docChanged) this._markMergeDirty(); });
    // The MergeView is created without a dark CM theme, so its "N unchanged lines" collapse bars and gutters
    // fall back to CodeMirror's light defaults (black-on-white in the dark app). Theme them from the kit tokens
    // so they track light/dark automatically, instead of switching CM's whole base theme.
    const mergeChrome = view.EditorView.theme({
      '.cm-collapsedLines': {
        color: 'var(--arazzo-muted, #6b7280)',
        background: 'linear-gradient(to bottom, transparent 0, var(--arazzo-surface, #f7f8fa) 30%, var(--arazzo-surface, #f7f8fa) 70%, transparent 100%)',
      },
      '.cm-collapsedLines:hover': { color: 'var(--arazzo-text, #1c2024)' },
      '.cm-gutters': { background: 'var(--arazzo-surface, #f7f8fa)', color: 'var(--arazzo-muted, #6b7280)', border: 'none' },
    });
    const pane = (doc, editable) => ({
      doc,
      extensions: [langJson.json(), view.lineNumbers(), mergeChrome, ...(editable ? [dirtyWatch] : readonlyExts)],
    });
    // revertControls push the OTHER side's chunks INTO the merge-target pane (a=left, b=right).
    const opts = {
      parent: this.$('.tm-view'),
      root: this.shadowRoot,
      orientation: 'a-b',
      highlightChanges: true,
      collapseUnchanged: {},
      a: pane(serializeDocument(this._sides.left?.document ?? {}), merging && this._mergeSide === 'left'),
      b: pane(serializeDocument(this._sides.right?.document ?? {}), merging && this._mergeSide === 'right'),
    };
    if (merging) opts.revertControls = this._mergeSide === 'left' ? 'b-to-a' : 'a-to-b';
    this._mergeView = new merge.MergeView(opts);
  }

  /** @private — a text-merge edit dirties the buffer: enable Apply, disable list Takes until apply/refresh. */
  _markMergeDirty() {
    if (this._mergeDirty) return;
    this._mergeDirty = true;
    const apply = this.$('.tm-apply');
    if (apply) apply.disabled = false;
    for (const b of this.$$('.cl-take, .cl-keep')) b.disabled = true;
  }

  /** @private — Apply the merged text: emit `merge-text-applied { text }` (the host applies + refreshes,
   *  which rebuilds this view pristine). Locks verbs until refresh, like a Take. */
  _applyTextMerge() {
    if (!this._mergeSide || !this._mergeView) return;
    const targetView = this._mergeSide === 'left' ? this._mergeView.a : this._mergeView.b;
    const text = targetView?.state.doc.toString() ?? '';
    this._setVerbsLocked(true);
    this.emit('merge-text-applied', { text });
  }

  /** @private — overlay mode's single surface: the ghost projection (base solid + the other side's ghosts). */
  _renderOverlaySurface() {
    this._ghost = buildGhostProjection(this._diff, this._base);
    const side = this._sides[this._base];
    const host = this.$('.side-overlay');
    host.querySelector('.side-head').textContent = side?.label ?? '';
    host.querySelector('.side-head').title = side?.label ?? '';
    const surface = document.createElement('arazzo-design-surface');
    surface.setAttribute('readonly', '');
    surface.layoutEngine = () => this._ghost.layout;
    surface.graph = this._ghost.graph;
    surface.diffState = this._highlight ? this._ghost.paint : null;
    host.appendChild(surface);
  }

  /** @private — show the mode switch only when there is a diff to compare. */
  _renderModes() {
    this.$('.modes').hidden = !this._diff;
  }

  /** @private — switch comparison mode. A disabled button (e.g. Text when CM failed to load) is inert. */
  _setMode(mode) {
    if (!mode || mode === this._mode || !this._diff) return;
    if (this.$(`.mode[data-mode="${mode}"]`)?.disabled) return;
    this._mode = mode;
    for (const b of this.$$('.mode')) b.setAttribute('aria-pressed', String(b.dataset.mode === mode));
    this.$('.sync').hidden = mode !== 'side'; // view sync is moot on a single surface / the text panes
    this._renderStage();
    this._wireViewSync();
    requestAnimationFrame(() => this._fitStage());
  }

  /** @private — fit the current mode's surface(s): the shared union fit side-by-side, one fit in overlay.
   *  Text mode has nothing to fit (the MergeView scrolls itself). */
  _fitStage() {
    if (this._mode === 'text') return;
    if (this._mode === 'overlay' && this._ghost) {
      const s = this.$('.side-overlay arazzo-design-surface');
      s?.fit(Object.values(this._ghost.layout));
      return;
    }
    this._sharedFit();
  }

  /** @private */
  _renderLegend() {
    const legend = this.$('.legend');
    const hl = this.$('.hl');
    if (!this._diff) { legend.textContent = ''; hl.hidden = true; return; }
    const s = this._diff.summary;
    const parts = [];
    if (s.added) parts.push(`${s.added} added`);
    if (s.removed) parts.push(`${s.removed} removed`);
    if (s.changed) parts.push(`${s.changed} changed`);
    if (s.renamed) parts.push(`${s.renamed} renamed`);
    if (s.moved) parts.push(`${s.moved} moved`);
    legend.textContent = parts.length ? parts.join(' · ') : 'No differences in this workflow';
    hl.hidden = false;
  }

  /** @private — the "Highlight changes" toggle: off clears diffState on the visible surface(s); legend stays. */
  _toggleHighlight() {
    if (!this._diff) return;
    this._highlight = !this._highlight;
    this.$('.hl').setAttribute('aria-pressed', String(this._highlight));
    if (this._mode === 'overlay') {
      const surface = this.$('.side-overlay arazzo-design-surface');
      if (surface && this._ghost) surface.diffState = this._highlight ? this._ghost.paint : null;
      return;
    }
    for (const which of ['left', 'right']) {
      const surface = this.$(`.side-${which} arazzo-design-surface`);
      if (surface) surface.diffState = this._highlight ? this._diff.paint[which] : null;
    }
  }

  /** @private — ONE fit over the union extent, assigned to BOTH surfaces so matched steps sit level (§4.6
   *  shared fit); without a diff each surface fits itself. Equal viewports (the 1fr columns) → identical view. */
  _sharedFit() {
    const left = this.$('.side-left arazzo-design-surface');
    const right = this.$('.side-right arazzo-design-surface');
    if (this._diff && left && right) {
      const points = [...Object.values(this._diff.layout.left), ...Object.values(this._diff.layout.right)];
      left.fit(points);
      right.view = left.view; // silent set; the same transform on the equal-width column → aligned
    } else {
      for (const s of [left, right]) s?.fit();
    }
  }

  /** @private — mirror each side's USER pan/zoom (view-changed) onto the other; the mirror's `set view` is
   *  silent so there is no feedback (the guard is belt-and-suspenders). Default on; "Sync views" flips it. */
  _wireViewSync() {
    const left = this.$('.side-left arazzo-design-surface');
    const right = this.$('.side-right arazzo-design-surface');
    const link = (from, to) => from?.addEventListener('view-changed', (e) => {
      if (!this._syncViews || this._mirroring || !to) return;
      this._mirroring = true;
      to.view = e.detail;
      this._mirroring = false;
    });
    link(left, right);
    link(right, left);
  }

  /** @private */
  _toggleSync() {
    this._syncViews = !this._syncViews;
    this.$('.sync').setAttribute('aria-pressed', String(this._syncViews));
  }

  /** @private — the grouped change list (§4.5 / §6 item 4). With a merge target each takeable entry gains
   *  Take / Keep verbs (§6.4); reviewed (Kept) entries dim and survive refresh. */
  _renderChangeList() {
    const grid = this.$('.grid');
    grid.classList.toggle('has-diff', !!this._diff);
    grid.classList.toggle('merging', !!this._mergeSide);
    if (!this._diff) return;
    this.$('.cl-title').textContent = `Changes (${this._entries.length})`;
    let html = '';
    for (const [group, heading] of GROUPS) {
      const items = this._entries.map((e, i) => [e, i]).filter(([e]) => e.group === group);
      if (!items.length) continue;
      html += `<div class="cl-group">${heading}</div>`;
      for (const [entry, i] of items) {
        const { mark, text } = this._entryLabel(entry);
        const reviewed = this._reviewed?.has(this._entryKey(entry));
        html += `<div class="cl-item${reviewed ? ' reviewed' : ''}" data-i="${i}">`
          + `<button class="cl-sel" type="button"><span class="cl-mark ${entry.type}">${escapeHtml(mark)}</span> ${escapeHtml(text)}</button>`
          + this._verbsHtml(entry) + '</div>';
      }
    }
    this.$('.cl-body').innerHTML = html || '<div class="cl-group">No differences</div>';
  }

  /** @private — the Take / Keep buttons for an entry (merge only). Seq and component-sourced flow entries
   *  are informational: no Take, a title pointing at their carrier (the moved step / the components entry). */
  _verbsHtml(entry) {
    if (!this._mergeSide) return '';
    const route = this._routeHint(entry);
    if (route) return `<span class="cl-route" title="${escapeHtml(route)}">↪</span>`;
    if (!this._takePayload(entry)) return '';
    const dis = this._verbsLocked ? ' disabled' : '';
    return `<button class="cl-take" type="button"${dis} title="Take the other version's change">Take</button>`
      + `<button class="cl-keep" type="button"${dis} title="Keep this side (mark reviewed)">Keep</button>`;
  }

  /** @private — why a flow entry routes rather than Takes: seq edges project step order (Take the moved
   *  step); a component-sourced action change lives on the components entry (Take that). */
  _routeHint(entry) {
    if (entry.group !== 'flow') return null;
    if (entry.kind === 'seq') return 'Ordering — Take the moved step entry instead.';
    if (entry.component || entry.reference) return `Shared action — Take the components entry (${entry.component ?? entry.reference}).`;
    return null;
  }

  /** @private — a mark glyph + text for one change-list entry. */
  _entryLabel(entry) {
    const groups = entry.changedGroups?.length ? ` — ${entry.changedGroups.join(', ')}` : '';
    if (entry.group === 'steps') {
      if (entry.type === 'added') return { mark: '+', text: entry.rightId };
      if (entry.type === 'removed') return { mark: '−', text: entry.leftId };
      if (entry.type === 'renamed') return { mark: 'renamed', text: `${entry.leftId} → ${entry.rightId}${groups}` };
      if (entry.type === 'moved') return { mark: 'moved', text: `${entry.id} (${(entry.fromIndex ?? 0) + 1} → ${(entry.toIndex ?? 0) + 1})` };
      return { mark: 'Δ', text: `${entry.id}${groups}` };
    }
    if (entry.group === 'flow') {
      const via = entry.component ? ` — via $components.${entry.component}` : '';
      const name = entry.actionName ? ` (${entry.actionName})` : '';
      const edge = `${entry.kind} edge ${entry.from} → ${entry.to}${name}`;
      if (entry.type === 'added') return { mark: '+', text: edge };
      if (entry.type === 'removed') return { mark: '−', text: edge };
      return { mark: 'Δ', text: `${edge}${via}` };
    }
    // workflow surfaces
    if (entry.area === 'components') return { mark: 'Δ', text: `components — ${entry.component}` };
    return { mark: 'Δ', text: `${entry.area}${groups}` };
  }

  /** @private — the canvas targets a change-list entry selects/centres, per side (null = not on that side). */
  _targetsFor(entry) {
    if (entry.group === 'steps') {
      return { left: entry.leftId ? { type: 'node', id: entry.leftId } : null, right: entry.rightId ? { type: 'node', id: entry.rightId } : null };
    }
    if (entry.group === 'flow') {
      return { left: entry.leftId ? { type: 'edge', id: entry.leftId } : null, right: entry.rightId ? { type: 'edge', id: entry.rightId } : null };
    }
    if (entry.area === 'inputs') { const t = { type: 'node', id: '#start' }; return { left: t, right: t }; }
    if (entry.area === 'outputs') { const t = { type: 'node', id: '#end' }; return { left: t, right: t }; }
    if (entry.area === 'defaults') { const t = { type: 'defaults' }; return { left: t, right: t }; }
    return { left: null, right: null }; // components / summary / description have no canvas element
  }

  /** @private — the single overlay surface's target for an entry, resolved through the ghost entry-id map. */
  _overlayTarget(entry) {
    const map = this._ghost.entryMap;
    if (entry.group === 'steps') { const id = map.get(entry.rightId) ?? map.get(entry.leftId); return id ? { type: 'node', id } : null; }
    if (entry.group === 'flow') { const id = map.get(entry.rightId) ?? map.get(entry.leftId); return id ? { type: 'edge', id } : null; }
    if (entry.area === 'inputs') return { type: 'node', id: '#start' };
    if (entry.area === 'outputs') return { type: 'node', id: '#end' };
    if (entry.area === 'defaults') return this._ghost.paint.defaults ? { type: 'defaults' } : null;
    return null; // components / summary / description have no canvas element
  }

  /** @private — select + centre a change-list entry on the side(s) that have it (one surface in overlay). */
  _selectEntry(i) {
    if (i < 0 || i >= this._entries.length) return;
    this._current = i;
    for (const b of this.$$('.cl-item')) b.classList.toggle('current', Number(b.dataset.i) === i);
    this.$(`.cl-item[data-i="${i}"]`)?.scrollIntoView({ block: 'nearest' });
    const entry = this._entries[i];
    if (this._mode === 'overlay') {
      const surface = this.$('.side-overlay arazzo-design-surface');
      if (!surface) return;
      const t = this._overlayTarget(entry);
      surface.selection = t || null;
      if (t?.id) surface.centerOn(t.id);
      return;
    }
    const targets = this._targetsFor(entry);
    for (const which of ['left', 'right']) {
      const surface = this.$(`.side-${which} arazzo-design-surface`);
      if (!surface) continue;
      const t = targets[which];
      surface.selection = t || null;
      if (t?.id) surface.centerOn(t.id);
    }
  }

  /** @private — ‹ Prev / Next › cycle through the entries in order, wrapping deterministically. */
  _cycle(delta) {
    if (!this._entries.length) return;
    const n = this._entries.length;
    this._selectEntry((((this._current < 0 ? (delta > 0 ? -1 : 0) : this._current) + delta) % n + n) % n);
  }

  /** @private */
  _toggleCollapse() {
    const grid = this.$('.grid');
    const collapsed = grid.classList.toggle('cl-collapsed');
    this.$('.cl-collapse').setAttribute('aria-expanded', String(!collapsed));
  }

  // ── Interactive merge (§6.4): the component emits events; the host owns the model ────────────────

  /** @private — a stable key for an entry so Keep survives refresh (never a list index / occurrence). */
  _entryKey(entry) {
    if (entry.group === 'steps') return `steps|${entry.type}|${entry.leftId ?? ''}→${entry.rightId ?? ''}`;
    if (entry.group === 'flow') return `flow|${entry.kind}|${entry.leftFrom ?? entry.rightFrom ?? entry.from}|${entry.to}|${entry.actionName ?? ''}`;
    return `workflow|${entry.area}|${entry.component ?? ''}`;
  }

  /** @private — 'left'/'right' of the side that is NOT the merge target. */
  _otherSide() { return this._mergeSide === 'left' ? 'right' : 'left'; }

  /** @private — the resolved workflow's steps for a side. */
  _stepsOf(side) {
    const doc = this._sides[side]?.document;
    const wf = (doc?.workflows || []).find((w) => w.workflowId === this._workflowIds[side]);
    return wf?.steps || [];
  }

  _workflowOf(side) {
    const doc = this._sides[side]?.document;
    return (doc?.workflows || []).find((w) => w.workflowId === this._workflowIds[side]) || {};
  }

  _stepById(side, id) { return this._stepsOf(side).find((s) => s.stepId === id); }

  /** @private — map a step id on the OTHER side to its id on OUR (merge) side (identity matches + renames). */
  _ourIdOf(theirId) {
    const idMap = this._diff.idMap; // left → right
    if (this._mergeSide === 'left') { for (const [l, r] of idMap) if (r === theirId) return l; return theirId; }
    return idMap.get(theirId) ?? theirId;
  }

  /** @private — our-side insert index for a step present in THEIRS at theirIdx: just after the nearest
   *  preceding their-step that also exists in ours (else 0) — preserves order against common siblings. */
  _ourStepIndex(theirStepId) {
    const their = this._stepsOf(this._otherSide());
    const our = this._stepsOf(this._mergeSide);
    const ti = their.findIndex((s) => s.stepId === theirStepId);
    for (let k = ti - 1; k >= 0; k--) {
      const oi = our.findIndex((s) => s.stepId === this._ourIdOf(their[k].stepId));
      if (oi >= 0) return oi + 1;
    }
    return 0;
  }

  /** @private — our-side insert index for an action Take: after the nearest common preceding action, but
   *  never after a catch-all (an action with no criteria must stay last — §3.2). */
  _actionInsertIndex(ourStepId, list, theirStep, theirRawIndex) {
    const ourList = (this._stepById(this._mergeSide, ourStepId)?.[list]) || [];
    const theirList = theirStep?.[list] || [];
    const nameAt = (a) => a?.name ?? null;
    let idx = ourList.length;
    for (let k = theirRawIndex - 1; k >= 0; k--) {
      const nm = nameAt(theirList[k]);
      const oi = nm != null ? ourList.findIndex((a) => nameAt(a) === nm) : -1;
      if (oi >= 0) { idx = oi + 1; break; }
    }
    const catchAll = ourList.findIndex((a) => !(a?.criteria?.length));
    return catchAll >= 0 ? Math.min(idx, catchAll) : idx;
  }

  /** @private — the `apply` payload for a takeable entry, or null when it isn't takeable (§6.4). Take makes
   *  OUR (merge-target) side match THEIRS for this one entry; indices are freshly derived every call. */
  _takePayload(entry) {
    if (!this._mergeSide) return null;
    const M = this._mergeSide;
    const T = this._otherSide();
    if (entry.group === 'steps') {
      const ourId = M === 'left' ? entry.leftId : entry.rightId;
      const theirId = M === 'left' ? entry.rightId : entry.leftId;
      if (entry.type === 'moved') {
        const id = ourId ?? theirId;
        return { kind: 'move-step', stepId: id, index: this._ourStepIndex(theirId ?? id) };
      }
      const ourHas = !!ourId && !!this._stepById(M, ourId);
      const theirStep = theirId ? this._stepById(T, theirId) : null;
      if (ourHas && !theirStep) return { kind: 'remove-step', stepId: ourId };
      if (!ourHas && theirStep) return { kind: 'insert-step', step: structuredClone(theirStep), index: this._ourStepIndex(theirId) };
      if (ourHas && theirStep) return { kind: 'replace-step', stepId: ourId, step: structuredClone(theirStep) };
      return null;
    }
    if (entry.group === 'flow') {
      if (this._routeHint(entry)) return null; // seq / component-sourced route elsewhere
      const list = entry.list;
      const ourFrom = M === 'left' ? (entry.leftFrom ?? (entry.rightFrom && this._ourIdOf(entry.rightFrom)))
        : (entry.rightFrom ?? (entry.leftFrom && this._ourIdOf(entry.leftFrom)));
      const theirFrom = M === 'left' ? entry.rightFrom : entry.leftFrom;
      const ourRaw = M === 'left' ? entry.leftRawIndex : entry.rightRawIndex;
      const theirRaw = M === 'left' ? entry.rightRawIndex : entry.leftRawIndex;
      const theirStep = theirFrom != null ? this._stepById(T, theirFrom) : null;
      const theirAction = theirStep && theirRaw != null ? theirStep[list]?.[theirRaw] : null;
      if (entry.type === 'removed' && ourFrom != null && ourRaw != null) {
        return { kind: 'remove-action', stepId: ourFrom, list, index: ourRaw };
      }
      if (entry.type === 'added' && ourFrom != null && theirAction) {
        return { kind: 'insert-action', stepId: ourFrom, list, action: structuredClone(theirAction), index: this._actionInsertIndex(ourFrom, list, theirStep, theirRaw) };
      }
      if (entry.type === 'changed' && ourFrom != null && ourRaw != null && theirAction) {
        return { kind: 'replace-action', stepId: ourFrom, list, index: ourRaw, action: structuredClone(theirAction) };
      }
      return null;
    }
    // workflow-area entries
    const theirWf = this._workflowOf(T);
    if (entry.area === 'inputs' || entry.area === 'outputs' || entry.area === 'summary' || entry.area === 'description') {
      return { kind: 'set-area', area: entry.area, value: structuredClone(theirWf[entry.area]) };
    }
    if (entry.area === 'defaults') {
      return { kind: 'set-area', area: 'defaults', value: { successActions: structuredClone(theirWf.successActions), failureActions: structuredClone(theirWf.failureActions) } };
    }
    if (entry.area === 'components' && entry.component) {
      const [clist, name] = entry.component.split('.');
      const value = this._sides[T]?.document?.components?.[clist]?.[name];
      return { kind: 'set-component', list: clist, name, value: value === undefined ? undefined : structuredClone(value) };
    }
    return null;
  }

  /** @private — Take: emit `change-accepted { entry, apply }` and lock ALL verbs until the host's
   *  refresh() arrives (verb serialization — indices are only fresh if no second Take fires meanwhile). */
  _take(i) {
    if (this._verbsLocked || this._mergeDirty) return;
    const entry = this._entries[i];
    const apply = entry && this._takePayload(entry);
    if (!apply) return;
    this._setVerbsLocked(true);
    this.emit('change-accepted', { entry, apply, workflowId: this._workflowIds[this._mergeSide] });
  }

  /** @private — Keep: mark the entry reviewed (session only; the working copy IS the merge state, §9.9). */
  _keep(i) {
    const entry = this._entries[i];
    if (!entry) return;
    this._reviewed.add(this._entryKey(entry));
    this.$(`.cl-item[data-i="${i}"]`)?.classList.add('reviewed');
  }

  /** @private — disable/enable every Take/Keep button (a failed host apply must not strand a stale diff). */
  _setVerbsLocked(locked) {
    this._verbsLocked = locked;
    for (const b of this.$$('.cl-take, .cl-keep')) b.disabled = locked;
  }
}

define('arazzo-workflow-compare', ArazzoWorkflowCompare);
export { ArazzoWorkflowCompare };
