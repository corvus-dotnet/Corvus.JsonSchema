// <arazzo-runner-authorizations> — the §5.5 runner-authorization approver inbox ("which runners may serve an environment").
//
//   <arazzo-runner-authorizations base-url="/arazzo/v1"></arazzo-runner-authorizations>
//   el.fetch = authFetch;                 // the BFF cookie/CSRF fetch (or set el.client directly)
//
// Attributes : base-url, environment (narrow to one environment's queue), theme (via tokens)
// Properties : .client, .fetch, .authProvider, .environment
// Events     : runner-authorization-decided {authorization, action}, error {problem}
// Parts      : panel, toolbar, table, row, status
//
// A single approver-inbox view over the identity-gated API. A runner cannot self-assert into an environment (receiving its
// runs means receiving its credentials), so it enters Pending on registration; an administrator of that environment
// authorizes it (making it dispatchable) or revokes it. GET /runnerAuthorizations returns every authorization across the
// environments the caller administers, defaulting to Pending (the actionable to-do). Status is an OPTIONAL filter; an
// environment narrows to GET /runnerAuthorizations?environment=… (that one environment's queue; a non-administrator sees
// an empty queue). The view pages with Prev/Next over the store's keyset cursor (server-side status filter).

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { ArazzoElement, SHARED_CSS, escapeHtml, absoluteTime, relativeTime, define } from './base.js';

const STATUS_COLOR = {
  Pending: 'var(--arazzo-status-suspended, #b07d18)',
  Authorized: 'var(--arazzo-status-completed, #2a8a4a)',
  Revoked: 'var(--arazzo-status-faulted, #d4351c)',
};

const STATUS_FILTERS = ['', 'Pending', 'Authorized', 'Revoked'];

