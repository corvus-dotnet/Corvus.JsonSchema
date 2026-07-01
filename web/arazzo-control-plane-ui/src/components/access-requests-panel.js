// <arazzo-access-requests> — the §16.5 access-request + approval surface.
//
//   <arazzo-access-requests base-url="/arazzo/v1"></arazzo-access-requests>
//   el.fetch = authFetch;                 // the BFF cookie/CSRF fetch (or set el.client directly)
//
// Attributes : base-url, view (mine|queue), base-workflow-id (the queue's workflow), theme (via tokens)
// Properties : .client, .fetch, .authProvider, .baseWorkflowId
// Events     : access-request-submitted {request}, access-request-decided {request, action}, error {problem}
// Parts      : panel, tabs, toolbar, table, row
//
// Two views over the same identity-gated API:
//   • "My requests" — GET /accessRequests?scope=mine returns the caller's own; submit a new request, or withdraw a
//     pending one.
//   • "Approver queue" — an INBOX of everything the caller can act on: GET /accessRequests?scope=queue returns every
//     request across the workflows the caller administers (§15.4 reverse index), defaulting to Pending (the actionable
//     to-do), oldest-first. Status and a single workflow are OPTIONAL filters, never a gate — choosing a workflow
//     switches to GET /accessRequests?baseWorkflowId=… (that one workflow's queue; 403 if not its administrator, shown
//     as a plain banner). Approve / approve-as-eligible / deny a pending request; revoke an approved grant.
// Both views page with explicit Prev/Next keyset pagination (server-side status filter) rather than loading the whole queue.
// Capability is never ambient (§16.5.3): a grant is what lets a principal act, and only on the workflow it names.

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { ArazzoElement, SHARED_CSS, escapeHtml, absoluteTime, relativeTime, countdown, confirmDialog, define } from './base.js';
import './workflow-id-input.js';
import './access-request-dialog.js';

const STATUS_COLOR = {
  Pending: 'var(--arazzo-status-suspended, #b07d18)',
  Approved: 'var(--arazzo-status-completed, #2a8a4a)',
  Eligible: 'var(--arazzo-accent, #3b6cf6)',
  Denied: 'var(--arazzo-status-faulted, #d4351c)',
  Withdrawn: 'var(--arazzo-muted, #6b7280)',
  Revoked: 'var(--arazzo-status-faulted, #d4351c)',
};

const STATUS_FILTERS = ['', 'Pending', 'Approved', 'Eligible', 'Denied', 'Withdrawn', 'Revoked'];

