// <arazzo-catalog> — the reference panel that composes the catalog kit into a working browse/govern screen.
//
//   <script type="module" src="arazzo-catalog.js"></script>
//   <arazzo-catalog base-url="/arazzo/v1" scopes="catalog:read catalog:write catalog:purge" theme="auto"></arazzo-catalog>
//   <script type="module">
//     document.querySelector('arazzo-catalog').authProvider = async () => `Bearer ${await app.token()}`;
//   </script>
//
// Attributes : base-url, scopes (space-separated), theme (auto|light|dark)
// Properties : .client (override the auto-built one), .authProvider (() => Authorization header), .fetch
// Events     : re-emits version-selected / version-changed / version-deleted / purge-completed / error
//
// The catalog sibling of <arazzo-control-plane>: same shell and theming, over the catalog surface.

import { ArazzoControlPlaneClient, CATALOG_STATUSES } from './arazzo-client.js';
import { ArazzoElement, SHARED_CSS, escapeHtml, confirmDialog, define } from './components/base.js';
import './components/catalog-table.js';
import './components/catalog-detail.js';
import './components/catalog-add-dialog.js';

const LIGHT = `
  --arazzo-bg:#fff; --arazzo-surface:#f7f8fa; --arazzo-border:#e3e6ea; --arazzo-text:#1c2024;
  --arazzo-muted:#6b7280; --arazzo-accent:#3b6cf6; --arazzo-danger:#d4351c;
  --arazzo-status-completed:#2a8a4a; --arazzo-status-cancelled:#6b7280;`;
const DARK = `
  --arazzo-bg:#14171b; --arazzo-surface:#1c2026; --arazzo-border:#2c3138; --arazzo-text:#e6e9ee;
  --arazzo-muted:#9aa3af; --arazzo-accent:#6f9bff; --arazzo-danger:#ff6b5e;
  --arazzo-status-completed:#4cc472; --arazzo-status-cancelled:#8b929c;`;

