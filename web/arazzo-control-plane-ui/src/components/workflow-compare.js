// <arazzo-workflow-compare> — a reusable SIDE-BY-SIDE visualization of any two workflow versions
// (snag 9): two read-only design surfaces in one dialog — the current state vs a commit, two
// commits, or any two documents the host supplies. The git pane's history browser opens it for
// "current vs commit"; the same host serves any pair (catalog versions, two commits) later, and a
// visual DIFF overlay is the designed follow-on (the surfaces already accept per-node state).
//
//   const dlg = document.createElement('arazzo-workflow-compare');
//   host.appendChild(dlg);
//   dlg.open({
//     left:  { label: 'Working copy (current)', document: currentDoc },
//     right: { label: 'a3f1c9d — Route failures to review', document: commitDoc },
//     workflowId: 'adopt-pet',                          // optional: defaults to each side's first workflow
//   });
//
// Methods : open({left, right, workflowId?}) · close()
// Sides   : { label: string, document: object, workflowId?: string } — a per-side workflowId wins
//           over the shared one (compare a renamed workflow across versions).

import { ArazzoElement, SHARED_CSS, define } from './base.js';
import { projectWorkflow } from '../workflow-graph.js';
import './design-surface.js';

class ArazzoWorkflowCompare extends ArazzoElement {
  /** Opens the comparison; each side projects read-only and fits itself into view. */
  open({ left, right, workflowId } = {}) {
    this.render();
    this.renderSide('.side-left', left, workflowId);
    this.renderSide('.side-right', right, workflowId);
    this.$('dialog').showModal();
    // Fit after the dialog lays out — a surface fits against its own measured viewport.
    requestAnimationFrame(() => {
      for (const surface of this.$$('arazzo-design-surface')) surface.fit();
    });
  }

  close() { this.$('dialog')?.close(); }

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: 10px; background: var(--_bg); color: inherit; padding: 0;
                 width: min(94vw, 1500px); height: 86vh; max-height: 86vh; display: grid; grid-template-rows: auto minmax(0, 1fr); }
        dialog:not([open]) { display: none; }
        dialog::backdrop { background: rgb(0 0 0 / 0.35); }
        .head { display: flex; align-items: center; gap: 8px; padding: 10px 14px; border-bottom: 1px solid var(--_border); }
        .head h2 { margin: 0 auto 0 0; font-size: 14px; }
        .grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); grid-template-rows: minmax(0, 1fr); min-height: 0; }
        .side { display: grid; grid-template-rows: auto minmax(0, 1fr); min-width: 0; min-height: 0; }
        .side + .side { border-left: 1px solid var(--_border); }
        .side-head { padding: 6px 12px; font-size: 12px; color: var(--_muted); border-bottom: 1px solid var(--_border);
                     overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .side arazzo-design-surface { display: block; width: 100%; height: 100%; min-height: 0; }
      </style>
      <dialog>
        <div class="head">
          <h2>Compare workflow versions</h2>
          <button class="close ghost" type="button" title="Close">✕</button>
        </div>
        <div class="grid">
          <div class="side side-left"><div class="side-head"></div></div>
          <div class="side side-right"><div class="side-head"></div></div>
        </div>
      </dialog>`;
    this.$('.close').addEventListener('click', () => this.close());
  }

  /** @private — one side: label + a read-only projection of its document's workflow. */
  renderSide(sel, side, sharedWorkflowId) {
    const host = this.$(sel);
    host.querySelector('.side-head').textContent = side?.label ?? '';
    host.querySelector('.side-head').title = side?.label ?? '';
    const doc = side?.document ?? {};
    const workflowId = side?.workflowId ?? sharedWorkflowId ?? doc.workflows?.[0]?.workflowId ?? '';
    const surface = document.createElement('arazzo-design-surface');
    surface.setAttribute('readonly', '');
    surface.graph = projectWorkflow(doc, workflowId);
    host.appendChild(surface);
  }
}

define('arazzo-workflow-compare', ArazzoWorkflowCompare);
export { ArazzoWorkflowCompare };