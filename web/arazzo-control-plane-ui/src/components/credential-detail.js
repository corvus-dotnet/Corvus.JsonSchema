// <arazzo-credential-detail> — the full record for one source credential binding (§13), plus its scope-gated actions.
//
//   <arazzo-credential-detail base-url="/arazzo/v1" scopes="credentials:read credentials:write"></arazzo-credential-detail>
//   detail.binding = bindingSummary; // select a binding to show
//
// Attributes : base-url, scopes (space-separated)
// Properties : .client, .binding (inject a summary to show without a fetch; it then loads authoritative detail)
// Events     : credential-edit {binding}, credential-duplicate {binding}, credential-deleted {sourceName, environment},
//              close, error {problem}
// Parts      : panel, header, status, meta, actions
//
// The Sources sibling of <arazzo-catalog-detail>: the master-detail RHS pane for the Sources list. It renders REFERENCES
// and non-secret metadata only — never secret material — and hosts the binding's governance actions (Edit / Duplicate /
// Revoke) inline, gated by `credentials:write` (a read-only caller sees the record with no action controls).

import { ArazzoElement, SHARED_CSS, GRANTEE_CHIP_CSS, granteeChip, escapeHtml, relativeTime, absoluteTime, countdown, confirmDialog, copyToClipboard, define } from './base.js';

const STATUS = {
  valid: { label: 'valid', color: 'var(--arazzo-status-completed, #2a8a4a)' },
  expiringSoon: { label: 'expiring soon', color: 'var(--arazzo-status-suspended, #b07d18)' },
  expired: { label: 'expired', color: 'var(--arazzo-status-faulted, #d4351c)' },
};

