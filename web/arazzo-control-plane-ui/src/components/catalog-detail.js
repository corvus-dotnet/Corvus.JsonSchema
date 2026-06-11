// <arazzo-catalog-detail> — the full record for one catalog version, plus its scope-gated governance actions.
//
//   <arazzo-catalog-detail base-url="/arazzo/v1" base-workflow-id="adopt-pet" version-number="1"
//                          scopes="catalog:read catalog:write catalog:purge"></arazzo-catalog-detail>
//
// Attributes : base-url, base-workflow-id, version-number, scopes (space-separated), show-forbidden
// Properties : .client, .version (inject a summary to skip the fetch)
// Events     : version-changed {version}, version-deleted {baseWorkflowId, versionNumber}, error {problem}, close
// Parts      : panel, header, status, meta, owner, sources, actions
//
// Standalone-capable: it renders metadata + governance (obsolete / delete) itself and offers download links
// for the package, workflow and each source document. Layer 2 listens to its events to keep the table in sync.

import { ArazzoElement, SHARED_CSS, escapeHtml, relativeTime, absoluteTime, confirmDialog, copyToClipboard, define } from './base.js';

const STATUS_COLOR = {
  Active: 'var(--arazzo-status-completed, #2a8a4a)',
  Obsolete: 'var(--arazzo-status-cancelled, #6b7280)',
};

