// <arazzo-source-operations> — the standard filterable operations/messages view over a REGISTERED
// source (backlog #843): what the designer's rail shows for an attached source, read-only, fed by
// GET /sources/{name}/operations (the browse-before-attaching endpoint).
//
//   const ops = document.createElement('arazzo-source-operations');
//   ops.client = client;                     // an ArazzoControlPlaneClient (sources:read)
//   ops.source = 'onboarding';               // loads the surface
//
// Properties : .client, .source (name; setting it loads)
// Events     : loaded {operations}, error {problem}
//
// Rows use the same binding-identity idiom as <arazzo-operation-browser> (method+path for OpenAPI,
// action+channel for AsyncAPI, id, summary) WITHOUT the authoring gestures — no drag, no
// click-to-add: this view answers "what does this source offer", not "bind a step".

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

const METHOD_COLOR = {
  GET: 'var(--arazzo-status-completed, #2a8a4a)',
  POST: 'var(--arazzo-accent, #3b6cf6)',
  PUT: '#b57706',
  PATCH: '#b57706',
  DELETE: 'var(--arazzo-status-failed, #c33)',
};

class ArazzoSourceOperations extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._source = null;
    /** @private */ this._operations = [];
    /** @private */ this._filter = '';
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._seq = 0;
    /** @private */ this._built = false;
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    if (this._source) void this.load();
  }

  get source() { return this._source; }

  set source(name) {
    this._source = name || null;
    if (this._built && this.isConnected) void this.load();
  }

  async load() {
    const client = this.client;
    if (!client || !this._source) return;
    const seq = ++this._seq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      const { operations } = await client.listRegisteredSourceOperations(this._source);
      if (seq !== this._seq) return;
      this._operations = operations ?? [];
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { operations: this._operations.length });
    } catch (err) {
      if (seq !== this._seq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error });
    }
  }

  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .filter { width: 100%; box-sizing: border-box; font: 12px var(--_font); padding: 5px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: inherit; margin-bottom: 6px; }
        .ops { display: flex; flex-direction: column; gap: 2px; max-height: 320px; overflow: auto; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); padding: 4px; }
        .op { display: grid; grid-template-columns: max-content 1fr; column-gap: 8px; align-items: baseline; padding: 5px 7px; border-radius: 6px; text-align: left; }
        .op .badge { font-size: 10px; font-weight: 700; color: #fff; border-radius: 4px; padding: 1px 6px; text-transform: uppercase; }
        .op .id { font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; overflow-wrap: anywhere; }
        .op .id .muted { color: var(--_muted); }
        .op .id.deprecated { text-decoration: line-through; }
        .op .summary { grid-column: 2; font-size: 11px; color: var(--_muted); overflow-wrap: anywhere; }
        .empty, .loading { font-size: 12px; color: var(--_muted); padding: 8px; }
        .error { font-size: 12px; color: var(--arazzo-status-faulted, #d4351c); padding: 8px; }
      </style>
      <input class="filter" type="search" placeholder="Filter operations…" aria-label="Filter operations">
      <div class="ops"></div>
    `;
    this.$('.filter').addEventListener('input', (e) => {
      this._filter = e.target.value.trim().toLowerCase();
      this.renderBody();
    });
  }

  renderBody() {
    const box = this.$('.ops');
    if (!box) return;
    if (this._loading) { box.innerHTML = '<div class="loading">Loading the operation surface…</div>'; return; }
    if (this._error) { box.innerHTML = `<div class="error">${escapeHtml(this._error.detail || this._error.title || 'Failed to load')}</div>`; return; }
    const shown = this._filter
      ? this._operations.filter((op) => `${op.operationId ?? ''} ${op.path ?? ''} ${op.channelPath ?? ''} ${op.summary ?? ''}`.toLowerCase().includes(this._filter))
      : this._operations;
    if (!shown.length) {
      box.innerHTML = `<div class="empty">${this._operations.length ? 'No operations match the filter.' : 'This source declares no operations.'}</div>`;
      return;
    }
    box.innerHTML = shown.map((op) => {
      const isHttp = op.kind === 'openapi';
      const badge = isHttp ? (op.method ?? '?') : (op.action ?? 'channel');
      const badgeColor = isHttp ? (METHOD_COLOR[op.method] ?? 'var(--_muted)') : 'var(--arazzo-status-running, #7048b7)';
      const id = op.operationId ?? (isHttp ? op.path : op.channelPath) ?? '(unnamed)';
      const where = isHttp ? op.path : op.channelPath;
      return `
        <div class="op" part="operation">
          <span class="badge" style="background:${badgeColor}">${escapeHtml(badge)}</span>
          <span class="id${op.deprecated ? ' deprecated' : ''}">${escapeHtml(id)}${where && where !== id ? ` <span class="muted">${escapeHtml(where)}</span>` : ''}</span>
          ${op.summary ? `<span class="summary">${escapeHtml(op.summary)}</span>` : ''}
        </div>`;
    }).join('');
  }
}

define('arazzo-source-operations', ArazzoSourceOperations);