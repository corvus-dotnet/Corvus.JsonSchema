// <arazzo-grants-panel> — author access grants (design §14.2/§16.5): WHO (a claim) → WHAT/WHERE (per-action
// read/write/purge access, scoped by reusable scopes). "Grant" is the user-facing term for what the API calls a
// security binding; "scope" for a security rule.
//
//   <arazzo-grants-panel base-url="/arazzo/v1" scopes="security:read security:write"></arazzo-grants-panel>
//
// Attributes : base-url, scopes (gates the mutating controls)
// Properties : .client
// Events     : grants-changed {grants}, loaded {count}, error {problem}
// Parts      : panel, list, row
//
// A grant keys a principal claim to per-action access. Authoring is WHO→WHAT→WHERE in a modal editor (the dialog
// pattern the credential editor uses):
//   WHO   — name a group/role grantee with the picker (its resolved identity derives the canonical claim), or enter a
//           raw claim. A *person* grantee is NOT granted here: per-person elevation goes through the access-request
//           flow, so the picker steers it there. The claim→identity mapping is issuer-sensitive, so the multi-IdP
//           caveat is surfaced; the server also rejects self-elevation (403).
//   WHERE — each of read/write/purge is Denied | Unrestricted | scoped (one or more scopes, chosen by typeahead so it
//           stays usable at hundreds — client-side filter for now, server-paged search in the paging campaign).
// The list is searchable. Pre-paging interim: the scope typeahead and the list filter the full loaded set.

import { ArazzoElement, SHARED_CSS, escapeHtml, confirmDialog, define } from './base.js';
import './grantee-picker.js';

const VERBS = ['read', 'write', 'purge'];

const grantMode = (g) => (g?.unrestricted ? 'unrestricted' : (Array.isArray(g?.ruleNames) && g.ruleNames.length > 0 ? 'scopes' : 'denied'));
const grantSummary = (g) => {
  const mode = grantMode(g);
  if (mode === 'unrestricted') return 'Unrestricted';
  if (mode === 'scopes') return g.ruleNames.join(', ');
  return 'Denied';
};

