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
import { diffWorkflowPair } from '../workflow-diff.js';
import './design-surface.js';

const GROUPS = [['steps', 'Steps'], ['flow', 'Flow'], ['workflow', 'Workflow']];

class ArazzoWorkflowCompare extends ArazzoElement {
  /** Opens the comparison; each side projects read-only and (when diff is on) paints the overlay. */
  open({ left, right, workflowId, diff = true } = {}) {
    this.render();
    const leftWorkflowId = this._resolveWorkflowId(left, workflowId);
    const rightWorkflowId = this._resolveWorkflowId(right, workflowId);
    this._diff = diff ? diffWorkflowPair(left?.document ?? {}, right?.document ?? {}, { leftWorkflowId, rightWorkflowId }) : null;
    this._highlight = !!this._diff;
    this._entries = this._diff ? [...this._diff.steps, ...this._diff.flow, ...this._diff.workflow] : [];
    this._current = -1;

    this.renderSide('.side-left', left, leftWorkflowId, 'left');
    this.renderSide('.side-right', right, rightWorkflowId, 'right');
    this._renderLegend();
    this._renderChangeList();

    this.$('dialog').showModal();
    this._wireViewSync();
    // Fit after the dialog lays out. With a diff, ONE fit over the union extent is assigned to both
    // surfaces (§4.6 shared fit) so matched steps sit level; without one, each side fits itself.
    requestAnimationFrame(() => this._sharedFit());
  }

  close() { this.$('dialog')?.close(); }

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
        .grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); grid-template-rows: minmax(0, 1fr); min-height: 0; }
        .grid.has-diff { grid-template-columns: var(--cl-w, 260px) minmax(0, 1fr) minmax(0, 1fr); }
        .grid:not(.has-diff) .changelist { display: none; }
        .grid.cl-collapsed { --cl-w: 44px; }
        .changelist { display: grid; grid-template-rows: auto minmax(0, 1fr); min-height: 0; border-right: 1px solid var(--_border); }
        .cl-head { display: flex; align-items: center; gap: 4px; padding: 6px 8px; border-bottom: 1px solid var(--_border); font-size: 12px; }
        .cl-title { margin-right: auto; font-weight: 600; }
        .grid.cl-collapsed .cl-title, .grid.cl-collapsed .cl-prev, .grid.cl-collapsed .cl-next, .grid.cl-collapsed .cl-body { display: none; }
        .cl-body { overflow: auto; padding: 4px 0 8px; }
        .cl-group { padding: 6px 10px 2px; font-size: 10px; text-transform: uppercase; letter-spacing: 0.05em; color: var(--_muted); }
        .cl-item { display: block; width: 100%; text-align: left; border: none; background: none; color: inherit; cursor: pointer;
                   font: 12px var(--_font); padding: 3px 10px; }
        .cl-item:hover { background: var(--_surface); }
        .cl-item.current { background: var(--_surface); box-shadow: inset 3px 0 0 var(--_accent); }
        .cl-mark { font-weight: 700; }
        .cl-mark.added { color: var(--arazzo-diff-added, var(--arazzo-status-completed, #2a8a4a)); }
        .cl-mark.removed { color: var(--arazzo-diff-removed, var(--arazzo-status-faulted, #d4351c)); }
        .cl-mark.changed, .cl-mark.renamed, .cl-mark.moved { color: var(--arazzo-diff-changed, var(--arazzo-status-suspended, #b07d18)); }
        .side { display: grid; grid-template-rows: auto minmax(0, 1fr); min-width: 0; min-height: 0; }
        .side + .side { border-left: 1px solid var(--_border); }
        .side-head { padding: 6px 12px; font-size: 12px; color: var(--_muted); border-bottom: 1px solid var(--_border);
                     overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .side arazzo-design-surface { display: block; width: 100%; height: 100%; min-height: 0; }
        .hl[aria-pressed="false"] { opacity: 0.6; text-decoration: line-through; }
        [hidden] { display: none !important; }
      </style>
      <dialog>
        <div class="head">
          <h2>Compare workflow versions</h2>
          <span class="legend"></span>
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
          <div class="side side-left"><div class="side-head"></div></div>
          <div class="side side-right"><div class="side-head"></div></div>
        </div>
      </dialog>`;
    this.$('.close').addEventListener('click', () => this.close());
    this.$('.hl').addEventListener('click', () => this._toggleHighlight());
    this.$('.sync').addEventListener('click', () => this._toggleSync());
    this.$('.cl-prev').addEventListener('click', () => this._cycle(-1));
    this.$('.cl-next').addEventListener('click', () => this._cycle(1));
    this.$('.cl-collapse').addEventListener('click', () => this._toggleCollapse());
    this.$('.cl-body').addEventListener('click', (e) => {
      const item = e.target.closest('.cl-item');
      if (item) this._selectEntry(Number(item.dataset.i));
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

  /** @private — the "Highlight changes" toggle: off clears diffState on both surfaces; the legend stays. */
  _toggleHighlight() {
    if (!this._diff) return;
    this._highlight = !this._highlight;
    this.$('.hl').setAttribute('aria-pressed', String(this._highlight));
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
    this._syncViews = true;
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

  /** @private — the grouped change list (§4.5 / §6 item 4). */
  _renderChangeList() {
    const grid = this.$('.grid');
    grid.classList.toggle('has-diff', !!this._diff);
    if (!this._diff) return;
    this.$('.cl-title').textContent = `Changes (${this._entries.length})`;
    let html = '';
    for (const [group, heading] of GROUPS) {
      const items = this._entries.map((e, i) => [e, i]).filter(([e]) => e.group === group);
      if (!items.length) continue;
      html += `<div class="cl-group">${heading}</div>`;
      for (const [entry, i] of items) {
        const { mark, text } = this._entryLabel(entry);
        html += `<button class="cl-item" type="button" data-i="${i}"><span class="cl-mark ${entry.type}">${escapeHtml(mark)}</span> ${escapeHtml(text)}</button>`;
      }
    }
    this.$('.cl-body').innerHTML = html || '<div class="cl-group">No differences</div>';
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

  /** @private — select + centre a change-list entry on the side(s) that have it. */
  _selectEntry(i) {
    if (i < 0 || i >= this._entries.length) return;
    this._current = i;
    for (const b of this.$$('.cl-item')) b.classList.toggle('current', Number(b.dataset.i) === i);
    this.$(`.cl-item[data-i="${i}"]`)?.scrollIntoView({ block: 'nearest' });
    const targets = this._targetsFor(this._entries[i]);
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
}

define('arazzo-workflow-compare', ArazzoWorkflowCompare);
export { ArazzoWorkflowCompare };
