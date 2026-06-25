// <arazzo-scopes-panel> — manage reusable access scopes (design §14.2): named row-filter expressions a grant binds
// per action. "Scope" is the user-facing term for what the API calls a security rule.
//
//   <arazzo-scopes-panel base-url="/arazzo/v1" scopes-write="security:write"></arazzo-scopes-panel>
//
// Attributes : base-url, scopes (gates the mutating controls)
// Properties : .client
// Events     : scopes-changed {scopes}, loaded {count}, error {problem}
// Parts      : panel, list, row
//
// A scope is { name, expression } over the row-filter grammar. Authoring is template-first in a modal editor (the
// dialog pattern the credential editor uses): pick a goal and the template writes the expression with a live preview;
// "Advanced" reveals the raw grammar. The list is searchable so it stays usable at hundreds of scopes.

import { ArazzoElement, SHARED_CSS, escapeHtml, confirmDialog, define } from './base.js';

// The template-first goals. `build` writes the expression; `suggest` proposes a name from the fields.
const TEMPLATES = [
  { id: 'label-eq', label: 'Records with a label value', build: (f) => `${dim(f, 'domain')} == ${lit(f.value)}`, suggest: (f) => `scope-${slug(f.value) || 'label'}` },
  { id: 'in', label: 'Records whose label is one of…', build: (f) => `${dim(f, 'tenant')} in (${values(f).map(lit).join(', ')})`, suggest: (f) => `scope-${slug(f.dim) || 'set'}` },
  { id: 'claim', label: "Records matching the caller's claim", build: (f) => `${dim(f, 'tenant')} == $claim.${(f.claimDim || '').trim() || dim(f, 'tenant')}`, suggest: (f) => `${slug(f.dim) || 'tenant'}-scoped` },
  { id: 'intersects', label: 'Records sharing any label with the caller', build: () => '$claims.intersects', suggest: () => 'shares-a-label' },
  { id: 'superset', label: 'Caller cleared for every label (ABAC)', build: () => '$claims.superset', suggest: () => 'abac-superset' },
  { id: 'ordered', label: 'Records at a classification level', build: (f) => `${(f.dim || '').trim()} ${f.comparator || '<='} ${lit(f.value)}`, suggest: (f) => `scope-${slug(f.value) || 'level'}` },
  { id: 'advanced', label: 'Advanced — write an expression', build: (f) => (f.expression || '').trim(), suggest: (f) => f.name || 'scope' },
];

const COMPARATORS = [
  { op: '<=', label: 'at or below' },
  { op: '<', label: 'below' },
  { op: '>=', label: 'at or above' },
  { op: '>', label: 'above' },
];