class ArazzoGrantsPanel extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes'];
  }

  constructor() {
    super();
    /** @private */ this._grants = [];
    /** @private */ this._scopes = [];
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
      this._grants = [];
      this.renderBody();
      return;
    }
    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      // Grants are required; the scope names (for the scoped-action typeahead) are best-effort.
      const [{ bindings }, scopesResult] = await Promise.all([
        client.listSecurityBindings(),
        client.listSecurityRules().catch(() => ({ rules: [] })),
      ]);
      if (seq !== this._reqSeq) return;
      this._grants = bindings;
      this._scopes = scopesResult.rules ?? [];
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._grants.length });
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
    if (!q) return this._grants;
    return this._grants.filter((g) => `${g.claimType} ${g.claimValue || ''} ${g.description || ''}`.toLowerCase().includes(q));
  }

  // ---- editor (modal dialog) --------------------------------------------------------------------

  blankForm(mode, editId) {
    return {
      mode, editId, personBlocked: false, granteeLabel: '',
      claimType: '', claimValue: '', description: '', formError: null,
      verbs: { read: { mode: 'denied', scopes: [] }, write: { mode: 'denied', scopes: [] }, purge: { mode: 'denied', scopes: [] } },
    };
  }

  openCreate() {
    this._form = this.blankForm('create', null);
    this.renderEditor();
    this.$('dialog').showModal();
  }

  openEdit(id) {
    const g = this._grants.find((x) => x.id === id);
    if (!g) return;
    const f = this.blankForm('edit', id);
    f.claimType = g.claimType || '';
    f.claimValue = g.claimValue || '';
    for (const verb of VERBS) {
      f.verbs[verb] = { mode: grantMode(g[verb]), scopes: Array.isArray(g[verb]?.ruleNames) ? [...g[verb].ruleNames] : [] };
    }
    f.description = g.description || '';
    this._form = f;
    this.renderEditor();
    this.$('dialog').showModal();
  }

  closeEditor() {
    this.$('dialog')?.close();
    this._form = null;
  }

  onGranteeSelected(grantee) {
    const f = this._form;
    if (!f) return;
    if (grantee.kind === 'person') {
      f.personBlocked = true;
      f.granteeLabel = grantee.label || grantee.value || 'this person';
      f.claimType = '';
      f.claimValue = '';
    } else {
      f.personBlocked = false;
      const primary = (grantee.identity || [])[0];
      f.claimType = primary?.dimension || grantee.kind || '';
      f.claimValue = primary?.value || grantee.value || '';
      f.granteeLabel = grantee.label || grantee.value || '';
    }
    this.renderEditor();
  }

  buildBody() {
    const f = this._form;
    const verb = (v) => (v.mode === 'unrestricted' ? { unrestricted: true } : v.mode === 'scopes' ? { ruleNames: v.scopes } : { unrestricted: false });
    const body = {
      claimType: f.claimType.trim(),
      read: verb(f.verbs.read),
      write: verb(f.verbs.write),
      purge: verb(f.verbs.purge),
    };
    if (f.claimValue.trim()) body.claimValue = f.claimValue.trim();
    if (f.description.trim()) body.description = f.description.trim();
    return body;
  }

  async submitForm() {
    const f = this._form;
    if (!f) return;
    if (f.personBlocked) {
      f.formError = { title: 'Use the access-request flow for a person', detail: `Per-person elevation for ${f.granteeLabel} is requested and approved, not granted directly.` };
      this.renderEditor();
      return;
    }
    if (!f.claimType.trim()) {
      f.formError = { title: 'A claim is required (pick a group/role grantee or enter a raw claim).' };
      this.renderEditor();
      return;
    }
    const body = this.buildBody();
    try {
      if (f.mode === 'edit') {
        await this.client.updateSecurityBinding(f.editId, body);
      } else {
        await this.client.createSecurityBinding(body);
      }
      this.closeEditor();
      await this.reloadAndEmit();
    } catch (err) {
      f.formError = err.problem || { title: err.message };
      this.renderEditor();
      this.emit('error', { problem: f.formError, error: err });
    }
  }

  async deleteGrant(id) {
    const g = this._grants.find((x) => x.id === id);
    const describe = g ? `${g.claimType}${g.claimValue ? '=' + g.claimValue : ''}` : id;
    const confirmed = await confirmDialog(this, {
      title: 'Delete grant',
      message: `Delete the grant for '${describe}'? The principals it matches will lose that access.`,
      confirmLabel: 'Delete', danger: true,
    });
    if (!confirmed) return;
    try {
      await this.client.deleteSecurityBinding(id);
      await this.reloadAndEmit();
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  async reloadAndEmit() {
    const { bindings } = await this.client.listSecurityBindings();
    this._grants = bindings;
    this._error = null;
    this.renderBody();
    this.emit('grants-changed', { grants: this._grants });
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
        .grow-row { display: flex; align-items: baseline; gap: 10px; padding: 9px 12px; border-bottom: 1px solid var(--_border); }
        .grow-row:last-child { border-bottom: none; }
        .gmeta { flex: 1; min-width: 0; }
        .claim { font-weight: 600; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
        .verbs { color: var(--_muted); font-size: 12px; margin-top: 3px; display: flex; gap: 12px; flex-wrap: wrap; }
        .verbs b { color: var(--_text); font-weight: 600; text-transform: capitalize; }
        .gdesc { color: var(--_muted); font-size: 12px; margin-top: 2px; }
        .err { margin: 10px 12px; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }

        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(560px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .dhead { padding: 14px 16px; border-bottom: 1px solid var(--_border); font-weight: 700; font-size: 15px; }
        .content { padding: 16px; display: grid; gap: 14px; max-height: 64vh; overflow: auto; }
        .section { display: grid; gap: 8px; }
        .section > .slabel { font-weight: 600; font-size: 13px; }
        .caveat { font-size: 12px; color: var(--_muted); border-left: 3px solid var(--_border); padding: 4px 0 4px 8px; }
        .field { display: grid; gap: 4px; }
        .field > span { font-size: 12px; color: var(--_muted); }
        .field input, .field select { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); box-sizing: border-box; }
        .field input[readonly] { background: var(--_surface); color: var(--_muted); }
        .verb-row { display: grid; grid-template-columns: 70px 1fr; gap: 8px; align-items: start; }
        .verb-row > .vname { text-transform: capitalize; font-weight: 600; font-size: 13px; padding-top: 8px; }
        .scope-pick { display: grid; gap: 6px; margin-top: 6px; }
        .chips { display: flex; gap: 6px; flex-wrap: wrap; }
        .chip { display: inline-flex; align-items: center; gap: 4px; font-size: 12px; background: var(--_surface); border: 1px solid var(--_border); border-radius: 999px; padding: 2px 8px; }
        .chip button { border: none; background: none; cursor: pointer; color: var(--_muted); font-size: 13px; line-height: 1; padding: 0; }
        .scope-empty { font-size: 12px; color: var(--_muted); }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <div class="panel" part="panel">
        <div class="head">
          <span class="title">Grants</span>
          <span class="grow"></span>
          <input class="search" type="search" placeholder="Search grants…" aria-label="Search grants">
          <button class="new primary" type="button" hidden>New grant</button>
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
      <datalist id="scope-options"></datalist>
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

    if (this._loading && this._grants.length === 0) {
      list.innerHTML = '<div class="skl"></div><div class="skl"></div>';
      return;
    }
    const items = this.filtered();
    if (items.length === 0) {
      list.innerHTML = `<div class="empty">${this._grants.length === 0 ? 'No grants defined.' : 'No grants match your search.'}</div>`;
      return;
    }

    list.innerHTML = items.map((g) => `
      <div class="grow-row" part="row">
        <div class="gmeta">
          <span class="claim">${escapeHtml(g.claimType)}${g.claimValue ? '=' + escapeHtml(g.claimValue) : ''}</span>
          <div class="verbs">${VERBS.map((v) => `<span><b>${v}</b> ${escapeHtml(grantSummary(g[v]))}</span>`).join('')}</div>
          ${g.description ? `<div class="gdesc">${escapeHtml(g.description)}</div>` : ''}
        </div>
        ${this.canWrite ? `
          <button class="edit ghost" type="button" data-id="${escapeHtml(g.id)}">Edit</button>
          <button class="del ghost" type="button" data-id="${escapeHtml(g.id)}">Delete</button>` : ''}
      </div>`).join('');
    this.$$('.edit').forEach((b) => b.addEventListener('click', () => this.openEdit(b.dataset.id)));
    this.$$('.del').forEach((b) => b.addEventListener('click', () => this.deleteGrant(b.dataset.id)));
  }

  verbRowHtml(verb) {
    const v = this._form.verbs[verb];
    const modes = [['denied', 'Denied'], ['unrestricted', 'Unrestricted'], ['scopes', 'Scoped']];
    let picker = '';
    if (v.mode === 'scopes') {
      const chips = v.scopes.map((name) => `<span class="chip">${escapeHtml(name)}<button type="button" class="chip-rm" data-verb="${verb}" data-scope="${escapeHtml(name)}" aria-label="remove ${escapeHtml(name)}">×</button></span>`).join('');
      picker = `
        <div class="scope-pick">
          <div class="chips">${chips || '<span class="scope-empty">No scopes yet — search to add.</span>'}</div>
          <input class="scope-input" data-verb="${verb}" list="scope-options" placeholder="Search scopes to add…" aria-label="add a scope to ${verb}">
        </div>`;
    }
    return `
      <div class="verb-row">
        <span class="vname">${verb}</span>
        <div>
          <select class="verb-mode" data-verb="${verb}">${modes.map(([m, l]) => `<option value="${m}" ${v.mode === m ? 'selected' : ''}>${l}</option>`).join('')}</select>
          ${picker}
        </div>
      </div>`;
  }

  renderEditor() {
    const content = this.$('.content');
    const f = this._form;
    if (!content || !f) return;
    const isEdit = f.mode === 'edit';
    this.$('.dhead').textContent = isEdit ? `Edit grant '${f.claimType}${f.claimValue ? '=' + f.claimValue : ''}'` : 'New grant';
    this.$('.confirm').textContent = isEdit ? 'Save' : 'Create';
    this.$('#scope-options').innerHTML = this._scopes.map((s) => `<option value="${escapeHtml(s.name)}"></option>`).join('');

    content.innerHTML = `
      <div class="section">
        <span class="slabel">WHO — the claim this grant keys on</span>
        ${isEdit ? '' : `
          <div class="field"><span>Grantee</span><arazzo-grantee-picker class="who-picker" placeholder="a team or role…"></arazzo-grantee-picker></div>
          ${f.personBlocked ? `<div class="error-banner steer"><span>Per-person elevation for <strong>${escapeHtml(f.granteeLabel)}</strong> goes through the <strong>access-request flow</strong>, not a direct grant — request and have it approved instead.</span></div>` : ''}`}
        <div class="field"><span>Claim type</span><input class="f-claimType" placeholder="team" value="${escapeHtml(f.claimType)}" ${isEdit ? 'readonly' : ''}></div>
        <div class="field"><span>Claim value</span><input class="f-claimValue" placeholder="(any value of the type)" value="${escapeHtml(f.claimValue)}"></div>
        <div class="caveat">A claim is only as trustworthy as the issuer asserting it. With multiple semi-trusted identity providers, prefer an issuer-qualified claim over a bare one.</div>
      </div>
      <div class="section">
        <span class="slabel">WHERE — per-action access</span>
        ${VERBS.map((v) => this.verbRowHtml(v)).join('')}
      </div>
      <div class="field"><span>Description</span><input class="f-description" placeholder="(optional)" value="${escapeHtml(f.description)}"></div>
      <div class="form-err">${f.formError ? `<div class="error-banner"><span><strong>${escapeHtml(f.formError.title || 'Request failed')}</strong>${f.formError.detail ? ' — ' + escapeHtml(f.formError.detail) : ''}</span></div>` : ''}</div>
    `;

    const picker = content.querySelector('.who-picker');
    if (picker) {
      picker.client = this.client;
      picker.addEventListener('grantee-selected', (e) => this.onGranteeSelected(e.detail.grantee));
      picker.addEventListener('grantee-cleared', () => { f.personBlocked = false; this.renderEditor(); });
    }

    content.querySelector('.f-claimType')?.addEventListener('input', (e) => { f.claimType = e.target.value; });
    content.querySelector('.f-claimValue')?.addEventListener('input', (e) => { f.claimValue = e.target.value; });
    content.querySelector('.f-description')?.addEventListener('input', (e) => { f.description = e.target.value; });

    content.querySelectorAll('.verb-mode').forEach((sel) => sel.addEventListener('change', () => {
      f.verbs[sel.dataset.verb].mode = sel.value;
      this.renderEditor();
    }));

    // Scope typeahead: a datalist-backed input; selecting a known scope name adds a removable chip.
    content.querySelectorAll('.scope-input').forEach((input) => input.addEventListener('change', () => {
      const name = input.value.trim();
      const verb = input.dataset.verb;
      if (name && this._scopes.some((s) => s.name === name) && !f.verbs[verb].scopes.includes(name)) {
        f.verbs[verb].scopes.push(name);
        this.renderEditor();
        content.querySelector(`.scope-input[data-verb="${verb}"]`)?.focus();
      } else {
        input.value = '';
      }
    }));
    content.querySelectorAll('.chip-rm').forEach((btn) => btn.addEventListener('click', () => {
      const list = f.verbs[btn.dataset.verb].scopes;
      const i = list.indexOf(btn.dataset.scope);
      if (i >= 0) list.splice(i, 1);
      this.renderEditor();
    }));
  }
}

define('arazzo-grants-panel', ArazzoGrantsPanel);
export { ArazzoGrantsPanel };
