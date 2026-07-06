// <arazzo-workspace-table> — lists the designer's working copies (workflow-designer design §4.1):
// mutable Arazzo documents saved during development without minting catalog versions.
//
//   <arazzo-workspace-table base-url="/arazzo/v1" selectable></arazzo-workspace-table>
//
// Attributes : base-url, page-size (default 50), selectable, can-write (shows New/Delete actions)
// Properties : .client
// Events     : working-copy-selected {workingCopy}, working-copy-created {workingCopy},
//              working-copy-deleted {id}, loaded {count, hasMore}, error {problem}
// Parts      : table, row, cell, pager, actions
//
// The workspace counterpart of <arazzo-catalog-table>: keyset-paged by id over listWorkingCopies
// (summaries — the documents stay server-side until one is opened), Prev/Next over the store
// cursor, explicit loading/empty/error states. Selecting a row emits `working-copy-selected` with
// the SUMMARY; the designer shell then getWorkingCopy()s the full document itself. "New" creates a
// blank working copy (create-from-version/Git ride the acquisition flows in later slices) and
// emits `working-copy-created` with the FULL working copy, ready to open.

import { ArazzoElement, SHARED_CSS, PAGER_CSS, escapeHtml, relativeTime, absoluteTime, define } from './base.js';
import './input-dialog.js';
import './pager.js';

