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
//   • "My requests" — GET /accessRequests (no workflow) returns the caller's own; submit a new request, or
//     withdraw a pending one.
//   • "Approver queue" — GET /accessRequests?baseWorkflowId=… returns that workflow's queue (the caller must
//     administer it, 403 otherwise, shown as a plain banner). Approve / approve-as-eligible / deny a pending
//     request; revoke an approved grant.
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
    return ['base-url', 'view', 'base-workflow-id', 'theme'];
  }

  constructor() {
    super();
    /** @private */ this._fetch = undefined;
    /** @private */ this._authProvider = undefined;
    /** @private */ this._requests = [];
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._statusFilter = '';
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.load();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'base-url') { this._client = undefined; this.load(); }
    else if (name === 'base-workflow-id') { if (this.view === 'queue') this.load(); }
    else { this.renderShell(); this.load(); }
  }

  /** A `fetch`-compatible override (the BFF cookie/CSRF fetch); rebuilds the client. */
  set fetch(fn) { this._fetch = fn; this._client = undefined; if (this.isConnected) this.load(); }

  /** A function returning the `Authorization` header value (when not using a cookie `fetch`). */
  set authProvider(fn) { this._authProvider = fn; this._client = undefined; if (this.isConnected) this.load(); }
  get authProvider() { return this._authProvider; }

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

  requestRender() { this.load(); }

  refresh() { this.load(); }

  async load() {
    const client = this.buildClient();
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url attribute or a .client property.' };
      this.renderBody();
      return;
    }

    // The approver queue is per-workflow; without one chosen there is nothing to fetch.
    if (this.view === 'queue' && !this.baseWorkflowId) {
      this._requests = [];
      this._error = null;
      this._loading = false;
      this.renderBody();
      return;
    }

    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      const query = this.view === 'queue' ? { baseWorkflowId: this.baseWorkflowId } : {};
      // Accumulate every keyset page (the panel lists and filters the whole queue client-side, so it must walk past the
      // server's first page rather than silently stop at it).
      const accessRequests = [];
      for await (const page of client.listAccessRequestsPaged(query)) {
        if (seq !== this._reqSeq) return;
        accessRequests.push(...page.accessRequests);
      }
      if (seq !== this._reqSeq) return;
      this._requests = accessRequests;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: accessRequests.length, view: this.view });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  visibleRequests() {
    return this._statusFilter ? this._requests.filter((r) => r.status === this._statusFilter) : this._requests;
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
      await this.load();
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
    const statusOptions = STATUS_FILTERS.map((s) =>
      `<option value="${s}"${s === this._statusFilter ? ' selected' : ''}>${s || 'all statuses'}</option>`).join('');
    if (this.view === 'mine') {
      toolbar.innerHTML = `
        <button class="new primary" type="button">Request access…</button>
        <span class="grow"></span>
        <label class="sub">Status <select class="status">${statusOptions}</select></label>
        <button class="refresh ghost" type="button" title="Refresh">↻</button>`;
      this.$('.new').addEventListener('click', () => this.openSubmitDialog());
    } else {
      toolbar.innerHTML = `
        <arazzo-workflow-id-input class="wf" placeholder="Workflow you administer…"></arazzo-workflow-id-input>
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
    this.$('.status').addEventListener('change', (e) => { this._statusFilter = e.target.value; this.renderBody(); });
    this.$('.refresh').addEventListener('click', () => this.load());
  }

  renderHead() {
    const thead = this.$('thead');
    if (!thead) return;
    thead.innerHTML = this.view === 'mine'
      ? '<tr><th>Workflow</th><th>Scopes</th><th>Status</th><th>Requested</th><th>Expires</th><th></th></tr>'
      : '<tr><th>Requester</th><th>Scopes</th><th>Status</th><th>Requested</th><th>Decision</th><th></th></tr>';
  }

  renderBody() {
    const tbody = this.$('tbody');
    if (!tbody) return;
    this.renderHead();

    const err = this.$('.err');
    if (err) {
      err.innerHTML = this._error
        ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
        : '';
    }

    if (this._loading && this._requests.length === 0) {
      tbody.innerHTML = Array.from({ length: 3 }, () => `<tr>${'<td><div class="skl"></div></td>'.repeat(6)}</tr>`).join('');
      this.renderFoot();
      return;
    }

    if (this.view === 'queue' && !this.baseWorkflowId) {
      tbody.innerHTML = `<tr><td colspan="6"><div class="empty">Choose a workflow you administer to see its access-request queue.</div></td></tr>`;
      this.renderFoot();
      return;
    }

    const rows = this.visibleRequests();
    if (rows.length === 0) {
      const msg = this.view === 'mine'
        ? 'You have no access requests. Use “Request access…” to ask for run access to a workflow.'
        : 'No access requests for this workflow match the current filter.';
      tbody.innerHTML = `<tr><td colspan="6"><div class="empty">${escapeHtml(msg)}</div></td></tr>`;
      this.renderFoot();
      return;
    }

    tbody.innerHTML = rows.map((r) => this.view === 'mine' ? this.renderMineRow(r) : this.renderQueueRow(r)).join('');
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
    const total = this.visibleRequests().length;
    const pending = this.visibleRequests().filter((r) => r.status === 'Pending').length;
    const parts = [`${total} request${total === 1 ? '' : 's'}`];
    if (pending > 0) parts.push(`${pending} pending`);
    foot.textContent = parts.join(' · ');
  }

  // ---- dialogs ----------------------------------------------------------------------------------

  openSubmitDialog() {
    let dlg = this.$('arazzo-access-request-dialog');
    if (!dlg) {
      dlg = document.createElement('arazzo-access-request-dialog');
      // The dialog submits and emits access-request-submitted (which bubbles to the host); reload our own list.
      dlg.addEventListener('access-request-submitted', () => this.load());
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