class ArazzoCatalog extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes', 'theme'];
  }

  constructor() {
    super();
    /** @private */ this._authProvider = undefined;
    /** @private */ this._fetch = undefined;
  }

  connectedCallback() {
    this.render();
    this.wire();
    this.applyClient();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'theme') { this.render(); this.wire(); this.applyClient(); }
    else if (name === 'base-url') { this._client = undefined; this.applyClient(); }
    else if (name === 'scopes') this.applyScopes();
  }

  set authProvider(fn) { this._authProvider = fn; this._client = undefined; this.applyClient(); }
  get authProvider() { return this._authProvider; }

  set fetch(fn) { this._fetch = fn; this._client = undefined; this.applyClient(); }

  get scopeList() {
    return (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
  }

  hasScope(scope) {
    const s = this.scopeList;
    return s.length === 0 || s.includes(scope);
  }

  buildClient() {
    if (this._client) return this._client;
    const baseUrl = this.getAttribute('base-url');
    if (!baseUrl) return undefined;
    this._client = new ArazzoControlPlaneClient({
      baseUrl,
      fetch: this._fetch,
      getAuthHeader: this._authProvider,
    });
    return this._client;
  }

  applyClient() {
    const client = this.buildClient();
    if (!client) return;
    const table = this.$('arazzo-catalog-table');
    if (table) table.client = client;
    const detail = this._pane?.querySelector('arazzo-catalog-detail');
    if (detail) detail.client = client;
  }

  applyScopes() {
    const detail = this._pane?.querySelector('arazzo-catalog-detail');
    if (detail) detail.setAttribute('scopes', this.getAttribute('scopes') || '');
    const purgeBtn = this.$('.purge-btn');
    if (purgeBtn) purgeBtn.hidden = !this.hasScope('catalog:purge');
    const addBtn = this.$('.add-btn');
    if (addBtn) addBtn.hidden = !this.hasScope('catalog:write');
  }

  themeTokens() {
    const theme = this.getAttribute('theme') || 'auto';
    if (theme === 'dark') return `:host{${DARK}}`;
    if (theme === 'light') return `:host{${LIGHT}}`;
    return `:host{${LIGHT}} @media (prefers-color-scheme: dark){:host{${DARK}}}`;
  }

  render() {
    const scopes = this.getAttribute('scopes') || '';
    this.shadowRoot.innerHTML = `
      <style>
        ${this.themeTokens()}
        ${SHARED_CSS}
        :host { display: block; }
        .toolbar { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; margin-bottom: 12px; }
        .chips { display: flex; gap: 6px; flex-wrap: wrap; }
        .chip { font-size: 12px; padding: 4px 11px; border-radius: 999px; }
        .chip[aria-pressed="true"] { background: var(--_accent); border-color: var(--_accent); color: #fff; }
        .search { flex: 1; min-width: 150px; }
        .search input { width: 100%; font: inherit; padding: 6px 10px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
        .grow { flex: 1; }
        .layout { display: grid; grid-template-columns: 1fr; gap: 14px; }
        @media (min-width: 880px) { .layout.has-selection { grid-template-columns: 1.4fr 1fr; align-items: start; } }
      </style>
      <div class="toolbar" part="toolbar">
        <div class="chips" part="filters">
          <button class="chip status-chip" type="button" data-status="" aria-pressed="true">All</button>
          ${CATALOG_STATUSES.map((s) => `<button class="chip status-chip" type="button" data-status="${s}" aria-pressed="false">${escapeHtml(s)}</button>`).join('')}
        </div>
        <div class="search"><input class="q-search" type="search" placeholder="Search title/description…" aria-label="Search title or description"></div>
        <div class="search"><input class="owner-search" type="search" placeholder="Owner…" aria-label="Filter by owner"></div>
        <div class="search"><input class="tag-search" type="search" placeholder="Tags (space-separated, AND)…" aria-label="Filter by tags"></div>
        <button class="refresh ghost" type="button" title="Refresh">↻</button>
        <button class="add-btn primary" type="button" ${this.hasScope('catalog:write') ? '' : 'hidden'}>Add version…</button>
        <button class="purge-btn danger" type="button" ${this.hasScope('catalog:purge') ? '' : 'hidden'}>Purge obsolete…</button>
      </div>
      <div class="layout" part="layout">
        <arazzo-catalog-table selectable part="table"></arazzo-catalog-table>
        <div class="detail-pane"></div>
      </div>
      <arazzo-catalog-add-dialog></arazzo-catalog-add-dialog>
    `;
    void scopes;
  }

  wire() {
    const table = this.$('arazzo-catalog-table');
    const pane = this.$('.detail-pane');
    const layout = this.$('.layout');

    table.addEventListener('version-selected', (e) => {
      this.showDetail(e.detail.version, e.detail.versions);
      this.emit('version-selected', e.detail);
    });
    table.addEventListener('error', (e) => this.emit('error', e.detail));

    this.$$('.status-chip').forEach((chip) => chip.addEventListener('click', () => {
      this.$$('.status-chip').forEach((c) => c.setAttribute('aria-pressed', String(c === chip)));
      const status = chip.dataset.status;
      if (status) table.setAttribute('status', status); else table.removeAttribute('status');
    }));

    const debounced = (input, apply) => input.addEventListener('input', (e) => {
      clearTimeout(input._t);
      const value = e.target.value.trim();
      input._t = setTimeout(() => apply(value), 300);
    });
    debounced(this.$('.q-search'), (v) => v ? table.setAttribute('q', v) : table.removeAttribute('q'));
    debounced(this.$('.owner-search'), (v) => v ? table.setAttribute('owner', v) : table.removeAttribute('owner'));
    debounced(this.$('.tag-search'), (v) => v ? table.setAttribute('tags', v) : table.removeAttribute('tags'));

    this.$('.refresh').addEventListener('click', () => table.reload());
    this.$('.purge-btn').addEventListener('click', () => this.confirmPurge());

    const addDialog = this.$('arazzo-catalog-add-dialog');
    this.$('.add-btn').addEventListener('click', () => { addDialog.client = this.buildClient(); addDialog.open(); });
    addDialog.addEventListener('version-added', (e) => {
      table.reload();
      this.showDetail(e.detail.version);
      this.emit('version-added', e.detail);
    });
    addDialog.addEventListener('error', (e) => this.emit('error', e.detail));

    pane.addEventListener('version-changed', (e) => { table.refresh(); this.emit('version-changed', e.detail); });
    pane.addEventListener('version-deleted', (e) => { table.refresh(); this.clearDetail(); this.emit('version-deleted', e.detail); });
    pane.addEventListener('close', () => this.clearDetail());
    pane.addEventListener('error', (e) => this.emit('error', e.detail));

    this._layout = layout;
    this._pane = pane;
  }

  async confirmPurge() {
    const confirmed = await confirmDialog(this, {
      title: 'Purge obsolete versions',
      message: 'Permanently delete all Obsolete versions with no referencing runs? This cannot be undone.',
      confirmLabel: 'Purge',
      danger: true,
    });
    if (!confirmed) return;
    try {
      const result = await this.buildClient().purgeCatalog();
      this.$('arazzo-catalog-table').reload();
      this.clearDetail();
      this.emit('purge-completed', result);
    } catch (err) {
      this.emit('error', { problem: err.problem || { title: err.message, status: err.status }, error: err });
    }
  }

  showDetail(version, versions) {
    if (!version) return this.clearDetail();
    let detail = this._pane.querySelector('arazzo-catalog-detail');
    if (!detail) {
      detail = document.createElement('arazzo-catalog-detail');
      const scopes = this.getAttribute('scopes');
      if (scopes) detail.setAttribute('scopes', scopes);
      detail.client = this.buildClient();
      this._pane.replaceChildren(detail);
    }
    if (versions) detail.versions = versions;
    detail.version = version;
    this._layout.classList.add('has-selection');
  }

  clearDetail() {
    this._pane.replaceChildren();
    this._layout.classList.remove('has-selection');
  }
}

define('arazzo-catalog', ArazzoCatalog);
export { ArazzoCatalog };
