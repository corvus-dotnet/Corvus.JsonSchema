// <arazzo-document-inspector> — the document-level editor (design §5.3): `info`, the
// `sourceDescriptions` a workflow binds by name, the workflows list (add/remove), and the
// `components` reusable library (inputs · parameters · successActions · failureActions) that
// `$components.…` references resolve against.
//
//   const insp = document.createElement('arazzo-document-inspector');
//   insp.stepIds = allStepIds;                 // union across workflows — component action targets
//   insp.workflowIds = allWorkflowIds;
//   insp.completionContext = { … };
//   insp.value = doc;                          // the whole Arazzo document (cloned in)
//   insp.addEventListener('document-changed', (e) => { /* replace via the model */ });
//
// External `.value` sets rebuild; internal edits mutate and emit (focus preserved while typing).
// Component keys are managed here (add/delete; rename = delete + re-add); the step/workflow
// editors AUTHOR `$components.…` references against these keys and localize copies from them.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './action-editor.js';

const COMPONENT_KINDS = [
  ['parameters', 'parameter'],
  ['successActions', 'success action'],
  ['failureActions', 'failure action'],
  ['inputs', 'input schema'],
];

class ArazzoDocumentInspector extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._doc = { arazzo: '1.1.0', info: { title: '', version: '' }, sourceDescriptions: [], workflows: [] };
    /** @private */ this._stepIds = [];
    /** @private */ this._workflowIds = [];
    /** @private */ this._completionContext = {};
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.renderForm();
  }

  /** The whole Arazzo document. Setting rebuilds the form. */
  get value() { return structuredClone(this._doc); }
  set value(doc) {
    this._doc = structuredClone(doc || {});
    if (this.isConnected) { if (!this._built) this.renderShell(); this.renderForm(); }
  }

  /** The union of stepIds across workflows — targets a reusable action may name. */
  set stepIds(ids) { this._stepIds = [...(ids || [])]; }

  /** The document's workflowIds — cross-workflow targets a reusable action may name. */
  set workflowIds(ids) { this._workflowIds = [...(ids || [])]; }

  set completionContext(ctx) { this._completionContext = ctx || {}; }
  get completionContext() { return this._completionContext; }

  /** @private */
  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .form { display: grid; gap: 12px; }
        .form > * { min-width: 0; }
        label { font-size: 11px; color: var(--_muted); display: block; margin-bottom: 2px; }
        h3 { font-size: 11px; letter-spacing: 0.05em; text-transform: uppercase; color: var(--_muted); margin: 6px 0 0; border-top: 1px solid var(--_border); padding-top: 10px; }
        h4 { font-size: 11px; color: var(--_muted); margin: 4px 0 2px; }
        input[type="text"], select, textarea {
          width: 100%; box-sizing: border-box; font: inherit; padding: 6px 9px;
          border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text);
        }
        textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; resize: vertical; }
        textarea.invalid { border-color: var(--_danger); }
        .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
        .pair > * { min-width: 0; }
        .row { display: grid; gap: 6px; align-items: center; margin-bottom: 6px; }
        .row > * { min-width: 0; }
        .hint { font-size: 11px; color: var(--_muted); }
        .mono { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; overflow-wrap: anywhere; }
        .srow { align-items: baseline; }
        .add { font-size: 12px; justify-self: start; }
        .entry { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 8px; display: grid; gap: 6px; min-width: 0; }
        .entry > * { min-width: 0; }
        .entry .ehead { display: flex; align-items: center; gap: 6px; }
        .entry .ehead code { font-size: 12px; font-weight: 600; min-width: 0; overflow-wrap: anywhere; }
        .entry .ehead .spacer { flex: 1; }
        .entry .edel { font-size: 11px; padding: 1px 7px; }
      </style>
      <div class="form" part="form"></div>
    `;
  }

  /** @private */
  renderForm() {
    const d = this._doc;
    const form = this.$('.form');
    form.innerHTML = `
      <div class="hint">arazzo ${escapeHtml(d.arazzo || '1.1.0')}</div>
      <h3>info</h3>
      <div class="pair">
        <div><label>title</label><input class="ititle" type="text" value="${escapeHtml(d.info?.title || '')}"></div>
        <div><label>version</label><input class="iversion" type="text" value="${escapeHtml(d.info?.version || '')}"></div>
      </div>
      <div><label>summary</label><input class="isummary" type="text" value="${escapeHtml(d.info?.summary || '')}"></div>
      <div><label>description</label><input class="idesc" type="text" value="${escapeHtml(d.info?.description || '')}"></div>

      <h3>source descriptions</h3>
      <div class="hint">managed from the Sources panel — attaching a source declares it here; detaching removes the declaration</div>
      <div class="sources"></div>

      <h3>workflows</h3>
      <div class="workflows"></div>
      <div class="pair">
        <input class="newwf" type="text" placeholder="new workflowId">
        <button class="add addwf ghost" type="button">+ Add workflow</button>
      </div>

      <h3>components (reusable library)</h3>
      <div class="hint">referenced as $components.&lt;kind&gt;.&lt;name&gt; from steps and workflows</div>
      <div class="components"></div>
    `;

    const info = (field, cls) => {
      form.querySelector(cls).addEventListener('input', (e) => {
        this._doc.info ??= {};
        if (e.target.value) this._doc.info[field] = e.target.value;
        else delete this._doc.info[field];
        this._emit();
      });
    };
    info('title', '.ititle');
    info('version', '.iversion');
    info('summary', '.isummary');
    info('description', '.idesc');

    this._renderSources();
    this._renderWorkflows();
    this._renderComponents();
  }

  /** @private — READ-ONLY: source descriptions are owned by the Sources panel (attach declares,
   *  detach removes); the settings page only shows what the document binds. */
  _renderSources() {
    const box = this.$('.sources');
    const sources = this._doc.sourceDescriptions || [];
    box.innerHTML = sources.length === 0
      ? '<div class="hint">none yet — attach one from the Sources panel</div>'
      : sources.map((src) => `
        <div class="row srow" style="grid-template-columns: 1fr 1.4fr auto;">
          <span class="sname mono">${escapeHtml(src.name ?? '')}</span>
          <span class="surl mono muted">${escapeHtml(src.url ?? '')}</span>
          <span class="stype muted">${escapeHtml(src.type ?? 'openapi')}</span>
        </div>`).join('');
  }

  /** @private — the workflows list: names + remove; adding appends an empty workflow. */
  _renderWorkflows() {
    const box = this.$('.workflows');
    const workflows = this._doc.workflows || [];
    box.innerHTML = workflows.length
      ? ''
      : '<div class="hint">no workflows yet</div>';
    workflows.forEach((wf, i) => {
      const row = document.createElement('div');
      row.className = 'row';
      row.style.gridTemplateColumns = '1fr auto';
      row.innerHTML = `
        <code>${escapeHtml(wf.workflowId || '(unnamed)')} <span class="hint">· ${wf.steps?.length ?? 0} step${(wf.steps?.length ?? 0) === 1 ? '' : 's'}</span></code>
        <button class="wdel ghost" type="button" title="Remove this workflow (undoable)">✕</button>`;
      row.querySelector('.wdel').addEventListener('click', () => {
        workflows.splice(i, 1);
        this._renderWorkflows();
        this._emit();
      });
      box.append(row);
    });
    this.$('.addwf').onclick = () => {
      const input = this.$('.newwf');
      const id = input.value.trim();
      if (!id || (this._doc.workflows || []).some((w) => w.workflowId === id)) return;
      (this._doc.workflows ??= []).push({ workflowId: id, steps: [] });
      input.value = '';
      this._renderWorkflows();
      this._emit();
    };
  }

  /** @private */
  _renderComponents() {
    const box = this.$('.components');
    box.innerHTML = '';
    for (const [kind, label] of COMPONENT_KINDS) {
      const group = document.createElement('div');
      group.innerHTML = `
        <h4>${kind}</h4>
        <div class="entries"></div>
        <div class="pair">
          <input class="newkey" type="text" placeholder="new ${label} name">
          <button class="add addkey ghost" type="button">+ Add ${label}</button>
        </div>`;
      const entriesBox = group.querySelector('.entries');
      const entries = this._doc.components?.[kind] || {};
      for (const key of Object.keys(entries)) {
        entriesBox.append(this._renderComponentEntry(kind, key));
      }

      group.querySelector('.addkey').addEventListener('click', () => {
        const input = group.querySelector('.newkey');
        const key = input.value.trim();
        if (!key) return;
        this._doc.components ??= {};
        this._doc.components[kind] ??= {};
        if (key in this._doc.components[kind]) {
          // A silent refusal reads as a dead button — flag the duplicate instead.
          input.style.borderColor = 'var(--_danger)';
          input.title = `'${key}' already exists in ${kind}`;
          return;
        }

        input.style.borderColor = '';
        input.title = '';
        this._doc.components[kind][key] = this._defaultComponent(kind, key);
        input.value = '';
        this._renderComponents();
        this._emit();
      });
      box.append(group);
    }
  }

  /** @private */
  _defaultComponent(kind, key) {
    if (kind === 'parameters') return { name: key, value: '' };
    if (kind === 'successActions') return { name: key, type: 'end' };
    if (kind === 'failureActions') return { name: key, type: 'end' };
    return { type: 'object' }; // inputs: a schema
  }

  /** @private */
  _renderComponentEntry(kind, key) {
    const entry = document.createElement('div');
    entry.className = 'entry';
    entry.innerHTML = `
      <div class="ehead">
        <code>$components.${escapeHtml(kind)}.${escapeHtml(key)}</code>
        <span class="spacer"></span>
        <button class="edel ghost" type="button" title="Delete (references to it will dangle — validate flags them)">✕</button>
      </div>
      <div class="econtent"></div>`;
    entry.querySelector('.edel').addEventListener('click', () => {
      delete this._doc.components[kind][key];
      if (!Object.keys(this._doc.components[kind]).length) delete this._doc.components[kind];
      if (!Object.keys(this._doc.components).length) delete this._doc.components;
      this._renderComponents();
      this._emit();
    });

    const content = entry.querySelector('.econtent');
    const value = this._doc.components[kind][key];
    if (kind === 'successActions' || kind === 'failureActions') {
      const editor = document.createElement('arazzo-action-editor');
      editor.kind = kind === 'successActions' ? 'success' : 'failure';
      editor.stepIds = this._stepIds;
      editor.workflowIds = this._workflowIds;
      editor.completionContext = this._completionContext;
      editor.value = value;
      content.append(editor);
      editor.addEventListener('action-changed', (e) => {
        e.stopPropagation();
        this._doc.components[kind][key] = e.detail.action;
        this._emit();
      });
    } else if (kind === 'parameters') {
      content.innerHTML = `
        <div class="row" style="grid-template-columns: 1fr auto 1fr; margin-bottom: 0;">
          <input class="cpname" type="text" placeholder="name" value="${escapeHtml(value.name ?? '')}">
          <select class="cpin">
            ${['', 'path', 'query', 'header', 'cookie', 'querystring'].map((v) => `<option value="${v}" ${v === (value.in ?? '') ? 'selected' : ''}>${v || '(in)'}</option>`).join('')}
          </select>
          <input class="cpvalue" type="text" placeholder="value or $expression" value="${escapeHtml(typeof value.value === 'string' ? value.value : JSON.stringify(value.value ?? ''))}">
        </div>`;
      content.querySelector('.cpname').addEventListener('input', (e) => { value.name = e.target.value; this._emit(); });
      content.querySelector('.cpin').addEventListener('change', (e) => {
        if (e.target.value) value.in = e.target.value;
        else delete value.in;
        this._emit();
      });
      content.querySelector('.cpvalue').addEventListener('input', (e) => { value.value = e.target.value; this._emit(); });
    } else {
      // inputs: a JSON Schema — the guarded JSON editor (typed schema authoring is a later slice).
      content.innerHTML = `
        <textarea rows="4" spellcheck="false">${escapeHtml(JSON.stringify(value, null, 2))}</textarea>
        <div class="hint schema-hint">a JSON Schema; referenced from workflow inputs</div>`;
      const area = content.querySelector('textarea');
      area.addEventListener('input', () => {
        const hint = content.querySelector('.schema-hint');
        try {
          this._doc.components[kind][key] = JSON.parse(area.value);
          area.classList.remove('invalid');
          hint.textContent = 'a JSON Schema; referenced from workflow inputs';
          this._emit();
        } catch (err) {
          area.classList.add('invalid');
          hint.textContent = `not JSON yet: ${String(err.message).slice(0, 80)}`;
        }
      });
    }

    return entry;
  }

  /** @private */
  _emit() {
    this.emit('document-changed', { document: this.value });
  }
}

define('arazzo-document-inspector', ArazzoDocumentInspector);
export { ArazzoDocumentInspector };