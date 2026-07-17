// <arazzo-sources> — manage the registered source registry (design §7.6/§7.8).
//
//   <arazzo-sources base-url="/arazzo/v1" scopes="sources:read sources:write"></arazzo-sources>
//
// Attributes : base-url, scopes (gates the mutating controls)
// Properties : .client, .authProvider (() => Authorization header), .fetch
// Events     : source-selected {source}, source-created {source}, source-changed {source},
//              source-deleted {name}, loaded {count}, error {problem}
// Parts      : panel, list, row, detail
//
// A master-detail over the source registry: the left list pages the sources the caller's reach admits; the right pane
// administers the selected one — its mutable metadata (display name, description, management-tags reach) and a read-only
// view of its registered OpenAPI/AsyncAPI document. Registering a source makes it resolvable by a workflow's
// sourceDescriptions entry. Mutating controls are gated by sources:write.

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { ArazzoElement, SHARED_CSS, PAGER_CSS, escapeHtml, relativeTime, absoluteTime, confirmDialog, define } from './base.js';
import './tag-editor.js';
import './pager.js';
import './credential-dialog.js';
import './source-operations.js';
import './json-view.js';
import './splitbar.js';

class ArazzoSources extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._authProvider = undefined;
    /** @private */ this._fetch = undefined;
    /** @private */ this._sources = [];
    /** @private */ this._selected = null;       // the selected source name
    /** @private */ this._detail = null;          // the selected source's full entity (with document)
    /** @private */ this._loading = false;
    /** @private */ this._detailLoading = false;
    /** @private */ this._error = null;
    /** @private */ this._history = [];           // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
    /** @private */ this._total = null;            // bounded total across all pages (null until counted)
    /** @private */ this._totalCapped = false;     // true when the true total meets/exceeds the server cap → render "N+"
    /** @private */ this._listSeq = 0;
    /** @private */ this._detailSeq = 0;
    /** @private */ this._form = null;            // the create-dialog form state
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'scopes') this.renderBody();
    else { this._client = undefined; this.reload(); }
  }

  set authProvider(fn) { this._authProvider = fn; this._client = undefined; this.reload(); }
  get authProvider() { return this._authProvider; }

  set fetch(fn) { this._fetch = fn; this._client = undefined; this.reload(); }

  requestRender() { this.reload(); }

  refresh() { this.reload(); }

  buildClient() {
    if (this._client) return this._client;
    const baseUrl = this.getAttribute('base-url');
    if (!baseUrl) return undefined;
    this._client = new ArazzoControlPlaneClient({ baseUrl, fetch: this._fetch, getAuthHeader: this._authProvider });
    return this._client;
  }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 50;
  }

  get scopeList() {
    return (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
  }

  hasScope(scope) {
    const s = this.scopeList;
    return s.length === 0 || s.includes(scope);
  }

  get canWrite() { return this.hasScope('sources:write'); }

  // ---- list -------------------------------------------------------------------------------------

  /** Reload from page 1 (resets the keyset cursor). */
  reload() {
    this._history = [];
    this._currentToken = undefined;
    this.load();
  }

  async load() {
    const client = this.buildClient();
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url or .client.' };
      this._sources = [];
      this.renderBody();
      return;
    }
    const seq = ++this._listSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      // Fetch the page and the bounded total (for the footer) together; the count is a no-rows bounded query, and a
      // count failure must not break the list, so it falls back to null (footer then shows the visible page count).
      const [page, total] = await Promise.all([
        client.listSources({ pageToken: this._currentToken, limit: this.pageSize }),
        client.countSources().catch(() => null),
      ]);
      if (seq !== this._listSeq) return;
      this._sources = page.sources;
      this._nextPageToken = page.nextPageToken;
      this._total = total ? total.count : null;
      this._totalCapped = total ? total.capped : false;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._sources.length, hasMore: !!this._nextPageToken });
    } catch (err) {
      if (seq !== this._listSeq) return;
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

  // ---- selection / detail -----------------------------------------------------------------------

  async select(name) {
    if (!name) return;
    this._selected = name;
    this._detail = null;
    const seq = ++this._detailSeq;
    this._detailLoading = true;
    this.renderBody();
    const client = this.buildClient();
    try {
      const detail = await client.getSource(name);
      if (seq !== this._detailSeq) return;
      this._detail = detail;
      this._detailLoading = false;
      this.renderBody();
      this.emit('source-selected', { source: detail });
    } catch (err) {
      if (seq !== this._detailSeq) return;
      this._detailLoading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  clearDetail() {
    this._selected = null;
    this._detail = null;
    this._detailSeq++;
  }

  async saveMetadata() {
    if (!this._detail) return;
    const displayName = this.$('.d-displayName')?.value ?? '';
    const description = this.$('.d-description')?.value ?? '';
    // A present managementTags re-tags the reach scope (§14.2); the editor is seeded with the current non-internal
    // labels, so re-saving is idempotent. The document is omitted, so the registered spec is preserved untouched.
    const mgmtEd = this.$('.d-mgmt-editor');
    const managementTags = mgmtEd ? mgmtEd.tags : [];
    const saveBtn = this.$('.d-save');
    if (saveBtn) saveBtn.disabled = true;
    try {
      const updated = await this.buildClient().updateSource(this._detail.name, { displayName, description, managementTags });
      this._detail = { ...this._detail, ...updated };
      const i = this._sources.findIndex((s) => s.name === updated.name);
      if (i >= 0) this._sources[i] = { ...this._sources[i], ...updated };
      this.renderBody();
      this.emit('source-changed', { source: this._detail });
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  async deleteSource(name) {
    const confirmed = await confirmDialog(this, {
      title: 'Delete source',
      message: `Delete the registered source '${name}'? Workflows that reference it will no longer resolve it. This cannot be undone.`,
      confirmLabel: 'Delete', danger: true,
    });
    if (!confirmed) return;
    try {
      await this.buildClient().deleteSource(name);
      this.clearDetail();
      await this.reload();
      this.emit('source-deleted', { name });
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  // ---- create (modal dialog) --------------------------------------------------------------------

  openCreate() {
    this._form = { name: '', type: 'openapi', displayName: '', description: '', document: '', managementTags: [], formError: null };
    this.renderEditor();
    this.$('dialog').showModal();
    this.$('.f-name')?.focus();
  }

  closeEditor() {
    this.$('dialog')?.close();
    this._form = null;
  }

  async submitForm() {
    if (!this._form) return;
    const form = this._form;
    const name = (form.name || '').trim();
    if (!name) { form.formError = { title: 'A source name is required.' }; this.renderEditor(); return; }
    let document;
    try {
      document = JSON.parse(form.document);
    } catch {
      form.formError = { title: 'The document must be valid JSON.', detail: 'Paste the source’s OpenAPI or AsyncAPI document.' };
      this.renderEditor();
      return;
    }
    try {
      const managementTags = this.$('.f-mgmt-editor')?.tags ?? [];
      const created = await this.buildClient().createSource({
        name,
        type: form.type,
        document,
        displayName: (form.displayName || '').trim() || undefined,
        description: (form.description || '').trim() || undefined,
        managementTags: managementTags.length ? managementTags : undefined,
      });
      this.closeEditor();
      await this.reload();
      await this.select(created.name);
      this.emit('source-created', { source: created });
    } catch (err) {
      form.formError = err.problem || { title: err.message };
      this.renderEditor();
      this.emit('error', { problem: form.formError, error: err });
    }
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: flex; flex-direction: column; min-height: 0; height: 100%; }
        .layout { flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1fr); grid-auto-rows: minmax(0, 1fr); gap: 14px; }
        .layout .splitbar { display: none; }
        @media (min-width: 880px) {
          .layout.has-selection { grid-template-columns: minmax(0, 1fr) auto var(--detail-w, 460px); gap: 0; }
          .layout.has-selection .splitbar { display: block; }
          .layout.has-selection > .wrap { margin-right: 14px; }
          .layout.has-selection > .detail-pane { margin-left: 14px; }
        }
        .layout > * { min-height: 0; }
        @media (min-width: 880px) { .layout.has-selection { grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr); } }

        .wrap { flex: 1; min-height: 0; display: flex; flex-direction: column; border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; background: var(--_bg); }
        .toolbar { flex: none; display: flex; align-items: center; gap: 8px; padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); }
        .toolbar .title { font-weight: 600; color: var(--_muted); font-size: 12px; }
        .toolbar .grow { flex: 1; }
        .err { flex: none; margin: 10px 12px; }
        .err:empty { display: none; }
        .tablescroll { flex: 1; min-height: 0; overflow: auto; scrollbar-gutter: stable; }
        table { width: 100%; border-collapse: collapse; }
        thead th { text-align: left; font-size: 12px; font-weight: 600; color: var(--_muted); padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); white-space: nowrap; position: sticky; top: 0; z-index: 1; }
        tbody td { padding: 9px 12px; border-bottom: 1px solid var(--_border); vertical-align: middle; }
        tbody tr:last-child td { border-bottom: none; }
        tbody tr.selectable { cursor: pointer; }
        tbody tr.selectable:hover { background: var(--_surface); }
        tbody tr[aria-selected="true"] { background: color-mix(in srgb, var(--_accent) 12%, transparent); }
        .sname { font-weight: 600; }
        .scode { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); }
        .sdesc { color: var(--_muted); overflow: hidden; text-overflow: ellipsis; }
        .stime { color: var(--_muted); font-size: 12px; white-space: nowrap; }
        .type-badge { display: inline-block; font-size: 11px; font-weight: 600; padding: 2px 7px; border-radius: 999px; border: 1px solid var(--_border); color: var(--_muted); background: var(--_surface); text-transform: uppercase; letter-spacing: 0.03em; }
        ${PAGER_CSS}
        .pager { flex: none; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }

        /* detail pane */
        .detail-pane { min-height: 0; overflow: auto; }
        .detail-pane:empty { display: none; }
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        .detail .dhead { display: flex; align-items: baseline; gap: 8px; }
        .detail .d-close { margin-left: auto; cursor: pointer; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_muted); width: 24px; height: 24px; line-height: 1; padding: 0; }
        .detail .dtitle { font-weight: 700; font-size: 16px; }
        .detail .dname { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); }
        .detail .section { padding: 12px; border-top: 1px solid var(--_border); }
        .detail .section:first-of-type { border-top: none; }
        .detail .section h4 { margin: 0 0 8px; font-size: 13px; color: var(--_muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.03em; }
        .field { display: grid; gap: 4px; margin-bottom: 10px; }
        .field > span { font-size: 12px; color: var(--_muted); }
        .field input, .field textarea, .field select { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        .field textarea { min-height: 52px; }
        .field textarea.doc { min-height: 160px; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; white-space: pre; }
        .audit { color: var(--_muted); font-size: 12px; margin-top: 4px; }
        .row-actions { display: flex; gap: 8px; justify-content: flex-end; }
        pre.doc { margin: 0; max-height: 320px; overflow: auto; padding: 10px 12px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }
        .mtags code { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }

        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(560px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .dlg-head { padding: 14px 16px; border-bottom: 1px solid var(--_border); font-weight: 700; font-size: 15px; }
        .content { padding: 16px; display: grid; gap: 12px; }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <div class="layout" part="layout">
        <div class="wrap" part="panel">
          <div class="toolbar" part="toolbar">
            <span class="title">Sources</span>
            <span class="grow"></span>
            <button class="refresh ghost" type="button" title="Refresh">↻</button>
            <button class="new primary" type="button" hidden>Register source</button>
          </div>
          <div class="err"></div>
          <div class="tablescroll">
            <table>
              <thead><tr><th>Source</th><th>Type</th><th>Description</th><th>Created</th><th></th></tr></thead>
              <tbody class="list" part="rows"></tbody>
            </table>
          </div>
          <arazzo-pager class="pager" part="pager"></arazzo-pager>
        </div>
        <arazzo-splitbar class="splitbar" orientation="vertical" target=".layout" prop="--detail-w"
                         min="320" max="820" invert storage-key="sources.split.detail"
                         aria-label="Resize the detail pane"></arazzo-splitbar>
        <div class="detail-pane"></div>
      </div>
      <dialog part="dialog">
        <div class="dlg-head">Register source</div>
        <div class="content"></div>
        <div class="foot">
          <button class="cancel ghost" type="button">Cancel</button>
          <button class="confirm primary" type="button">Register</button>
        </div>
      </dialog>
      <arazzo-credential-dialog></arazzo-credential-dialog>
    `;
    this.$('.refresh').addEventListener('click', () => this.reload());
    this.$('.new').addEventListener('click', () => this.openCreate());
    this.$('arazzo-pager').addEventListener('prev', () => this.prevPage());
    this.$('arazzo-pager').addEventListener('next', () => this.nextPage());
    this.$('.cancel').addEventListener('click', () => this.closeEditor());
    this.$('.confirm').addEventListener('click', () => this.submitForm());
    this.$('dialog').addEventListener('close', () => { this._form = null; });
    this.$('dialog').addEventListener('cancel', (e) => { e.preventDefault(); this.closeEditor(); });
    this.$('arazzo-credential-dialog').addEventListener('credential-saved', (e) => this.emit('credential-saved', e.detail));
  }

  renderBody() {
    const err = this.$('.err');
    const list = this.$('.list');
    if (!list) return;
    this.$('.new').hidden = !this.canWrite;
    this.$('.layout').classList.toggle('has-selection', !!this._selected);

    err.innerHTML = this._error
      ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
      : '';

    if (this._loading && this._sources.length === 0) {
      list.innerHTML = `<tr><td colspan="4"><div class="skl"></div><div class="skl"></div></td></tr>`;
    } else if (this._sources.length === 0) {
      list.innerHTML = `<tr><td colspan="4"><div class="empty">No sources registered.</div></td></tr>`;
    } else {
      list.innerHTML = this._sources.map((s) => `
        <tr class="srow selectable" part="row" data-name="${escapeHtml(s.name)}" aria-selected="${String(s.name === this._selected)}">
          <td part="cell"><span class="sname">${escapeHtml(s.displayName || s.name)}</span> <span class="scode">${escapeHtml(s.name)}</span></td>
          <td part="cell"><span class="type-badge">${escapeHtml(s.type || '')}</span></td>
          <td part="cell" class="sdesc">${s.description ? escapeHtml(s.description) : '<span class="muted">—</span>'}</td>
          <td part="cell" class="stime" title="${escapeHtml(absoluteTime(s.createdAt))}">${escapeHtml(relativeTime(s.createdAt))}</td>
          <td part="cell" class="sact"><button class="cred-add ghost" type="button" data-source="${escapeHtml(s.name)}" title="Add a credential binding for ${escapeHtml(s.name)} in an environment">＋ credential</button></td>
        </tr>`).join('');
      this.$$('.srow').forEach((b) => b.addEventListener('click', () => this.select(b.dataset.name)));
      // The job a connections admin brings to this page: credential the source. The dialog opens
      // pre-filled and source-locked; environment and references are the choices being made.
      this.$$('.cred-add').forEach((btn) => btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const dialog = this.$('arazzo-credential-dialog');
        dialog.client = this.buildClient();
        dialog.open(null, { sourceName: btn.dataset.source, lockSource: true });
      }));
    }

    this.renderFoot();
    this.renderDetail();
  }

  renderFoot() {
    // The footer shows the bounded grand total the caller's reach admits (with "+" when the server capped it), not just
    // the current page's length; it falls back to the visible count if the count query was unavailable.
    const shown = this._total != null ? `${this._total}${this._totalCapped ? '+' : ''}` : `${this._sources.length}`;
    const noun = (this._total != null ? this._total : this._sources.length) === 1 ? 'source' : 'sources';
    const info = this._loading
      ? 'Loading…'
      : `${shown} ${noun}${this._history.length ? ` · page ${this._history.length + 1}` : ''}`;
    this.$('arazzo-pager')?.update({ hasPrev: this._history.length > 0, hasNext: !!this._nextPageToken, loading: this._loading, info });
  }

  renderDetail() {
    const pane = this.$('.detail-pane');
    if (!pane) return;
    if (!this._selected) { pane.replaceChildren(); return; }
    if (this._detailLoading && !this._detail) {
      pane.innerHTML = '<div class="panel detail"><div class="section"><div class="skl"></div><div class="skl"></div></div></div>';
      return;
    }
    if (!this._detail) { pane.replaceChildren(); return; }

    const s = this._detail;
    const writable = this.canWrite;
    const auditUpdated = s.lastUpdatedAt
      ? ` · updated ${escapeHtml(relativeTime(s.lastUpdatedAt))}${s.lastUpdatedBy ? ` by ${escapeHtml(s.lastUpdatedBy)}` : ''}`
      : '';
    let docText;
    try { docText = JSON.stringify(s.document, null, 2); } catch { docText = String(s.document ?? ''); }
    pane.innerHTML = `
      <div class="panel detail" part="detail">
        <div class="section">
          <div class="dhead"><span class="dtitle">${escapeHtml(s.displayName || s.name)}</span><span class="dname">${escapeHtml(s.name)}</span> <span class="type-badge">${escapeHtml(s.type || '')}</span><button class="d-close" type="button" title="Close" aria-label="Close">✕</button></div>
          <div class="audit">Created ${escapeHtml(relativeTime(s.createdAt))}${s.createdBy ? ` by ${escapeHtml(s.createdBy)}` : ''}${auditUpdated}</div>
        </div>
        <div class="section">
          <h4>Details</h4>
          ${writable ? `
            <div class="field"><span>Display name</span><input class="d-displayName" value="${escapeHtml(s.displayName || '')}" placeholder="${escapeHtml(s.name)}"></div>
            <div class="field"><span>Description</span><textarea class="d-description" placeholder="(optional)">${escapeHtml(s.description || '')}</textarea></div>
            <div class="field"><span>Management tags</span><arazzo-tag-editor class="d-mgmt-editor"></arazzo-tag-editor></div>
            <div class="hint">Who may manage and see this source. An administrator may re-tag; the deployment-internal tags are preserved and the reserved <code>sys:</code> prefix is not allowed.</div>
            <div class="row-actions"><button class="d-save primary" type="button">Save</button></div>
          ` : `
            <div class="field"><span>Description</span><div>${s.description ? escapeHtml(s.description) : '<span class="muted">—</span>'}</div></div>
            <div class="field"><span>Management tags</span><div>${Array.isArray(s.managementTags) && s.managementTags.length
              ? `<span class="mtags">${s.managementTags.map((t) => `<code>${escapeHtml(t.key)}=${escapeHtml(t.value)}</code>`).join(' ')}</span>`
              : '<span class="muted">None — visible to everyone within reach.</span>'}</div></div>
          `}
        </div>
        <div class="section">
          <h4>Operations</h4>
          <arazzo-source-operations class="d-ops"></arazzo-source-operations>
        </div>
        <div class="section">
          <h4>Document</h4>
          <arazzo-json-view class="d-doc"></arazzo-json-view>
        </div>
        ${writable ? `
        <div class="section">
          <div class="row-actions"><button class="d-delete danger" type="button">Delete source…</button></div>
        </div>` : ''}
      </div>
    `;

    pane.querySelector('.d-close')?.addEventListener('click', () => { this.clearDetail(); this.renderBody(); });
    // The structured operation surface (#843): the standard filterable view over the registered document,
    // plus the raw document as a highlighted read-only editor rather than plain text.
    const opsView = pane.querySelector('.d-ops');
    opsView.client = this.buildClient();
    opsView.source = s.name;
    opsView.addEventListener('error', (e) => e.stopPropagation()); // its inline error row is the surface
    pane.querySelector('.d-doc').value = docText;
    const mgmtEd = pane.querySelector('.d-mgmt-editor');
    if (mgmtEd) mgmtEd.tags = Array.isArray(s.managementTags) ? s.managementTags : [];
    const saveBtn = pane.querySelector('.d-save');
    if (saveBtn) saveBtn.addEventListener('click', () => this.saveMetadata());
    const delBtn = pane.querySelector('.d-delete');
    if (delBtn) delBtn.addEventListener('click', () => this.deleteSource(s.name));
  }

  renderEditor() {
    const content = this.$('.content');
    const f = this._form;
    if (!content || !f) return;
    content.innerHTML = `
      <div class="field"><span>Name</span><input class="f-name" placeholder="onboarding" value="${escapeHtml(f.name)}"></div>
      <div class="field"><span>Type</span><select class="f-type">
        <option value="openapi"${f.type === 'openapi' ? ' selected' : ''}>OpenAPI</option>
        <option value="asyncapi"${f.type === 'asyncapi' ? ' selected' : ''}>AsyncAPI</option>
      </select></div>
      <div class="field"><span>Display name</span><input class="f-displayName" placeholder="(optional)" value="${escapeHtml(f.displayName)}"></div>
      <div class="field"><span>Description</span><textarea class="f-description" placeholder="(optional)">${escapeHtml(f.description)}</textarea></div>
      <div class="field"><span>Document (JSON)</span><textarea class="f-document doc" placeholder='{"openapi":"3.1.0", …}'>${escapeHtml(f.document)}</textarea></div>
      <div class="field"><span>Management tags</span><arazzo-tag-editor class="f-mgmt-editor"></arazzo-tag-editor></div>
      <div class="hint">Register the source's OpenAPI or AsyncAPI document so a workflow's <code>sourceDescriptions</code> entry resolves it. Management tags scope who may manage and see it; the reserved <code>sys:</code> prefix is not allowed.</div>
      <div class="form-err">${f.formError ? `<div class="error-banner"><span><strong>${escapeHtml(f.formError.title || 'Request failed')}</strong>${f.formError.detail ? ' — ' + escapeHtml(f.formError.detail) : ''}</span></div>` : ''}</div>
    `;
    content.querySelector('.f-name').addEventListener('input', (ev) => { f.name = ev.target.value; });
    content.querySelector('.f-type').addEventListener('change', (ev) => { f.type = ev.target.value; });
    content.querySelector('.f-displayName').addEventListener('input', (ev) => { f.displayName = ev.target.value; });
    content.querySelector('.f-description').addEventListener('input', (ev) => { f.description = ev.target.value; });
    content.querySelector('.f-document').addEventListener('input', (ev) => { f.document = ev.target.value; });
    const mgmtEd = content.querySelector('.f-mgmt-editor');
    mgmtEd.tags = Array.isArray(f.managementTags) ? f.managementTags : [];
    mgmtEd.addEventListener('tags-changed', () => { f.managementTags = mgmtEd.tags; });
  }
}

define('arazzo-sources', ArazzoSources);
export { ArazzoSources };