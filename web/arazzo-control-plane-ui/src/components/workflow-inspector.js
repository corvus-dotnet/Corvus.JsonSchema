// <arazzo-workflow-inspector> — the workflow-level editor (design §5.3): summary/description,
// the typed `inputs` JSON Schema, the workflow-level successActions/failureActions (the
// inherited-defaults layer the design surface renders), and the workflow `outputs`. Selecting the
// surface background, the start node (inputs), the end node (outputs), or the defaults card all
// land here; `focus-section` hints which section the host wants foremost.
//
//   const insp = document.createElement('arazzo-workflow-inspector');
//   insp.stepIds = wf.steps.map((s) => s.stepId);
//   insp.completionContext = { … };
//   insp.value = wf;                                   // the workflow object (cloned in)
//   insp.addEventListener('workflow-changed', (e) => { doc.workflows[i] = e.detail.workflow; });

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import { buildActionList, ACTION_LIST_CSS } from './action-list.js';
import './outputs-editor.js';

class ArazzoWorkflowInspector extends ArazzoElement {
  static get observedAttributes() { return ['focus-section']; }

  constructor() {
    super();
    /** @private */ this._workflow = { workflowId: '', steps: [] };
    /** @private */ this._completionContext = {};
    /** @private */ this._stepIds = [];
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.renderForm();
  }

  attributeChangedCallback() {
    if (this._built && this.isConnected) this._applyFocus();
  }

  /** The workflow object. Setting rebuilds the form. */
  get value() { return structuredClone(this._workflow); }
  set value(workflow) {
    this._workflow = structuredClone(workflow || { workflowId: '', steps: [] });
    if (this.isConnected) { if (!this._built) this.renderShell(); this.renderForm(); }
  }

  set stepIds(ids) { this._stepIds = [...(ids || [])]; }
  set completionContext(ctx) { this._completionContext = ctx || {}; }
  get completionContext() { return this._completionContext; }

  /** @private */
  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${ACTION_LIST_CSS}
        :host { display: block; }
        .form { display: grid; gap: 12px; }
        label { font-size: 11px; color: var(--_muted); display: block; margin-bottom: 2px; }
        h3 { font-size: 11px; letter-spacing: 0.05em; text-transform: uppercase; color: var(--_muted); margin: 6px 0 0; border-top: 1px solid var(--_border); padding-top: 10px; }
        h3.focused { color: var(--_accent); }
        input[type="text"], textarea {
          width: 100%; box-sizing: border-box; font: inherit; padding: 6px 9px;
          border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text);
        }
        textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; resize: vertical; }
        textarea.invalid { border-color: var(--_danger); }
        .hint { font-size: 11px; color: var(--_muted); }
      </style>
      <div class="form" part="form"></div>
    `;
  }

  /** @private */
  renderForm() {
    const w = this._workflow;
    const form = this.$('.form');
    form.innerHTML = `
      <div>
        <label>summary</label>
        <input class="summary" type="text" value="${escapeHtml(w.summary || '')}">
      </div>
      <div>
        <label>description</label>
        <input class="wdesc" type="text" value="${escapeHtml(w.description || '')}">
      </div>

      <h3 data-section="inputs">inputs (JSON Schema)</h3>
      <div>
        <textarea class="inputs" rows="6" spellcheck="false">${w.inputs !== undefined ? escapeHtml(JSON.stringify(w.inputs, null, 2)) : ''}</textarea>
        <div class="hint inputs-hint">drives the typed inputs form and $inputs completions</div>
      </div>

      <h3 data-section="success">workflow successActions (defaults layer)</h3>
      <div class="wsuccess"></div>
      <h3 data-section="failure">workflow failureActions (defaults layer)</h3>
      <div class="wfailure"></div>

      <h3 data-section="outputs">outputs</h3>
      <div class="wouts"></div>
    `;

    this._mountActionList('wsuccess', 'successActions', 'success');
    this._mountActionList('wfailure', 'failureActions', 'failure');

    const outs = document.createElement('arazzo-outputs-editor');
    outs.completionContext = this._completionContext;
    outs.value = w.outputs || {};
    form.querySelector('.wouts').append(outs);
    outs.addEventListener('outputs-changed', (e) => {
      e.stopPropagation();
      if (Object.keys(e.detail.outputs).length) this._workflow.outputs = e.detail.outputs;
      else delete this._workflow.outputs;
      this._emit();
    });

    form.querySelector('.summary').addEventListener('input', (e) => {
      if (e.target.value) this._workflow.summary = e.target.value;
      else delete this._workflow.summary;
      this._emit();
    });
    form.querySelector('.wdesc').addEventListener('input', (e) => {
      if (e.target.value) this._workflow.description = e.target.value;
      else delete this._workflow.description;
      this._emit();
    });
    const inputs = form.querySelector('.inputs');
    inputs.addEventListener('input', () => {
      const hint = form.querySelector('.inputs-hint');
      if (!inputs.value.trim()) {
        inputs.classList.remove('invalid');
        hint.textContent = 'drives the typed inputs form and $inputs completions';
        delete this._workflow.inputs;
        this._emit();
        return;
      }
      try {
        this._workflow.inputs = JSON.parse(inputs.value);
        inputs.classList.remove('invalid');
        hint.textContent = 'drives the typed inputs form and $inputs completions';
        this._emit();
      } catch (err) {
        inputs.classList.add('invalid');
        hint.textContent = `not JSON yet: ${String(err.message).slice(0, 80)}`;
        // Keep the last valid schema until this parses.
      }
    });

    this._applyFocus();
  }

  /** @private */
  _mountActionList(slotClass, listName, kind) {
    const actions = (this._workflow[listName] ??= []);
    const el = buildActionList({
      actions,
      kind,
      stepIds: this._stepIds,
      completionContext: this._completionContext,
      onChange: () => {
        if (!actions.length) delete this._workflow[listName];
        else this._workflow[listName] = actions;
        this._emit();
      },
    });
    this.$(`.${slotClass}`).replaceChildren(el);
    if (!actions.length) delete this._workflow[listName];
  }

  /** @private — highlight + scroll the section named by the focus-section attribute. */
  _applyFocus() {
    const section = this.getAttribute('focus-section');
    for (const h of this.$$('h3')) {
      const focused = !!section && h.dataset.section === section;
      h.classList.toggle('focused', focused);
      if (focused) h.scrollIntoView({ block: 'nearest' });
    }
  }

  /** @private */
  _emit() {
    this.emit('workflow-changed', { workflow: this.value });
  }
}

define('arazzo-workflow-inspector', ArazzoWorkflowInspector);
export { ArazzoWorkflowInspector };
