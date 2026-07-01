// <arazzo-availability-requests> — the §7.8 promotion-request + approval surface ("make this version available here").
//
//   <arazzo-availability-requests base-url="/arazzo/v1"></arazzo-availability-requests>
//   el.fetch = authFetch;                 // the BFF cookie/CSRF fetch (or set el.client directly)
//
// Attributes : base-url, view (mine|queue), environment (the queue's environment), theme (via tokens)
// Properties : .client, .fetch, .authProvider, .environment
// Events     : availability-request-submitted {request}, availability-request-decided {request, action}, error {problem}
// Parts      : panel, tabs, toolbar, table, row
//
// Two views over the same identity-gated API:
//   • "My requests" — GET /availabilityRequests?scope=mine returns the caller's own; submit a new request, or withdraw a
//     pending one.
//   • "Approver inbox" — everything the caller can act on: GET /availabilityRequests?scope=queue returns every request
//     across the environments the caller administers, defaulting to Pending (the actionable to-do), oldest-first. Status
//     and a single environment are OPTIONAL filters, never a gate — choosing an environment switches to
//     GET /availabilityRequests?environment=… (that one environment's queue; 403 if not its administrator, shown as a
//     plain banner). Approve (makes the version available, readiness-gated) or deny a pending request.
// Both views page with explicit Prev/Next (keyset, server-side status filter) rather than loading the whole queue.

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { ArazzoElement, SHARED_CSS, PAGER_CSS, escapeHtml, absoluteTime, relativeTime, confirmDialog, define } from './base.js';
import './pager.js';
import './availability-request-dialog.js';

const STATUS_COLOR = {
  Pending: 'var(--arazzo-status-suspended, #b07d18)',
  Approved: 'var(--arazzo-status-completed, #2a8a4a)',
  Denied: 'var(--arazzo-status-faulted, #d4351c)',
  Withdrawn: 'var(--arazzo-muted, #6b7280)',
};

const STATUS_FILTERS = ['', 'Pending', 'Approved', 'Denied', 'Withdrawn'];