class ArazzoWorkspaceTable extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'page-size', 'selectable', 'can-write'];
  }

  constructor() {
    super();
    /** @private */ this._rows = [];
    /** @private */ this._history = []; // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextToken = null;
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._selectedId = null;
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'selectable' || name === 'can-write') this.renderBody();
    else this.reload();
  }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 50;
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
      const { workingCopies, nextPageToken } = await client.listWorkingCopies({
        limit: this.pageSize,
        pageToken: this._currentToken,
      });
      if (seq !== this._reqSeq) return;
      this._rows = workingCopies;
      this._nextToken = nextPageToken;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: workingCopies.length, hasMore: !!nextPageToken });
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
    if (this._history.length === 0) return;
    this._currentToken = this._history.pop();
    this.load();
  }

  /** Re-fetch the current page without resetting the cursor (e.g. after a save elsewhere). */
  refresh() {
    this.load(true);
  }

  // ---- actions ----------------------------------------------------------------------------------

  /** Create a blank working copy and emit it (`working-copy-created`), refreshing the list. */
  async createBlank() {
    const client = this.client;
    if (!client) return;
    try {
      // Ask for a name up front so "untitled" documents don't accumulate — via the kit's standard
      // dialog (system prompts are banned); `promptFn` remains an injectable test seam.
      const answer = this.promptFn
        ? this.promptFn('Name the new working copy:', 'untitled')
        : await this.$('.ask').ask({ title: 'New working copy', field: { label: 'Name', value: 'untitled' } });
      if (answer === null) return;
      const workingCopy = await client.createWorkingCopy({ name: (answer || 'untitled').trim() || 'untitled' });
      this.reload();
      this.emit('working-copy-created', { workingCopy });
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  /** @private */
  async deleteRow(id) {
    const client = this.client;
    if (!client) return;
    const ok = this.confirmFn
      ? this.confirmFn('Delete this working copy? Published catalog versions are unaffected.')
      : await this.$('.ask').ask({ title: 'Delete this working copy?', message: 'Its draft document and scenarios go with it. Published catalog versions are unaffected.', confirmLabel: 'Delete', danger: true });
    if (!ok) return;
    try {
      await client.deleteWorkingCopy(id);
      if (this._selectedId === id) this._selectedId = null;
      this.load(true);
      this.emit('working-copy-deleted', { id });
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        .wrap { border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; background: var(--_bg); }
        .bar { display: flex; justify-content: space-between; align-items: center; gap: 8px; padding: 8px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); }
        .bar h2 { margin: 0; font-size: 13px; font-weight: 600; }
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
        .name { font-weight: 600; }
        .id { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }
        .prov { font-size: 12px; }
        .skl { height: 12px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        button.rowaction { font-size: 12px; padding: 2px 8px; }
        ${PAGER_CSS}
      </style>
      <div class="wrap" part="table">
        <div class="bar" part="actions">
          <h2>Working copies</h2>
          <button class="new" type="button" hidden>New working copy</button>
        </div>
        <table>
          <thead>
            <tr>
              <th>Name</th><th>Based on</th><th>Updated</th><th class="actions-col"></th>
            </tr>
          </thead>
          <tbody part="rows"></tbody>
        </table>
        <arazzo-pager class="pager" part="pager"></arazzo-pager>
      </div>
      <arazzo-input-dialog class="ask"></arazzo-input-dialog>
    `;
    this.$('arazzo-pager').addEventListener('prev', () => this.prevPage());
    this.$('arazzo-pager').addEventListener('next', () => this.nextPage());
    this.$('button.new').addEventListener('click', () => this.createBlank());
  }

  renderBody() {
    const tbody = this.$('tbody');
    if (!tbody) return;
    const selectable = this.hasAttribute('selectable');
    const canWrite = this.hasAttribute('can-write');
    const newButton = this.$('button.new');
    if (newButton) newButton.hidden = !canWrite;

    if (this._error) {
      tbody.innerHTML = `<tr><td colspan="4">
        <div class="error-banner">
          <span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span>
          <button class="retry" type="button">Retry</button>
        </div></td></tr>`;
      tbody.querySelector('.retry').addEventListener('click', () => this.load());
      this.updatePager();
      return;
    }

    if (this._loading && this._rows.length === 0) {
      tbody.innerHTML = Array.from({ length: 4 }, () =>
        `<tr>${'<td><div class="skl"></div></td>'.repeat(4)}</tr>`).join('');
      this.updatePager();
      return;
    }

    if (this._rows.length === 0) {
      tbody.innerHTML = `<tr><td colspan="4"><div class="empty">No working copies yet${canWrite ? ' — create one to start designing' : ''}.</div></td></tr>`;
      this.updatePager();
      return;
    }

    tbody.innerHTML = this._rows.map((w) => this.renderRow(w, selectable, canWrite)).join('');

    if (selectable) {
      this.$$('tbody tr.selectable').forEach((tr) => {
        tr.addEventListener('click', () => this.select(tr.dataset.id));
      });
    }
    this.$$('tbody button.rowaction').forEach((button) => {
      button.addEventListener('click', (e) => {
        e.stopPropagation(); // do not also select the row
        this.deleteRow(button.dataset.id);
      });
    });
    this.updatePager();
  }

  renderRow(w, selectable, canWrite) {
    const updated = w.lastUpdatedAt || w.createdAt;
    const updatedBy = w.lastUpdatedBy || w.createdBy;
    const provenance = w.baseWorkflowId
      ? `<span class="prov">${escapeHtml(w.baseWorkflowId)}${w.basedOnVersion != null ? ` <span class="muted">v${escapeHtml(String(w.basedOnVersion))}</span>` : ''}</span>`
      : '<span class="muted">—</span>';
    const sel = this._selectedId === w.id ? ' aria-selected="true"' : '';
    return `
      <tr part="row" class="${selectable ? 'selectable' : ''}" data-id="${escapeHtml(w.id)}"${sel}>
        <td part="cell" class="name">${escapeHtml(w.name || w.id)}<br><span class="muted id">${escapeHtml(w.id)}</span></td>
        <td part="cell">${provenance}</td>
        <td part="cell" class="muted" title="${escapeHtml(absoluteTime(updated))}">${escapeHtml(relativeTime(updated))}${updatedBy ? ` · ${escapeHtml(updatedBy)}` : ''}</td>
        <td part="cell">${canWrite ? `<button class="rowaction" type="button" data-id="${escapeHtml(w.id)}">Delete</button>` : ''}</td>
      </tr>`;
  }

  updatePager() {
    const info = this._loading
      ? 'Loading…'
      : `${this._rows.length} working ${this._rows.length === 1 ? 'copy' : 'copies'}${this._history.length ? ` · page ${this._history.length + 1}` : ''}`;
    this.$('arazzo-pager')?.update({ hasPrev: this._history.length > 0, hasNext: !!this._nextToken, loading: this._loading, info });
  }

  /** Select a working copy by id (highlights the row and emits `working-copy-selected` with its summary). */
  select(id) {
    this._selectedId = id;
    this.$$('tbody tr').forEach((tr) => {
      tr.setAttribute('aria-selected', String(tr.dataset.id === id));
    });
    const row = this._rows.find((w) => w.id === id);
    if (row) this.emit('working-copy-selected', { workingCopy: row });
  }
}

define('arazzo-workspace-table', ArazzoWorkspaceTable);
export { ArazzoWorkspaceTable };