class ArazzoCatalogDetail extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'base-workflow-id', 'version-number', 'scopes', 'show-forbidden'];
  }

  constructor() {
    super();
    /** @private */ this._version = null;
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    if (this._version && !this.getAttribute('version-number')) this.renderBody(); else this.load();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected || oldValue === newValue) return;
    if (name === 'base-workflow-id' || name === 'version-number') this.load();
    else this.renderBody();
  }

  /** The current version summary. Set it to render without a fetch (then it loads authoritative detail). */
  get version() { return this._version; }

  set version(value) {
    this._version = value;
    if (value) {
      if (value.baseWorkflowId != null) this.setAttribute('base-workflow-id', value.baseWorkflowId);
      if (value.versionNumber != null) this.setAttribute('version-number', String(value.versionNumber));
    }
    if (this.isConnected) { this.renderBody(); this.load(); }
  }

  get baseWorkflowId() { return this.getAttribute('base-workflow-id') || this._version?.baseWorkflowId || null; }

  get versionNumber() {
    const attr = this.getAttribute('version-number');
    return attr != null ? Number(attr) : (this._version?.versionNumber ?? null);
  }

  requestRender() { this.load(); }

  hasScope(scope) {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes(scope);
  }

  // ---- loading ----------------------------------------------------------------------------------

  async load() {
    const client = this.client;
    const base = this.baseWorkflowId;
    const num = this.versionNumber;
    if (!client || !base || num == null) return;
    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      const version = await client.getCatalogVersion(base, num);
      if (seq !== this._reqSeq) return;
      this._version = version;
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

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        header { display: flex; align-items: center; gap: 10px; padding: 12px 14px; background: var(--_surface); border-bottom: 1px solid var(--_border); }
        header .wf { font-weight: 700; font-size: 15px; }
        header .ver { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 13px; color: var(--_muted); }
        header .grow { flex: 1; }
        header .close { font-size: 16px; line-height: 1; }
        .badge { display: inline-block; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; color: #fff; }
        dl { margin: 0; padding: 14px; display: grid; grid-template-columns: max-content 1fr; gap: 8px 16px; }
        dt { color: var(--_muted); font-size: 12px; }
        dd { margin: 0; font-size: 13px; }
        .mono { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; word-break: break-all; }
        .copy { font-size: 12px; padding: 0 6px; margin-left: 6px; line-height: 1.4; vertical-align: baseline; }
        .tags { display: flex; gap: 4px; flex-wrap: wrap; }
        .tag { font-size: 11px; padding: 1px 7px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_muted); }
        .block { margin: 0 14px 14px; padding: 10px 12px; border: 1px solid var(--_border); border-radius: var(--_radius); }
        .block h4 { margin: 0 0 6px; font-size: 12px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--_muted); }
        .sources { display: flex; flex-direction: column; gap: 6px; }
        .src { display: flex; align-items: center; gap: 8px; font-size: 13px; }
        .src .name { font-weight: 600; }
        .src .type { font-size: 11px; color: var(--_muted); }
        .src .grow { flex: 1; }
        .downloads { display: flex; gap: 8px; flex-wrap: wrap; }
        .actions { display: flex; gap: 8px; flex-wrap: wrap; padding: 12px 14px; border-top: 1px solid var(--_border); }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .pad { padding: 14px; }
      </style>
      <div class="panel" part="panel">
        <header part="header">
          <span class="badge" part="status"></span>
          <span class="wf"></span>
          <span class="ver"></span>
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
    const wf = this.$('header .wf');
    const ver = this.$('header .ver');
    const body = this.$('.body');
    if (!body) return;

    if (this._error) {
      badge.style.display = 'none';
      wf.textContent = this.baseWorkflowId || '';
      ver.textContent = this.versionNumber != null ? `v${this.versionNumber}` : '';
      const notFound = this._error.status === 404;
      body.innerHTML = `<div class="pad"><div class="error-banner">
        <span><strong>${escapeHtml(notFound ? 'Version not found' : (this._error.title || 'Request failed'))}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span>
        ${notFound ? '' : '<button class="retry" type="button">Retry</button>'}
      </div></div>`;
      body.querySelector('.retry')?.addEventListener('click', () => this.load());
      return;
    }

    if (this._loading && !this._version) {
      badge.style.display = 'none';
      wf.innerHTML = '<span class="skl" style="width:140px;display:inline-block"></span>';
      body.innerHTML = `<div class="pad"><div class="skl" style="width:60%"></div><br><div class="skl" style="width:40%"></div></div>`;
      return;
    }

    const v = this._version;
    if (!v) { body.innerHTML = `<div class="empty">No version selected.</div>`; return; }

    badge.style.display = '';
    badge.textContent = v.status;
    badge.style.background = STATUS_COLOR[v.status] || 'var(--_muted)';
    wf.textContent = v.title || v.baseWorkflowId;
    ver.textContent = `${v.baseWorkflowId} · v${v.versionNumber}`;

    body.innerHTML = `
      <dl>
        <dt>Workflow id</dt><dd class="mono">${escapeHtml(v.workflowId || `${v.baseWorkflowId}-v${v.versionNumber}`)}</dd>
        ${v.description ? `<dt>Description</dt><dd>${escapeHtml(v.description)}</dd>` : ''}
        <dt>Content hash</dt><dd class="mono" part="hash">${escapeHtml(v.hash || '—')}<button class="copy ghost copy-hash" type="button" title="Copy content hash" aria-label="Copy content hash">⧉</button></dd>
        ${Array.isArray(v.tags) && v.tags.length > 0 ? `<dt>Tags</dt><dd part="tags"><div class="tags">${v.tags.map((t) => `<span class="tag">${escapeHtml(t)}</span>`).join('')}</div></dd>` : ''}
      </dl>
      ${this.renderOwner(v)}
      ${this.renderGovernance(v)}
      ${this.renderSources(v)}
      ${this.renderDownloads(v)}
      <div class="actions" part="actions"></div>
    `;
    this.$('.copy-hash')?.addEventListener('click', async (e) => {
      const button = e.currentTarget;
      if (await copyToClipboard(v.hash)) {
        button.textContent = '✓';
        setTimeout(() => { button.textContent = '⧉'; }, 1200);
      }
    });
    this.wireDownloads(v);
    this.renderActions(v);
  }

  renderOwner(v) {
    const o = v.owner;
    if (!o) return '';
    const rows = [
      `<div><strong>${escapeHtml(o.name || '—')}</strong>${o.team ? ` <span class="muted">· ${escapeHtml(o.team)}</span>` : ''}</div>`,
      o.email ? `<div class="muted"><a href="mailto:${escapeHtml(o.email)}">${escapeHtml(o.email)}</a></div>` : '',
      o.url ? `<div class="muted"><a href="${escapeHtml(o.url)}" target="_blank" rel="noopener">${escapeHtml(o.url)}</a></div>` : '',
    ].join('');
    return `<div class="block" part="owner"><h4>Owner (governance)</h4>${rows}</div>`;
  }

  renderGovernance(v) {
    const rows = [
      v.createdBy || v.createdAt ? `<div>Created by <strong>${escapeHtml(v.createdBy || '—')}</strong>${v.createdAt ? ` · <span class="muted" title="${escapeHtml(absoluteTime(v.createdAt))}">${escapeHtml(relativeTime(v.createdAt))}</span>` : ''}</div>` : '',
      v.lastUpdatedBy || v.lastUpdatedAt ? `<div>Updated by <strong>${escapeHtml(v.lastUpdatedBy || '—')}</strong>${v.lastUpdatedAt ? ` · <span class="muted" title="${escapeHtml(absoluteTime(v.lastUpdatedAt))}">${escapeHtml(relativeTime(v.lastUpdatedAt))}</span>` : ''}</div>` : '',
      v.obsoletedBy || v.obsoletedAt ? `<div>Obsoleted by <strong>${escapeHtml(v.obsoletedBy || '—')}</strong>${v.obsoletedAt ? ` · <span class="muted" title="${escapeHtml(absoluteTime(v.obsoletedAt))}">${escapeHtml(relativeTime(v.obsoletedAt))}</span>` : ''}</div>` : '',
    ].filter(Boolean).join('');
    if (!rows) return '';
    return `<div class="block" part="audit"><h4>Audit</h4>${rows}</div>`;
  }

  renderSources(v) {
    const sources = Array.isArray(v.sources) ? v.sources : [];
    if (sources.length === 0) return '';
    const rows = sources.map((s) => `
      <div class="src">
        <span class="name">${escapeHtml(s.name)}</span>
        <span class="type">${escapeHtml(s.type || '')}</span>
        <span class="grow"></span>
        <button class="ghost dl-source" type="button" data-name="${escapeHtml(s.name)}" title="Download ${escapeHtml(s.name)}">Download</button>
      </div>`).join('');
    return `<div class="block" part="sources"><h4>Source descriptions</h4><div class="sources">${rows}</div></div>`;
  }

  renderDownloads() {
    return `<div class="block" part="downloads"><h4>Download</h4><div class="downloads">
      <button class="dl-package" type="button">Package (.zip)</button>
      <button class="ghost dl-workflow" type="button">Workflow (.json)</button>
    </div></div>`;
  }

  wireDownloads(v) {
    this.$('.dl-package')?.addEventListener('click', () => this.download('package', v));
    this.$('.dl-workflow')?.addEventListener('click', () => this.download('workflow', v));
    this.$$('.dl-source').forEach((btn) => btn.addEventListener('click', () => this.download('source', v, btn.dataset.name)));
  }

  async download(kind, v, sourceName) {
    try {
      let blob; let filename;
      if (kind === 'package') {
        blob = await this.client.getCatalogPackage(v.baseWorkflowId, v.versionNumber);
        filename = `${v.baseWorkflowId}-v${v.versionNumber}.zip`;
      } else if (kind === 'workflow') {
        const doc = await this.client.getCatalogWorkflow(v.baseWorkflowId, v.versionNumber);
        blob = new Blob([JSON.stringify(doc, null, 2)], { type: 'application/json' });
        filename = `${v.baseWorkflowId}-v${v.versionNumber}.workflow.json`;
      } else {
        const doc = await this.client.getCatalogSource(v.baseWorkflowId, v.versionNumber, sourceName);
        blob = new Blob([JSON.stringify(doc, null, 2)], { type: 'application/json' });
        filename = `${v.baseWorkflowId}-v${v.versionNumber}.${sourceName}.json`;
      }
      saveBlob(blob, filename);
    } catch (err) {
      this._error = err.problem || { title: err.message, status: err.status };
      this.emit('error', { problem: this._error, error: err });
    }
  }

  renderActions(v) {
    const host = this.$('.actions');
    const showForbidden = this.hasAttribute('show-forbidden');
    const canWrite = this.hasScope('catalog:write');
    const canPurge = this.hasScope('catalog:purge');
    const buttons = [];

    // Obsolete — Active versions only, catalog:write.
    if (v.status === 'Active' && (canWrite || showForbidden)) {
      buttons.push(`<button class="obsolete" type="button" ${canWrite ? '' : 'disabled title="Requires catalog:write"'}>Obsolete…</button>`);
    }

    // Delete — any status, catalog:purge, behind a confirm (refused server-side while runs reference it).
    if (canPurge || showForbidden) {
      buttons.push(`<button class="delete danger" type="button" ${canPurge ? '' : 'disabled title="Requires catalog:purge"'}>Delete…</button>`);
    }

    host.innerHTML = buttons.join('');
    host.querySelector('.obsolete')?.addEventListener('click', () => this.confirmObsolete(v));
    host.querySelector('.delete')?.addEventListener('click', () => this.confirmDelete(v));
  }

  async confirmObsolete(v) {
    const confirmed = await confirmDialog(this, {
      title: 'Obsolete version',
      message: `Mark ${v.baseWorkflowId} v${v.versionNumber} as Obsolete? It stays in the catalog but is flagged for retirement.`,
      confirmLabel: 'Obsolete',
    });
    if (!confirmed) return;
    try {
      const updated = await this.client.obsoleteCatalogVersion(v.baseWorkflowId, v.versionNumber);
      this._version = updated;
      this.renderBody();
      this.emit('version-changed', { version: updated });
    } catch (err) {
      this._error = err.problem || { title: err.message, status: err.status };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  async confirmDelete(v) {
    const confirmed = await confirmDialog(this, {
      title: 'Delete version',
      message: `Permanently delete ${v.baseWorkflowId} v${v.versionNumber}? This cannot be undone, and is refused while runs reference it.`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!confirmed) return;
    try {
      await this.client.deleteCatalogVersion(v.baseWorkflowId, v.versionNumber);
      this.emit('version-deleted', { baseWorkflowId: v.baseWorkflowId, versionNumber: v.versionNumber });
      this.emit('close');
    } catch (err) {
      this._error = err.problem || { title: err.message, status: err.status };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }
}

function saveBlob(blob, filename) {
  if (typeof document === 'undefined' || typeof URL.createObjectURL !== 'function') return;
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

define('arazzo-catalog-detail', ArazzoCatalogDetail);
export { ArazzoCatalogDetail };