class ArazzoAvailabilityRequests extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'view', 'environment', 'theme', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._fetch = undefined;
    /** @private */ this._authProvider = undefined;
    /** @private */ this._requests = [];
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    // null = "use the view's default status" (Pending for the approver inbox — the actionable to-do; all for My requests).
    /** @private */ this._statusFilter = null;
    /** @private */ this._history = [];          // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
    /** @private */ this._query = {};            // the view + filter query (NEVER carries a pageToken)
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected) return;
    if (name === 'base-url') { this._client = undefined; this.reload(); }
    else if (name === 'environment') { if (this.view === 'queue') this.reload(); }
    else if (name === 'view' && oldValue !== newValue) {
      // Switching tabs resets the status filter to the new view's default (Pending for the inbox, all for mine).
      this._statusFilter = null;
      this.renderShell();
      this.reload();
    }
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

  get view() {
    return this.getAttribute('view') === 'queue' ? 'queue' : 'mine';
  }

  set view(value) {
    this.setAttribute('view', value === 'queue' ? 'queue' : 'mine');
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

  /** The status filter actually in effect: the explicit choice, or the view default (Pending for the inbox, all for mine). */
  effectiveStatus() {
    return this._statusFilter ?? (this.view === 'queue' ? 'Pending' : '');
  }

  /** The server query for the current view + filters (status server-side; the approver inbox is scope=queue unless a
   *  single environment is chosen, which narrows to that environment's admin-checked queue). */
  buildQuery() {
    const query = this.view === 'queue'
      ? (this.environment ? { environment: this.environment } : { scope: 'queue' })
      : { scope: 'mine' };
    const status = this.effectiveStatus();
    if (status) query.status = status;
    return query;
  }

  /** Reload from page 1 (resets the keyset cursor and re-derives the view/filter query). */
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
      // One keyset page. The cursor lives in `_currentToken` (Prev/Next walk it); `_query` never carries a pageToken.
      // Status is filtered server-side, so a page never hides matches on a later page.
      const page = await client.listAvailabilityRequests({ ...this._query, pageToken: this._currentToken, limit: this.pageSize });
      if (seq !== this._reqSeq) return;
      this._requests = page.availabilityRequests;
      this._nextPageToken = page.nextPageToken;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._requests.length, view: this.view, hasMore: !!this._nextPageToken });
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

  async decide(request, action) {
    const client = this.buildClient();
    const call = {
      approve: (note) => client.approveAvailabilityRequest(request.id, note),
      deny: (note) => client.denyAvailabilityRequest(request.id, note),
      withdraw: (note) => client.withdrawAvailabilityRequest(request.id, note),
    }[action];

    const note = await this.collectNote(request, action);
    if (note === null) return; // cancelled
    try {
      const updated = await call(note);
      this._error = null;
      this.emit('availability-request-decided', { request: updated, action });
      await this.reload();
    } catch (err) {
      this.showError(err.problem || { title: err.message }, err);
    }
  }

  /** Gather the optional decision note. Null = cancelled. */
  async collectNote(request, action) {
    if (action === 'withdraw') {
      const ok = await confirmDialog(this, {
        title: 'Withdraw request', confirmLabel: 'Withdraw', danger: true,
        message: `Withdraw your pending request to make ${request.baseWorkflowId} v${request.versionNumber} available in '${request.environment}'?`,
      });
      return ok ? {} : null;
    }
    return this.decisionDialog(request, action);
  }

  showError(problem, error) {
    this._error = problem;
    this.renderBody();
    this.emit('error', { problem, error });
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    const view = this.view;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .tabs { display: flex; gap: 4px; margin-bottom: 12px; border-bottom: 1px solid var(--_border); }
        .tabs button { font: inherit; font-size: 14px; border: none; background: none; color: var(--_muted); padding: 8px 14px; border-bottom: 2px solid transparent; margin-bottom: -1px; border-radius: 0; }
        .tabs button[aria-selected="true"] { color: var(--_text); border-bottom-color: var(--_accent); font-weight: 600; }
        .wrap { border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; background: var(--_bg); }
        .toolbar { display: flex; align-items: center; gap: 8px; padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); flex-wrap: wrap; }
        .toolbar .grow { flex: 1; }
        .toolbar .env { min-width: 220px; flex: 1; font: inherit; font-size: 13px; padding: 5px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        select { font: inherit; font-size: 13px; padding: 5px 28px 5px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        table { width: 100%; border-collapse: collapse; }
        thead th { text-align: left; font-size: 12px; font-weight: 600; color: var(--_muted); padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); white-space: nowrap; }
        tbody td { padding: 9px 12px; border-bottom: 1px solid var(--_border); vertical-align: top; }
        tbody tr:last-child td { border-bottom: none; }
        .wf-id { font-weight: 600; }
        .ver { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }
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
        ${PAGER_CSS}
        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(480px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        dialog .dhead { padding: 14px 16px; font-weight: 700; border-bottom: 1px solid var(--_border); }
        dialog .dbody { padding: 14px 16px; display: grid; gap: 12px; }
        dialog .dfoot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
        dialog label { font-size: 12px; color: var(--_muted); display: grid; gap: 4px; }
        dialog textarea { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); resize: vertical; min-height: 56px; }
      </style>
      <div part="panel">
        <div class="tabs" part="tabs" role="tablist">
          <button class="tab-mine" type="button" role="tab" aria-selected="${view === 'mine'}">My requests</button>
          <button class="tab-queue" type="button" role="tab" aria-selected="${view === 'queue'}">Approver inbox</button>
        </div>
        <div class="wrap" part="table">
          <div class="toolbar" part="toolbar"></div>
          <div class="err"></div>
          <table>
            <thead></thead>
            <tbody part="rows"></tbody>
          </table>
          <arazzo-pager class="pager" part="foot"></arazzo-pager>
        </div>
      </div>
    `;
    this.$('.tab-mine').addEventListener('click', () => { if (this.view !== 'mine') this.view = 'mine'; });
    this.$('.tab-queue').addEventListener('click', () => { if (this.view !== 'queue') this.view = 'queue'; });
    this.$('arazzo-pager').addEventListener('prev', () => this.prevPage());
    this.$('arazzo-pager').addEventListener('next', () => this.nextPage());
    this.renderToolbar();
    this.renderHead();
  }

  renderToolbar() {
    const toolbar = this.$('.toolbar');
    if (!toolbar) return;
    const current = this.effectiveStatus();
    const statusOptions = STATUS_FILTERS.map((s) =>
      `<option value="${s}"${s === current ? ' selected' : ''}>${s || 'all statuses'}</option>`).join('');
    if (this.view === 'mine') {
      toolbar.innerHTML = `
        <button class="new primary" type="button">Request promotion…</button>
        <span class="grow"></span>
        <label class="sub">Status <select class="status">${statusOptions}</select></label>
        <button class="refresh ghost" type="button" title="Refresh">↻</button>`;
      this.$('.new').addEventListener('click', () => this.openSubmitDialog());
    } else {
      // The approver inbox: a single environment is an OPTIONAL filter (absent = every environment you administer).
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
    }
    // Status is a server-side filter, so a change reloads the first page (it cannot just re-filter a partial page).
    this.$('.status').addEventListener('change', (e) => { this._statusFilter = e.target.value; this.reload(); });
    this.$('.refresh').addEventListener('click', () => this.reload());
  }

  renderHead() {
    const thead = this.$('thead');
    if (!thead) return;
    thead.innerHTML = this.view === 'mine'
      ? '<tr><th>Workflow</th><th>Version</th><th>Environment</th><th>Status</th><th>Requested</th><th></th></tr>'
      : '<tr><th>Workflow</th><th>Version</th><th>Environment</th><th>Requester</th><th>Status</th><th>Requested</th><th>Decision</th><th></th></tr>';
  }

  /** The number of table columns for the current view (My requests: 6; the approver inbox adds Requester + Decision: 8). */
  columnCount() {
    return this.view === 'mine' ? 6 : 8;
  }

  /** A contextual empty-state message for the current view + filters. */
  emptyMessage() {
    const status = this.effectiveStatus();
    if (this.view === 'mine') {
      return status
        ? `You have no ${status.toLowerCase()} requests.`
        : 'You have no promotion requests. Use “Request promotion…” to ask for a version to be made available in an environment.';
    }
    if (this.environment) {
      return status
        ? `No ${status.toLowerCase()} requests for ‘${this.environment}’.`
        : `No promotion requests for ‘${this.environment}’.`;
    }
    // The inbox: a positive "inbox zero" when nothing is pending, else a plain no-match for a broader filter.
    return status === 'Pending'
      ? '🎉 You’re all caught up — no pending requests across the environments you administer.'
      : status
        ? `No ${status.toLowerCase()} requests across the environments you administer.`
        : 'No promotion requests across the environments you administer.';
  }

  renderBody() {
    const tbody = this.$('tbody');
    if (!tbody) return;
    this.renderHead();
    const cols = this.columnCount();

    const err = this.$('.err');
    if (err) {
      err.innerHTML = this._error
        ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
        : '';
    }

    if (this._loading && this._requests.length === 0) {
      tbody.innerHTML = Array.from({ length: 3 }, () => `<tr>${'<td><div class="skl"></div></td>'.repeat(cols)}</tr>`).join('');
      this.renderFoot();
      return;
    }

    if (this._requests.length === 0) {
      tbody.innerHTML = `<tr><td colspan="${cols}"><div class="empty">${escapeHtml(this.emptyMessage())}</div></td></tr>`;
      this.renderFoot();
      return;
    }

    tbody.innerHTML = this._requests.map((r) => this.view === 'mine' ? this.renderMineRow(r) : this.renderQueueRow(r)).join('');
    this.wireRowActions();
    this.renderFoot();
  }

  renderMineRow(r) {
    const withdraw = r.status === 'Pending'
      ? `<button class="ghost act" type="button" data-id="${escapeHtml(r.id)}" data-action="withdraw">Withdraw</button>`
      : '';
    return `
      <tr part="row" data-id="${escapeHtml(r.id)}">
        <td><span class="wf-id">${escapeHtml(r.baseWorkflowId)}</span>${r.reason ? `<div class="reason">${escapeHtml(r.reason)}</div>` : ''}</td>
        <td><span class="ver">v${escapeHtml(String(r.versionNumber))}</span></td>
        <td><span class="env-name">${escapeHtml(r.environment)}</span></td>
        <td>${this.statusBadge(r)}</td>
        <td><span title="${escapeHtml(absoluteTime(r.createdAt))}">${escapeHtml(relativeTime(r.createdAt))}</span></td>
        <td><div class="actions">${withdraw}</div></td>
      </tr>`;
  }

  renderQueueRow(r) {
    const decision = r.decidedBy
      ? `<span class="sub">${escapeHtml(r.decidedBy)}${r.decidedAt ? ' · ' + escapeHtml(relativeTime(r.decidedAt)) : ''}</span>${r.decisionReason ? `<div class="reason">${escapeHtml(r.decisionReason)}</div>` : ''}`
      : '<span class="muted">—</span>';
    let actions = '';
    if (r.status === 'Pending') {
      actions = `
        <button class="primary act" type="button" data-id="${escapeHtml(r.id)}" data-action="approve">Approve</button>
        <button class="danger act" type="button" data-id="${escapeHtml(r.id)}" data-action="deny">Deny</button>`;
    }
    return `
      <tr part="row" data-id="${escapeHtml(r.id)}">
        <td><span class="wf-id">${escapeHtml(r.baseWorkflowId)}</span></td>
        <td><span class="ver">v${escapeHtml(String(r.versionNumber))}</span></td>
        <td><span class="env-name">${escapeHtml(r.environment)}</span></td>
        <td><span class="who">${escapeHtml(r.requesterLabel || r.createdBy)}</span>${r.reason ? `<div class="reason">${escapeHtml(r.reason)}</div>` : ''}</td>
        <td>${this.statusBadge(r)}</td>
        <td><span title="${escapeHtml(absoluteTime(r.createdAt))}">${escapeHtml(relativeTime(r.createdAt))}</span></td>
        <td>${decision}</td>
        <td><div class="actions">${actions}</div></td>
      </tr>`;
  }

  statusBadge(r) {
    const color = STATUS_COLOR[r.status] || 'var(--_muted)';
    return `<span class="badge" part="status" style="background:${color}">${escapeHtml(r.status)}</span>`;
  }

  wireRowActions() {
    this.$$('.act').forEach((btn) => btn.addEventListener('click', () => {
      const request = this._requests.find((r) => r.id === btn.dataset.id);
      if (request) this.decide(request, btn.dataset.action);
    }));
  }

  renderFoot() {
    let info;
    if (this._loading) {
      info = 'Loading…';
    } else {
      const total = this._requests.length;
      const parts = [`${total} request${total === 1 ? '' : 's'}${this._history.length > 0 ? ` · page ${this._history.length + 1}` : ''}`];
      const pending = this.effectiveStatus() !== 'Pending' ? this._requests.filter((r) => r.status === 'Pending').length : 0;
      if (pending > 0) parts.push(`${pending} pending`);
      info = escapeHtml(parts.join(' · '));
    }
    this.$('arazzo-pager')?.update({ hasPrev: this._history.length > 0, hasNext: !!this._nextPageToken, loading: this._loading, info });
  }

  // ---- dialogs ----------------------------------------------------------------------------------

  openSubmitDialog() {
    let dlg = this.$('arazzo-availability-request-dialog');
    if (!dlg) {
      dlg = document.createElement('arazzo-availability-request-dialog');
      // The dialog submits and emits availability-request-submitted (which bubbles to the host); reload our own list.
      dlg.addEventListener('availability-request-submitted', () => this.reload());
      this.shadowRoot.querySelector('[part="panel"]').appendChild(dlg);
    }
    dlg.client = this.buildClient();
    dlg.open({});
  }

  /** A small approve/deny dialog capturing an optional reason. Null = cancelled. */
  decisionDialog(request, action) {
    const meta = {
      approve: { title: 'Approve request', confirm: 'Approve', cls: 'primary' },
      deny: { title: 'Deny request', confirm: 'Deny', cls: 'danger' },
    }[action];
    return new Promise((resolve) => {
      const dlg = document.createElement('dialog');
      dlg.className = 'decision-dialog';
      dlg.innerHTML = `
        <form method="dialog">
          <div class="dhead">${escapeHtml(meta.title)}</div>
          <div class="dbody">
            <div class="sub">${escapeHtml(request.requesterLabel || request.createdBy)} → make <strong>${escapeHtml(request.baseWorkflowId)} v${escapeHtml(String(request.versionNumber))}</strong> available in <strong>${escapeHtml(request.environment)}</strong></div>
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

define('arazzo-availability-requests', ArazzoAvailabilityRequests);
export { ArazzoAvailabilityRequests };