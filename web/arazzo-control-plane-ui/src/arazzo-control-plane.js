// <arazzo-control-plane> — the reference panel that composes the kit into a working run-management screen.
//
//   <script type="module" src="arazzo-control-plane.js"></script>
//   <arazzo-control-plane base-url="/arazzo/v1" scopes="runs:read runs:write" theme="auto"></arazzo-control-plane>
//   <script type="module">
//     document.querySelector('arazzo-control-plane').authProvider = async () => `Bearer ${await app.token()}`;
//   </script>
//
// Attributes : base-url, scopes (space-separated), theme (auto|light|dark), poll (ms; default 5000)
// Properties : .client (override the auto-built one), .authProvider (() => Authorization header), .fetch
// Events     : re-emits run-selected / run-changed / run-deleted / error from its children
//
// Importing this file registers every kit element (table, detail, dialogs, badge).

import { ArazzoControlPlaneClient, RUN_STATUSES } from './arazzo-client.js';
import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './components/base.js';
import './components/runs-table.js';
import './components/run-detail.js';
import './components/purge-dialog.js';
import './components/workflow-id-input.js';

// Token values mirror arazzo-kit.css so the panel themes itself even without that stylesheet.
const LIGHT = `
  --arazzo-bg:#fff; --arazzo-surface:#f7f8fa; --arazzo-border:#e3e6ea; --arazzo-text:#1c2024;
  --arazzo-muted:#6b7280; --arazzo-accent:#3b6cf6; --arazzo-danger:#d4351c;
  --arazzo-status-pending:#9aa1ab; --arazzo-status-running:#2f74d0; --arazzo-status-suspended:#b07d18;
  --arazzo-status-completed:#2a8a4a; --arazzo-status-cancelled:#6b7280; --arazzo-status-faulted:#d4351c;`;
const DARK = `
  --arazzo-bg:#14171b; --arazzo-surface:#1c2026; --arazzo-border:#2c3138; --arazzo-text:#e6e9ee;
  --arazzo-muted:#9aa3af; --arazzo-accent:#6f9bff; --arazzo-danger:#ff6b5e;
  --arazzo-status-pending:#8b929c; --arazzo-status-running:#6f9bff; --arazzo-status-suspended:#e0a93f;
  --arazzo-status-completed:#4cc472; --arazzo-status-cancelled:#8b929c; --arazzo-status-faulted:#ff6b5e;`;


