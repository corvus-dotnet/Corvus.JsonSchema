// <arazzo-credentials-table> — the source-credential rotation worklist (§13). Status-first.
//
//   <arazzo-credentials-table base-url="/arazzo/v1" status="expiring" selectable></arazzo-credentials-table>
//
// Attributes : base-url, status (valid|expiring|expired), source, page-size (default 50), selectable, scopes (gates the Duplicate action)
// Properties : .client, .filters = { status, source }
// Events     : credential-selected {binding}, credential-duplicate {binding}, loaded {count, expiring, expired}, error {problem}
// Parts      : table, row, cell, status, toolbar, foot, pager
//
// This is a MANAGEMENT surface, not a creation one: rows are rotated/inspected (credential-selected), duplicated to
// another environment (credential-duplicate — clone source + auth, re-point the secret), and revoked. Creating a
// credential is rooted where the source and its auth are known — the catalog's per-workflow Sources panel (§7.5) —
// not free-typed here.
//
// A binding manages a REFERENCE and non-secret metadata only — never secret material — so the table renders a
// `secretRef` and a derived `credentialStatus`, never a secret. The operator's question is "what's about to
// break?", so status is the headline column (colour-coded) with an "N expiring / M expired" footer.

import { ArazzoElement, SHARED_CSS, GRANTEE_CHIP_CSS, granteeChip, escapeHtml, absoluteTime, countdown, define } from './base.js';

const STATUS = {
  valid: { label: 'valid', color: 'var(--arazzo-status-completed, #2a8a4a)' },
  expiringSoon: { label: 'expiring soon', color: 'var(--arazzo-status-suspended, #b07d18)' },
  expired: { label: 'expired', color: 'var(--arazzo-status-faulted, #d4351c)' },
};

// The operator-friendly --status filter words map to the API's credentialStatus tokens.
function normalizeStatus(value) {
  const v = (value || '').toLowerCase();
  if (v === 'expiring' || v === 'expiringsoon') return 'expiringSoon';
  if (v === 'valid' || v === 'expired') return v;
  return undefined;
}

function shortDate(iso) {
  if (!iso) return '';
  const t = Date.parse(iso);
  return Number.isNaN(t) ? String(iso) : new Date(t).toISOString().slice(0, 10);
}

