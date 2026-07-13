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
import './schema-editor.js';
import './outputs-editor.js';

class ArazzoWorkflowInspector extends ArazzoElement {
  static get observedAttributes() { return ['focus-section']; }

  constructor() {
    super();
    /** @private */ this._workflow = { workflowId: '', steps: [] };
    /** @private */ this._completionContext = {};
    /** @private */ this._stepIds = [];
    /** @private */ this._workflowIds = [];
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

  /** Scope the form to named sections ('summary'|'dependson'|'parameters'|'inputs'|'success'|
   *  'failure'|'outputs'); null renders everything. The canvas anchors use this so selecting
   *  START edits inputs (not outputs — those belong to the END anchor). */
  set only(sections) {
    this._only = sections?.length ? new Set(sections) : null;
    if (this.isConnected && this._built) this.renderForm();
  }

  /** The document's OTHER workflowIds — dependsOn choices and cross-workflow action targets. */
  set workflowIds(ids) { this._workflowIds = [...(ids || [])]; }

  /** The document's components object — enables $components references in the defaults layer. */
  set components(value) { this._components = value || {}; }
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
        .form > * { min-width: 0; }
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
        .chips { display: flex; flex-wrap: wrap; gap: 6px; }
        .wparams { min-width: 0; }
        .wparams > div > * { min-width: 0; }
        .chip { font-size: 12px; padding: 3px 10px; border-radius: 999px; }
        .add-dep { font-size: 12px; max-width: 100%; min-width: 0; }
        .chip.on { border-color: var(--_accent); color: var(--_accent); font-weight: 600; }
      </style>
      <div class="form" part="form"></div>
    `;
  }

  /** @private */
  renderForm() {
    const w = this._workflow;
    const form = this.$('.form');
    const wants = (section) => !this._only || this._only.has(section);
    form.innerHTML = `
      ${wants('summary') ? `
      <div>
        <label>summary</label>
        <input class="summary" type="text" value="${escapeHtml(w.summary || '')}">
      </div>
      <div>
        <label>description</label>
        <input class="wdesc" type="text" value="${escapeHtml(w.description || '')}">
      </div>` : ''}

      ${wants('dependson') ? `
      <h3 data-section="dependson">depends on</h3>
      <div class="hint">workflows that must complete before this one starts</div>
      <div class="wdependson chips"></div>` : ''}

      ${wants('parameters') ? `
      <h3 data-section="parameters">parameters</h3>
      <div class="hint">applied to every step in this workflow (a step's own parameter with the same name and location wins)</div>
      <div class="wparams"></div>
      <button class="addwp ghost" type="button" style="font-size:12px; justify-self:start;">+ Add parameter</button>` : ''}

      ${wants('inputs') ? `
      <h3 data-section="inputs">inputs (JSON Schema)</h3>
      <div>
        <arazzo-schema-editor class="inputs"></arazzo-schema-editor>
        <div class="hint">drives the typed inputs form and $inputs completions</div>
      </div>` : ''}

      ${wants('success') ? `
      <h3 data-section="success">workflow successActions (defaults layer)</h3>
      <div class="wsuccess"></div>` : ''}
      ${wants('failure') ? `
      <h3 data-section="failure">workflow failureActions (defaults layer)</h3>
      <div class="wfailure"></div>` : ''}

      ${wants('outputs') ? `
      <h3 data-section="outputs">outputs</h3>
      <div class="wouts"></div>` : ''}
    `;

    if (wants('dependson')) this._renderDependsOn();
    if (wants('parameters')) this._renderParameters();
    if (wants('success')) this._mountActionList('wsuccess', 'successActions', 'success');
    if (wants('failure')) this._mountActionList('wfailure', 'failureActions', 'failure');

    if (wants('outputs')) {
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
    }

    form.querySelector('.summary')?.addEventListener('input', (e) => {
      if (e.target.value) this._workflow.summary = e.target.value;
      else delete this._workflow.summary;
      this._emit();
    });
    form.querySelector('.wdesc')?.addEventListener('input', (e) => {
      if (e.target.value) this._workflow.description = e.target.value;
      else delete this._workflow.description;
      this._emit();
    });
    const inputs = form.querySelector('arazzo-schema-editor.inputs');
    if (inputs) {
      inputs.emptyDeletes = true; // JSON-tier blank clears workflow.inputs (§3.4)
      inputs.library = this._components?.inputs; // the components.inputs library for the $ref picker (§6)
      inputs.value = this._workflow.inputs ?? { type: 'object' };
      inputs.addEventListener('schema-changed', (e) => {
        const schema = e.detail.schema;
        if (schema === undefined) delete this._workflow.inputs;
        else this._workflow.inputs = schema;
        this._emit();
      });
    }

    this._applyFocus();
  }

  /** @private — actual dependencies render as removable pills; new ones add through an explicit
   *  select (offering every workflow as a toggle chip read as an assertion, not a choice). */
  _renderDependsOn() {
    const box = this.$('.wdependson');
    const current = this._workflow.dependsOn || [];
    box.innerHTML = '';

    if (!current.length) {
      const none = document.createElement('span');
      none.className = 'hint';
      none.textContent = this._workflowIds.length
        ? 'none — this workflow starts independently'
        : 'no other workflows in this document';
      box.append(none);
    }

    for (const id of current) {
      const pill = document.createElement('button');
      pill.type = 'button';
      pill.className = 'chip on';
      pill.title = 'Remove this dependency';
      pill.textContent = `${id} ✕`;
      pill.addEventListener('click', () => {
        const next = current.filter((x) => x !== id);
        if (next.length) this._workflow.dependsOn = next;
        else delete this._workflow.dependsOn;
        this._renderDependsOn();
        this._emit();
      });
      box.append(pill);
    }

    const remaining = this._workflowIds.filter((id) => !current.includes(id));
    if (remaining.length) {
      const select = document.createElement('select');
      select.className = 'add-dep';
      select.innerHTML = `<option value="">+ Add dependency…</option>`
        + remaining.map((id) => `<option value="${escapeHtml(id)}">${escapeHtml(id)}</option>`).join('');
      select.addEventListener('change', () => {
        if (!select.value) return;
        const set = new Set([...current, select.value]);
        this._workflow.dependsOn = this._workflowIds.filter((id) => set.has(id));
        this._renderDependsOn();
        this._emit();
      });
      box.append(select);
    }
  }

  /** @private — the workflow-level parameters (name/in/value; a reusable reference renders read-only). */
  _renderParameters() {
    const box = this.$('.wparams');
    const parameters = this._workflow.parameters || [];
    box.innerHTML = '';
    parameters.forEach((param, i) => {
      const row = document.createElement('div');
      row.style.cssText = 'display:grid; grid-template-columns: 1fr auto auto auto; gap:6px; align-items:center; margin-bottom:6px;';
      if (param.reference != null) {
        row.style.gridTemplateColumns = '1fr auto';
        row.innerHTML = `<span class="hint">↺ ${escapeHtml(param.reference)} <em>reusable — edit in components</em></span>
          <button class="wpdel ghost" type="button" title="Remove">✕</button>`;
      } else {
        row.innerHTML = `
          <input class="wpname" type="text" placeholder="name" value="${escapeHtml(param.name ?? '')}">
          <select class="wpin">
            ${['', 'path', 'query', 'header', 'cookie', 'querystring'].map((v) => `<option value="${v}" ${v === (param.in ?? '') ? 'selected' : ''}>${v || '(in)'}</option>`).join('')}
          </select>
          <input class="wpvalue" type="text" placeholder="value or $expression" value="${escapeHtml(typeof param.value === 'string' ? param.value : JSON.stringify(param.value ?? ''))}">
          <button class="wpdel ghost" type="button" title="Remove">✕</button>`;
        row.querySelector('.wpname').addEventListener('input', (e) => { param.name = e.target.value; this._emit(); });
        row.querySelector('.wpin').addEventListener('change', (e) => {
          if (e.target.value) param.in = e.target.value;
          else delete param.in;
          this._emit();
        });
        row.querySelector('.wpvalue').addEventListener('input', (e) => { param.value = e.target.value; this._emit(); });
      }

      row.querySelector('.wpdel').addEventListener('click', () => {
        parameters.splice(i, 1);
        if (!parameters.length) delete this._workflow.parameters;
        this._renderParameters();
        this._emit();
      });
      box.append(row);
    });
    this.$('.addwp').onclick = () => {
      (this._workflow.parameters ??= []).push({ name: '', value: '' });
      this._renderParameters();
      box.querySelector('div:last-child .wpname')?.focus();
      this._emit();
    };
  }

  /** @private */
  _mountActionList(slotClass, listName, kind) {
    const actions = (this._workflow[listName] ??= []);
    const el = buildActionList({
      actions,
      kind,
      stepIds: this._stepIds,
      workflowIds: this._workflowIds,
      components: (this._components || {})[kind === 'success' ? 'successActions' : 'failureActions'],
      onComponentChange: (name, action) => {
        const kindKey = kind === 'success' ? 'successActions' : 'failureActions';
        (this._components ??= {})[kindKey] ??= {};
        this._components[kindKey][name] = action;
        this.emit('component-changed', { kind: kindKey, name, action });
      },
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
