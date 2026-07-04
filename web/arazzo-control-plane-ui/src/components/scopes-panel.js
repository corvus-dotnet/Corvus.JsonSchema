// <arazzo-rules-panel> — manage reusable reach RULES (design §14.2): named row-filter expressions a grant binds per
// verb. "Rule" is the user-facing term (it matches the API's security rule). Registered as `arazzo-rules-panel`
// (primary); `arazzo-scopes-panel` is kept as a deprecated alias so kit consumers don't break. A rule is reach (which
// rows), never capability (which operations) — that lives in the token (§14.1).
//
//   <arazzo-rules-panel base-url="/arazzo/v1" scopes="security:read security:write"></arazzo-rules-panel>
//
// Attributes : base-url, scopes (gates the mutating controls)
// Properties : .client
// Events     : scopes-changed {scopes}, loaded {count}, error {problem}
// Parts      : panel, list, row, detail
//
// A master-detail over the rule list (the same shape as the Grants / Catalog / Connections surfaces): a selectable list on
// the left, a detail pane on the right that AUTHORS the selected rule in place — no modal. A rule is
// { name, expression } over the row-filter grammar. Authoring is template-first: pick a goal and the template writes the
// expression with a live preview; "Advanced" reveals the raw grammar. The name is the immutable key (read-only on edit).

import { ArazzoElement, SHARED_CSS, PAGER_CSS, escapeHtml, confirmDialog, define } from './base.js';
import './pager.js';

// How long to wait after the last keystroke before issuing a server-side search (so typing doesn't fire a request per
// character). The list is paged + searched on the server, so search scales to any number of scopes.
const SEARCH_DEBOUNCE_MS = 250;