class ArazzoCredentialsTable extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'status', 'source', 'selectable', 'scopes', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._bindings = [];
    /** @private */ this._history = []; // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
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
    if (name === 'base-url' || name === 'page-size') this.reload();
    else this.renderBody();
  }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 50;
  }

  /** Imperative filter set (equivalent to the attributes). */
  get filters() {
    return { status: this.getAttribute('status') || undefined, source: this.getAttribute('source') || undefined };
  }

  set filters(value = {}) {
    const setOrRemove = (attr, v) => { if (v) this.setAttribute(attr, v); else this.removeAttribute(attr); };
    setOrRemove('status', value.status);
    setOrRemove('source', value.source);
  }

  requestRender() {
    this.reload();
  }

  /** Re-fetch from the server (e.g. after a create/update/delete) — back to page 1. */
  refresh() {
    this.reload();
  }

  /** Reload from page 1 (resets the keyset cursor). */
  reload() {
    this._history = [];
    this._currentToken = undefined;
    this.load();
  }

  /** Fetch ONE keyset page at the current cursor, replacing the rows (the store pages server-side). */
  async load() {
    const client = this.client;
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url attribute or a .client property.' };
      this.renderBody();
      return;
    }

    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this._bindings = [];
    this._nextPageToken = null;
    this.renderBody();

    try {
      const { credentials, nextPageToken } = await client.listCredentials({ limit: this.pageSize, pageToken: this._currentToken });
      if (seq !== this._reqSeq) return;
      this._bindings = credentials;
      this._nextPageToken = nextPageToken;
      this._loading = false;
      this.renderBody();
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

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${GRANTEE_CHIP_CSS}
        .wrap { border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; background: var(--_bg); }
        .toolbar { display: flex; align-items: center; gap: 8px; padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); }
        .toolbar .grow { flex: 1; }
        .toolbar label { font-size: 12px; color: var(--_muted); }
        select { font: inherit; font-size: 13px; padding: 5px 28px 5px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        input.src { font: inherit; font-size: 13px; padding: 5px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); width: 140px; }
        table { width: 100%; border-collapse: collapse; }
        thead th { text-align: left; font-size: 12px; font-weight: 600; color: var(--_muted); padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); white-space: nowrap; }
        tbody td { padding: 9px 12px; border-bottom: 1px solid var(--_border); vertical-align: middle; }
        tbody tr:last-child td { border-bottom: none; }
        tbody tr.selectable { cursor: pointer; }
        tbody tr.selectable:hover { background: var(--_surface); }
        tbody tr[aria-selected="true"] { background: color-mix(in srgb, var(--_accent) 12%, transparent); }
        .src-name { font-weight: 600; }
        .actions-cell { white-space: nowrap; text-align: right; }
        .dup { font-size: 12px; padding: 3px 8px; }
        .ref { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; color: var(--_muted); }
        .badge { display: inline-block; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; color: #fff; white-space: nowrap; }
        .grants { display: flex; gap: 4px; flex-wrap: wrap; }
        .grant { font-size: 11px; padding: 1px 7px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_muted); white-space: nowrap; }
        .skl { height: 12px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .foot { display: flex; align-items: center; gap: 12px; padding: 9px 12px; background: var(--_surface); border-top: 1px solid var(--_border); font-size: 12px; color: var(--_muted); }
        .pill { font-weight: 600; }
        .pill.amber { color: var(--arazzo-status-suspended, #b07d18); }
        .pill.red { color: var(--arazzo-status-faulted, #d4351c); }
        .pager { display: flex; align-items: center; gap: 8px; padding: 9px 12px; background: var(--_surface); border-top: 1px solid var(--_border); }
        .pager .grow { flex: 1; }
        .pager .count { font-size: 12px; color: var(--_muted); }
      </style>
      <div class="wrap" part="table">
        <div class="toolbar" part="toolbar">
          <label>Status
            <select class="status">
              <option value="">all</option>
              <option value="valid">valid</option>
              <option value="expiring">expiring soon</option>
              <option value="expired">expired</option>
            </select>
          </label>
          <input class="src" type="text" placeholder="source…" aria-label="Filter by source">
          <span class="grow"></span>
        </div>
        <table>
          <thead>
            <tr><th>Source</th><th>Environment</th><th>Auth</th><th>Status</th><th>Expires</th><th>Grants</th><th></th></tr>
          </thead>
          <tbody part="rows"></tbody>
        </table>
        <div class="foot" part="foot"></div>
        <div class="pager" part="pager" hidden>
          <button class="prev ghost" type="button">‹ Prev</button>
          <button class="next ghost" type="button">Next ›</button>
          <span class="grow"></span>
          <span class="count"></span>
        </div>
      </div>
    `;
    const status = this.$('.status');
    status.value = this.getAttribute('status') || '';
    status.addEventListener('change', () => this.filters = { ...this.filters, status: status.value || undefined });
    const src = this.$('.src');
    src.value = this.getAttribute('source') || '';
    src.addEventListener('input', () => this.filters = { ...this.filters, source: src.value.trim() || undefined });
    this.$('.prev').addEventListener('click', () => this.prevPage());
    this.$('.next').addEventListener('click', () => this.nextPage());
  }

  visibleBindings() {
    const wantStatus = normalizeStatus(this.getAttribute('status'));
    const wantSource = (this.getAttribute('source') || '').toLowerCase();
    return this._bindings.filter((b) =>
      (!wantStatus || b.credentialStatus === wantStatus)
      && (!wantSource || (b.sourceName || '').toLowerCase().includes(wantSource)));
  }

  renderBody() {
    const tbody = this.$('tbody');
    if (!tbody) return;
    const selectable = this.hasAttribute('selectable');
    const canWrite = (() => {
      const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
      return scopes.length === 0 || scopes.includes('credentials:write');
    })();

    if (this._error) {
      tbody.innerHTML = `<tr><td colspan="7">
        <div class="error-banner">
          <span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span>
          <button class="retry" type="button">Retry</button>
        </div></td></tr>`;
      tbody.querySelector('.retry').addEventListener('click', () => this.load());
      this.renderFoot(0, 0);
      return;
    }

    if (this._loading && this._bindings.length === 0) {
      tbody.innerHTML = Array.from({ length: 3 }, () => `<tr>${'<td><div class="skl"></div></td>'.repeat(7)}</tr>`).join('');
      this.renderFoot(0, 0);
      return;
    }

    const rows = this.visibleBindings();
    if (rows.length === 0) {
      tbody.innerHTML = `<tr><td colspan="7"><div class="empty">No credential bindings match the current filters.</div></td></tr>`;
      this.renderFoot(0, 0);
      return;
    }

    tbody.innerHTML = rows.map((b) => this.renderRow(b, selectable, canWrite)).join('');
    if (selectable) {
      this.$$('tbody tr.selectable').forEach((tr) => tr.addEventListener('click', () => this.select(tr.dataset.key)));
    }
    // Duplicate-to-environment: clone this binding's source + auth into a new environment (the one legitimate
    // "create" on the Sources tab — rooted in an existing source, not free-typed). Stops the row's select click.
    this.$$('tbody .dup').forEach((btn) => btn.addEventListener('click', (e) => {
      e.stopPropagation();
      const key = btn.closest('tr')?.dataset.key;
      const binding = this._bindings.find((b) => `${b.sourceName}@${b.environment}` === key);
      if (binding) this.emit('credential-duplicate', { binding });
    }));

    const expiring = rows.filter((b) => b.credentialStatus === 'expiringSoon').length;
    const expired = rows.filter((b) => b.credentialStatus === 'expired').length;
    this.renderFoot(expiring, expired);
    this.emit('loaded', { count: rows.length, expiring, expired });
  }

  renderRow(b, selectable, canWrite = true) {
    const key = `${b.sourceName}@${b.environment}`;
    const status = STATUS[b.credentialStatus] || { label: b.credentialStatus, color: 'var(--_muted)' };
    const ref = b.secretRefs?.[0]?.ref;
    const grants = b.usageGrantee && Array.isArray(b.usageGrantee.identity) && b.usageGrantee.identity.length > 0
      ? `<div class="grants">${granteeChip(b.usageGrantee)}</div>`
      : '<span class="muted">—</span>';
    const expires = b.expiresAt
      ? `<span title="${escapeHtml(absoluteTime(b.expiresAt))}">${escapeHtml(shortDate(b.expiresAt))} <span class="muted">(${escapeHtml(countdown(b.expiresAt))})</span></span>`
      : '<span class="muted">—</span>';
    const sel = this._selectedKey === key ? ' aria-selected="true"' : '';
    return `
      <tr part="row" class="${selectable ? 'selectable' : ''}" data-key="${escapeHtml(key)}"${sel}>
        <td part="cell" class="src-name">${escapeHtml(b.sourceName)}${ref ? `<br><span class="ref">${escapeHtml(ref)}</span>` : ''}</td>
        <td part="cell">${escapeHtml(b.environment)}</td>
        <td part="cell">${escapeHtml(b.authKind)}</td>
        <td part="cell"><span class="badge" part="status" style="background:${status.color}">${escapeHtml(status.label)}</span></td>
        <td part="cell">${expires}</td>
        <td part="cell">${grants}</td>
        <td part="cell" class="actions-cell">${canWrite ? '<button class="dup ghost" type="button" title="Duplicate to another environment">Duplicate</button>' : ''}</td>
      </tr>`;
  }

  renderFoot(expiring, expired) {
    this.updatePager();
    const foot = this.$('.foot');
    if (!foot) return;
    if (this._loading) { foot.textContent = 'Loading…'; return; }
    const total = this.visibleBindings().length;
    const parts = [`${total} binding${total === 1 ? '' : 's'}`];
    if (expiring > 0) parts.push(`<span class="pill amber">${expiring} expiring soon</span>`);
    if (expired > 0) parts.push(`<span class="pill red">${expired} expired</span>`);
    foot.innerHTML = parts.join(' · ');
  }

  updatePager() {
    const pager = this.$('.pager');
    if (!pager) return;
    const hasPages = this._history.length > 0 || !!this._nextPageToken;
    pager.hidden = !hasPages;
    const prev = this.$('.prev');
    const next = this.$('.next');
    if (prev) prev.disabled = this._history.length === 0 || this._loading;
    if (next) next.disabled = !this._nextPageToken || this._loading;
    const count = this.$('.pager .count');
    if (count) count.textContent = this._loading ? 'Loading…' : (this._history.length ? `page ${this._history.length + 1}` : '');
  }

  /** Select a binding by `source@environment` (highlights the row and emits `credential-selected`). */
  select(key) {
    this._selectedKey = key;
    this.$$('tbody tr').forEach((tr) => tr.setAttribute('aria-selected', String(tr.dataset.key === key)));
    const binding = this._bindings.find((b) => `${b.sourceName}@${b.environment}` === key);
    if (binding) this.emit('credential-selected', { binding });
  }
}

define('arazzo-credentials-table', ArazzoCredentialsTable);
export { ArazzoCredentialsTable };
