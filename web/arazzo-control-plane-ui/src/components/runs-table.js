// <arazzo-runs-table> — lists runs with filters and keyset pagination.
//
//   <arazzo-runs-table base-url="/arazzo/v1" status="Faulted" selectable poll="5000"></arazzo-runs-table>
//
// Attributes : base-url, status, workflow-id, page-size (default 100), poll (ms; 0/absent = off), selectable
// Properties : .client, .filters = { status, workflowId }
// Events     : run-selected {run}, loaded {count, hasMore}, error {problem}
// Parts      : table, row, cell, status, pager, filters

import { ArazzoElement, SHARED_CSS, escapeHtml, relativeTime, absoluteTime, countdown, copyToClipboard, define } from './base.js';
import './status-badge.js';

class ArazzoRunsTable extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'status', 'workflow-id', 'created-after', 'created-before', 'updated-after', 'updated-before', 'tags', 'correlation-id', 'page-size', 'poll', 'selectable'];
  }

  constructor() {
    super();
    /** @private */ this._runs = [];
    /** @private */ this._history = []; // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextToken = null;
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._selectedId = null;
    /** @private */ this._pollTimer = null;
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
    this.syncPolling();
  }

  disconnectedCallback() {
    this.stopPolling();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'poll') {
      this.syncPolling();
    } else if (name === 'selectable') {
      this.renderBody();
    } else {
      this.reload(); // a filter or target changed — back to page 1
    }
  }

  /** Imperative filter set (equivalent to the `status` / `workflow-id` attributes). */
  get filters() {
    return { status: this.getAttribute('status') || undefined, workflowId: this.getAttribute('workflow-id') || undefined };
  }

  set filters(value = {}) {
    if (value.status) this.setAttribute('status', value.status); else this.removeAttribute('status');
    if (value.workflowId) this.setAttribute('workflow-id', value.workflowId); else this.removeAttribute('workflow-id');
  }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 100;
  }

  requestRender() {
    this.reload();
  }

  // ---- loading ----------------------------------------------------------------------------------

  /** Reload from page 1 (resets the keyset cursor). */
  reload() {
    this._history = [];
    this._currentToken = undefined;
    this.load();
  }

  /** @param {boolean} [silent] When true, keep current rows visible (no skeleton) — used by polling. */
  async load(silent = false) {
    const client = this.client;
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url attribute or a .client property.' };
      this.renderBody();
      return;
    }

    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    if (!silent) this.renderBody();

    try {
      const { runs, nextPageToken } = await client.listRuns({
        status: this.filters.status,
        workflowId: this.filters.workflowId,
        createdAfter: this.getAttribute('created-after') || undefined,
        createdBefore: this.getAttribute('created-before') || undefined,
        updatedAfter: this.getAttribute('updated-after') || undefined,
        updatedBefore: this.getAttribute('updated-before') || undefined,
        tags: (this.getAttribute('tags') || '').split(/[,\s]+/).filter(Boolean),
        correlationId: this.getAttribute('correlation-id') || undefined,
        limit: this.pageSize,
        pageToken: this._currentToken,
      });
      if (seq !== this._reqSeq) return; // a newer request superseded this one
      this._runs = runs;
      this._nextToken = nextPageToken;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: runs.length, hasMore: !!nextPageToken });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  nextPage() {
    if (!this._nextToken) return;
    this._history.push(this._currentToken);
    this._currentToken = this._nextToken;
    this.load();
  }

  prevPage() {
    if (!this._history.length) return;
    this._currentToken = this._history.pop();
    this.load();
  }

  /** Re-fetch the current page without resetting the cursor (e.g. after an external action). */
  refresh() {
    this.load(true);
  }

  // ---- polling ----------------------------------------------------------------------------------

  syncPolling() {
    this.stopPolling();
    const ms = Number(this.getAttribute('poll')) || 0;
    if (ms > 0) this._pollTimer = setInterval(() => this.load(true), ms);
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
        .wrap { border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; background: var(--_bg); }
        table { width: 100%; border-collapse: collapse; }
        thead th {
          text-align: left; font-size: 12px; font-weight: 600; color: var(--_muted);
          padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); white-space: nowrap;
        }
        tbody td { padding: 9px 12px; border-bottom: 1px solid var(--_border); vertical-align: middle; }
        tbody tr:last-child td { border-bottom: none; }
        tbody tr.selectable { cursor: pointer; }
        tbody tr.selectable:hover { background: var(--_surface); }
        tbody tr[aria-selected="true"] { background: color-mix(in srgb, var(--_accent) 12%, transparent); }
        .id { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }
        .copy { font-size: 12px; padding: 0 6px; margin-left: 6px; line-height: 1.4; vertical-align: baseline; }
        .wf { font-weight: 600; }
        .wait, .err { font-size: 12px; }
        .err { color: var(--arazzo-status-faulted, #d4351c); }
        .tags { display: flex; gap: 4px; flex-wrap: wrap; }
        .tag { font-size: 11px; padding: 1px 7px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_muted); white-space: nowrap; }
        .skl { height: 12px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .pager { display: flex; align-items: center; gap: 10px; padding: 9px 12px; background: var(--_surface); border-top: 1px solid var(--_border); }
        .pager .grow { flex: 1; }
        .pager .count { font-size: 12px; color: var(--_muted); }
      </style>
      <div class="wrap" part="table">
        <table>
          <thead>
            <tr>
              <th>Status</th><th>Workflow</th><th>Environment</th><th>Run</th><th>Age</th><th>Waiting on</th><th>Error</th><th>Tags</th>
            </tr>
          </thead>
          <tbody part="rows"></tbody>
        </table>
        <div class="pager" part="pager">
          <button class="prev ghost" type="button">‹ Prev</button>
          <button class="next ghost" type="button">Next ›</button>
          <span class="grow"></span>
          <span class="count"></span>
        </div>
      </div>
    `;
    this.$('.prev').addEventListener('click', () => this.prevPage());
    this.$('.next').addEventListener('click', () => this.nextPage());
  }

  renderBody() {
    const tbody = this.$('tbody');
    if (!tbody) return;
    const selectable = this.hasAttribute('selectable');

    if (this._error) {
      tbody.innerHTML = `<tr><td colspan="8">
        <div class="error-banner">
          <span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span>
          <button class="retry" type="button">Retry</button>
        </div></td></tr>`;
      tbody.querySelector('.retry').addEventListener('click', () => this.load());
      this.updatePager();
      return;
    }

    if (this._loading && this._runs.length === 0) {
      tbody.innerHTML = Array.from({ length: 4 }, () =>
        `<tr>${'<td><div class="skl"></div></td>'.repeat(8)}</tr>`).join('');
      this.updatePager();
      return;
    }

    if (this._runs.length === 0) {
      tbody.innerHTML = `<tr><td colspan="8"><div class="empty">No runs match the current filters.</div></td></tr>`;
      this.updatePager();
      return;
    }

    tbody.innerHTML = this._runs.map((run) => this.renderRow(run, selectable)).join('');

    if (selectable) {
      this.$$('tbody tr.selectable').forEach((tr) => {
        tr.addEventListener('click', (e) => {
          if (e.target.closest('button')) return; // copy button etc. shouldn't select
          this.select(tr.dataset.id);
        });
      });
    }
    this.$$('button.copy').forEach((btn) => btn.addEventListener('click', async (e) => {
      e.stopPropagation();
      if (await copyToClipboard(btn.dataset.id)) {
        btn.textContent = '✓';
        setTimeout(() => { btn.textContent = '⧉'; }, 1200);
      }
    }));
    this.updatePager();
  }

  renderRow(run, selectable) {
    const waiting = run.dueAt
      ? `<span class="wait muted" title="${escapeHtml(absoluteTime(run.dueAt))}">⏱ ${escapeHtml(countdown(run.dueAt))}</span>`
      : run.awaitingChannel
        ? `<span class="wait muted">✉ ${escapeHtml(run.awaitingChannel)}${run.awaitingCorrelationId ? ' · ' + escapeHtml(run.awaitingCorrelationId) : ''}</span>`
        : '';
    const err = run.errorType ? `<span class="err" title="${escapeHtml(run.errorType)}">${escapeHtml(run.errorType)}</span>` : '';
    const tags = Array.isArray(run.tags) && run.tags.length > 0
      ? `<div class="tags">${run.tags.map((t) => `<span class="tag">${escapeHtml(t)}</span>`).join('')}</div>`
      : '';
    const sel = this._selectedId === run.id ? ' aria-selected="true"' : '';
    return `
      <tr part="row" class="${selectable ? 'selectable' : ''}" data-id="${escapeHtml(run.id)}"${sel}>
        <td part="cell"><arazzo-status-badge part="status" status="${escapeHtml(run.status)}"></arazzo-status-badge></td>
        <td part="cell" class="wf">${escapeHtml(run.workflowId)}</td>
        <td part="cell" class="env">${run.environment ? `<span class="tag">${escapeHtml(run.environment)}</span>` : '<span class="muted">—</span>'}</td>
        <td part="cell" class="id"><span title="${escapeHtml(run.id)}">${escapeHtml(shortId(run.id))}</span><button class="copy ghost" type="button" data-id="${escapeHtml(run.id)}" title="Copy run id" aria-label="Copy run id">⧉</button></td>
        <td part="cell" class="muted" title="${escapeHtml(absoluteTime(run.createdAt))}">${escapeHtml(relativeTime(run.createdAt))}</td>
        <td part="cell">${waiting}</td>
        <td part="cell">${err}</td>
        <td part="cell">${tags}</td>
      </tr>`;
  }

  updatePager() {
    const prev = this.$('.prev');
    const next = this.$('.next');
    if (prev) prev.disabled = this._history.length === 0 || this._loading;
    if (next) next.disabled = !this._nextToken || this._loading;
    const count = this.$('.count');
    if (count) {
      count.textContent = this._loading
        ? 'Loading…'
        : `${this._runs.length} run${this._runs.length === 1 ? '' : 's'}${this._history.length ? ` · page ${this._history.length + 1}` : ''}`;
    }
  }

  /** Select a run by id (highlights the row and emits `run-selected`). */
  select(runId) {
    this._selectedId = runId;
    this.$$('tbody tr').forEach((tr) => {
      tr.setAttribute('aria-selected', String(tr.dataset.id === runId));
    });
    const run = this._runs.find((r) => r.id === runId);
    this.emit('run-selected', { run });
  }
}

function shortId(id) {
  const s = String(id ?? '');
  return s.length > 12 ? `${s.slice(0, 8)}…${s.slice(-3)}` : s;
}

define('arazzo-runs-table', ArazzoRunsTable);
export { ArazzoRunsTable };
