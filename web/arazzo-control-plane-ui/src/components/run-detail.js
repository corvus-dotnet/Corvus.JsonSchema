// <arazzo-run-detail> — the full record for one run, plus its scope-gated actions.
//
//   <arazzo-run-detail base-url="/arazzo/v1" runid="abc" scopes="runs:read runs:write"></arazzo-run-detail>
//
// Attributes : base-url, runid, poll (ms), scopes (space-separated), show-forbidden
// Properties : .client, .run (inject to skip the fetch)
// Events     : run-changed {run}, run-deleted {runId}, error {problem}, close
// Parts      : header, status, cursor, wait, fault, actions
//
// Standalone-capable: it embeds <arazzo-resume-dialog> and <arazzo-cancel-button> and performs delete
// itself, so dropping just this element gives a working remediation surface. Layer 2 listens to its
// events to keep the runs list in sync.

import { ArazzoElement, SHARED_CSS, escapeHtml, relativeTime, absoluteTime, countdown, confirmDialog, copyToClipboard, define } from './base.js';
import './status-badge.js';
import './resume-dialog.js';
import './cancel-button.js';

const TERMINAL = new Set(['Completed', 'Cancelled']);

class ArazzoRunDetail extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'runid', 'poll', 'scopes', 'show-forbidden'];
  }

  constructor() {
    super();
    /** @private */ this._run = null;
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._pollTimer = null;
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    if (this._run) this.renderBody(); else this.load();
    this.syncPolling();
  }

  disconnectedCallback() {
    this.stopPolling();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected || oldValue === newValue) return;
    if (name === 'poll') this.syncPolling();
    else if (name === 'runid') this.load();
    else this.renderBody();
  }

  /** The current run detail. Set it to render without a fetch. */
  get run() { return this._run; }

  set run(value) {
    // An injected run is typically a list *summary* (no cursor/wait/fault/etag), shown for an instant header;
    // we always follow with a load() for the authoritative detail. Changing runid triggers load() via
    // attributeChangedCallback; when re-selecting the SAME run the attribute doesn't change, so load() here.
    const sameId = value?.id != null && value.id === this.getAttribute('runid');
    this._run = value;
    if (value?.id) this.setAttribute('runid', value.id);
    if (this.isConnected) {
      this.renderBody();
      if (sameId) this.load();
    }
  }

  get runId() { return this.getAttribute('runid') || this._run?.id || null; }

  requestRender() { this.load(); }

  hasScope(scope) {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    // No scopes attribute at all => assume full access (host hasn't told us otherwise).
    return scopes.length === 0 || scopes.includes(scope);
  }

  // ---- loading ----------------------------------------------------------------------------------

  async load() {
    const client = this.client;
    const runId = this.runId;
    if (!client || !runId) return;
    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      const run = await client.getRun(runId);
      if (seq !== this._reqSeq) return;
      this._run = run;
      this._loading = false;
      this.renderBody();
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message, status: err.status };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  syncPolling() {
    this.stopPolling();
    const ms = Number(this.getAttribute('poll')) || 0;
    // Only poll while the run can still change.
    if (ms > 0) this._pollTimer = setInterval(() => { if (!this._run || !TERMINAL.has(this._run.status)) this.load(); }, ms);
  }

  stopPolling() {
    if (this._pollTimer) clearInterval(this._pollTimer);
    this._pollTimer = null;
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        header { display: flex; align-items: center; gap: 10px; padding: 12px 14px; background: var(--_surface); border-bottom: 1px solid var(--_border); }
        header .wf { font-weight: 700; font-size: 15px; }
        header .grow { flex: 1; }
        header .close { font-size: 16px; line-height: 1; }
        dl { margin: 0; padding: 14px; display: grid; grid-template-columns: max-content 1fr; gap: 8px 16px; }
        dt { color: var(--_muted); font-size: 12px; }
        dd { margin: 0; font-size: 13px; }
        .mono { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; word-break: break-all; }
        .copy { font-size: 12px; padding: 0 6px; margin-left: 6px; line-height: 1.4; vertical-align: baseline; }
        .tags { display: flex; gap: 4px; flex-wrap: wrap; }
        .tag { font-size: 11px; padding: 1px 7px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_muted); }
        .block { margin: 0 14px 14px; padding: 10px 12px; border: 1px solid var(--_border); border-radius: var(--_radius); }
        .block h4 { margin: 0 0 6px; font-size: 12px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--_muted); }
        .fault { border-color: color-mix(in srgb, var(--arazzo-status-faulted, #d4351c) 40%, var(--_border)); }
        .fault .err { color: var(--arazzo-status-faulted, #d4351c); font-family: ui-monospace, monospace; font-size: 12px; }
        .actions { display: flex; gap: 8px; flex-wrap: wrap; padding: 12px 14px; border-top: 1px solid var(--_border); }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .pad { padding: 14px; }
      </style>
      <div class="panel" part="panel">
        <header part="header">
          <arazzo-status-badge part="status"></arazzo-status-badge>
          <span class="wf"></span>
          <span class="grow"></span>
          <button class="close ghost" type="button" title="Close" aria-label="Close">✕</button>
        </header>
        <div class="body"></div>
      </div>
      <arazzo-resume-dialog></arazzo-resume-dialog>
      <arazzo-cancel-button hidden></arazzo-cancel-button>
    `;
    this.$('.close').addEventListener('click', () => this.emit('close'));

    this._resumeDialog = this.$('arazzo-resume-dialog');
    this._resumeDialog.addEventListener('resume-submitted', (e) => this.applyResult(e.detail.run));

    this._cancelButton = this.$('arazzo-cancel-button');
    this._cancelButton.addEventListener('run-cancelled', (e) => this.applyResult(e.detail.run));
    this._cancelButton.addEventListener('error', (e) => this.emit('error', e.detail));
  }

  applyResult(run) {
    if (run) {
      this._run = run;
      this.renderBody();
      this.emit('run-changed', { run });
    }
  }

  renderBody() {
    const badge = this.$('arazzo-status-badge');
    const wf = this.$('header .wf');
    const body = this.$('.body');
    if (!body) return;

    if (this._error) {
      badge.removeAttribute('status');
      wf.textContent = this.runId || '';
      const notFound = this._error.status === 404;
      body.innerHTML = `<div class="pad"><div class="error-banner">
        <span><strong>${escapeHtml(notFound ? 'Run not found' : (this._error.title || 'Request failed'))}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span>
        ${notFound ? '' : '<button class="retry" type="button">Retry</button>'}
      </div></div>`;
      body.querySelector('.retry')?.addEventListener('click', () => this.load());
      return;
    }

    if (this._loading && !this._run) {
      wf.innerHTML = '<span class="skl" style="width:140px;display:inline-block"></span>';
      body.innerHTML = `<div class="pad"><div class="skl" style="width:60%"></div><br><div class="skl" style="width:40%"></div></div>`;
      return;
    }

    const run = this._run;
    if (!run) { body.innerHTML = `<div class="empty">No run selected.</div>`; return; }

    badge.setAttribute('status', run.status);
    wf.textContent = run.workflowId;

    body.innerHTML = `
      <dl>
        <dt>Run id</dt><dd class="mono" part="cursor">${escapeHtml(run.id)}</dd>
        <dt>Cursor</dt><dd part="cursor">${run.cursor == null ? '<span class="muted">…</span>' : escapeHtml(String(run.cursor)) + ' <span class="muted">(next step index)</span>'}</dd>
        <dt>Created</dt><dd class="muted" title="${escapeHtml(absoluteTime(run.createdAt))}">${escapeHtml(relativeTime(run.createdAt))}</dd>
        ${run.correlationId ? `<dt>Correlation</dt><dd class="mono" part="correlation" title="telemetry trace id">${escapeHtml(run.correlationId)}<button class="copy ghost" type="button" part="copy-correlation" title="Copy correlation id" aria-label="Copy correlation id">⧉</button></dd>` : ''}
        ${Array.isArray(run.tags) && run.tags.length > 0 ? `<dt>Tags</dt><dd part="tags"><div class="tags">${run.tags.map((t) => `<span class="tag">${escapeHtml(t)}</span>`).join('')}</div></dd>` : ''}
        <dt>ETag</dt><dd class="mono muted">${escapeHtml(run.etag || '—')}</dd>
      </dl>
      ${this.renderWait(run)}
      ${this.renderFault(run)}
      <div class="actions" part="actions"></div>
    `;
    this.$('.copy')?.addEventListener('click', async (e) => {
      const button = e.currentTarget;
      if (await copyToClipboard(run.correlationId)) {
        button.textContent = '✓';
        setTimeout(() => { button.textContent = '⧉'; }, 1200);
      }
    });
    this.renderActions(run);
  }

  renderWait(run) {
    const wait = run.wait;
    if (!wait) return '';
    const rows = wait.kind === 'Timer'
      ? `<div>Timer · due <strong>${escapeHtml(countdown(wait.dueAt))}</strong> <span class="muted" title="${escapeHtml(absoluteTime(wait.dueAt))}">(${escapeHtml(absoluteTime(wait.dueAt))})</span></div>`
      : `<div>Message on channel <strong>${escapeHtml(wait.channel || '—')}</strong>${wait.correlationId ? ` · correlation <span class="mono">${escapeHtml(wait.correlationId)}</span>` : ''}</div>`;
    return `<div class="block" part="wait"><h4>Suspended — waiting</h4>${rows}</div>`;
  }

  renderFault(run) {
    const fault = run.fault;
    if (!fault) return '';
    return `<div class="block fault" part="fault">
      <h4>Fault</h4>
      <div>Step <strong>${escapeHtml(fault.stepId)}</strong> · attempt ${escapeHtml(String(fault.attempt))} · <span class="muted" title="${escapeHtml(absoluteTime(fault.at))}">${escapeHtml(relativeTime(fault.at))}</span></div>
      <div class="err">${escapeHtml(fault.error)}</div>
    </div>`;
  }

  renderActions(run) {
    const host = this.$('.actions');
    const showForbidden = this.hasAttribute('show-forbidden');
    const canWrite = this.hasScope('runs:write');
    const canPurge = this.hasScope('runs:purge');
    const isTerminal = TERMINAL.has(run.status);
    const buttons = [];

    // Resume — faulted runs only, runs:write.
    if (run.status === 'Faulted' && (canWrite || showForbidden)) {
      buttons.push(`<button class="resume primary" type="button" ${canWrite ? '' : 'disabled title="Requires runs:write"'}>Resume…</button>`);
    }

    // Cancel — non-terminal runs, runs:write. Delegated to the embedded <arazzo-cancel-button>.
    const showCancel = !isTerminal && (canWrite || showForbidden);

    // Delete — any status, runs:purge, behind a confirm.
    if (canPurge || showForbidden) {
      buttons.push(`<button class="delete danger" type="button" ${canPurge ? '' : 'disabled title="Requires runs:purge"'}>Delete…</button>`);
    }

    host.innerHTML = buttons.join('');

    if (showCancel) {
      this._cancelButton.client = this.client;
      this._cancelButton.setAttribute('runid', run.id);
      if (canWrite) this._cancelButton.removeAttribute('disabled'); else this._cancelButton.setAttribute('disabled', '');
      this._cancelButton.hidden = false;
      host.prepend(this._cancelButton);
    } else {
      this._cancelButton.hidden = true;
    }

    host.querySelector('.resume')?.addEventListener('click', () => {
      this._resumeDialog.client = this.client;
      this._resumeDialog.open(run);
    });
    host.querySelector('.delete')?.addEventListener('click', () => this.confirmDelete(run));
  }

  async confirmDelete(run) {
    const confirmed = await confirmDialog(this, {
      title: 'Delete run',
      message: `Permanently delete run ${run.id}? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!confirmed) return;
    try {
      await this.client.deleteRun(run.id);
      this.emit('run-deleted', { runId: run.id });
      this.emit('close');
    } catch (err) {
      this._error = err.problem || { title: err.message, status: err.status };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }
}

define('arazzo-run-detail', ArazzoRunDetail);
export { ArazzoRunDetail };