class ArazzoControlPlane extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes', 'theme', 'poll'];
  }

  constructor() {
    super();
    /** @private */ this._authProvider = undefined;
    /** @private */ this._fetch = undefined;
    /** @private */ this._search = '';
    /** @private */ this._searchTimer = null;
  }

  connectedCallback() {
    this.render();
    this.wire();
    this.applyClient();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'theme') this.render(), this.wire(), this.applyClient();
    else if (name === 'base-url') { this._client = undefined; this.applyClient(); }
    else if (name === 'scopes') this.applyScopes();
    else if (name === 'poll') this.applyPoll();
  }

  /** A function returning the `Authorization` header value for each request (host owns the OAuth/OIDC flow). */
  set authProvider(fn) { this._authProvider = fn; this._client = undefined; this.applyClient(); }
  get authProvider() { return this._authProvider; }

  /** An optional `fetch`-compatible override (interceptors / retries / mTLS host). */
  set fetch(fn) { this._fetch = fn; this._client = undefined; this.applyClient(); }

  get scopeList() {
    return (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
  }

  hasScope(scope) {
    const s = this.scopeList;
    return s.length === 0 || s.includes(scope);
  }

  get poll() {
    const v = this.getAttribute('poll');
    return v == null ? 5000 : Number(v) || 0;
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
    const table = this.$('arazzo-runs-table');
    const detail = this.$('arazzo-run-detail');
    const purge = this.$('arazzo-purge-dialog');
    if (table) table.client = client;
    if (detail) detail.client = client;
    if (purge) purge.client = client;
    const wfInput = this.$('.wf-search');
    if (wfInput) wfInput.client = client; // the <arazzo-workflow-id-input> owns its catalog autocomplete
  }

  applyScopes() {
    const detail = this.$('arazzo-run-detail');
    if (detail) detail.setAttribute('scopes', this.getAttribute('scopes') || '');
    const purgeBtn = this.$('.purge-btn');
    if (purgeBtn) purgeBtn.hidden = !this.hasScope('runs:purge');
  }

  applyPoll() {
    const table = this.$('arazzo-runs-table');
    const toggle = this.$('#autorefresh');
    if (table) {
      if (toggle?.checked) table.setAttribute('poll', String(this.poll));
      else table.removeAttribute('poll');
    }
  }

  /**
   * Reload from page 1 and clear any open detail — the public hook the demo calls on a persona change (the new caller's
   * scopes + reach change the whole visible set, so a stale page/cursor and a now-out-of-reach detail must be discarded).
   */
  reload() {
    this.$('arazzo-runs-table')?.reload();
    this.clearDetail?.();
  }

  refresh() { this.reload(); }

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
        .search { flex: 1; min-width: 160px; }
        .search input { width: 100%; font: inherit; padding: 6px 10px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
        .toggle { display: inline-flex; gap: 6px; align-items: center; font-size: 12px; color: var(--_muted); white-space: nowrap; }
        .grow { flex: 1; }
        .layout { display: grid; grid-template-columns: 1fr; gap: 14px; }
        @media (min-width: 880px) { .layout.has-selection { grid-template-columns: 1.4fr 1fr; align-items: start; } }
        .detail-pane:empty::before { content: ""; }
        .placeholder { border: 1px dashed var(--_border); border-radius: var(--_radius); color: var(--_muted); padding: 28px; text-align: center; }
        .timewindow { display: flex; gap: 14px; flex-wrap: wrap; align-items: flex-end; margin-bottom: 12px; }
        .timewindow fieldset { display: flex; gap: 8px; flex-wrap: wrap; border: 1px solid var(--_border); border-radius: var(--_radius); padding: 6px 10px; margin: 0; }
        .timewindow legend { font-size: 11px; color: var(--_muted); padding: 0 4px; }
        .timewindow label { font-size: 11px; color: var(--_muted); display: inline-flex; flex-direction: column; gap: 2px; }
        .timewindow input { font: inherit; font-size: 12px; padding: 4px 6px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: var(--_text); }
      </style>
      <div class="toolbar" part="toolbar">
        <div class="chips" part="filters">
          <button class="chip status-chip" type="button" data-status="" aria-pressed="true">All</button>
          ${RUN_STATUSES.map((s) => `<button class="chip status-chip" type="button" data-status="${s}" aria-pressed="false">${escapeHtml(s)}</button>`).join('')}
        </div>
        <div class="search"><arazzo-workflow-id-input class="wf-search" placeholder="Filter by workflowId…"></arazzo-workflow-id-input></div>
        <div class="search"><input class="tag-search" type="search" placeholder="Tags (space-separated, AND)…" aria-label="Filter by tags"></div>
        <div class="search"><input class="corr-search" type="search" placeholder="Correlation id…" aria-label="Filter by correlation id"></div>
        <label class="toggle"><input type="checkbox" id="autorefresh"> auto-refresh</label>
        <button class="refresh ghost" type="button" title="Refresh">↻</button>
        <button class="purge-btn danger" type="button" ${this.hasScope('runs:purge') ? '' : 'hidden'}>Purge…</button>
      </div>
      <div class="timewindow" part="time-filters">
        <fieldset>
          <legend>Created</legend>
          <label>after<input type="datetime-local" data-attr="created-after"></label>
          <label>before<input type="datetime-local" data-attr="created-before"></label>
        </fieldset>
        <fieldset>
          <legend>Updated</legend>
          <label>after<input type="datetime-local" data-attr="updated-after"></label>
          <label>before<input type="datetime-local" data-attr="updated-before"></label>
        </fieldset>
        <button class="clear-time ghost" type="button">Clear dates</button>
      </div>
      <div class="layout" part="layout">
        <arazzo-runs-table selectable part="table"></arazzo-runs-table>
        <div class="detail-pane"></div>
      </div>
      <arazzo-purge-dialog></arazzo-purge-dialog>
    `;
    const table = this.$('arazzo-runs-table');
    if (scopes) table.setAttribute('scopes', scopes);
  }

  wire() {
    const table = this.$('arazzo-runs-table');
    const pane = this.$('.detail-pane');
    const layout = this.$('.layout');

    table.addEventListener('run-selected', (e) => {
      this.showDetail(e.detail.run);
      this.emit('run-selected', e.detail);
    });
    table.addEventListener('error', (e) => this.emit('error', e.detail));

    this.$$('.status-chip').forEach((chip) => chip.addEventListener('click', () => {
      this.$$('.status-chip').forEach((c) => c.setAttribute('aria-pressed', String(c === chip)));
      const status = chip.dataset.status;
      if (status) table.setAttribute('status', status); else table.removeAttribute('status');
    }));

    // Debounced text filters → table attributes (workflowId, tags, correlationId).
    const debounced = (input, apply) => input.addEventListener('input', (e) => {
      clearTimeout(input._t);
      const value = e.target.value.trim();
      input._t = setTimeout(() => apply(value), 300);
    });
    // <arazzo-workflow-id-input> owns its own catalog autocomplete; its input event bubbles out to us.
    debounced(this.$('.wf-search'), (v) => v ? table.setAttribute('workflow-id', v) : table.removeAttribute('workflow-id'));
    debounced(this.$('.tag-search'), (v) => v ? table.setAttribute('tags', v) : table.removeAttribute('tags'));
    debounced(this.$('.corr-search'), (v) => v ? table.setAttribute('correlation-id', v) : table.removeAttribute('correlation-id'));

    this.$$('.timewindow input[data-attr]').forEach((input) => input.addEventListener('change', () => {
      const attr = input.dataset.attr;
      if (input.value) table.setAttribute(attr, new Date(input.value).toISOString());
      else table.removeAttribute(attr);
    }));
    this.$('.clear-time').addEventListener('click', () => {
      this.$$('.timewindow input[data-attr]').forEach((i) => { i.value = ''; table.removeAttribute(i.dataset.attr); });
    });

    this.$('.refresh').addEventListener('click', () => table.reload());
    this.$('#autorefresh').addEventListener('change', () => this.applyPoll());

    const purgeDialog = this.$('arazzo-purge-dialog');
    this.$('.purge-btn').addEventListener('click', () => { purgeDialog.client = this.buildClient(); purgeDialog.open(); });
    purgeDialog.addEventListener('purge-completed', (e) => { table.reload(); this.emit('purge-completed', e.detail); });
    purgeDialog.addEventListener('error', (e) => this.emit('error', e.detail));

    // Detail events bubble (composed) up to here; listen on the pane.
    pane.addEventListener('run-changed', (e) => { table.refresh(); this.emit('run-changed', e.detail); });
    pane.addEventListener('run-deleted', (e) => { table.refresh(); this.clearDetail(); this.emit('run-deleted', e.detail); });
    pane.addEventListener('close', () => this.clearDetail());
    pane.addEventListener('error', (e) => this.emit('error', e.detail));

    this._layout = layout;
    this._pane = pane;
  }

  showDetail(run) {
    if (!run) return this.clearDetail();
    let detail = this._pane.querySelector('arazzo-run-detail');
    if (!detail) {
      detail = document.createElement('arazzo-run-detail');
      const scopes = this.getAttribute('scopes');
      if (scopes) detail.setAttribute('scopes', scopes);
      detail.client = this.buildClient();
      this._pane.replaceChildren(detail);
    }
    detail.run = run; // sets runid + renders; the element refreshes detail itself
    this._layout.classList.add('has-selection');
  }

  clearDetail() {
    this._pane.replaceChildren();
    this._layout.classList.remove('has-selection');
  }
}

define('arazzo-control-plane', ArazzoControlPlane);
export { ArazzoControlPlane };
