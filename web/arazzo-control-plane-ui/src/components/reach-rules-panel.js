// <arazzo-reach-rules-panel> — author the row-security reach vocabulary (§14.2): the named rules a binding
// scopes read/write/purge reach with.
//
//   <arazzo-reach-rules-panel base-url="/arazzo/v1" scopes="security:read security:write"></arazzo-reach-rules-panel>
//
// Attributes : base-url, scopes (gates the mutating controls)
// Properties : .client
// Events     : rules-changed {rules}, loaded {count}, error {problem}
// Parts      : panel, list, row, form
//
// A rule is { name, expression, description? } over the security-rule grammar. Authoring is template-first: pick a
// goal (a label value, a set of values, the caller's claim, sharing any label, ABAC clearance) and the template
// writes the expression, with a live preview and an auto-suggested name; "Advanced" reveals the raw grammar. The
// generated expression is the source of truth sent to the server, which compiles it (a 400 surfaces as a banner).
// (Ordered/classification templates need a configured label ordering and land in a following slice.)

import { ArazzoElement, SHARED_CSS, escapeHtml, confirmDialog, define } from './base.js';

// The template-first goals. `build` writes the expression; `suggest` proposes a name from the fields.
const TEMPLATES = [
  {
    id: 'label-eq', label: 'Rows with a label value',
    build: (f) => `${dim(f, 'domain')} == ${lit(f.value)}`,
    suggest: (f) => `reach-${slug(f.value) || 'label'}`,
  },
  {
    id: 'in', label: 'Rows whose label is one of…',
    build: (f) => `${dim(f, 'tenant')} in (${values(f).map(lit).join(', ')})`,
    suggest: (f) => `reach-${slug(f.dim) || 'set'}`,
  },
  {
    id: 'claim', label: "Rows matching the caller's claim",
    build: (f) => `${dim(f, 'tenant')} == $claim.${(f.claimDim || '').trim() || dim(f, 'tenant')}`,
    suggest: (f) => `${slug(f.dim) || 'tenant'}-scoped`,
  },
  {
    id: 'intersects', label: 'Rows sharing any label with the caller',
    build: () => '$claims.intersects',
    suggest: () => 'shares-a-label',
  },
  {
    id: 'superset', label: 'Caller cleared for every label (ABAC)',
    build: () => '$claims.superset',
    suggest: () => 'abac-superset',
  },
  {
    id: 'advanced', label: 'Advanced — write an expression',
    build: (f) => (f.expression || '').trim(),
    suggest: (f) => f.name || 'reach-rule',
  },
];

