// <arazzo-run-detail> — the full record for one run, plus its scope-gated actions.
//
//   <arazzo-run-detail base-url="/arazzo/v1" runid="abc" scopes="runs:read runs:write"></arazzo-run-detail>
//
// Attributes : base-url, runid, poll (ms), scopes (space-separated), show-forbidden
// Properties : .client, .run (inject to skip the fetch)
// Events     : run-changed {run}, run-deleted {runId}, run-rerun {runId, rerunOf}, run-open {runId} (show another run: a re-run, or the run this one re-runs), error {problem}, close
// Parts      : header, status, cursor, wait, fault, fault-help, budget, rebudget, rerun-of, actions
//
// Standalone-capable: it embeds <arazzo-resume-dialog> and <arazzo-cancel-button> and performs delete
// itself, so dropping just this element gives a working remediation surface. Layer 2 listens to its
// events to keep the runs list in sync.

import { ArazzoElement, SHARED_CSS, escapeHtml, relativeTime, absoluteTime, countdown, confirmDialog, copyToClipboard, define } from './base.js';
import './status-badge.js';
import './resume-dialog.js';
import './cancel-button.js';
import { BUDGET_LIMITS, formatLimit, describeFault, resumability } from '../execution-budget.js';

// Truly-terminal statuses, used to gate the Cancel action (a terminal run can't be cancelled). Faulted is NOT here:
// it is "terminal-but-recoverable", so it stays cancellable (and resumable).
const TERMINAL = new Set(['Completed', 'Cancelled']);
// Statuses that never self-progress, so the detail stops polling them — TERMINAL plus Faulted, which waits for an
// operator to resume it (that repaints explicitly), so polling it forever just burns requests.
const NO_POLL = new Set(['Completed', 'Cancelled', 'Faulted']);

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

  /** Shows another run by id, dropping what is displayed so nothing of the old run lingers under the new id. */
  showRun(runId) {
    if (!runId) return;
    this._run = null;
    this._error = null;
    if (this.getAttribute('runid') === runId) this.load(); else this.setAttribute('runid', runId);
  }

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
    // Only paint now if there is nothing to show yet (the skeleton). When a summary is already displayed, skip this
    // intermediate render: re-rendering the same body just rebuilds the action buttons under the user's pointer,
    // which — during the summary → authoritative-detail transition — can make a button unclickable under load.
    if (!this._run) this.renderBody();
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
    // Only poll while the run can still change — and never repaint under an open modal: load()
    // rebuilds the body, which would tear a confirm dialog (cancel/resume) out from under the
    // user's pointer mid-decision. The next tick lands after it closes.
    if (ms > 0) {
      this._pollTimer = setInterval(() => {
        if (this.hasOpenModal()) return;
        if (!this._run || !NO_POLL.has(this._run.status)) this.load();
      }, ms);
    }
  }

  /** True while any component under this detail (cancel/resume/…) has an open modal dialog. */
  hasOpenModal() {
    const walk = (root) => [...root.querySelectorAll('*')].some((el) =>
      (el.localName === 'dialog' && el.open) || (el.shadowRoot && walk(el.shadowRoot)));
    return walk(this.shadowRoot);
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
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: visible; }
        /* Sticky header: it stays put while the body scrolls under it (the .detail-pane is the scroll container). */
        header { display: flex; align-items: center; gap: 10px; padding: 12px 14px; background: var(--_surface); border-bottom: 1px solid var(--_border); border-radius: var(--_radius) var(--_radius) 0 0; position: sticky; top: 0; z-index: 5; }
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
        .prog-steps { margin: 6px 0 0; padding-left: 20px; font-size: 12.5px; }
        .prog-steps li { padding: 1px 0; }
        .prog-steps li.dispatched { color: var(--_muted); }
        /* The attested per-step journal: status glyph + attempt + duration, sharing the debug tray's grammar
           (✓/✗/⏭ in the status palette, ↻N for a retried step). Absent for a run recorded before journaling. */
        .jst { font-size: 12px; margin-right: 2px; }
        .jst.ok { color: var(--arazzo-status-completed, #2a8a4a); }
        .jst.bad { color: var(--arazzo-status-faulted, #d4351c); }
        .jst.skip { color: var(--_muted); }
        .jst.retry { color: var(--arazzo-status-suspended, #b58105); }
        .jmeta { font-size: 11px; color: var(--_muted); font-variant-numeric: tabular-nums; white-space: nowrap; }
        .prog-note { margin: 4px 0 0; font-size: 11.5px; color: var(--arazzo-status-suspended, #b45309); }
        .pos-line { font-size: 13px; }
        .pos { font-size: 11px; border: 1px solid var(--_border); border-radius: 999px; padding: 1px 7px; color: var(--_accent); white-space: nowrap; }
        .pos.wait { color: var(--arazzo-status-suspended, #b45309); }
        .pos.fault { color: var(--_danger); }
        .pos.out { color: var(--_muted); cursor: pointer; }
        /* §14: sensitive step outputs withheld from this caller — an amber held-back marker, never the payload. */
        .pos.out.held { color: var(--arazzo-status-quarantined, #d97706); border-color: var(--arazzo-status-quarantined, #d97706); cursor: help; }
        .step-out summary { cursor: pointer; list-style: none; }
        .step-out summary::-webkit-details-marker { display: none; }
        .step-out pre { margin: 4px 0 6px; padding: 8px 10px; background: var(--_surface); border: 1px solid var(--_border); border-radius: 6px; font-size: 11.5px; overflow-x: auto; }
        .block h4 { margin: 0 0 6px; font-size: 12px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--_muted); }
        .fault { border-color: color-mix(in srgb, var(--arazzo-status-faulted, #d4351c) 40%, var(--_border)); }
        .fault .err { color: var(--arazzo-status-faulted, #d4351c); font-family: ui-monospace, monospace; font-size: 12px; }
        /* What one of the platform's own fault types means and what to do about it, under the recorded error. */
        .fault .help { margin-top: 8px; font-size: 12.5px; display: grid; gap: 4px; }
        .fault .help .remedy { color: var(--_muted); }
        .fault .verdict { margin-top: 8px; font-size: 12.5px; font-weight: 600; }
        .fault .verdict.yes { color: var(--arazzo-status-completed, #2a8a4a); }
        .fault .verdict.no { color: var(--arazzo-status-suspended, #b45309); }
        /* A budget is six label/value pairs. The grid sizes the label column to its content and lets values wrap. */
        .limits { margin: 0; display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 4px 16px; font-size: 12.5px; }
        .limits dt { color: var(--_muted); font-size: 12px; }
        .limits dd { margin: 0; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
        .limits.offered { margin-top: 6px; }
        .link { background: none; border: 0; padding: 0; color: var(--_accent); cursor: pointer; font: inherit; text-decoration: underline; }
        .why { font-size: 12px; color: var(--_muted); flex-basis: 100%; }
        .actions { display: flex; gap: 8px; flex-wrap: wrap; padding: 12px 14px; border-top: 1px solid var(--_border); }
        /* The buttons are a display:contents wrapper so resume/delete become direct flex children of .actions,
           laid out with the persistent <arazzo-cancel-button> (which is never re-parented — see renderActions). */
        .action-buttons { display: contents; }
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
        <div class="actions" part="actions" hidden>
          <arazzo-cancel-button hidden></arazzo-cancel-button>
          <span class="action-buttons"></span>
        </div>
      </div>
      <arazzo-resume-dialog></arazzo-resume-dialog>
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
    // The actions bar is persistent (its <arazzo-cancel-button> holds a confirm dialog, so it must never be torn out
    // of the DOM by a body rebuild). Hide it by default; the success path's renderActions un-hides and updates it.
    this.$('.actions').hidden = true;

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
        <dt>Created</dt><dd class="muted" title="${escapeHtml(absoluteTime(run.createdAt))}">${escapeHtml(relativeTime(run.createdAt))}</dd>
        ${run.updatedAt ? `<dt>Updated</dt><dd class="muted" title="${escapeHtml(absoluteTime(run.updatedAt))}">${escapeHtml(relativeTime(run.updatedAt))}</dd>` : ''}
        ${run.environment ? `<dt>Environment</dt><dd part="environment"><div class="tags"><span class="tag">${escapeHtml(run.environment)}</span></div></dd>` : ''}
        ${run.rerunOf ? `<dt>Re-run of</dt><dd part="rerun-of"><button class="link mono rerun-of" type="button" title="Open the run this one re-runs">${escapeHtml(run.rerunOf)}</button></dd>` : ''}
        ${run.correlationId ? `<dt>Correlation</dt><dd class="mono" part="correlation" title="telemetry trace id">${escapeHtml(run.correlationId)}<button class="copy ghost" type="button" part="copy-correlation" title="Copy correlation id" aria-label="Copy correlation id">⧉</button></dd>` : ''}
        ${Array.isArray(run.tags) && run.tags.length > 0 ? `<dt>Tags</dt><dd part="tags"><div class="tags">${run.tags.map((t) => `<span class="tag">${escapeHtml(t)}</span>`).join('')}</div></dd>` : ''}
      </dl>
      <div class="block progress" part="progress" hidden><h4>Progress</h4><div class="prog-body"></div></div>
      ${this.renderWait(run)}
      ${this.renderFault(run)}
      ${this.renderBudget(run)}
    `;
    this.$('.rerun-of')?.addEventListener('click', () => this.emit('run-open', { runId: run.rerunOf }));
    this.$('.copy')?.addEventListener('click', async (e) => {
      const button = e.currentTarget;
      if (await copyToClipboard(run.correlationId)) {
        button.textContent = '✓';
        setTimeout(() => { button.textContent = '⧉'; }, 1200);
      }
    });
    this.renderActions(run);
    this.renderProgress(run);
  }

  /**
   * The run's place in its workflow (the operator's "what has this run done") — an honest
   * projection of what the store attests: the catalogued step list with the run's POSITION marked
   * (the compiled step order the cursor indexes; goto and retries can revisit earlier steps, so
   * steps before the position are "dispatched", never claimed "completed"), plus the wait/fault
   * step in context. Degrades to a plain position line when the document is unavailable.
   */
  async renderProgress(run) {
    const host = this.$('.progress');
    if (!host || run.cursor == null) return;
    const bodyEl = host.querySelector('.prog-body');
    const match = /^(.*)-v(\d+)$/.exec(run.workflowId || '');
    let steps = null;
    let journal = new Map(); // stepId → recorded journal entry (status, attempt, timing, outputs)
    let journalTruncated = false; // a long loop drove the journal past its cap, so the oldest entries were dropped
    if (match && this.client) {
      try {
        const [, recorded] = await Promise.all([
          (async () => {
            if (this._progressFor !== run.workflowId) {
              const doc = await this.client.getCatalogWorkflow(match[1], Number(match[2]));
              const wf = (doc.workflows || []).find((w) => w.workflowId === run.workflowId) || (doc.workflows || [])[0];
              this._progressSteps = wf ? (wf.steps || []).map((st) => st.stepId) : null;
              this._progressFor = run.workflowId;
            }
          })(),
          this.client.getRunSteps(run.id).catch(() => null), // best-effort: older servers have no journal endpoint
        ]);
        steps = this._progressSteps;
        for (const rec of (recorded?.steps || [])) journal.set(rec.stepId, rec);
        journalTruncated = recorded?.truncated === true;
      } catch {
        steps = null;
      }
    }
    if (this._run !== run && this._run?.id !== run.id) return; // superseded selection
    if (!steps || !steps.length) {
      bodyEl.innerHTML = `<div class="muted pos-line">Position: step index ${escapeHtml(String(run.cursor))} <span class="muted">(the step list could not be loaded)</span></div>`;
      host.hidden = false;
      return;
    }
    const atEnd = run.cursor >= steps.length;
    const next = atEnd ? null : steps[run.cursor];
    const waitStep = run.status === 'Suspended' && !atEnd ? next : null;
    const faultStep = run.fault?.stepId || (run.status === 'Faulted' ? next : null);
    // The attested per-step journal in the debug tray's grammar: a status glyph (✓/✗/⏭ in the status palette),
    // the attempt the step settled on (↻N when it took more than one), and its recorded duration.
    const statusGlyph = (rec) => {
      switch (rec.status) {
        case 'Succeeded': return '<span class="jst ok" title="Succeeded">✓</span>';
        case 'Faulted': return '<span class="jst bad" title="Faulted">✗</span>';
        case 'Skipped': return '<span class="jst skip" title="Skipped without executing the step">⏭</span>';
        // The journal holds one entry per attempt (ADR 0068) and a step's row shows its latest, so this is a step
        // whose last attempt failed and is being retried: mid-retry, or parked on its retry timer.
        case 'Retrying': return '<span class="jst retry" title="The last attempt failed and the step is being retried">↻</span>';
        default: return ''; // a run recorded before per-step journaling attests no status
      }
    };
    const stepMeta = (rec) => {
      const bits = [];
      if (rec.status === 'Retrying') bits.push(`<span class="jmeta" title="attempt ${escapeHtml(String(rec.attempt))} failed">attempt ${escapeHtml(String(rec.attempt))}</span>`);
      else if (rec.attempt > 1) bits.push(`<span class="jmeta" title="settled on attempt ${escapeHtml(String(rec.attempt))}">↻${escapeHtml(String(rec.attempt))}</span>`);
      const ms = rec.startedAt && rec.endedAt ? Date.parse(rec.endedAt) - Date.parse(rec.startedAt) : NaN;
      if (Number.isFinite(ms) && ms >= 0) {
        const dur = ms < 1000 ? `${ms}ms` : ms < 60000 ? `${(ms / 1000).toFixed(1)}s` : `${Math.floor(ms / 60000)}m${Math.round((ms % 60000) / 1000)}s`;
        bits.push(`<span class="jmeta" title="started ${escapeHtml(absoluteTime(rec.startedAt))}">${dur}</span>`);
      }
      return bits.join(' ');
    };
    const rowFor = (id, i, dispatched, marks) => {
      const rec = journal.get(id);
      const cls = dispatched ? 'dispatched' : '';
      if (rec) {
        const glyph = statusGlyph(rec);
        const meta = [marks.join(' '), stepMeta(rec)].filter(Boolean).join(' ');
        // A redacted step (§14): the checkpoint attests it recorded outputs, but they are classified sensitive and
        // withheld from this caller — show a held-back affordance, never the payload.
        if (rec.redacted) {
          return `<li class="${cls}">${glyph}<span class="mono">${escapeHtml(id)}</span> ${meta} <span class="pos out held" title="Outputs are classified sensitive and withheld — reading them needs write access to this run.">🔒 outputs withheld</span></li>`;
        }

        // A step that recorded outputs expands to show them, verbatim from the checkpoint. The glyph rides inside
        // the summary so it stays inline with the step id (a block-level <details> would otherwise drop it to its own line).
        if (rec.outputs !== undefined) {
          return `<li class="${cls}"><details class="step-out">
            <summary>${glyph}<span class="mono">${escapeHtml(id)}</span> ${meta} <span class="pos out">outputs</span></summary>
            <pre>${escapeHtml(JSON.stringify(rec.outputs, null, 2))}</pre>
          </details></li>`;
        }

        // A journaled step that produced no outputs still attests its status and timing.
        return `<li class="${cls}">${glyph}<span class="mono">${escapeHtml(id)}</span> ${meta}</li>`;
      }

      return `<li class="${cls}"><span class="mono">${escapeHtml(id)}</span> ${marks.join(' ')}</li>`;
    };
    const listed = new Set(steps);
    const rows = steps.map((id, i) => {
      const marks = [];
      if (i === run.cursor && run.status !== 'Completed') marks.push('<span class="pos">▶ next</span>');
      if (waitStep === id && i === run.cursor) marks.push('<span class="pos wait">waiting</span>');
      if (faultStep === id) marks.push('<span class="pos fault">✗ faulted</span>');
      return rowFor(id, i, i < run.cursor, marks);
    }).join('')
      // Journal entries the document list doesn't carry (a revisited or renamed step) still show.
      + [...journal.keys()].filter((id) => !listed.has(id)).map((id) => rowFor(id, -1, true, [])).join('');
    const summary = run.status === 'Completed'
      ? `All ${steps.length} steps dispatched.`
      : (atEnd
        ? `All ${steps.length} steps dispatched${run.status === 'Suspended' ? ' · waiting (see below)' : ''}.`
        : `Position ${escapeHtml(String(run.cursor))} of ${steps.length}${next ? ` · next: <span class="mono">${escapeHtml(next)}</span>` : ''}`);
    bodyEl.innerHTML = `
      <div class="pos-line">${summary}</div>
      <ol class="prog-steps" title="The compiled step order the run's position indexes. goto and retries can revisit earlier steps, so earlier entries mean dispatched, not completed.">${rows}</ol>${journalTruncated
        ? '<div class="prog-note" title="A long-running loop drove the recorded journal past its cap. The oldest step entries were dropped.">⚠ Journal capped. Older step entries were dropped.</div>'
        : ''}`;
    host.hidden = false;
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
      ${this.renderFaultHelp(run)}
    </div>`;
  }

  /**
   * What one of the platform's own fault types means and what to do about it (ADR 0068). A step's own failure is
   * shown as recorded and gets nothing here. On a budget fault the server says whether a resume would run now
   * (`rebudget`), and the budget the resume would give the run is shown, so the operator sees what to raise.
   */
  renderFaultHelp(run) {
    const described = describeFault(run.fault?.error);
    if (!described) return '';
    let verdict = '';
    if (described.budget && run.rebudget) {
      verdict = `<div class="verdict ${run.rebudget.resumable ? 'yes' : 'no'}" part="rebudget">${run.rebudget.resumable
        ? "Resumable now. A resume re-budgets this run from its environment's current budget:"
        : 'Not resumable yet. This run is still outside the budget a resume would give it:'}</div>
        ${this.renderLimits(run.rebudget.effective, 'offered')}`;
    }
    return `<div class="help" part="fault-help">
      <div class="meaning">${escapeHtml(described.meaning)}</div>
      <div class="remedy">${escapeHtml(described.remedy)}</div>
    </div>${verdict}`;
  }

  renderLimits(budget, extraClass = '') {
    return `<dl class="limits ${extraClass}">${BUDGET_LIMITS.map((limit) =>
      `<dt title="${escapeHtml(limit.hint)}">${escapeHtml(limit.label)}</dt><dd data-limit="${limit.key}">${escapeHtml(formatLimit(limit, budget))}</dd>`).join('')}</dl>`;
  }

  /** The budget frozen into the run (ADR 0068). A run that carries none, the scheduler's, has no block. */
  renderBudget(run) {
    if (!run.budget) return '';
    return `<div class="block" part="budget">
      <h4 title="Resolved when the run started, or by a resume that re-budgeted it. A later change to the environment does not move it.">Budget · frozen into the run</h4>
      ${this.renderLimits(run.budget)}
    </div>`;
  }

  renderActions(run) {
    // Update the persistent actions bar IN PLACE. Only the resume/delete buttons are rebuilt (in .action-buttons);
    // the <arazzo-cancel-button> stays put across renders — never re-parented — so its confirm dialog and its DOM
    // position remain stable while the body around it is rebuilt.
    const host = this.$('.action-buttons');
    const showForbidden = this.hasAttribute('show-forbidden');
    const canWrite = this.hasScope('runs:write');
    const canPurge = this.hasScope('runs:purge');
    const isTerminal = TERMINAL.has(run.status);
    const buttons = [];

    // Resume — faulted runs only, runs:write. A run faulted on its budget is resumable exactly when the server says
    // a re-budget would let it run (ADR 0068). Otherwise the button is disabled and says why, so the operator is
    // not walked into a refusal, and Re-run beside it is the way forward.
    const resume = resumability(run);
    if (run.status === 'Faulted' && (canWrite || showForbidden)) {
      const blocked = !canWrite ? 'Requires runs:write' : (resume.resumable ? '' : resume.reason);
      buttons.push(`<button class="resume primary" type="button" ${blocked ? `disabled title="${escapeHtml(blocked)}"` : ''}>Resume…</button>`);
    }

    // Re-run — any run the caller can read, runs:write: a new run of the same version, environment and inputs,
    // which the server reads from this one. Not offered while the run is still going, where it would double it.
    const settled = run.status === 'Faulted' || isTerminal;
    if (settled && (canWrite || showForbidden)) {
      buttons.push(`<button class="rerun" type="button" ${canWrite ? '' : 'disabled title="Requires runs:write"'}>Re-run…</button>`);
    }

    // Cancel — non-terminal runs, runs:write. Delegated to the embedded <arazzo-cancel-button> (first in the bar).
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
    } else {
      this._cancelButton.hidden = true;
    }

    host.querySelector('.resume')?.addEventListener('click', () => {
      this._resumeDialog.client = this.client;
      this._resumeDialog.open(run);
    });
    host.querySelector('.rerun')?.addEventListener('click', (e) => this.confirmRerun(run, e.currentTarget));
    host.querySelector('.delete')?.addEventListener('click', (e) => this.confirmDelete(run, e.currentTarget));
    if (run.status === 'Faulted' && canWrite && !resume.resumable) {
      host.insertAdjacentHTML('beforeend', `<span class="why" part="resume-blocked">${escapeHtml(resume.reason)}</span>`);
    }

    // Show the bar only when it has something in it.
    this.$('.actions').hidden = !(showCancel || buttons.length > 0);
  }

  /**
   * Re-runs the run from the beginning, as a new run (ADR 0072), behind the kit's own confirm: it repeats the
   * workflow's effects on its sources. One idempotency key is minted per confirmed intent, so a retried or doubled
   * request starts one run. The new run is announced and selected. This run is left as it is.
   */
  async confirmRerun(run, trigger) {
    const confirmed = await confirmDialog(this, {
      title: 'Re-run',
      message: `Start a new run of ${run.workflowId}${run.environment ? ` in ${run.environment}` : ''} with the same inputs as run ${run.id}? It runs from the beginning and repeats the workflow's effects. This run is left as it is.`,
      confirmLabel: 'Re-run',
    });
    if (!confirmed) return;
    const idempotencyKey = `rerun-${run.id}-${(globalThis.crypto?.randomUUID?.() ?? String(Date.now()))}`;
    await this.runAction(trigger, async () => {
      try {
        const accepted = await this.client.rerunRun(run.id, { idempotencyKey });
        this.emit('run-rerun', { runId: accepted.runId, rerunOf: run.id });
        this.emit('run-open', { runId: accepted.runId });
      } catch (err) {
        this._error = err.problem || { title: err.message, status: err.status };
        this.renderBody();
        this.emit('error', { problem: this._error, error: err });
      }
    });
  }

  async confirmDelete(run, trigger) {
    const confirmed = await confirmDialog(this, {
      title: 'Delete run',
      message: `Permanently delete run ${run.id}? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!confirmed) return;
    await this.runAction(trigger, async () => {
      try {
        await this.client.deleteRun(run.id);
        this.emit('run-deleted', { runId: run.id });
        this.emit('close');
      } catch (err) {
        this._error = err.problem || { title: err.message, status: err.status };
        this.renderBody();
        this.emit('error', { problem: this._error, error: err });
      }
    });
  }
}

define('arazzo-run-detail', ArazzoRunDetail);
export { ArazzoRunDetail };
