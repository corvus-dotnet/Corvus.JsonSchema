// <arazzo-catalog-table> — lists the catalog with ONE row per base workflow (its versions collapse together).
//
//   <arazzo-catalog-table base-url="/arazzo/v1" status="Active" selectable></arazzo-catalog-table>
//
// Attributes : base-url, q, base-workflow-id, status, owner, tags, page-size (default 50), selectable
// Properties : .client, .filters = { q, baseWorkflowId, status, owner, tags }
// Events     : version-selected {version, baseWorkflowId}, loaded {count, hasMore}, error {problem}
// Parts      : table, row, cell, status, pager
//
// The catalog counterpart of <arazzo-runs-table>. Because a base workflow has many immutable versions, the
// list shows one row per base — its representative version (newest Active, else newest Obsolete, else newest).
// The collapse is done SERVER-SIDE (searchCatalog distinctWorkflows=true), keyset-paged by base workflow id,
// so this table pages with Prev/Next over the store cursor exactly like <arazzo-runs-table> — never loading the
// whole catalog. Selecting a row opens the detail, which loads that base's full version list itself.

import { ArazzoElement, SHARED_CSS, escapeHtml, relativeTime, absoluteTime, define } from './base.js';

const STATUS_COLOR = {
  Active: 'var(--arazzo-status-completed, #2a8a4a)',
  Obsolete: 'var(--arazzo-status-cancelled, #6b7280)',
};

class ArazzoCatalogTable extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'q', 'base-workflow-id', 'status', 'owner', 'tags', 'page-size', 'selectable'];
  }

  constructor() {
    super();
    /** @private */ this._rows = [];
    /** @private */ this._history = []; // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextToken = null;
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._selectedKey = null;
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'selectable') this.renderBody();
    else this.reload(); // a filter or target changed — back to page 1
  }

  /** Imperative filter set (equivalent to the attributes). */
  get filters() {
    return {
      q: this.getAttribute('q') || undefined,
      baseWorkflowId: this.getAttribute('base-workflow-id') || undefined,
      status: this.getAttribute('status') || undefined,
      owner: this.getAttribute('owner') || undefined,
      tags: (this.getAttribute('tags') || '').split(/[,\s]+/).filter(Boolean),
    };
  }

  set filters(value = {}) {
    const setOrRemove = (attr, v) => { if (v) this.setAttribute(attr, v); else this.removeAttribute(attr); };
    setOrRemove('q', value.q);
    setOrRemove('base-workflow-id', value.baseWorkflowId);
    setOrRemove('status', value.status);
    setOrRemove('owner', value.owner);
    setOrRemove('tags', Array.isArray(value.tags) ? value.tags.join(' ') : value.tags);
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
      const f = this.filters;
      const { versions, nextPageToken } = await client.searchCatalog({
        q: f.q,
        baseWorkflowId: f.baseWorkflowId,
        status: f.status,
        owner: f.owner,
        tags: f.tags,
        distinctWorkflows: true,
        limit: this.pageSize,
        pageToken: this._currentToken,
      });
      if (seq !== this._reqSeq) return;
      this._rows = versions;
      this._nextToken = nextPageToken;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: versions.length, hasMore: !!nextPageToken });
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

  /** Re-fetch the current page without resetting the cursor (e.g. after a governance action). */
  refresh() {
    this.load(true);
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
        .wf { font-weight: 600; }
        .ver { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }
        .badge { display: inline-block; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; color: #fff; }
        .owner { font-size: 12px; }
        .owner .team { color: var(--_muted); }
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
              <th>Workflow</th><th>Latest</th><th>Status</th><th>Owner</th><th>Updated</th><th>Tags</th>
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
      tbody.innerHTML = `<tr><td colspan="6">
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
        `<tr>${'<td><div class="skl"></div></td>'.repeat(6)}</tr>`).join('');
      this.updatePager();
      return;
    }

    if (this._rows.length === 0) {
      tbody.innerHTML = `<tr><td colspan="6"><div class="empty">No catalog workflows match the current filters.</div></td></tr>`;
      this.updatePager();
      return;
    }

    tbody.innerHTML = this._rows.map((v) => this.renderRow(v, selectable)).join('');

    if (selectable) {
      this.$$('tbody tr.selectable').forEach((tr) => {
        tr.addEventListener('click', () => this.select(tr.dataset.key));
      });
    }
    this.updatePager();
  }

  renderRow(v, selectable) {
    const key = v.baseWorkflowId;
    const updated = v.lastUpdatedAt || v.obsoletedAt || v.createdAt;
    const owner = v.owner
      ? `<span class="owner">${escapeHtml(v.owner.name || v.owner.email || '—')}${v.owner.team ? ` <span class="team">· ${escapeHtml(v.owner.team)}</span>` : ''}</span>`
      : '<span class="muted">—</span>';
    const tags = Array.isArray(v.tags) && v.tags.length > 0
      ? `<div class="tags">${v.tags.map((t) => `<span class="tag">${escapeHtml(t)}</span>`).join('')}</div>`
      : '';
    const sel = this._selectedKey === key ? ' aria-selected="true"' : '';
    return `
      <tr part="row" class="${selectable ? 'selectable' : ''}" data-key="${escapeHtml(key)}"${sel}>
        <td part="cell" class="wf">${escapeHtml(v.title || v.baseWorkflowId)}<br><span class="muted ver">${escapeHtml(v.baseWorkflowId)}</span></td>
        <td part="cell" class="ver">v${escapeHtml(String(v.versionNumber))}</td>
        <td part="cell"><span class="badge" part="status" style="background:${STATUS_COLOR[v.status] || 'var(--_muted)'}">${escapeHtml(v.status)}</span></td>
        <td part="cell">${owner}</td>
        <td part="cell" class="muted" title="${escapeHtml(absoluteTime(updated))}">${escapeHtml(relativeTime(updated))}</td>
        <td part="cell">${tags}</td>
      </tr>`;
  }

  updatePager() {
    const prev = this.$('.prev');
    const next = this.$('.next');
    if (prev) prev.disabled = this._history.length === 0 || this._loading;
    if (next) next.disabled = !this._nextToken || this._loading;
    const count = this.$('.pager .count');
    if (count) {
      count.textContent = this._loading
        ? 'Loading…'
        : `${this._rows.length} workflow${this._rows.length === 1 ? '' : 's'}${this._history.length ? ` · page ${this._history.length + 1}` : ''}`;
    }
  }

  /** Select a base workflow by id (highlights the row and emits `version-selected` for its representative version). */
  select(key) {
    this._selectedKey = key;
    this.$$('tbody tr').forEach((tr) => {
      tr.setAttribute('aria-selected', String(tr.dataset.key === key));
    });
    const row = this._rows.find((v) => v.baseWorkflowId === key);
    // The detail loads the base's full version list itself (listCatalogVersions) — distinct mode has only the representative.
    if (row) this.emit('version-selected', { version: row, baseWorkflowId: key });
  }
}

define('arazzo-catalog-table', ArazzoCatalogTable);
export { ArazzoCatalogTable };
