// <arazzo-schedules> — the durable-schedule management surface (#896).
//
//   <arazzo-schedules base-url="/arazzo/v1" poll="8000"></arazzo-schedules>
//
// Attributes : base-url, poll (ms auto-refresh; omit/0 = off), page-size, scopes (informational — needs runs:read/runs:write)
// Properties : .client, .fetch, .authProvider
// Events     : loaded {count, hasMore}, error {problem}
// Parts      : panel, list, schedule
//
// A schedule is a durable run of the reserved scheduler workflow (#896): a cadence, pinned to an environment, that
// starts a target workflow version on each occurrence through the governed run endpoint. This surface lists them and
// manages their lifecycle — create, run-now (fire the target immediately without touching the cadence), and delete
// (cancel). Creating one needs a runner in the target environment that advertises scheduling; the panel reads the
// runner registry to surface which environments are schedulable up front, and the create call is the authoritative gate.

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { ArazzoElement, SHARED_CSS, PAGER_CSS, escapeHtml, relativeTime, absoluteTime, confirmDialog, define } from './base.js';
import './pager.js';

class ArazzoSchedules extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'poll', 'scopes', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._schedules = [];
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._history = [];           // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
    /** @private */ this._reqSeq = 0;
    /** @private */ this._timer = null;
    /** @private */ this._schedulingEnvs = null;   // Set of environments a runner advertises scheduling in (null until known)
    /** @private */ this._busy = new Set();        // scheduleIds with an in-flight run-now / delete
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
    this.startPolling();
  }

  disconnectedCallback() {
    this.stopPolling();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'poll') this.startPolling();
    else if (name === 'scopes') this.renderBody();
    else if (name === 'page-size') this.reload();
    else { this._client = undefined; this.reload(); } // base-url
  }

  buildClient() {
    if (this._client) return this._client;
    const baseUrl = this.getAttribute('base-url');
    if (baseUrl) this._client = new ArazzoControlPlaneClient({ baseUrl, fetch: this._fetch, getAuthHeader: this._authProvider });
    return this._client;
  }

  set fetch(fn) { this._fetch = fn; this._client = undefined; this.reload(); }

  set authProvider(fn) { this._authProvider = fn; this._client = undefined; this.reload(); }

  requestRender() { this.reload(); }

  refresh() { this.reload(); }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 50;
  }

  startPolling() {
    this.stopPolling();
    const ms = Number(this.getAttribute('poll'));
    if (Number.isFinite(ms) && ms > 0) {
      this._timer = setInterval(() => this.load({ silent: true }), ms);
    }
  }

  stopPolling() {
    if (this._timer) { clearInterval(this._timer); this._timer = null; }
  }

  // ---- data -------------------------------------------------------------------------------------

  reload() {
    this._history = [];
    this._currentToken = undefined;
    this.load();
  }

  async load({ silent = false } = {}) {
    const client = this.buildClient();
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url or .client.' };
      this._schedules = [];
      this.renderBody();
      return;
    }
    const seq = ++this._reqSeq;
    if (!silent) { this._loading = true; this._schedules = []; }
    this._error = null;
    this.renderBody();
    try {
      // The page loads with the runner registry: the set of environments a runner advertises scheduling in drives the
      // up-front "is this environment schedulable" hint in the create form. A failed runner read degrades to no hint
      // (the create call still gates authoritatively) rather than failing the panel.
      const [page, runners] = await Promise.all([
        client.listSchedules({ pageToken: this._currentToken, limit: this.pageSize }),
        this.loadSchedulingEnvs(client).catch(() => null),
      ]);
      if (seq !== this._reqSeq) return;
      if (runners) this._schedulingEnvs = runners;
      this._schedules = page.schedules;
      this._nextPageToken = page.nextPageToken;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._schedules.length, hasMore: !!this._nextPageToken });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  /** The set of environments at least one registered runner advertises scheduling in (#896). */
  async loadSchedulingEnvs(client) {
    const envs = new Set();
    for await (const page of client.listRunnersPaged({ limit: 200 })) {
      for (const r of page.runners) {
        if (r.servesSchedules && r.environment) envs.add(r.environment);
      }
    }
    return envs;
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

  // ---- actions ----------------------------------------------------------------------------------

  async runNow(scheduleId) {
    const ok = await confirmDialog(this, {
      title: 'Run now',
      message: `Start ${escapeHtml(scheduleId)}'s target workflow immediately? This does not change the cadence.`,
      confirmLabel: 'Run now',
    });
    if (!ok) return;
    const client = this.buildClient();
    this._busy.add(scheduleId);
    this.renderBody();
    try {
      const accepted = await client.runScheduleNow(scheduleId);
      this._busy.delete(scheduleId);
      this._flash = { kind: 'ok', text: `Started ${accepted.workflowId} (run ${accepted.runId}).` };
      this.renderBody();
    } catch (err) {
      this._busy.delete(scheduleId);
      this._flash = { kind: 'err', text: (err.problem?.detail || err.problem?.title || err.message) };
      this.renderBody();
    }
  }

  async remove(scheduleId) {
    const ok = await confirmDialog(this, {
      title: 'Delete schedule',
      message: `Delete schedule ${escapeHtml(scheduleId)}? Its cadence stops firing and it reads back as absent. Runs it already started are unaffected.`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!ok) return;
    const client = this.buildClient();
    this._busy.add(scheduleId);
    this.renderBody();
    try {
      await client.deleteSchedule(scheduleId);
      this._busy.delete(scheduleId);
      this._flash = { kind: 'ok', text: `Deleted schedule ${scheduleId}.` };
      this.reload();
    } catch (err) {
      this._busy.delete(scheduleId);
      this._flash = { kind: 'err', text: (err.problem?.detail || err.problem?.title || err.message) };
      this.renderBody();
    }
  }

  // ---- create form ------------------------------------------------------------------------------

  openCreate() {
    this._creating = { open: true, error: null, submitting: false };
    this.renderModal();
  }

  closeCreate() {
    this._creating = null;
    this.renderModal();
  }

  /** The inline scheduling-capability hint for the environment currently typed in the create form (ask #1). */
  capabilityHint(environment) {
    if (!environment || !this._schedulingEnvs) return '';
    if (this._schedulingEnvs.has(environment)) {
      return `<div class="hint ok">A runner in <b>${escapeHtml(environment)}</b> advertises scheduling.</div>`;
    }
    return `<div class="hint warn">No runner in <b>${escapeHtml(environment)}</b> advertises scheduling. Start (or configure) a runner
      there with <code>servesSchedules</code> enabled — otherwise creating a schedule here will be refused.</div>`;
  }

  async submitCreate() {
    const root = this.shadowRoot;
    const val = (sel) => root.querySelector(sel)?.value?.trim() ?? '';
    const scheduleId = val('#f-scheduleId');
    const environment = val('#f-environment');
    const targetBaseWorkflowId = val('#f-base');
    const versionRaw = val('#f-version');
    const cron = val('#f-cron');
    const timeZone = val('#f-tz') || 'UTC';
    const includeSeconds = !!root.querySelector('#f-seconds')?.checked;
    const inputsRaw = val('#f-inputs');

    if (!scheduleId || !environment || !targetBaseWorkflowId || !versionRaw || !cron) {
      this._creating.error = { detail: 'scheduleId, environment, target workflow, version, and cron are required.' };
      this.renderModal();
      return;
    }
    const targetVersionNumber = Number(versionRaw);
    if (!Number.isInteger(targetVersionNumber) || targetVersionNumber < 1) {
      this._creating.error = { detail: 'Version must be a positive whole number.' };
      this.renderModal();
      return;
    }
    const body = { scheduleId, environment, targetBaseWorkflowId, targetVersionNumber, cron, timeZone, includeSeconds };
    if (inputsRaw) {
      try {
        body.targetInputs = JSON.parse(inputsRaw);
      } catch {
        this._creating.error = { detail: 'Inputs must be a valid JSON object, or left blank.' };
        this.renderModal();
        return;
      }
    }

    this._creating.submitting = true;
    this._creating.error = null;
    this.renderModal();
    try {
      await this.buildClient().createSchedule(body);
      this._creating = null;
      this.renderModal(); // tear down the modal overlay before showing the list (a lingering backdrop eats clicks)
      this._flash = { kind: 'ok', text: `Created schedule ${scheduleId}.` };
      this.reload();
    } catch (err) {
      this._creating.submitting = false;
      // A 409 with the no-scheduler guidance is the authoritative form of the ask-1 diagnostic; surface its detail.
      this._creating.error = err.problem || { title: err.message };
      this.renderModal();
    }
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: flex; flex-direction: column; min-height: 0; height: 100%; }
        .panel { flex: 1; min-height: 0; display: flex; flex-direction: column; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        .head { flex: none; padding: 10px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); display: flex; align-items: center; gap: 8px; }
        .head .title { font-weight: 700; }
        .head .grow { flex: 1; }
        .flash { flex: none; margin: 10px 12px 0; font-size: 13px; padding: 8px 10px; border-radius: var(--_radius); border: 1px solid var(--_border); }
        .flash.ok { color: var(--arazzo-status-completed, #1a7f37); border-color: currentColor; }
        .flash.err { color: var(--arazzo-status-faulted, #b3261e); border-color: currentColor; }
        .err { flex: none; margin: 10px 12px; }
        .list { display: grid; flex: 1; min-height: 0; overflow: auto; scrollbar-gutter: stable; }
        .sched { padding: 11px 12px; border-bottom: 1px solid var(--_border); }
        .sched:last-child { border-bottom: none; }
        .shead { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; }
        .sid { font-weight: 600; }
        .senv { font-size: 11px; padding: 1px 8px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_text); font-weight: 600; }
        .sstatus { font-size: 11px; padding: 1px 8px; border-radius: 999px; border: 1px solid currentColor; color: var(--_muted); }
        .sstatus.running, .sstatus.suspended, .sstatus.pending { color: var(--arazzo-status-suspended, #b45309); }
        .sstatus.cancelled, .sstatus.faulted { color: var(--arazzo-status-faulted, #b3261e); }
        .sgrow { flex: 1; }
        .actions { display: inline-flex; gap: 6px; }
        .smeta { display: flex; flex-wrap: wrap; gap: 6px 16px; margin-top: 5px; color: var(--_muted); font-size: 12px; }
        .smeta b { color: var(--_text); font-weight: 600; }
        .smeta code, .cron { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
        ${PAGER_CSS}
        .pager { flex: none; }
        .skl { height: 16px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 11px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        /* create modal */
        .backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.4); display: flex; align-items: center; justify-content: center; z-index: 50; }
        .modal { width: min(560px, 92vw); max-height: 90vh; overflow: auto; background: var(--_bg); border: 1px solid var(--_border); border-radius: var(--_radius); box-shadow: 0 12px 40px rgba(0,0,0,0.3); }
        .modal .mhead { padding: 12px 14px; border-bottom: 1px solid var(--_border); font-weight: 700; }
        .modal .mbody { padding: 14px; display: grid; gap: 10px; }
        .modal .mfoot { padding: 12px 14px; border-top: 1px solid var(--_border); display: flex; justify-content: flex-end; gap: 8px; }
        .field { display: grid; gap: 3px; }
        .field label { font-size: 12px; color: var(--_muted); font-weight: 600; }
        .field input[type=text], .field input[type=number], .field textarea { font: inherit; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_surface); color: var(--_text); }
        .field textarea { min-height: 64px; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }
        .row2 { display: grid; grid-template-columns: 1fr 120px; gap: 10px; }
        .row3 { display: grid; grid-template-columns: 1fr 1fr auto; gap: 10px; align-items: end; }
        .check { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; }
        .hint { font-size: 12px; padding: 7px 9px; border-radius: 6px; border: 1px solid var(--_border); }
        .hint.ok { color: var(--arazzo-status-completed, #1a7f37); border-color: currentColor; }
        .hint.warn { color: var(--arazzo-status-suspended, #b45309); border-color: currentColor; }
        .modal .merror { margin: 0 14px; color: var(--arazzo-status-faulted, #b3261e); font-size: 13px; }
      </style>
      <div class="panel" part="panel">
        <div class="head">
          <span class="title">Schedules</span>
          <span class="grow"></span>
          <button class="new primary" type="button">New schedule</button>
          <button class="refresh ghost" type="button" title="Refresh">↻</button>
        </div>
        <div class="flash" hidden></div>
        <div class="err"></div>
        <div class="list" part="list"></div>
        <arazzo-pager class="pager" part="pager"></arazzo-pager>
      </div>
      <div class="modal-host"></div>
    `;
    this.$('.refresh').addEventListener('click', () => { this._flash = null; this.reload(); });
    this.$('.new').addEventListener('click', () => this.openCreate());
    this.$('arazzo-pager').addEventListener('prev', () => this.prevPage());
    this.$('arazzo-pager').addEventListener('next', () => this.nextPage());
  }

  renderBody() {
    const list = this.$('.list');
    if (!list) return;

    const flash = this.$('.flash');
    if (flash) {
      if (this._flash) { flash.className = `flash ${this._flash.kind}`; flash.textContent = this._flash.text; flash.hidden = false; }
      else { flash.hidden = true; }
    }

    const err = this.$('.err');
    err.innerHTML = this._error
      ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
      : '';

    if (this._loading && this._schedules.length === 0) {
      list.innerHTML = '<div class="skl"></div><div class="skl"></div>';
      this.$('arazzo-pager')?.update({ loading: true, info: 'Loading…' });
      return;
    }
    if (this._schedules.length === 0) {
      list.innerHTML = '<div class="empty">No schedules. Use “New schedule” to create one.</div>';
      this.$('arazzo-pager')?.update({ info: '' });
      return;
    }

    list.innerHTML = this._schedules.map((s) => this.scheduleHtml(s)).join('');
    for (const el of this.shadowRoot.querySelectorAll('[data-run]')) {
      el.addEventListener('click', () => this.runNow(el.getAttribute('data-run')));
    }
    for (const el of this.shadowRoot.querySelectorAll('[data-del]')) {
      el.addEventListener('click', () => this.remove(el.getAttribute('data-del')));
    }

    const parts = [`${this._schedules.length} shown`];
    if (this._history.length) parts.push(`page ${this._history.length + 1}`);
    this.$('arazzo-pager')?.update({ hasPrev: this._history.length > 0, hasNext: !!this._nextPageToken, loading: this._loading, info: parts.join(' · ') });
  }

  scheduleHtml(s) {
    const status = (s.status || '').toLowerCase();
    const busy = this._busy.has(s.scheduleId);
    const next = s.nextOccurrence ? `<span>next <b title="${escapeHtml(absoluteTime(s.nextOccurrence))}">${escapeHtml(relativeTime(s.nextOccurrence))}</b></span>` : '';
    const last = s.lastFiredOccurrence ? `<span>last fired <b title="${escapeHtml(absoluteTime(s.lastFiredOccurrence))}">${escapeHtml(relativeTime(s.lastFiredOccurrence))}</b></span>` : '';
    return `
      <div class="sched" part="schedule">
        <div class="shead">
          <span class="sid">${escapeHtml(s.scheduleId)}</span>
          ${s.environment ? `<span class="senv" title="Pinned to the ${escapeHtml(s.environment)} environment">${escapeHtml(s.environment)}</span>` : ''}
          ${s.status ? `<span class="sstatus ${escapeHtml(status)}">${escapeHtml(s.status)}</span>` : ''}
          <span class="sgrow"></span>
          <span class="actions">
            <button class="ghost" type="button" data-run="${escapeHtml(s.scheduleId)}" ${busy ? 'disabled' : ''}>Run now</button>
            <button class="ghost danger" type="button" data-del="${escapeHtml(s.scheduleId)}" ${busy ? 'disabled' : ''}>Delete</button>
          </span>
        </div>
        <div class="smeta">
          <span>target <b>${escapeHtml(s.targetWorkflowId || (s.targetBaseWorkflowId + ' v' + s.targetVersionNumber))}</b></span>
          <span>cron <b class="cron">${escapeHtml(s.cron)}</b>${s.timeZone ? ` <span class="muted">(${escapeHtml(s.timeZone)})</span>` : ''}</span>
          ${next}
          ${last}
        </div>
      </div>`;
  }

  renderModal() {
    const host = this.$('.modal-host');
    if (!host) return;
    if (!this._creating || !this._creating.open) { host.innerHTML = ''; return; }
    const c = this._creating;
    const errHtml = c.error
      ? `<div class="merror"><strong>${escapeHtml(c.error.title || 'Could not create')}</strong>${c.error.detail ? ' — ' + escapeHtml(c.error.detail) : ''}</div>`
      : '';
    host.innerHTML = `
      <div class="backdrop">
        <div class="modal" role="dialog" aria-modal="true" aria-label="New schedule">
          <div class="mhead">New schedule</div>
          <div class="mbody">
            <div class="field"><label for="f-scheduleId">Schedule id</label><input id="f-scheduleId" type="text" placeholder="nightly-reconcile" autocomplete="off"></div>
            <div class="field"><label for="f-environment">Environment</label><input id="f-environment" type="text" placeholder="development" autocomplete="off"></div>
            <div class="cap"></div>
            <div class="row2">
              <div class="field"><label for="f-base">Target workflow (base id)</label><input id="f-base" type="text" placeholder="nightly-reconcile" autocomplete="off"></div>
              <div class="field"><label for="f-version">Version</label><input id="f-version" type="number" min="1" step="1" placeholder="2"></div>
            </div>
            <div class="row3">
              <div class="field"><label for="f-cron">Cron</label><input id="f-cron" type="text" placeholder="0 3 * * *" autocomplete="off"></div>
              <div class="field"><label for="f-tz">Time zone</label><input id="f-tz" type="text" placeholder="UTC" autocomplete="off"></div>
              <label class="check"><input id="f-seconds" type="checkbox"> seconds field</label>
            </div>
            <div class="field"><label for="f-inputs">Target inputs (JSON, optional)</label><textarea id="f-inputs" placeholder='{"date":"2026-07-20"}'></textarea></div>
          </div>
          ${errHtml}
          <div class="mfoot">
            <button class="cancel ghost" type="button">Cancel</button>
            <button class="submit primary" type="button" ${c.submitting ? 'disabled' : ''}>${c.submitting ? 'Creating…' : 'Create'}</button>
          </div>
        </div>
      </div>`;
    const envInput = host.querySelector('#f-environment');
    const cap = host.querySelector('.cap');
    const paintHint = () => { cap.innerHTML = this.capabilityHint(envInput.value.trim()); };
    envInput.addEventListener('input', paintHint);
    paintHint();
    host.querySelector('.cancel').addEventListener('click', () => this.closeCreate());
    host.querySelector('.submit').addEventListener('click', () => this.submitCreate());
    host.querySelector('.backdrop').addEventListener('click', (e) => { if (e.target === e.currentTarget) this.closeCreate(); });
    host.querySelector('#f-scheduleId').focus();
  }
}

define('arazzo-schedules', ArazzoSchedules);
export { ArazzoSchedules };