const templateById = (id) => TEMPLATES.find((t) => t.id === id) || TEMPLATES[0];
const dim = (f, fallback) => ((f.dim || '').trim() || fallback);
const lit = (v) => `'${String(v ?? '').replace(/'/g, "''")}'`;
const values = (f) => (f.valuesText || '').split(',').map((v) => v.trim()).filter(Boolean);
const slug = (s) => String(s ?? '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

class ArazzoScopesPanel extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes'];
  }

  constructor() {
    super();
    /** @private */ this._scopes = [];
    /** @private */ this._orderings = [];
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._reqSeq = 0;
    /** @private */ this._query = '';
    /** @private */ this._form = null;
  }

  connectedCallback() {
    this.renderShell();
    this.load();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'scopes') this.renderBody();
    else this.load();
  }

  requestRender() { this.load(); }

  refresh() { this.load(); }

  get canWrite() {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes('security:write');
  }

  async load() {
    const client = this.client;
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url or .client.' };
      this._scopes = [];
      this.renderBody();
      return;
    }
    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      const [{ rules }, orderingsResult] = await Promise.all([
        client.listSecurityRules(),
        client.listSecurityOrderings().catch(() => ({ orderings: [] })),
      ]);
      if (seq !== this._reqSeq) return;
      this._scopes = rules;
      this._orderings = orderingsResult.orderings ?? [];
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._scopes.length });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  filtered() {
    const q = this._query.trim().toLowerCase();
    if (!q) return this._scopes;
    return this._scopes.filter((s) => `${s.name} ${s.expression} ${s.description || ''}`.toLowerCase().includes(q));
  }

  // ---- editor (modal dialog) --------------------------------------------------------------------

  availableTemplates() {
    return this._orderings.length > 0 ? TEMPLATES : TEMPLATES.filter((t) => t.id !== 'ordered');
  }

  labelsFor(dimension) {
    return this._orderings.find((o) => o.dimension === dimension)?.labels ?? [];
  }

  openCreate() {
    this._form = { mode: 'create', editName: null, template: 'label-eq', name: '', nameEdited: false, formError: null, fields: {} };
    this._form.name = templateById('label-eq').suggest(this._form.fields);
    this.renderEditor();
    this.$('dialog').showModal();
  }

  openEdit(name) {
    const scope = this._scopes.find((s) => s.name === name);
    if (!scope) return;
    this._form = {
      mode: 'edit', editName: scope.name, template: 'advanced', name: scope.name, nameEdited: true, formError: null,
      fields: { expression: scope.expression, description: scope.description || '' },
    };
    this.renderEditor();
    this.$('dialog').showModal();
  }

  closeEditor() {
    this.$('dialog')?.close();
    this._form = null;
  }

  previewExpression() {
    return this._form ? templateById(this._form.template).build(this._form.fields) : '';
  }

  async submitForm() {
    if (!this._form) return;
    const form = this._form;
    const expression = this.previewExpression();
    const description = (form.fields.description || '').trim();
    if (!expression) { form.formError = { title: 'The scope expression is empty.' }; this.renderEditor(); return; }
    try {
      if (form.mode === 'edit') {
        await this.client.updateSecurityRule(form.editName, { expression, description: description || undefined });
      } else {
        const name = (form.name || '').trim();
        if (!name) { form.formError = { title: 'A scope name is required.' }; this.renderEditor(); return; }
        await this.client.createSecurityRule({ name, expression, description: description || undefined });
      }
      this.closeEditor();
      await this.reloadAndEmit();
    } catch (err) {
      form.formError = err.problem || { title: err.message };
      this.renderEditor();
      this.emit('error', { problem: form.formError, error: err });
    }
  }

  async deleteScope(name) {
    const confirmed = await confirmDialog(this, {
      title: 'Delete scope',
      message: `Delete the scope '${name}'? Grants that reference it will lose that reach.`,
      confirmLabel: 'Delete', danger: true,
    });
    if (!confirmed) return;
    try {
      await this.client.deleteSecurityRule(name);
      await this.reloadAndEmit();
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  async reloadAndEmit() {
    const { rules } = await this.client.listSecurityRules();
    this._scopes = rules;
    this._error = null;
    this.renderBody();
    this.emit('scopes-changed', { scopes: this._scopes });
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        .head { padding: 10px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); display: flex; align-items: center; gap: 8px; }
        .head .title { font-weight: 700; }
        .head .grow { flex: 1; }
        .search { font: inherit; font-size: 13px; padding: 5px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: inherit; width: 180px; }
        .list { display: grid; }
        .srow { display: flex; align-items: baseline; gap: 10px; padding: 9px 12px; border-bottom: 1px solid var(--_border); }
        .srow:last-child { border-bottom: none; }
        .smeta { flex: 1; min-width: 0; }
        .sname { font-weight: 600; }
        .sexpr { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); display: block; margin-top: 2px; overflow-wrap: anywhere; }
        .sdesc { color: var(--_muted); font-size: 12px; margin-top: 2px; }
        .err { margin: 10px 12px; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }

        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(560px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .dhead { padding: 14px 16px; border-bottom: 1px solid var(--_border); font-weight: 700; font-size: 15px; }
        .content { padding: 16px; display: grid; gap: 14px; max-height: 64vh; overflow: auto; }
        .goal { display: grid; gap: 4px; }
        .goal label { display: flex; align-items: center; gap: 8px; font-size: 13px; }
        .fields { display: grid; gap: 8px; }
        .field { display: grid; gap: 4px; }
        .field > span { font-size: 12px; color: var(--_muted); }
        .field input, .field select, .field textarea { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); box-sizing: border-box; }
        .field input[readonly] { background: var(--_surface); color: var(--_muted); }
        .field textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; min-height: 56px; }
        .preview { font-size: 13px; color: var(--_muted); }
        .preview code { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; color: var(--_text); background: var(--_surface); padding: 2px 6px; border-radius: 5px; border: 1px solid var(--_border); }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <div class="panel" part="panel">
        <div class="head">
          <span class="title">Scopes</span>
          <span class="grow"></span>
          <input class="search" type="search" placeholder="Search scopes…" aria-label="Search scopes">
          <button class="new primary" type="button" hidden>New scope</button>
        </div>
        <div class="err"></div>
        <div class="list" part="list"></div>
      </div>
      <dialog part="dialog">
        <div class="dhead"></div>
        <div class="content"></div>
        <div class="foot">
          <button class="cancel ghost" type="button">Cancel</button>
          <button class="confirm primary" type="button">Create</button>
        </div>
      </dialog>
    `;
    this.$('.new').addEventListener('click', () => this.openCreate());
    this.$('.search').addEventListener('input', (e) => { this._query = e.target.value; this.renderBody(); });
    this.$('.cancel').addEventListener('click', () => this.closeEditor());
    this.$('.confirm').addEventListener('click', () => this.submitForm());
    this.$('dialog').addEventListener('close', () => { this._form = null; });
  }

  renderBody() {
    const err = this.$('.err');
    const list = this.$('.list');
    if (!list) return;
    this.$('.new').hidden = !this.canWrite;

    err.innerHTML = this._error
      ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
      : '';

    if (this._loading && this._scopes.length === 0) {
      list.innerHTML = '<div class="skl"></div><div class="skl"></div>';
      return;
    }
    const items = this.filtered();
    if (items.length === 0) {
      list.innerHTML = `<div class="empty">${this._scopes.length === 0 ? 'No scopes defined.' : 'No scopes match your search.'}</div>`;
      return;
    }

    list.innerHTML = items.map((s) => `
      <div class="srow" part="row">
        <div class="smeta">
          <span class="sname">${escapeHtml(s.name)}</span>
          <code class="sexpr">${escapeHtml(s.expression)}</code>
          ${s.description ? `<div class="sdesc">${escapeHtml(s.description)}</div>` : ''}
        </div>
        ${this.canWrite ? `
          <button class="edit ghost" type="button" data-name="${escapeHtml(s.name)}">Edit</button>
          <button class="del ghost" type="button" data-name="${escapeHtml(s.name)}">Delete</button>` : ''}
      </div>`).join('');
    this.$$('.edit').forEach((b) => b.addEventListener('click', () => this.openEdit(b.dataset.name)));
    this.$$('.del').forEach((b) => b.addEventListener('click', () => this.deleteScope(b.dataset.name)));
  }

  fieldsHtml(form) {
    const v = (x) => escapeHtml(form.fields[x] || '');
    switch (form.template) {
      case 'label-eq':
        return `
          <div class="field"><span>Dimension</span><input class="f-dim" placeholder="domain" value="${v('dim')}"></div>
          <div class="field"><span>Value</span><input class="f-value" placeholder="payments" value="${v('value')}"></div>`;
      case 'ordered': {
        const dims = this._orderings.map((o) => o.dimension);
        const d = dims.includes(form.fields.dim) ? form.fields.dim : (dims[0] || '');
        form.fields.dim = d;
        const labels = this.labelsFor(d);
        const value = labels.includes(form.fields.value) ? form.fields.value : (labels[0] || '');
        form.fields.value = value;
        const comparator = COMPARATORS.some((c) => c.op === form.fields.comparator) ? form.fields.comparator : '<=';
        form.fields.comparator = comparator;
        return `
          <div class="field"><span>Dimension</span><select class="f-dim">${dims.map((dm) => `<option ${dm === d ? 'selected' : ''}>${escapeHtml(dm)}</option>`).join('')}</select></div>
          <div class="field"><span>Level is</span><select class="f-comparator">${COMPARATORS.map((c) => `<option value="${c.op}" ${c.op === comparator ? 'selected' : ''}>${escapeHtml(c.label)}</option>`).join('')}</select></div>
          <div class="field"><span>Value</span><select class="f-value">${labels.map((l) => `<option ${l === value ? 'selected' : ''}>${escapeHtml(l)}</option>`).join('')}</select></div>`;
      }
      case 'in':
        return `
          <div class="field"><span>Dimension</span><input class="f-dim" placeholder="tenant" value="${v('dim')}"></div>
          <div class="field"><span>Values</span><input class="f-valuesText" placeholder="acme, globex" value="${v('valuesText')}"></div>`;
      case 'claim':
        return `
          <div class="field"><span>Dimension</span><input class="f-dim" placeholder="tenant" value="${v('dim')}"></div>
          <div class="field"><span>Caller claim</span><input class="f-claimDim" placeholder="(same as dimension)" value="${v('claimDim')}"></div>`;
      case 'advanced':
        return `<div class="field"><span>Expression</span><textarea class="f-expression" placeholder="tenant == $claim.tenant">${v('expression')}</textarea></div>`;
      default:
        return '';
    }
  }

  renderEditor() {
    const content = this.$('.content');
    const f = this._form;
    if (!content || !f) return;
    const isEdit = f.mode === 'edit';
    this.$('.dhead').textContent = isEdit ? `Edit scope '${f.editName}'` : 'New scope';
    this.$('.confirm').textContent = isEdit ? 'Save' : 'Create';
    content.innerHTML = `
      ${isEdit ? '' : `
        <div class="goal">
          ${this.availableTemplates().map((t) => `<label><input type="radio" name="tmpl" value="${t.id}" ${t.id === f.template ? 'checked' : ''}> ${escapeHtml(t.label)}</label>`).join('')}
        </div>`}
      <div class="fields">${this.fieldsHtml(f)}</div>
      <div class="field"><span>Name</span><input class="f-name" value="${escapeHtml(f.name)}" ${isEdit ? 'readonly' : ''}></div>
      <div class="field"><span>Description</span><input class="f-description" placeholder="(optional)" value="${escapeHtml(f.fields.description || '')}"></div>
      <div class="preview">Preview: <code class="preview-expr">${escapeHtml(this.previewExpression() || '—')}</code></div>
      <div class="form-err">${f.formError ? `<div class="error-banner"><span><strong>${escapeHtml(f.formError.title || 'Request failed')}</strong>${f.formError.detail ? ' — ' + escapeHtml(f.formError.detail) : ''}</span></div>` : ''}</div>
    `;

    content.querySelectorAll('input[name="tmpl"]').forEach((r) => r.addEventListener('change', () => {
      f.template = r.value;
      if (!f.nameEdited) f.name = templateById(f.template).suggest(f.fields);
      this.renderEditor();
    }));

    content.querySelectorAll('.fields input, .fields textarea, .fields select').forEach((input) => {
      const key = [...input.classList].find((c) => c.startsWith('f-'))?.slice(2);
      if (!key) return;
      input.addEventListener('input', () => {
        f.fields[key] = input.value;
        if (f.template === 'ordered' && key === 'dim') {
          f.fields.value = '';
          if (!f.nameEdited) f.name = templateById(f.template).suggest(f.fields);
          this.renderEditor();
          return;
        }
        const expr = content.querySelector('.preview-expr');
        if (expr) expr.textContent = this.previewExpression() || '—';
        const nameInput = content.querySelector('.f-name');
        if (nameInput && !f.nameEdited && !isEdit) { f.name = templateById(f.template).suggest(f.fields); nameInput.value = f.name; }
      });
    });

    const nameInput = content.querySelector('.f-name');
    if (nameInput && !isEdit) nameInput.addEventListener('input', () => { f.nameEdited = true; f.name = nameInput.value; });
    const descInput = content.querySelector('.f-description');
    if (descInput) descInput.addEventListener('input', () => { f.fields.description = descInput.value; });
  }
}

define('arazzo-scopes-panel', ArazzoScopesPanel);
export { ArazzoScopesPanel };