const templateById = (id) => TEMPLATES.find((t) => t.id === id) || TEMPLATES[0];
const dim = (f, fallback) => ((f.dim || '').trim() || fallback);
const lit = (v) => `'${String(v ?? '').replace(/'/g, "''")}'`;
const values = (f) => (f.valuesText || '').split(',').map((v) => v.trim()).filter(Boolean);
const slug = (s) => String(s ?? '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

class ArazzoReachRulesPanel extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes'];
  }

  constructor() {
    super();
    /** @private */ this._rules = [];
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._reqSeq = 0;
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

  requestRender() {
    this.load();
  }

  refresh() {
    this.load();
  }

  get canWrite() {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes('security:write');
  }

  async load() {
    const client = this.client;
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url or .client.' };
      this._rules = [];
      this.renderBody();
      return;
    }
    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      const { rules } = await client.listSecurityRules();
      if (seq !== this._reqSeq) return;
      this._rules = rules;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._rules.length });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  // ---- form lifecycle ---------------------------------------------------------------------------

  openCreate() {
    this._form = { mode: 'create', editName: null, template: 'label-eq', name: '', nameEdited: false, formError: null, fields: {} };
    this._form.name = templateById('label-eq').suggest(this._form.fields);
    this.renderForm();
  }

  openEdit(name) {
    const rule = this._rules.find((r) => r.name === name);
    if (!rule) return;
    // Editing an existing rule always uses the advanced (raw expression) form — the template that produced it is not
    // recoverable from the expression, and the name is the immutable key.
    this._form = {
      mode: 'edit', editName: rule.name, template: 'advanced', name: rule.name, nameEdited: true, formError: null,
      fields: { expression: rule.expression, description: rule.description || '' },
    };
    this.renderForm();
  }

  closeForm() {
    this._form = null;
    this.renderForm();
  }

  // The live expression the current form produces — the source of truth sent to the server.
  previewExpression() {
    return this._form ? templateById(this._form.template).build(this._form.fields) : '';
  }

  async submitForm() {
    if (!this._form) return;
    const form = this._form;
    const expression = this.previewExpression();
    const description = (form.fields.description || '').trim();
    if (!expression) {
      form.formError = { title: 'The reach expression is empty.' };
      this.renderForm();
      return;
    }
    try {
      if (form.mode === 'edit') {
        await this.client.updateSecurityRule(form.editName, { expression, description: description || undefined });
      } else {
        const name = (form.name || '').trim();
        if (!name) { form.formError = { title: 'A rule name is required.' }; this.renderForm(); return; }
        await this.client.createSecurityRule({ name, expression, description: description || undefined });
      }
      this._form = null;
      await this.reloadAndEmit();
    } catch (err) {
      form.formError = err.problem || { title: err.message };
      this.renderForm();
      this.emit('error', { problem: form.formError, error: err });
    }
  }

  async deleteRule(name) {
    const confirmed = await confirmDialog(this, {
      title: 'Delete reach rule',
      message: `Delete the security rule '${name}'? Bindings that reference it will lose that reach.`,
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
    this._rules = rules;
    this._error = null;
    this.renderBody();
    this.renderForm();
    this.emit('rules-changed', { rules: this._rules });
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
        .list { display: grid; }
        .rrow { display: flex; align-items: baseline; gap: 10px; padding: 9px 12px; border-bottom: 1px solid var(--_border); }
        .rrow:last-child { border-bottom: none; }
        .rmeta { flex: 1; min-width: 0; }
        .rname { font-weight: 600; }
        .rexpr { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); display: block; margin-top: 2px; overflow-wrap: anywhere; }
        .rdesc { color: var(--_muted); font-size: 12px; margin-top: 2px; }
        .err { margin: 10px 12px; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .form { border-top: 1px solid var(--_border); background: var(--_surface); padding: 12px; display: grid; gap: 10px; }
        .form[hidden] { display: none; }
        .form-head { font-weight: 700; }
        .goal { display: grid; gap: 4px; }
        .goal label { display: flex; align-items: center; gap: 8px; }
        .fields { display: grid; gap: 6px; }
        .field { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
        .field label { min-width: 92px; color: var(--_muted); font-size: 13px; }
        .field input, .field textarea { flex: 1; min-width: 160px; padding: 5px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: inherit; font: inherit; }
        .field textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; min-height: 56px; }
        .preview { font-size: 13px; color: var(--_muted); }
        .preview code { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; color: var(--_text); background: var(--_bg); padding: 2px 6px; border-radius: 5px; border: 1px solid var(--_border); }
        .actions { display: flex; justify-content: flex-end; gap: 8px; }
      </style>
      <div class="panel" part="panel">
        <div class="head">
          <span class="title">Reach (Rules)</span>
          <span class="grow"></span>
          <button class="new primary" type="button" hidden>New reach</button>
        </div>
        <div class="err"></div>
        <div class="list" part="list"></div>
        <div class="form" part="form" hidden></div>
      </div>
    `;
    this.$('.new').addEventListener('click', () => this.openCreate());
  }

  renderBody() {
    const err = this.$('.err');
    const list = this.$('.list');
    const newBtn = this.$('.new');
    if (!list) return;

    err.innerHTML = this._error
      ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
      : '';

    newBtn.hidden = !this.canWrite;

    if (this._loading && this._rules.length === 0) {
      list.innerHTML = '<div class="skl"></div><div class="skl"></div>';
      return;
    }
    if (this._rules.length === 0) {
      list.innerHTML = '<div class="empty">No reach rules defined.</div>';
      return;
    }

    list.innerHTML = this._rules.map((r) => `
      <div class="rrow" part="row">
        <div class="rmeta">
          <span class="rname">${escapeHtml(r.name)}</span>
          <code class="rexpr">${escapeHtml(r.expression)}</code>
          ${r.description ? `<div class="rdesc">${escapeHtml(r.description)}</div>` : ''}
        </div>
        ${this.canWrite ? `
          <button class="edit ghost" type="button" data-name="${escapeHtml(r.name)}">Edit</button>
          <button class="del ghost" type="button" data-name="${escapeHtml(r.name)}">Delete</button>` : ''}
      </div>`).join('');
    this.$$('.edit').forEach((b) => b.addEventListener('click', () => this.openEdit(b.dataset.name)));
    this.$$('.del').forEach((b) => b.addEventListener('click', () => this.deleteRule(b.dataset.name)));
  }

  fieldsHtml(form) {
    const v = (x) => escapeHtml(form.fields[x] || '');
    switch (form.template) {
      case 'label-eq':
        return `
          <div class="field"><label>Dimension</label><input class="f-dim" placeholder="domain" value="${v('dim')}"></div>
          <div class="field"><label>Value</label><input class="f-value" placeholder="payments" value="${v('value')}"></div>`;
      case 'in':
        return `
          <div class="field"><label>Dimension</label><input class="f-dim" placeholder="tenant" value="${v('dim')}"></div>
          <div class="field"><label>Values</label><input class="f-valuesText" placeholder="acme, globex" value="${v('valuesText')}"></div>`;
      case 'claim':
        return `
          <div class="field"><label>Dimension</label><input class="f-dim" placeholder="tenant" value="${v('dim')}"></div>
          <div class="field"><label>Caller claim</label><input class="f-claimDim" placeholder="(same as dimension)" value="${v('claimDim')}"></div>`;
      case 'advanced':
        return `<div class="field"><label>Expression</label><textarea class="f-expression" placeholder="tenant == $claim.tenant">${v('expression')}</textarea></div>`;
      default:
        return ''; // intersects / superset take no fields
    }
  }

  renderForm() {
    const form = this.$('.form');
    if (!form) return;
    if (!this._form) {
      form.hidden = true;
      form.innerHTML = '';
      return;
    }
    const f = this._form;
    const isEdit = f.mode === 'edit';
    form.hidden = false;
    form.innerHTML = `
      <div class="form-head">${isEdit ? `Edit rule '${escapeHtml(f.editName)}'` : 'New reach'}</div>
      ${isEdit ? '' : `
        <div class="goal">
          ${TEMPLATES.map((t) => `
            <label><input type="radio" name="tmpl" value="${t.id}" ${t.id === f.template ? 'checked' : ''}> ${escapeHtml(t.label)}</label>`).join('')}
        </div>`}
      <div class="fields">${this.fieldsHtml(f)}</div>
      <div class="field"><label>Name</label><input class="f-name" value="${escapeHtml(f.name)}" ${isEdit ? 'readonly' : ''}></div>
      <div class="field"><label>Description</label><input class="f-description" placeholder="(optional)" value="${escapeHtml(f.fields.description || '')}"></div>
      <div class="preview">Preview: <code class="preview-expr">${escapeHtml(this.previewExpression() || '—')}</code></div>
      <div class="form-err">${f.formError ? `<div class="error-banner"><span><strong>${escapeHtml(f.formError.title || 'Request failed')}</strong>${f.formError.detail ? ' — ' + escapeHtml(f.formError.detail) : ''}</span></div>` : ''}</div>
      <div class="actions">
        <button class="cancel ghost" type="button">Cancel</button>
        <button class="create primary" type="button">${isEdit ? 'Save' : 'Create'}</button>
      </div>
    `;

    // Template radios re-render the field set (a deliberate change, so focus loss is fine).
    this.$$('input[name="tmpl"]').forEach((r) => r.addEventListener('change', () => {
      f.template = r.value;
      if (!f.nameEdited) f.name = templateById(f.template).suggest(f.fields);
      this.renderForm();
    }));

    // Field inputs update state + the live preview/name in place (no re-render, so focus is kept).
    this.$$('.fields input, .fields textarea').forEach((input) => {
      const key = [...input.classList].find((c) => c.startsWith('f-'))?.slice(2);
      if (!key) return;
      input.addEventListener('input', () => {
        f.fields[key] = input.value;
        const expr = this.$('.preview-expr');
        if (expr) expr.textContent = this.previewExpression() || '—';
        const nameInput = this.$('.f-name');
        if (nameInput && !f.nameEdited && !isEdit) {
          f.name = templateById(f.template).suggest(f.fields);
          nameInput.value = f.name;
        }
      });
    });

    const nameInput = this.$('.f-name');
    if (nameInput && !isEdit) {
      nameInput.addEventListener('input', () => { f.nameEdited = true; f.name = nameInput.value; });
    }
    const descInput = this.$('.f-description');
    if (descInput) descInput.addEventListener('input', () => { f.fields.description = descInput.value; });

    this.$('.cancel').addEventListener('click', () => this.closeForm());
    this.$('.create').addEventListener('click', () => this.submitForm());
  }
}

define('arazzo-reach-rules-panel', ArazzoReachRulesPanel);
export { ArazzoReachRulesPanel };