class ArazzoAccessRequests extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'view', 'base-workflow-id', 'theme', 'page-size'];
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
    else if (name === 'base-workflow-id') { if (this.view === 'queue') this.reload(); }
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

  get baseWorkflowId() {
    return this.getAttribute('base-workflow-id') || '';
  }

  set baseWorkflowId(value) {
    if (value) this.setAttribute('base-workflow-id', value);
    else this.removeAttribute('base-workflow-id');
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
   *  single workflow is chosen, which narrows to that workflow's admin-checked queue). */
  buildQuery() {
    const query = this.view === 'queue'
      ? (this.baseWorkflowId ? { baseWorkflowId: this.baseWorkflowId } : { scope: 'queue' })
      : { scope: 'mine' };
    const status = this.effectiveStatus();
    if (status) query.status = status;
    return query;
  }

  /** Reload from page 1 (resets the keyset cursor). Every view/filter change and every mutation lands here. */
  reload() {
    this._history = [];
    this._currentToken = undefined;
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
    this._nextPageToken = null;
    // The static server query (view + server-side status filter); the keyset cursor is passed explicitly, never stored in it.
    this._query = this.buildQuery();
    this.renderBody();
    try {
      // One keyset page; Prev/Next walk the cursor. Status is filtered server-side, so a page never hides matches on a later
      // page (the old client-side status filter over an accumulate-all read could).
      const page = await client.listAccessRequests({ ...this._query, pageToken: this._currentToken, limit: this.pageSize });
      if (seq !== this._reqSeq) return;
      this._requests = page.accessRequests;
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
      approve: (note) => client.approveAccessRequest(request.id, note),
      'approve-as-eligible': (note) => client.approveAccessRequestAsEligible(request.id, note),
      deny: (note) => client.denyAccessRequest(request.id, note),
      withdraw: (note) => client.withdrawAccessRequest(request.id, note),
      revoke: (note) => client.revokeAccessRequest(request.id, note),
    }[action];

    const note = await this.collectNote(request, action);
    if (note === null) return; // cancelled
    try {
      const updated = await call(note);
      this._error = null;
      this.emit('access-request-decided', { request: updated, action });
      await this.reload();
    } catch (err) {
      this.showError(err.problem || { title: err.message }, err);
    }
  }

  /** Gather the optional decision note (and, for approve-as-eligible, the eligibility window). Null = cancelled. */
  async collectNote(request, action) {
    if (action === 'withdraw') {
      const ok = await confirmDialog(this, {
        title: 'Withdraw request', confirmLabel: 'Withdraw', danger: true,
        message: `Withdraw your pending request for '${request.baseWorkflowId}'?`,
      });
      return ok ? {} : null;
    }
    if (action === 'revoke') {
      const ok = await confirmDialog(this, {
        title: 'Revoke grant', confirmLabel: 'Revoke', danger: true,
        message: `Revoke ${request.requesterLabel || request.subjectClaimValue}'s active grant on '${request.baseWorkflowId}'? Access stops at the next resolution.`,
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
        .toolbar .wf { min-width: 220px; flex: 1; }
        select { font: inherit; font-size: 13px; padding: 5px 28px 5px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        table { width: 100%; border-collapse: collapse; }
        thead th { text-align: left; font-size: 12px; font-weight: 600; color: var(--_muted); padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); white-space: nowrap; }
        tbody td { padding: 9px 12px; border-bottom: 1px solid var(--_border); vertical-align: top; }
        tbody tr:last-child td { border-bottom: none; }
        .wf-id { font-weight: 600; }
        .who { font-weight: 600; }
        .sub { font-size: 11px; color: var(--_muted); }
        .reason { font-size: 12px; color: var(--_muted); margin-top: 2px; max-width: 320px; }
        .scopes { display: flex; gap: 4px; flex-wrap: wrap; }
        .scope { font-size: 11px; padding: 1px 7px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_muted); white-space: nowrap; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
        .badge { display: inline-block; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; color: #fff; white-space: nowrap; }
        .actions { display: flex; gap: 6px; flex-wrap: wrap; justify-content: flex-end; }
        .actions button { font-size: 12px; padding: 4px 9px; }
        .skl { height: 12px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .err { margin: 10px 12px; }
        .foot { display: flex; align-items: center; gap: 12px; padding: 9px 12px; background: var(--_surface); border-top: 1px solid var(--_border); font-size: 12px; color: var(--_muted); }
        .pager { display: flex; align-items: center; gap: 8px; margin-left: auto; }
        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(480px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        dialog .dhead { padding: 14px 16px; font-weight: 700; border-bottom: 1px solid var(--_border); }
        dialog .dbody { padding: 14px 16px; display: grid; gap: 12px; }
        dialog .dfoot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
        dialog label { font-size: 12px; color: var(--_muted); display: grid; gap: 4px; }
        dialog input, dialog textarea { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
        dialog textarea { resize: vertical; min-height: 56px; }
        .checks { display: grid; gap: 6px; }
        .checks label { display: flex; gap: 8px; align-items: center; flex-direction: row; color: var(--_text); }
        .dur { display: flex; gap: 8px; align-items: end; }
        .dur input { width: 90px; }
      </style>
      <div part="panel">
        <div class="tabs" part="tabs" role="tablist">
          <button class="tab-mine" type="button" role="tab" aria-selected="${view === 'mine'}">My requests</button>
          <button class="tab-queue" type="button" role="tab" aria-selected="${view === 'queue'}">Approver queue</button>
        </div>
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
    this.$('.tab-mine').addEventListener('click', () => { if (this.view !== 'mine') this.view = 'mine'; });
    this.$('.tab-queue').addEventListener('click', () => { if (this.view !== 'queue') this.view = 'queue'; });
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
        <button class="new primary" type="button">Request access…</button>
        <span class="grow"></span>
        <label class="sub">Status <select class="status">${statusOptions}</select></label>
        <button class="refresh ghost" type="button" title="Refresh">↻</button>`;
      this.$('.new').addEventListener('click', () => this.openSubmitDialog());
    } else {
      // The approver inbox: a single workflow is an OPTIONAL filter (absent = every workflow you administer), never a gate.
      toolbar.innerHTML = `
        <arazzo-workflow-id-input class="wf" placeholder="All workflows you administer"></arazzo-workflow-id-input>
        <span class="grow"></span>
        <label class="sub">Status <select class="status">${statusOptions}</select></label>
        <button class="refresh ghost" type="button" title="Refresh">↻</button>`;
      const wf = this.$('.wf');
      wf.client = this.buildClient();
      wf.value = this.baseWorkflowId;
      wf.addEventListener('input', (e) => {
        clearTimeout(this._wfTimer);
        const value = e.target.value.trim();
        this._wfTimer = setTimeout(() => { this.baseWorkflowId = value; }, 300);
      });
    }
    // Status is a server-side filter now, so a change reloads from page 1 (it cannot just re-filter a partial page).
    this.$('.status').addEventListener('change', (e) => { this._statusFilter = e.target.value; this.reload(); });
    this.$('.refresh').addEventListener('click', () => this.reload());
  }

  renderHead() {
    const thead = this.$('thead');
    if (!thead) return;
    thead.innerHTML = this.view === 'mine'
      ? '<tr><th>Workflow</th><th>Scopes</th><th>Status</th><th>Requested</th><th>Expires</th><th></th></tr>'
      : '<tr><th>Workflow</th><th>Requester</th><th>Scopes</th><th>Status</th><th>Requested</th><th>Decision</th><th></th></tr>';
  }

  /** The number of table columns for the current view (My requests: 6; the approver inbox adds a Workflow column: 7). */
  columnCount() {
    return this.view === 'mine' ? 6 : 7;
  }

  /** A contextual empty-state message for the current view + filters. */
  emptyMessage() {
    const status = this.effectiveStatus();
    if (this.view === 'mine') {
      return status
        ? `You have no ${status.toLowerCase()} requests.`
        : 'You have no access requests. Use “Request access…” to ask for run access to a workflow.';
    }
    if (this.baseWorkflowId) {
      return status
        ? `No ${status.toLowerCase()} requests for ‘${this.baseWorkflowId}’.`
        : `No access requests for ‘${this.baseWorkflowId}’.`;
    }
    // The inbox: a positive "inbox zero" when nothing is pending, else a plain no-match for a broader filter.
    return status === 'Pending'
      ? '🎉 You’re all caught up — no pending requests across the workflows you administer.'
      : status
        ? `No ${status.toLowerCase()} requests across the workflows you administer.`
        : 'No access requests across the workflows you administer.';
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
    const expires = r.grantedUntil
      ? `<span title="${escapeHtml(absoluteTime(r.grantedUntil))}">${escapeHtml(countdown(r.grantedUntil))}</span>`
      : '<span class="muted">—</span>';
    const withdraw = r.status === 'Pending'
      ? `<button class="ghost act" type="button" data-id="${escapeHtml(r.id)}" data-action="withdraw">Withdraw</button>`
      : '';
    return `
      <tr part="row" data-id="${escapeHtml(r.id)}">
        <td><span class="wf-id">${escapeHtml(r.baseWorkflowId)}</span>${r.reason ? `<div class="reason">${escapeHtml(r.reason)}</div>` : ''}</td>
        <td>${this.scopeChips(r.requestedScopes)}</td>
        <td>${this.statusBadge(r)}</td>
        <td><span title="${escapeHtml(absoluteTime(r.createdAt))}">${escapeHtml(relativeTime(r.createdAt))}</span></td>
        <td>${expires}</td>
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
        <button class="ghost act" type="button" data-id="${escapeHtml(r.id)}" data-action="approve-as-eligible">Make eligible</button>
        <button class="danger act" type="button" data-id="${escapeHtml(r.id)}" data-action="deny">Deny</button>`;
    } else if (r.status === 'Approved') {
      actions = `<button class="danger act" type="button" data-id="${escapeHtml(r.id)}" data-action="revoke">Revoke</button>`;
    }
    return `
      <tr part="row" data-id="${escapeHtml(r.id)}">
        <td><span class="wf-id">${escapeHtml(r.baseWorkflowId)}</span></td>
        <td><span class="who">${escapeHtml(r.requesterLabel || r.subjectClaimValue)}</span><div class="sub">${escapeHtml(r.subjectClaimType)}=${escapeHtml(r.subjectClaimValue)}</div>${r.reason ? `<div class="reason">${escapeHtml(r.reason)}</div>` : ''}</td>
        <td>${this.scopeChips(r.requestedScopes)}</td>
        <td>${this.statusBadge(r)}</td>
        <td><span title="${escapeHtml(absoluteTime(r.createdAt))}">${escapeHtml(relativeTime(r.createdAt))}</span></td>
        <td>${decision}</td>
        <td><div class="actions">${actions}</div></td>
      </tr>`;
  }

  scopeChips(scopes) {
    return `<div class="scopes">${(scopes ?? []).map((s) => `<span class="scope">${escapeHtml(s)}</span>`).join('')}</div>`;
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
    const foot = this.$('.foot');
    if (!foot) return;
    if (this._loading) { foot.textContent = 'Loading…'; return; }
    const total = this._requests.length;
    // The keyset page count is not a total; show what's on this page, with a Prev/Next pager over the store cursor. When the
    // status filter is not Pending, surface the pending count among the shown rows as the actionable subset.
    const page = this._history.length + 1;
    const label = `${total} request${total === 1 ? '' : 's'}${(this._history.length > 0 || this._nextPageToken) ? ` · page ${page}` : ''}`;
    const pending = this.effectiveStatus() !== 'Pending' ? this._requests.filter((r) => r.status === 'Pending').length : 0;
    const parts = [label];
    if (pending > 0) parts.push(`${pending} pending`);
    const pager = (this._history.length > 0 || this._nextPageToken)
      ? `<div class="pager" part="pager">
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

  openSubmitDialog() {
    let dlg = this.$('arazzo-access-request-dialog');
    if (!dlg) {
      dlg = document.createElement('arazzo-access-request-dialog');
      // The dialog submits and emits access-request-submitted (which bubbles to the host); reload our own list from page 1.
      dlg.addEventListener('access-request-submitted', () => this.reload());
      this.shadowRoot.querySelector('[part="panel"]').appendChild(dlg);
    }
    dlg.client = this.buildClient();
    // In the approver-queue view a workflow is already chosen — prefill it, but still let the requester change it.
    dlg.open({ baseWorkflowId: this.view === 'queue' ? this.baseWorkflowId : '' });
  }

  /** A small approve/deny/make-eligible dialog capturing an optional reason (+ eligibility window). Null = cancelled. */
  decisionDialog(request, action) {
    const eligible = action === 'approve-as-eligible';
    const meta = {
      approve: { title: 'Approve request', confirm: 'Approve', cls: 'primary' },
      'approve-as-eligible': { title: 'Make eligible (self-elevation)', confirm: 'Make eligible', cls: 'primary' },
      deny: { title: 'Deny request', confirm: 'Deny', cls: 'danger' },
    }[action];
    return new Promise((resolve) => {
      const dlg = document.createElement('dialog');
      dlg.className = 'decision-dialog';
      dlg.innerHTML = `
        <form method="dialog">
          <div class="dhead">${escapeHtml(meta.title)}</div>
          <div class="dbody">
            <div class="sub">${escapeHtml(request.requesterLabel || request.subjectClaimValue)} → <strong>${escapeHtml(request.baseWorkflowId)}</strong> · ${(request.requestedScopes ?? []).map(escapeHtml).join(', ')}</div>
            <label>Note (optional)
              <textarea class="reason-in" placeholder="Recorded with the decision…"></textarea>
            </label>
            ${eligible ? `<div class="dur"><label>Eligibility window (hours)<input class="win-in" type="number" min="1" step="1" placeholder="standing"></label><span class="sub">Absent = standing eligibility; each activation is independently capped.</span></div>` : ''}
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
        const note = {};
        if (reason) note.reason = reason;
        if (eligible) {
          const hours = Number(dlg.querySelector('.win-in').value);
          if (hours > 0) note.eligibilityWindowSeconds = Math.round(hours * 3600);
        }
        finish(note);
      });
      dlg.showModal();
      dlg.querySelector('.reason-in').focus();
    });
  }
}

define('arazzo-access-requests', ArazzoAccessRequests);
export { ArazzoAccessRequests };