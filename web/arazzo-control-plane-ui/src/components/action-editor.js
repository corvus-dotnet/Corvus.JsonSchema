// <arazzo-action-editor> — one Arazzo success/failure action (design §5.3): name, type
// (end | goto | retry — retry only on the failure side), the goto target step, retry settings,
// and the action's criteria (an embedded <arazzo-criteria-editor>). This is the editor the
// drop → select → conditions flow lands on when an action edge is selected.
//
//   const ed = document.createElement('arazzo-action-editor');
//   ed.kind = 'failure';                       // offers retry
//   ed.stepIds = ['validate-order', …];        // step targets (this workflow)
//   ed.workflowIds = ['refund-order', …];      // cross-workflow goto/retry targets
//   ed.completionContext = { … };
//   ed.value = step.onFailure[i];
//   ed.addEventListener('action-changed', (e) => { step.onFailure[i] = e.detail.action; });
//
// External `.value` sets rebuild; internal edits mutate and emit (focus preserved while typing).

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './criteria-editor.js';

class ArazzoActionEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._action = { type: 'end' };
    /** @private */ this._kind = 'success';
    /** @private */ this._stepIds = [];
    /** @private */ this._workflowIds = [];
    /** @private */ this._completionContext = {};
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.renderForm();
  }

  /** The action object. Setting rebuilds the form. */
  get value() { return structuredClone(this._action); }
  set value(action) {
    this._action = structuredClone(action || { type: 'end' });
    if (this.isConnected) { if (!this._built) this.renderShell(); this.renderForm(); }
  }

  /** 'success' | 'failure' — failure actions may retry. */
  get kind() { return this._kind; }
  set kind(value) {
    this._kind = value === 'failure' ? 'failure' : 'success';
    if (this.isConnected && this._built) this.renderForm();
  }

  /** The stepIds offered as goto/retry targets within this workflow. */
  get stepIds() { return [...this._stepIds]; }
  set stepIds(ids) {
    this._stepIds = [...(ids || [])];
    if (this.isConnected && this._built) this.renderForm();
  }

  /** The other workflowIds offered as cross-workflow goto/retry targets. */
  get workflowIds() { return [...this._workflowIds]; }
  set workflowIds(ids) {
    this._workflowIds = [...(ids || [])];
    if (this.isConnected && this._built) this.renderForm();
  }

  /** Schema context for the criteria expression inputs. */
  get completionContext() { return this._completionContext; }
  set completionContext(ctx) {
    this._completionContext = ctx || {};
    const criteria = this.$('arazzo-criteria-editor');
    if (criteria) criteria.completionContext = this._completionContext;
  }

  /** @private */
  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .form { display: grid; gap: 10px; }
        .form > *, .pair > * { min-width: 0; }
        label { font-size: 11px; color: var(--_muted); display: block; margin-bottom: 2px; }
        input[type="text"], input[type="number"] {
          width: 100%; box-sizing: border-box; font: inherit; padding: 6px 9px;
          border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text);
        }
        .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
        .crit label.section { margin-top: 2px; }
      </style>
      <div class="form" part="form"></div>
    `;
  }

  /** @private */
  renderForm() {
    const a = this._action;
    const type = a.type || 'end';
    const types = this._kind === 'failure' ? ['end', 'goto', 'retry'] : ['end', 'goto'];
    const form = this.$('.form');
    form.innerHTML = `
      <div class="pair">
        <div>
          <label>name</label>
          <input class="name" type="text" value="${escapeHtml(a.name || '')}" placeholder="e.g. manual-review-on-decline">
        </div>
        <div>
          <label>type</label>
          <select class="atype">${types.map((t) => `<option ${t === type ? 'selected' : ''}>${t}</option>`).join('')}</select>
        </div>
      </div>
      <div class="target" ${type === 'goto' || type === 'retry' ? '' : 'hidden'}>
        <label>${type === 'retry' ? 'retry from (optional — blank re-runs this step)' : 'goto target'}</label>
        <select class="step">${this._targetOptions(a, type)}</select>
      </div>
      <div class="pair retry" ${type === 'retry' ? '' : 'hidden'}>
        <div>
          <label>retryAfter (seconds)</label>
          <input class="rafter" type="number" min="0" step="1" value="${a.retryAfter ?? ''}">
        </div>
        <div>
          <label>retryLimit</label>
          <input class="rlimit" type="number" min="0" step="1" value="${a.retryLimit ?? ''}">
        </div>
      </div>
      <div class="crit">
        <label class="section">criteria (all must match for the action to fire)</label>
      </div>
    `;

    const criteria = document.createElement('arazzo-criteria-editor');
    criteria.completionContext = this._completionContext;
    criteria.value = a.criteria || [];
    form.querySelector('.crit').append(criteria);

    form.querySelector('.name').addEventListener('input', (e) => {
      if (e.target.value) this._action.name = e.target.value;
      else delete this._action.name;
      this._emit();
    });
    form.querySelector('.atype').addEventListener('change', (e) => {
      this._setActionType(e.target.value, form);
    });
    form.querySelector('.step').addEventListener('change', (e) => {
      // Values are 'step:<id>' | 'workflow:<id>' | '' (retry only: re-run this step).
      const v = e.target.value;
      delete this._action.stepId;
      delete this._action.workflowId;
      if (v.startsWith('step:')) this._action.stepId = v.slice('step:'.length);
      else if (v.startsWith('workflow:')) this._action.workflowId = v.slice('workflow:'.length);
      this._emit();
    });
    form.querySelector('.rafter').addEventListener('input', (e) => {
      if (e.target.value === '') delete this._action.retryAfter;
      else this._action.retryAfter = Number(e.target.value);
      this._emit();
    });
    form.querySelector('.rlimit').addEventListener('input', (e) => {
      if (e.target.value === '') delete this._action.retryLimit;
      else this._action.retryLimit = Number(e.target.value);
      this._emit();
    });
    criteria.addEventListener('criteria-changed', (e) => {
      e.stopPropagation();
      if (e.detail.criteria.length) this._action.criteria = e.detail.criteria;
      else delete this._action.criteria;
      this._emit();
    });
  }

  /** @private — the grouped target options: this workflow's steps, then other workflows. */
  _targetOptions(a, type) {
    const selected = a.stepId ? `step:${a.stepId}` : a.workflowId ? `workflow:${a.workflowId}` : '';
    const optional = type === 'retry' ? `<option value="" ${selected === '' ? 'selected' : ''}>(re-run this step)</option>` : '';
    const steps = this._stepIds.map((id) => `<option value="step:${escapeHtml(id)}" ${selected === `step:${id}` ? 'selected' : ''}>${escapeHtml(id)}</option>`).join('');
    const workflows = this._workflowIds.map((id) => `<option value="workflow:${escapeHtml(id)}" ${selected === `workflow:${id}` ? 'selected' : ''}>${escapeHtml(id)}</option>`).join('');
    return optional
      + (steps ? `<optgroup label="steps">${steps}</optgroup>` : '')
      + (workflows ? `<optgroup label="workflows">${workflows}</optgroup>` : '');
  }

  /** @private — change the action type, pruning fields the new type does not carry. */
  _setActionType(type, form) {
    this._action.type = type;
    if (type !== 'goto' && type !== 'retry') { delete this._action.stepId; delete this._action.workflowId; }
    if (type !== 'retry') { delete this._action.retryAfter; delete this._action.retryLimit; }
    if (type === 'goto' && !this._action.stepId && !this._action.workflowId && this._stepIds.length) {
      this._action.stepId = this._stepIds[0];
    }
    this.renderForm(); // the target label/options depend on the type
    this._emit();
  }

  /** @private */
  _emit() {
    this.emit('action-changed', { action: this.value });
  }
}

define('arazzo-action-editor', ArazzoActionEditor);
export { ArazzoActionEditor };
