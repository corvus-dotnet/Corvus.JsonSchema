// <arazzo-workflow-compare> — a reusable SIDE-BY-SIDE comparison of any two workflow versions: two
// read-only design surfaces in one dialog (the current state vs a commit, two commits, two catalog
// versions, or any two documents the host supplies). The git pane's history browser opens it for
// "current vs commit"; the same host serves any pair.
//
// When `diff` is on (the default), it overlays a VISUAL DIFF: `diffWorkflowPair` (workflow-diff.js)
// classifies steps/edges/workflow surfaces, both sides share ONE union layout so matched steps sit
// level, each surface paints its classification via `diffState`, and the head shows a legend of live
// counts plus a "Highlight changes" toggle. `diff: false` reproduces the plain side-by-side view.
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

import { ArazzoElement, SHARED_CSS, define } from './base.js';
import { projectWorkflow } from '../workflow-graph.js';
import { diffWorkflowPair } from '../workflow-diff.js';
import './design-surface.js';

class ArazzoWorkflowCompare extends ArazzoElement {
  /** Opens the comparison; each side projects read-only and (when diff is on) paints the overlay. */
  open({ left, right, workflowId, diff = true } = {}) {
    this.render();
    const leftWorkflowId = this._resolveWorkflowId(left, workflowId);
    const rightWorkflowId = this._resolveWorkflowId(right, workflowId);
    this._diff = diff ? diffWorkflowPair(left?.document ?? {}, right?.document ?? {}, { leftWorkflowId, rightWorkflowId }) : null;
    this._highlight = !!this._diff;

    this.renderSide('.side-left', left, leftWorkflowId, 'left');
    this.renderSide('.side-right', right, rightWorkflowId, 'right');
    this._renderLegend();

    this.$('dialog').showModal();
    // Fit after the dialog lays out — a surface fits against its own measured viewport.
    requestAnimationFrame(() => {
      for (const surface of this.$$('arazzo-design-surface')) surface.fit();
    });
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
          <button class="close ghost" type="button" title="Close">✕</button>
        </div>
        <div class="grid">
          <div class="side side-left"><div class="side-head"></div></div>
          <div class="side side-right"><div class="side-head"></div></div>
        </div>
      </dialog>`;
    this.$('.close').addEventListener('click', () => this.close());
    this.$('.hl').addEventListener('click', () => this._toggleHighlight());
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
}

define('arazzo-workflow-compare', ArazzoWorkflowCompare);
export { ArazzoWorkflowCompare };
