// <arazzo-workspace-table> — lists the designer's working copies (workflow-designer design §4.1):
// mutable Arazzo documents saved during development without minting catalog versions.
//
//   <arazzo-workspace-table base-url="/arazzo/v1" selectable></arazzo-workspace-table>
//
// Attributes : base-url, page-size (default 50), selectable, can-write (shows the New-workflow /
//              New-working-copy (catalog carry-over) / Delete actions)
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
import './catalog-table.js';
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
    /** @private */ this._total = null;         // bounded total across all pages (null until counted)
    /** @private */ this._totalCapped = false;  // true when the true total meets/exceeds the server cap → render "N+"
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
      // Fetch the page and the bounded total (for the footer) together; the count is a no-rows bounded query, and a
      // count failure must not break the list, so it falls back to null (footer then shows the visible page count).
      const [{ workingCopies, nextPageToken }, total] = await Promise.all([
        client.listWorkingCopies({ limit: this.pageSize, pageToken: this._currentToken }),
        client.countWorkingCopies().catch(() => null),
      ]);
      if (seq !== this._reqSeq) return;
      this._rows = workingCopies;
      this._nextToken = nextPageToken;
      this._total = total ? total.count : null;
      this._totalCapped = total ? total.capped : false;
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
        .bar { display: flex; align-items: center; gap: 8px; padding: 8px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); }
        .bar h2 { margin: 0 auto 0 0; font-size: 13px; font-weight: 600; }
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
        .fromcat-dialog { border: 1px solid var(--_border); border-radius: 10px; background: var(--_bg); color: inherit; padding: 0; width: min(640px, 92vw); }
        .fromcat-dialog::backdrop { background: rgba(0, 0, 0, 0.35); }
        .fc-body { padding: 14px; display: grid; gap: 10px; }
        .fc-body h2 { margin: 0; font-size: 14px; }
        .fc-hint { font-size: 12px; color: var(--_muted); }
        .fc-body label { display: grid; gap: 4px; font-size: 12px; color: var(--_muted); }
        .fc-body select, .fc-body input { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: inherit; }
        .fc-actions { display: flex; justify-content: flex-end; gap: 8px; }
        ${PAGER_CSS}
      </style>
      <div class="wrap" part="table">
        <div class="bar" part="actions">
          <h2>Working copies</h2>
          <button class="fromcat" type="button" hidden title="Open a working copy OF an existing catalog workflow — its document and sources carry over as a draft">New working copy…</button>
          <button class="new" type="button" hidden title="Start a brand-new workflow from an empty document">New workflow</button>
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
      <dialog class="fromcat-dialog">
        <div class="fc-body">
          <h2>New working copy</h2>
          <div class="fc-hint">Carry a published catalog workflow into the workspace: its document opens as a draft, its sources attached. Publishing later mints the next version.</div>
          <label>Search the catalog
            <input class="fc-q" type="text" placeholder="name, tag, owner…" spellcheck="false">
          </label>
          <arazzo-catalog-table class="fc-table" selectable status="Active" page-size="6"></arazzo-catalog-table>
          <label>Working copy name
            <input class="fc-name" type="text" spellcheck="false">
          </label>
          <div class="fc-actions">
            <button class="fc-cancel" type="button">Cancel</button>
            <button class="fc-create" type="button" disabled>Create</button>
          </div>
        </div>
      </dialog>
    `;
    this.$('arazzo-pager').addEventListener('prev', () => this.prevPage());
    this.$('arazzo-pager').addEventListener('next', () => this.nextPage());
    this.$('button.new').addEventListener('click', () => this.createBlank());
    this.$('button.fromcat').addEventListener('click', () => this.openFromCatalog());
    this.$('.fc-cancel').addEventListener('click', () => this.$('.fromcat-dialog').close());
    this.$('.fromcat-dialog').addEventListener('cancel', (e) => { e.preventDefault(); this.$('.fromcat-dialog').close(); });
    this.$('.fc-create').addEventListener('click', () => { void this.createFromCatalog(); });
    this.$('.fc-name').addEventListener('input', () => this.updateFromCatalogState());
  }

  /** "New working copy…": browse the catalog (server-side filtered, keyset-paged — hundreds of
   *  workflows stay browsable), carry the picked version into the workspace. */
  openFromCatalog() {
    const dialog = this.$('.fromcat-dialog');
    const table = this.$('.fc-table');
    if (table.client !== this.client) {
      table.client = this.client;
      table.addEventListener('version-selected', (e) => {
        this._fromCatalogPick = e.detail.version;
        this.$('.fc-name').value = e.detail.version.title ?? e.detail.version.baseWorkflowId;
        this.updateFromCatalogState();
      });
      let debounce = 0;
      this.$('.fc-q').addEventListener('input', (e) => {
        clearTimeout(debounce);
        debounce = setTimeout(() => { table.filters = { ...table.filters, q: e.target.value.trim() || undefined }; }, 250);
      });
    }

    this._fromCatalogPick = null;
    this.$('.fc-name').value = '';
    dialog.showModal();
    this.updateFromCatalogState();
  }

  /** @private */
  updateFromCatalogState() {
    this.$('.fc-create').disabled = !this._fromCatalogPick || !this.$('.fc-name').value.trim();
  }

  /** @private */
  async createFromCatalog() {
    const v = this._fromCatalogPick;
    if (!v) return;
    try {
      const workingCopy = await this.client.createWorkingCopy({
        fromBaseWorkflowId: v.baseWorkflowId,
        fromVersionNumber: v.versionNumber,
        name: this.$('.fc-name').value.trim(),
      });
      this.$('.fromcat-dialog').close();
      this.reload();
      this.emit('working-copy-created', { workingCopy });
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.$('.fromcat-dialog').close();
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  renderBody() {
    const tbody = this.$('tbody');
    if (!tbody) return;
    const selectable = this.hasAttribute('selectable');
    const canWrite = this.hasAttribute('can-write');
    const newButton = this.$('button.new');
    if (newButton) newButton.hidden = !canWrite;
    const fromcatButton = this.$('button.fromcat');
    if (fromcatButton) fromcatButton.hidden = !canWrite;

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
    // The footer shows the bounded grand total the caller's reach admits (with "+" when the server capped it), not just
    // the current page's length; it falls back to the visible count if the count query was unavailable.
    const n = this._total != null ? this._total : this._rows.length;
    const shown = this._total != null ? `${this._total}${this._totalCapped ? '+' : ''}` : `${this._rows.length}`;
    const info = this._loading
      ? 'Loading…'
      : `${shown} working ${n === 1 ? 'copy' : 'copies'}${this._history.length ? ` · page ${this._history.length + 1}` : ''}`;
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