class ArazzoCredentialDetail extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes'];
  }

  constructor() {
    super();
    /** @private */ this._binding = null;
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    if (this._binding) { this.renderBody(); this.load(); }
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'scopes') this.renderBody();
  }

  /** The binding summary to show. Set it to render immediately, then it loads authoritative detail. */
  get binding() { return this._binding; }

  set binding(value) {
    this._binding = value;
    if (this.isConnected) { this._error = null; this.renderBody(); this.load(); }
  }

  requestRender() { this.renderBody(); }

  hasScope(scope) {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes(scope);
  }

  // ---- loading ----------------------------------------------------------------------------------

  /** Re-fetch the authoritative binding by its (sourceName, environment) key, so a rotate/edit elsewhere is reflected. */
  async load() {
    const client = this.client;
    const b = this._binding;
    if (!client || !b?.sourceName || !b?.environment) return;
    const seq = ++this._reqSeq;
    try {
      const binding = await client.getCredential(b.sourceName, b.environment);
      if (seq !== this._reqSeq) return;
      this._binding = binding;
      this.renderBody();
    } catch (err) {
      if (seq !== this._reqSeq) return;
      // A 404 means the binding was revoked out from under us; surface it and close.
      if (err.status === 404) { this.emit('close'); return; }
      this._error = err.problem || { title: err.message, status: err.status };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${GRANTEE_CHIP_CSS}
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); }
        header { display: flex; align-items: center; gap: 10px; padding: 12px 14px; background: var(--_surface); border-bottom: 1px solid var(--_border); border-radius: var(--_radius) var(--_radius) 0 0; }
        header .src { font-weight: 700; font-size: 15px; }
        header .env { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 13px; color: var(--_muted); }
        header .grow { flex: 1; }
        header .close { font-size: 16px; line-height: 1; }
        .badge { display: inline-block; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; color: #fff; white-space: nowrap; }
        dl { margin: 0; padding: 14px; display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 8px 16px; }
        dt { color: var(--_muted); font-size: 12px; }
        dd { margin: 0; font-size: 13px; }
        .mono { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; word-break: break-all; }
        .refs, .cfg { display: grid; gap: 4px; }
        .refs .r { display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 8px; align-items: baseline; }
        .refs .role { color: var(--_muted); font-size: 12px; }
        .copy { font-size: 12px; padding: 0 6px; margin-left: 6px; line-height: 1.4; vertical-align: baseline; }
        .tags { display: flex; gap: 4px; flex-wrap: wrap; }
        .tag { font-size: 11px; padding: 1px 7px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_muted); }
        .note { margin: 0 14px 12px; font-size: 12px; color: var(--_muted); border-left: 3px solid var(--_border); padding: 4px 0 4px 8px; }
        .actions { display: flex; gap: 8px; flex-wrap: wrap; padding: 12px 14px; border-top: 1px solid var(--_border); }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .pad { padding: 14px; }
      </style>
      <div class="panel" part="panel">
        <header part="header">
          <span class="badge" part="status"></span>
          <span class="src"></span>
          <span class="env"></span>
          <span class="grow"></span>
          <button class="close ghost" type="button" title="Close" aria-label="Close">✕</button>
        </header>
        <div class="body"></div>
      </div>
    `;
    this.$('.close').addEventListener('click', () => this.emit('close'));
  }

  renderBody() {
    const badge = this.$('header .badge');
    const src = this.$('header .src');
    const env = this.$('header .env');
    const body = this.$('.body');
    if (!body) return;

    if (this._error) {
      badge.style.display = 'none';
      src.textContent = this._binding?.sourceName || '';
      env.textContent = this._binding ? `@${this._binding.environment}` : '';
      body.innerHTML = `<div class="pad"><div class="error-banner">
        <span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span>
        <button class="retry" type="button">Retry</button>
      </div></div>`;
      body.querySelector('.retry')?.addEventListener('click', () => this.load());
      return;
    }

    const b = this._binding;
    if (!b) { badge.style.display = 'none'; src.textContent = ''; env.textContent = ''; body.innerHTML = `<div class="empty">No credential selected.</div>`; return; }

    const status = STATUS[b.credentialStatus] || { label: b.credentialStatus || '—', color: 'var(--_muted)' };
    badge.style.display = '';
    badge.textContent = status.label;
    badge.style.background = status.color;
    src.textContent = b.sourceName;
    env.textContent = `@${b.environment}`;

    const refs = Array.isArray(b.secretRefs) && b.secretRefs.length
      ? `<div class="refs">${b.secretRefs.map((r) => `<div class="r"><span class="role">${escapeHtml(r.name)}</span><span class="mono">${escapeHtml(r.ref)}<button class="copy ghost copy-ref" type="button" data-ref="${escapeHtml(r.ref)}" title="Copy reference" aria-label="Copy reference">⧉</button></span></div>`).join('')}</div>`
      : '<span class="muted">—</span>';
    const config = Array.isArray(b.config) && b.config.length
      ? `<div class="cfg">${b.config.map((c) => `<div class="mono">${escapeHtml(c.key)} = ${escapeHtml(c.value)}</div>`).join('')}</div>`
      : '<span class="muted">—</span>';
    const usage = b.usageGrantee && Array.isArray(b.usageGrantee.identity) && b.usageGrantee.identity.length
      ? granteeChip(b.usageGrantee)
      : '<span class="muted">Shared — any run that uses this source</span>';
    const mgmt = Array.isArray(b.managementTags) && b.managementTags.length
      ? `<div class="tags">${b.managementTags.map((t) => `<span class="tag">${escapeHtml(t.key)}=${escapeHtml(t.value)}</span>`).join('')}</div>`
      : '<span class="muted">—</span>';
    const expires = b.expiresAt
      ? `<span title="${escapeHtml(absoluteTime(b.expiresAt))}">${escapeHtml(String(b.expiresAt).slice(0, 10))} <span class="muted">(${escapeHtml(countdown(b.expiresAt))})</span></span>`
      : '<span class="muted">no expiry</span>';

    body.innerHTML = `
      <dl part="meta">
        <dt>Auth kind</dt><dd>${escapeHtml(b.authKind)}</dd>
        <dt>Secret references</dt><dd>${refs}</dd>
        <dt>Config</dt><dd>${config}</dd>
        <dt>Usage</dt><dd>${usage}</dd>
        <dt>Management tags</dt><dd>${mgmt}</dd>
        ${b.description ? `<dt>Description</dt><dd>${escapeHtml(b.description)}</dd>` : ''}
        <dt>Expires</dt><dd>${expires}</dd>
        ${b.rotatedAt ? `<dt>Rotated</dt><dd class="muted" title="${escapeHtml(absoluteTime(b.rotatedAt))}">${escapeHtml(relativeTime(b.rotatedAt))}</dd>` : ''}
      </dl>
      <div class="note">The control plane stores <strong>references</strong> only — never secret material. The workflow runner reads the secret at run time as its own identity.</div>
      <div class="actions" part="actions"></div>
    `;
    this.$$('.copy-ref').forEach((btn) => btn.addEventListener('click', async () => {
      if (await copyToClipboard(btn.dataset.ref)) { btn.textContent = '✓'; setTimeout(() => { btn.textContent = '⧉'; }, 1200); }
    }));
    this.renderActions(b);
  }

  renderActions(b) {
    const host = this.$('.actions');
    if (!host) return;
    // Write controls are gated on credentials:write — a read-only caller sees the record with no action controls (the
    // honest UI: no editable-looking button that only fails on Save). The server also refuses the write (403 backstop).
    if (!this.hasScope('credentials:write')) { host.innerHTML = '<span class="muted">Read-only — you don’t have <code>credentials:write</code> for this binding.</span>'; return; }
    host.innerHTML = `
      <button class="edit primary" type="button">Edit…</button>
      <button class="duplicate ghost" type="button" title="Duplicate to another environment — clone this source + auth, re-point the secret">Duplicate…</button>
      <button class="revoke danger" type="button">Revoke…</button>`;
    host.querySelector('.edit').addEventListener('click', () => this.emit('credential-edit', { binding: b }));
    host.querySelector('.duplicate').addEventListener('click', () => this.emit('credential-duplicate', { binding: b }));
    host.querySelector('.revoke').addEventListener('click', () => this.confirmRevoke(b));
  }

  async confirmRevoke(b) {
    const confirmed = await confirmDialog(this, {
      title: 'Revoke credential',
      message: `Revoke the ${b.sourceName}@${b.environment} credential binding? Runs that use this source in ${b.environment} will no longer resolve a secret. This cannot be undone.`,
      confirmLabel: 'Revoke',
      danger: true,
    });
    if (!confirmed) return;
    try {
      await this.client.deleteCredential(b.sourceName, b.environment);
      this.emit('credential-deleted', { sourceName: b.sourceName, environment: b.environment });
      this.emit('close');
    } catch (err) {
      this._error = err.problem || { title: err.message, status: err.status };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }
}

define('arazzo-credential-detail', ArazzoCredentialDetail);
export { ArazzoCredentialDetail };