class ArazzoRunnerAuthorizations extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'environment', 'theme', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._fetch = undefined;
    /** @private */ this._authProvider = undefined;
    /** @private */ this._auths = [];
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    // null = "use the view default" (Pending — the actionable to-do).
    /** @private */ this._statusFilter = null;
    /** @private */ this._history = [];          // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
    /** @private */ this._query = {};
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected) return;
    if (name === 'base-url') { this._client = undefined; this.reload(); }
    else if (name === 'environment') { this.renderToolbar(); this.reload(); }
    else if (name === 'page-size') { this.reload(); }
    else { this.renderShell(); this.reload(); }
  }

  /** A `fetch`-compatible override (the BFF cookie/CSRF fetch); rebuilds the client. */
  set fetch(fn) { this._fetch = fn; this._client = undefined; if (this.isConnected) this.reload(); }

  /** A function returning the `Authorization` header value (when not using a cookie `fetch`). */
  set authProvider(fn) { this._authProvider = fn; this._client = undefined; if (this.isConnected) this.reload(); }
  get authProvider() { return this._authProvider; }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 50;
  }

  get environment() {
    return this.getAttribute('environment') || '';
  }

  set environment(value) {
    if (value) this.setAttribute('environment', value);
    else this.removeAttribute('environment');
  }

  /** Build (and cache) the Layer-0 client from `base-url` + the supplied `fetch`/`authProvider`. */
  buildClient() {
    if (this._client) return this._client;
    const baseUrl = this.getAttribute('base-url');
    if (!baseUrl) return undefined;
    this._client = new ArazzoControlPlaneClient({ baseUrl, fetch: this._fetch, getAuthHeader: this._authProvider });
    return this._client;
  }

  requestRender() { this.reload(); }

  refresh() { this.reload(); }

  /** The status filter actually in effect: the explicit choice, or the view default (Pending). */
  effectiveStatus() {
    return this._statusFilter ?? 'Pending';
  }

  /** The server query for the current filters (status server-side; an environment narrows to its admin-checked queue). */
  buildQuery() {
    const query = {};
    if (this.environment) query.environment = this.environment;
    const status = this.effectiveStatus();
    if (status) query.status = status;
    return query;
  }

  /** Reload from page 1 (resets the keyset cursor and captures the current filters). */
  reload() {
    this._history = [];
    this._currentToken = undefined;
    this._query = this.buildQuery();
    this.load();
  }

  async load() {
    const client = this.buildClient();
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url attribute or a .client property.' };
      this.renderBody();
      return;
    }

    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      // One keyset page. Status is filtered server-side, so a page never hides matches on a later page; the pageToken is
      // held in `_currentToken` (the cursor), never inside `_query` (the filters), so a filter change starts at page 1.
      const page = await client.listRunnerAuthorizations({ ...this._query, pageToken: this._currentToken, limit: this.pageSize });
      if (seq !== this._reqSeq) return;
      this._auths = page.authorizations;
      this._nextPageToken = page.nextPageToken;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._auths.length, hasMore: !!this._nextPageToken });
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

  // ---- mutations --------------------------------------------------------------------------------

  async decide(auth, action) {
    const client = this.buildClient();
    const call = {
      authorize: (note) => client.authorizeRunner(auth.environment, auth.runnerId, note),
      revoke: (note) => client.revokeRunner(auth.environment, auth.runnerId, note),
    }[action];
    if (!call) return;

    const note = await this.decisionDialog(auth, action);
    if (note === null) return; // cancelled
    try {
      const updated = await call(note);
      this._error = null;
      this.emit('runner-authorization-decided', { authorization: updated, action });
      await this.reload();
    } catch (err) {
      this.showError(err.problem || { title: err.message }, err);
    }
  }

  showError(problem, error) {
    this._error = problem;
    this.renderBody();
    this.emit('error', { problem, error });
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .wrap { border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; background: var(--_bg); }
        .toolbar { display: flex; align-items: center; gap: 8px; padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); flex-wrap: wrap; }
        .toolbar .grow { flex: 1; }
        .toolbar .env { min-width: 220px; flex: 1; font: inherit; font-size: 13px; padding: 5px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        select { font: inherit; font-size: 13px; padding: 5px 28px 5px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        table { width: 100%; border-collapse: collapse; }
        thead th { text-align: left; font-size: 12px; font-weight: 600; color: var(--_muted); padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); white-space: nowrap; }
        tbody td { padding: 9px 12px; border-bottom: 1px solid var(--_border); vertical-align: top; }
        tbody tr:last-child td { border-bottom: none; }
        .runner-id { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; font-weight: 600; }
        .env-name { font-weight: 600; }
        .who { font-weight: 600; }
        .sub { font-size: 11px; color: var(--_muted); }
        .reason { font-size: 12px; color: var(--_muted); margin-top: 2px; max-width: 320px; }
        .badge { display: inline-block; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; color: #fff; white-space: nowrap; }
        .actions { display: flex; gap: 6px; flex-wrap: wrap; justify-content: flex-end; }
        .actions button { font-size: 12px; padding: 4px 9px; }
        .skl { height: 12px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .err { margin: 10px 12px; }
        .foot { display: flex; align-items: center; gap: 12px; padding: 9px 12px; background: var(--_surface); border-top: 1px solid var(--_border); font-size: 12px; color: var(--_muted); }
        .pager { display: flex; gap: 8px; margin-left: auto; }
        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(480px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        dialog .dhead { padding: 14px 16px; font-weight: 700; border-bottom: 1px solid var(--_border); }
        dialog .dbody { padding: 14px 16px; display: grid; gap: 12px; }
        dialog .dfoot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
        dialog label { font-size: 12px; color: var(--_muted); display: grid; gap: 4px; }
        dialog textarea { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); resize: vertical; min-height: 56px; }
      </style>
      <div part="panel">
        <div class="wrap" part="table">
          <div class="toolbar" part="toolbar"></div>
          <div class="err"></div>
          <table>
            <thead></thead>
            <tbody part="rows"></tbody>
          </table>
          <div class="foot" part="foot"></div>
        </div>
      </div>
    `;
    this.renderToolbar();
    this.renderHead();
  }

  renderToolbar() {
    const toolbar = this.$('.toolbar');
    if (!toolbar) return;
    const current = this.effectiveStatus();
    const statusOptions = STATUS_FILTERS.map((s) =>
      `<option value="${s}"${s === current ? ' selected' : ''}>${s || 'all statuses'}</option>`).join('');
    // A single environment is an OPTIONAL filter (absent = every environment you administer).
    toolbar.innerHTML = `
      <input class="env" type="text" placeholder="All environments you administer" value="${escapeHtml(this.environment)}">
      <span class="grow"></span>
      <label class="sub">Status <select class="status">${statusOptions}</select></label>
      <button class="refresh ghost" type="button" title="Refresh">↻</button>`;
    this.$('.env').addEventListener('input', (e) => {
      clearTimeout(this._envTimer);
      const value = e.target.value.trim();
      this._envTimer = setTimeout(() => { this.environment = value; }, 300);
    });
    // Status is a server-side filter, so a change resets to page 1 (it cannot just re-filter a partial page).
    this.$('.status').addEventListener('change', (e) => { this._statusFilter = e.target.value; this.reload(); });
    this.$('.refresh').addEventListener('click', () => this.reload());
  }

  renderHead() {
    const thead = this.$('thead');
    if (!thead) return;
    thead.innerHTML = '<tr><th>Environment</th><th>Runner</th><th>Status</th><th>Registered</th><th>Decision</th><th></th></tr>';
  }

  /** A contextual empty-state message for the current filters. */
  emptyMessage() {
    const status = this.effectiveStatus();
    if (this.environment) {
      return status
        ? `No ${status.toLowerCase()} runner authorizations for ‘${this.environment}’.`
        : `No runner authorizations for ‘${this.environment}’.`;
    }
    return status === 'Pending'
      ? '🎉 You’re all caught up — no runners awaiting authorization across the environments you administer.'
      : status
        ? `No ${status.toLowerCase()} runner authorizations across the environments you administer.`
        : 'No runner authorizations across the environments you administer.';
  }

  renderBody() {
    const tbody = this.$('tbody');
    if (!tbody) return;
    this.renderHead();
    const cols = 6;

    const err = this.$('.err');
    if (err) {
      err.innerHTML = this._error
        ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
        : '';
    }

    if (this._loading && this._auths.length === 0) {
      tbody.innerHTML = Array.from({ length: 3 }, () => `<tr>${'<td><div class="skl"></div></td>'.repeat(cols)}</tr>`).join('');
      this.renderFoot();
      return;
    }

    if (this._auths.length === 0) {
      tbody.innerHTML = `<tr><td colspan="${cols}"><div class="empty">${escapeHtml(this.emptyMessage())}</div></td></tr>`;
      this.renderFoot();
      return;
    }

    tbody.innerHTML = this._auths.map((a) => this.renderRow(a)).join('');
    this.wireRowActions();
    this.renderFoot();
  }

  renderRow(a) {
    const decision = a.decidedBy
      ? `<span class="sub">${escapeHtml(a.decidedBy)}${a.decidedAt ? ' · ' + escapeHtml(relativeTime(a.decidedAt)) : ''}</span>${a.reason ? `<div class="reason">${escapeHtml(a.reason)}</div>` : ''}`
      : '<span class="muted">—</span>';
    // Authorize is offered for a Pending or Revoked runner (re-authorize); Revoke for a Pending or Authorized one.
    const actions = [];
    if (a.status !== 'Authorized') {
      actions.push(`<button class="primary act" type="button" data-key="${escapeHtml(this.rowKey(a))}" data-action="authorize">Authorize</button>`);
    }
    if (a.status !== 'Revoked') {
      actions.push(`<button class="danger act" type="button" data-key="${escapeHtml(this.rowKey(a))}" data-action="revoke">Revoke</button>`);
    }
    return `
      <tr part="row" data-key="${escapeHtml(this.rowKey(a))}">
        <td><span class="env-name">${escapeHtml(a.environment)}</span></td>
        <td><span class="runner-id">${escapeHtml(a.runnerId)}</span></td>
        <td>${this.statusBadge(a)}</td>
        <td><span title="${escapeHtml(absoluteTime(a.createdAt))}">${escapeHtml(relativeTime(a.createdAt))}</span></td>
        <td>${decision}</td>
        <td><div class="actions">${actions.join('')}</div></td>
      </tr>`;
  }

  /** The stable per-row key (the authorization's identity, (environment, runnerId)). */
  rowKey(a) {
    return `${a.environment} ${a.runnerId}`;
  }

  statusBadge(a) {
    const color = STATUS_COLOR[a.status] || 'var(--_muted)';
    return `<span class="badge" part="status" style="background:${color}">${escapeHtml(a.status)}</span>`;
  }

  wireRowActions() {
    this.$$('.act').forEach((btn) => btn.addEventListener('click', () => {
      const auth = this._auths.find((a) => this.rowKey(a) === btn.dataset.key);
      if (auth) this.decide(auth, btn.dataset.action);
    }));
  }

  renderFoot() {
    const foot = this.$('.foot');
    if (!foot) return;
    if (this._loading) { foot.textContent = 'Loading…'; return; }
    const total = this._auths.length;
    const page = this._history.length + 1;
    const label = `${total} authorization${total === 1 ? '' : 's'}${this._history.length ? ` · page ${page}` : ''}`;
    const pending = this.effectiveStatus() !== 'Pending' ? this._auths.filter((a) => a.status === 'Pending').length : 0;
    const parts = [label];
    if (pending > 0) parts.push(`${pending} pending`);
    // Prev/Next keyset paging over the store cursor. The pager only appears when there is somewhere to page to.
    const pager = (this._history.length > 0 || this._nextPageToken)
      ? `<div class="pager">
           <button class="prev ghost" type="button"${this._history.length === 0 || this._loading ? ' disabled' : ''}>‹ Prev</button>
           <button class="next ghost" type="button"${!this._nextPageToken || this._loading ? ' disabled' : ''}>Next ›</button>
         </div>`
      : '';
    foot.innerHTML = `<span>${escapeHtml(parts.join(' · '))}</span>${pager}`;
    const prevBtn = this.$('.prev');
    if (prevBtn) prevBtn.addEventListener('click', () => this.prevPage());
    const nextBtn = this.$('.next');
    if (nextBtn) nextBtn.addEventListener('click', () => this.nextPage());
  }

  // ---- dialogs ----------------------------------------------------------------------------------

  /** A small authorize/revoke dialog capturing an optional reason. Null = cancelled. */
  decisionDialog(auth, action) {
    const meta = {
      authorize: { title: 'Authorize runner', confirm: 'Authorize', cls: 'primary' },
      revoke: { title: 'Revoke runner', confirm: 'Revoke', cls: 'danger' },
    }[action];
    return new Promise((resolve) => {
      const dlg = document.createElement('dialog');
      dlg.className = 'decision-dialog';
      dlg.innerHTML = `
        <form method="dialog">
          <div class="dhead">${escapeHtml(meta.title)}</div>
          <div class="dbody">
            <div class="sub">${action === 'authorize' ? 'Allow' : 'Stop'} <strong>${escapeHtml(auth.runnerId)}</strong> ${action === 'authorize' ? 'to serve' : 'serving'} <strong>${escapeHtml(auth.environment)}</strong></div>
            <label>Note (optional)
              <textarea class="reason-in" placeholder="Recorded with the decision…"></textarea>
            </label>
          </div>
          <div class="dfoot">
            <button class="cancel ghost" type="button">Cancel</button>
            <button class="ok ${meta.cls}" type="button">${escapeHtml(meta.confirm)}</button>
          </div>
        </form>`;
      this.shadowRoot.querySelector('[part="panel"]').appendChild(dlg);
      const finish = (value) => { dlg.close(); dlg.remove(); resolve(value); };
      dlg.querySelector('.cancel').addEventListener('click', () => finish(null));
      dlg.addEventListener('cancel', (e) => { e.preventDefault(); finish(null); });
      dlg.querySelector('.ok').addEventListener('click', () => {
        const reason = dlg.querySelector('.reason-in').value.trim() || undefined;
        finish(reason ? { reason } : {});
      });
      dlg.showModal();
      dlg.querySelector('.reason-in').focus();
    });
  }
}

define('arazzo-runner-authorizations', ArazzoRunnerAuthorizations);
export { ArazzoRunnerAuthorizations };