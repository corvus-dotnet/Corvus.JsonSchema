// <arazzo-workflow-add> — the "new workflowId + Add workflow" widget, shared by the document
// inspector's workflows section and the canvas empty-state overlay so both offer the identical
// affordance. On a non-empty submit (the button or Enter) it emits `workflow-add {workflowId}` and
// clears the field; dedup and the actual creation belong to the host, which owns the document and
// knows its existing workflows. The host may flag a rejected id with setError().
//
//   const w = document.createElement('arazzo-workflow-add');
//   w.addEventListener('workflow-add', (e) => addWorkflow(e.detail.workflowId));

import { ArazzoElement, SHARED_CSS, define } from './base.js';

class ArazzoWorkflowAdd extends ArazzoElement {
  connectedCallback() {
    if (this._built) return;
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .pair { display: flex; gap: 8px; align-items: center; }
        /* SHARED_CSS themes select / radio / file inputs but not a plain text input, so style it here to
           the kit frame (matching the document inspector's own inputs). The explicit color also wins over
           an ancestor's muted color inheriting into the shadow (the empty-canvas overlay sets one). */
        .newwf {
          flex: 1 1 auto; min-width: 0; box-sizing: border-box; font: inherit; padding: 6px 9px;
          border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text);
        }
        .newwf:focus { outline: none; border-color: var(--_accent); }
        .addwf { flex: none; white-space: nowrap; }
        .newwf.invalid { border-color: var(--_danger); }
      </style>
      <div class="pair">
        <input class="newwf" type="text" placeholder="new workflowId" aria-label="new workflowId">
        <button class="addwf ghost" type="button">+ Add workflow</button>
      </div>
    `;
    const input = this.$('.newwf');
    const submit = () => {
      const id = input.value.trim();
      if (!id) return;
      this.emit('workflow-add', { workflowId: id });
    };
    this.$('.addwf').addEventListener('click', submit);
    input.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); submit(); } });
    // Typing clears any host-flagged rejection.
    input.addEventListener('input', () => this.setError(null));
  }

  /** Clear the id field (the host calls this once it has accepted the add). */
  clear() { const input = this.$('.newwf'); if (input) input.value = ''; }

  /** Focus the id field (e.g. when the empty-state overlay first appears). */
  focusInput() { this.$('.newwf')?.focus(); }

  /** Flag the field as rejected (e.g. a duplicate id), or clear the flag with null. */
  setError(message) {
    const input = this.$('.newwf');
    if (!input) return;
    input.classList.toggle('invalid', !!message);
    input.title = message || '';
  }
}

define('arazzo-workflow-add', ArazzoWorkflowAdd);
export { ArazzoWorkflowAdd };