// The template-first goals. `build` writes the expression; `suggest` proposes a name from the fields.
const TEMPLATES = [
  { id: 'label-eq', label: 'Records with a label value', build: (f) => `${dim(f, 'domain')} == ${lit(f.value)}`, suggest: (f) => `rule-${slug(f.value) || 'label'}` },
  { id: 'in', label: 'Records whose label is one of…', build: (f) => `${dim(f, 'tenant')} in (${values(f).map(lit).join(', ')})`, suggest: (f) => `rule-${slug(f.dim) || 'set'}` },
  { id: 'claim', label: "Records matching the caller's claim", build: (f) => `${dim(f, 'tenant')} == $claim.${(f.claimDim || '').trim() || dim(f, 'tenant')}`, suggest: (f) => `${slug(f.dim) || 'tenant'}-scoped` },
  { id: 'intersects', label: 'Records sharing any label with the caller', build: () => '$claims.intersects', suggest: () => 'shares-a-label' },
  { id: 'superset', label: 'Caller cleared for every label (ABAC)', build: () => '$claims.superset', suggest: () => 'abac-superset' },
  { id: 'ordered', label: 'Records at a classification level', build: (f) => `${(f.dim || '').trim()} ${f.comparator || '<='} ${lit(f.value)}`, suggest: (f) => `rule-${slug(f.value) || 'level'}` },
  { id: 'advanced', label: 'Advanced — write an expression', build: (f) => (f.expression || '').trim(), suggest: (f) => f.name || 'rule' },
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
    return ['base-url', 'scopes', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._scopes = [];
    /** @private */ this._orderings = [];
    /** @private */ this._orderingsLoaded = false;
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._history = [];          // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
    /** @private */ this._reqSeq = 0;
    /** @private */ this._query = '';
    /** @private */ this._form = null;           // the detail-pane form state (null = nothing selected)
    /** @private */ this._selectedName = null;   // the selected scope name (null when creating / nothing selected)
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'scopes') { this.renderBody(); this.renderDetail(); }
    else this.reload();
  }

  requestRender() { this.reload(); }

  refresh() { this.reload(); }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 50;
  }

  get canWrite() {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes('security:write');
  }

  /** Reload from page 1 under the current search term (resets the keyset cursor + history). */
  reload() {
    this._history = [];
    this._currentToken = undefined;
    this.load();
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
      const q = this._query.trim();
      // One keyset page for the current cursor, filtered server-side by q — the page REPLACES the list. Orderings are
      // small config (the classification templates); fetch them once and reuse across searches and page turns rather
      // than on every reload/page turn.
      const tasks = [client.searchSecurityRules({ q: q || undefined, pageToken: this._currentToken, limit: this.pageSize })];
      if (!this._orderingsLoaded) tasks.push(client.listSecurityOrderings().catch(() => ({ orderings: [] })));
      const [page, orderingsResult] = await Promise.all(tasks);
      if (seq !== this._reqSeq) return;
      this._scopes = page.rules;
      this._nextPageToken = page.nextPageToken;
      if (orderingsResult) { this._orderings = orderingsResult.orderings ?? []; this._orderingsLoaded = true; }
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._scopes.length, hasMore: !!this._nextPageToken });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  nextPage() {
    if (!this._nextPageToken) return;
    this._history.push(this._currentToken);
    this._currentToken = this._nextPageToken;
    this.load();
  }

  prevPage() {
    if (this._history.length === 0) return;
    this._currentToken = this._history.pop();
    this.load();
  }

  // ---- detail-pane authoring --------------------------------------------------------------------

  availableTemplates() {
    return this._orderings.length > 0 ? TEMPLATES : TEMPLATES.filter((t) => t.id !== 'ordered');
  }

  labelsFor(dimension) {
    return this._orderings.find((o) => o.dimension === dimension)?.labels ?? [];
  }

  /** Open a blank detail pane to author a new scope. */
  openCreate() {
    this._selectedName = null;
    this._form = { mode: 'create', editName: null, template: 'label-eq', name: '', nameEdited: false, formError: null, fields: {} };
    this._form.name = templateById('label-eq').suggest(this._form.fields);
    this.renderBody();
    this.renderDetail();
  }

  /** Select a scope row: open it in the detail pane for editing (the name is the immutable key). */
  select(name) {
    const scope = this._scopes.find((s) => s.name === name);
    if (!scope) return;
    this._selectedName = name;
    this._form = {
      mode: 'edit', editName: scope.name, template: 'advanced', name: scope.name, nameEdited: true, formError: null,
      fields: { expression: scope.expression, description: scope.description || '' },
    };
    this.renderBody();
    this.renderDetail();
  }

  clearDetail() {
    this._form = null;
    this._selectedName = null;
    this.renderBody();
    this.renderDetail();
  }

  previewExpression() {
    return this._form ? templateById(this._form.template).build(this._form.fields) : '';
  }

  async submitForm() {
    if (!this._form) return;
    const form = this._form;
    const expression = this.previewExpression();
    const description = (form.fields.description || '').trim();
    if (!expression) { form.formError = { title: 'The rule expression is empty.' }; this.renderDetail(); return; }
    try {
      if (form.mode === 'edit') {
        await this.client.updateSecurityRule(form.editName, { expression, description: description || undefined });
      } else {
        const name = (form.name || '').trim();
        if (!name) { form.formError = { title: 'A rule name is required.' }; this.renderDetail(); return; }
        await this.client.createSecurityRule({ name, expression, description: description || undefined });
      }
      this.clearDetail();
      await this.reloadAndEmit();
    } catch (err) {
      form.formError = err.problem || { title: err.message };
      this.renderDetail();
      this.emit('error', { problem: form.formError, error: err });
    }
  }

  async deleteScope(name) {
    const confirmed = await confirmDialog(this, {
      title: 'Delete rule',
      message: `Delete the rule '${name}'? Grants that reference it will lose that reach.`,
      confirmLabel: 'Delete', danger: true,
    });
    if (!confirmed) return;
    try {
      await this.client.deleteSecurityRule(name);
      this.clearDetail();
      await this.reloadAndEmit();
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  async reloadAndEmit() {
    // Reload from page 1 under the current search term (a mutation may have added/removed a matching scope).
    this._history = [];
    this._currentToken = undefined;
    await this.load();
    this.emit('scopes-changed', { scopes: this._scopes });
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${PAGER_CSS}
        :host { display: block; }
        .layout { display: grid; grid-template-columns: minmax(0, 1fr); gap: 14px; align-items: start; }
        @media (min-width: 880px) { .layout.has-selection { grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr); } }
        .wrap { border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; background: var(--_bg); }
        .toolbar { display: flex; align-items: center; gap: 8px; padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); }
        .toolbar .title { font-weight: 600; color: var(--_muted); font-size: 12px; }
        .toolbar .grow { flex: 1; }
        .search { font: inherit; font-size: 13px; padding: 5px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: inherit; width: 160px; }
        .err { margin: 10px 12px; }
        .err:empty { display: none; }
        table { width: 100%; border-collapse: collapse; }
        thead th { text-align: left; font-size: 12px; font-weight: 600; color: var(--_muted); padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); white-space: nowrap; }
        tbody td { padding: 9px 12px; border-bottom: 1px solid var(--_border); vertical-align: top; }
        tbody tr:last-child td { border-bottom: none; }
        tbody tr.selectable { cursor: pointer; }
        tbody tr.selectable:hover { background: var(--_surface); }
        tbody tr[aria-selected="true"] { background: color-mix(in srgb, var(--_accent) 12%, transparent); }
        .sname { font-weight: 600; }
        .sexpr { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); overflow-wrap: anywhere; }
        .sdesc { color: var(--_muted); font-size: 12px; margin-top: 2px; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }

        /* detail pane */
        .detail { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        .dhead { padding: 12px 14px; border-bottom: 1px solid var(--_border); background: var(--_surface); font-weight: 700; display: flex; align-items: center; gap: 8px; }
        .dhead .grow { flex: 1; }
        .dhead .close { font-size: 16px; line-height: 1; }
        .content { padding: 14px; display: grid; gap: 14px; }
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
        .dfoot { display: flex; gap: 8px; align-items: center; padding: 12px 14px; border-top: 1px solid var(--_border); }
        .dfoot .grow { flex: 1; }
      </style>
      <div class="layout" part="layout">
        <div class="wrap" part="panel">
          <div class="toolbar" part="toolbar">
            <span class="title">Rules</span>
            <span class="grow"></span>
            <input class="search" type="search" placeholder="Search rules…" aria-label="Search rules">
            <button class="refresh ghost" type="button" title="Refresh">↻</button>
            <button class="new primary" type="button" hidden>New rule</button>
          </div>
          <div class="err"></div>
          <table>
            <thead><tr><th>Rule</th><th>Expression</th></tr></thead>
            <tbody class="list" part="rows"></tbody>
          </table>
          <arazzo-pager class="pager" part="pager"></arazzo-pager>
        </div>
        <div class="detail-pane"></div>
      </div>
    `;
    this.$('.new').addEventListener('click', () => this.openCreate());
    this.$('.refresh').addEventListener('click', () => this.reload());
    this.$('.search').addEventListener('input', (e) => {
      this._query = e.target.value;
      clearTimeout(this._searchTimer);
      this._searchTimer = setTimeout(() => this.reload(), SEARCH_DEBOUNCE_MS);
    });
    this.$('arazzo-pager').addEventListener('prev', () => this.prevPage());
    this.$('arazzo-pager').addEventListener('next', () => this.nextPage());
    this._pane = this.$('.detail-pane');
    this._layout = this.$('.layout');
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
      list.innerHTML = `<tr><td colspan="2"><div class="skl"></div><div class="skl"></div></td></tr>`;
    } else if (this._scopes.length === 0) {
      list.innerHTML = `<tr><td colspan="2"><div class="empty">${this._query.trim() ? `No rules match “${escapeHtml(this._query.trim())}”.` : 'No rules defined.'}</div></td></tr>`;
    } else {
      list.innerHTML = this._scopes.map((s) => `
        <tr class="srow selectable" part="row" data-name="${escapeHtml(s.name)}" aria-selected="${String(s.name === this._selectedName)}">
          <td part="cell"><span class="sname">${escapeHtml(s.name)}</span>${s.description ? `<div class="sdesc">${escapeHtml(s.description)}</div>` : ''}</td>
          <td part="cell"><code class="sexpr">${escapeHtml(s.expression)}</code></td>
        </tr>`).join('');
      this.$$('.srow').forEach((tr) => tr.addEventListener('click', () => this.select(tr.dataset.name)));
    }

    this.$('arazzo-pager')?.update({
      hasPrev: this._history.length > 0,
      hasNext: !!this._nextPageToken,
      loading: this._loading,
      info: this._loading ? 'Loading…' : `${this._scopes.length} rule${this._scopes.length === 1 ? '' : 's'}${this._history.length ? ` · page ${this._history.length + 1}` : ''}`,
    });
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

  renderDetail() {
    const pane = this._pane;
    if (!pane) return;
    const f = this._form;
    this._layout.classList.toggle('has-selection', !!f);
    if (!f) { pane.replaceChildren(); return; }

    const isEdit = f.mode === 'edit';
    pane.innerHTML = `
      <div class="detail" part="detail">
        <div class="dhead">
          <span class="dtitle">${isEdit ? `Edit rule '${escapeHtml(f.editName)}'` : 'New rule'}</span>
          <span class="grow"></span>
          <button class="close ghost" type="button" title="Close" aria-label="Close">✕</button>
        </div>
        <div class="content">
          ${isEdit ? '' : `
            <div class="goal">
              ${this.availableTemplates().map((t) => `<label><input type="radio" name="tmpl" value="${t.id}" ${t.id === f.template ? 'checked' : ''}> ${escapeHtml(t.label)}</label>`).join('')}
            </div>`}
          <div class="fields">${this.fieldsHtml(f)}</div>
          <div class="field"><span>Name</span><input class="f-name" value="${escapeHtml(f.name)}" ${isEdit ? 'readonly' : ''}></div>
          <div class="field"><span>Description</span><input class="f-description" placeholder="(optional)" value="${escapeHtml(f.fields.description || '')}"></div>
          <div class="preview">Preview: <code class="preview-expr">${escapeHtml(this.previewExpression() || '—')}</code></div>
          <div class="form-err">${f.formError ? `<div class="error-banner"><span><strong>${escapeHtml(f.formError.title || 'Request failed')}</strong>${f.formError.detail ? ' — ' + escapeHtml(f.formError.detail) : ''}</span></div>` : ''}</div>
        </div>
        <div class="dfoot">
          ${isEdit ? '<button class="del danger" type="button">Delete…</button>' : ''}
          <span class="grow"></span>
          <button class="cancel ghost" type="button">Cancel</button>
          <button class="confirm primary" type="button">${isEdit ? 'Save' : 'Create'}</button>
        </div>
      </div>
    `;

    pane.querySelectorAll('input[name="tmpl"]').forEach((r) => r.addEventListener('change', () => {
      f.template = r.value;
      if (!f.nameEdited) f.name = templateById(f.template).suggest(f.fields);
      this.renderDetail();
    }));

    pane.querySelectorAll('.fields input, .fields textarea, .fields select').forEach((input) => {
      const key = [...input.classList].find((c) => c.startsWith('f-'))?.slice(2);
      if (!key) return;
      input.addEventListener('input', () => {
        f.fields[key] = input.value;
        if (f.template === 'ordered' && key === 'dim') {
          f.fields.value = '';
          if (!f.nameEdited) f.name = templateById(f.template).suggest(f.fields);
          this.renderDetail();
          return;
        }
        const expr = pane.querySelector('.preview-expr');
        if (expr) expr.textContent = this.previewExpression() || '—';
        const nameInput = pane.querySelector('.f-name');
        if (nameInput && !f.nameEdited && !isEdit) { f.name = templateById(f.template).suggest(f.fields); nameInput.value = f.name; }
      });
    });

    const nameInput = pane.querySelector('.f-name');
    if (nameInput && !isEdit) nameInput.addEventListener('input', () => { f.nameEdited = true; f.name = nameInput.value; });
    const descInput = pane.querySelector('.f-description');
    if (descInput) descInput.addEventListener('input', () => { f.fields.description = descInput.value; });

    pane.querySelector('.close').addEventListener('click', () => this.clearDetail());
    pane.querySelector('.cancel').addEventListener('click', () => this.clearDetail());
    pane.querySelector('.confirm').addEventListener('click', () => this.submitForm());
    pane.querySelector('.del')?.addEventListener('click', () => this.deleteScope(f.editName));

    // Scope honesty: a caller without security:write views the rule read-only — inputs disabled, no Save/Delete.
    if (!this.canWrite) {
      pane.querySelectorAll('input, select, textarea').forEach((c) => { c.disabled = true; });
      pane.querySelector('.confirm')?.remove();
      pane.querySelector('.del')?.remove();
      pane.querySelector('.cancel').textContent = 'Close';
    }
  }
}

define('arazzo-rules-panel', ArazzoScopesPanel);
// Deprecated alias — the panel manages security *rules*; kit consumers may still use the old tag (normalization A). A
// trivial subclass is required because one constructor cannot be registered under two tag names.
define('arazzo-scopes-panel', class ArazzoScopesPanelAlias extends ArazzoScopesPanel {});
export { ArazzoScopesPanel };