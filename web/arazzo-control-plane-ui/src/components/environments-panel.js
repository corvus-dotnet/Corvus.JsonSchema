// <arazzo-environments> — manage governed deployment environments (design §7.7).
//
//   <arazzo-environments base-url="/arazzo/v1" scopes="environments:read environments:write availability:read"></arazzo-environments>
//
// Attributes : base-url, scopes (gates the mutating controls)
// Properties : .client, .authProvider (() => Authorization header), .fetch
// Events     : environment-selected {environment}, environment-created {environment}, environment-changed {environment},
//              environment-deleted {name}, loaded {count}, error {problem}
// Parts      : panel, list, row, detail
//
// A master-detail over the environment registry: the left list pages the environments the caller's reach admits; the
// right pane administers the selected one — its mutable metadata (display name, description), its administrator set (the
// same resolved-identity governance as a workflow, via <arazzo-administrators-panel environment=…>), and the workflow
// versions made available in it (§7.8). Creating an environment grants the caller administration of it; the set may
// never be left without an administrator. Mutating controls are gated by environments:write.

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { actorLabel, ArazzoElement, SHARED_CSS, PAGER_CSS, escapeHtml, relativeTime, absoluteTime, confirmDialog, define } from './base.js';
import './tag-editor.js';
import './administrators-panel.js';
import './pager.js';

class ArazzoEnvironments extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._authProvider = undefined;
    /** @private */ this._fetch = undefined;
    /** @private */ this._envs = [];
    /** @private */ this._selected = null;       // the selected environment name
    /** @private */ this._detail = null;         // the selected environment's full summary
    /** @private */ this._availability = [];     // versions available in the selected environment
    /** @private */ this._loading = false;
    /** @private */ this._detailLoading = false;
    /** @private */ this._error = null;
    /** @private */ this._history = [];          // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
    /** @private */ this._total = null;            // bounded total across all pages (null until counted)
    /** @private */ this._totalCapped = false;     // true when the true total meets/exceeds the server cap → render "N+"
    /** @private */ this._listSeq = 0;
    /** @private */ this._detailSeq = 0;
    /** @private */ this._form = null;           // the create-dialog form state
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'scopes') this.renderBody();
    else if (name === 'page-size') this.reload(); // page size never invalidates an injected client
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

  get canWrite() { return this.hasScope('environments:write'); }

  get canReadAvailability() { return this.hasScope('availability:read'); }

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
      this._envs = [];
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
        client.listEnvironments({ pageToken: this._currentToken, limit: this.pageSize }),
        client.countEnvironments().catch(() => null),
      ]);
      if (seq !== this._listSeq) return;
      this._envs = page.environments;
      this._nextPageToken = page.nextPageToken;
      this._total = total ? total.count : null;
      this._totalCapped = total ? total.capped : false;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._envs.length, hasMore: !!this._nextPageToken });
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
    this._availability = [];
    const seq = ++this._detailSeq;
    this._detailLoading = true;
    this.renderBody();
    const client = this.buildClient();
    try {
      const detail = await client.getEnvironment(name);
      // Availability needs its own scope; skip the read (and the pane section) when it is not granted.
      const availability = this.canReadAvailability
        ? (await client.listEnvironmentAvailability(name).catch(() => ({ availability: [] }))).availability
        : [];
      if (seq !== this._detailSeq) return;
      this._detail = detail;
      this._availability = availability;
      this._detailLoading = false;
      this.renderBody();
      this.emit('environment-selected', { environment: detail });
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
    this._availability = [];
    this._detailSeq++;
  }

  async saveMetadata() {
    if (!this._detail) return;
    const displayName = this.$('.d-displayName')?.value ?? '';
    const description = this.$('.d-description')?.value ?? '';
    // A present managementTags re-tags the reach scope (§14.2): the editor is seeded with the current non-internal
    // labels, so re-saving is idempotent, editing re-tags, and clearing removes them. The server preserves the
    // deployment-internal tags and rejects the reserved sys: prefix (400).
    const mgmtEd = this.$('.d-mgmt-editor');
    const managementTags = mgmtEd ? mgmtEd.tags : [];
    // The checkbox state IS the desired requirement — a present requireEvidence replaces the stored flag (§4.6).
    const requireEvidence = this.$('.d-requireEvidence')?.checked ?? false;
    const allowsDraftRuns = this.$('.d-allowsDraftRuns')?.checked ?? false;
    const saveBtn = this.$('.d-save');
    if (saveBtn) saveBtn.disabled = true;
    try {
      const updated = await this.buildClient().updateEnvironment(this._detail.name, { displayName, description, managementTags, requireEvidence, allowsDraftRuns });
      this._detail = updated;
      const i = this._envs.findIndex((e) => e.name === updated.name);
      if (i >= 0) this._envs[i] = { ...this._envs[i], ...updated };
      this.renderBody();
      this.emit('environment-changed', { environment: updated });
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  async deleteEnvironment(name) {
    const confirmed = await confirmDialog(this, {
      title: 'Delete environment',
      message: `Delete the environment '${name}'? Its administration and availability entries are removed. This cannot be undone.`,
      confirmLabel: 'Delete', danger: true,
    });
    if (!confirmed) return;
    try {
      await this.buildClient().deleteEnvironment(name);
      this.clearDetail();
      await this.reload();
      this.emit('environment-deleted', { name });
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  // ---- create (modal dialog) --------------------------------------------------------------------

  openCreate() {
    this._form = { name: '', displayName: '', description: '', managementTags: [], requireEvidence: false, allowsDraftRuns: false, formError: null };
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
    if (!name) { form.formError = { title: 'An environment name is required.' }; this.renderEditor(); return; }
    try {
      const managementTags = this.$('.f-mgmt-editor')?.tags ?? [];
      const created = await this.buildClient().createEnvironment({
        name,
        displayName: (form.displayName || '').trim() || undefined,
        description: (form.description || '').trim() || undefined,
        requireEvidence: form.requireEvidence || undefined,
        allowsDraftRuns: form.allowsDraftRuns || undefined,
        managementTags: managementTags.length ? managementTags : undefined,
      });
      this.closeEditor();
      await this.reload();
      await this.select(created.name);
      this.emit('environment-created', { environment: created });
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
        .opt { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 8px 10px; margin: 6px 0; display: grid; gap: 4px; }
        :host { display: flex; flex-direction: column; min-height: 0; height: 100%; }
        .layout { flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1fr); grid-auto-rows: minmax(0, 1fr); gap: 14px; }
        .layout > * { min-height: 0; }
        .detail-pane { min-height: 0; overflow: auto; }
        .detail-pane:empty { display: none; }
        @media (min-width: 880px) { .layout.has-selection { grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr); } }

        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        /* The list is a bordered table, matching the Runs/Catalog/Sources lists. */
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
        .ename { font-weight: 600; }
        .ecode { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); }
        .edesc { color: var(--_muted); overflow: hidden; text-overflow: ellipsis; }
        .etime { color: var(--_muted); font-size: 12px; white-space: nowrap; }
        ${PAGER_CSS}
        .pager { flex: none; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }

        /* detail pane */
        .detail .dhead { display: flex; align-items: baseline; gap: 8px; }
        .detail .dtitle { font-weight: 700; font-size: 16px; }
        .detail .dname { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); }
        .detail .d-close { margin-left: auto; cursor: pointer; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_muted); width: 24px; height: 24px; line-height: 1; padding: 0; }
        .detail .section { padding: 12px; border-top: 1px solid var(--_border); }
        .detail .section:first-of-type { border-top: none; }
        .detail .section h4 { margin: 0 0 8px; font-size: 13px; color: var(--_muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.03em; }
        .field { display: grid; gap: 4px; margin-bottom: 10px; }
        .field > span { font-size: 12px; color: var(--_muted); }
        label.check { display: flex; gap: 8px; align-items: center; color: var(--_text); font-size: 13px; cursor: pointer; margin: 0 0 4px; }
        label.check input { width: auto; }
        .field input, .field textarea { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        .field textarea { min-height: 52px; }
        .audit { color: var(--_muted); font-size: 12px; margin-top: 4px; }
        .row-actions { display: flex; gap: 8px; justify-content: flex-end; }
        .avail-row { display: flex; align-items: baseline; gap: 8px; padding: 7px 0; border-bottom: 1px solid var(--_border); }
        .avail-row:last-child { border-bottom: none; }
        .avail-wf { font-weight: 600; }
        .avail-ver { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); }
        .avail-when { margin-left: auto; color: var(--_muted); font-size: 12px; }

        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(480px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .dlg-head { padding: 14px 16px; border-bottom: 1px solid var(--_border); font-weight: 700; font-size: 15px; }
        .content { padding: 16px; display: grid; gap: 12px; }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <div class="layout" part="layout">
        <div class="wrap" part="panel">
          <div class="toolbar" part="toolbar">
            <span class="title">Environments</span>
            <span class="grow"></span>
            <button class="refresh ghost" type="button" title="Refresh">↻</button>
            <button class="new primary" type="button" hidden>New environment</button>
          </div>
          <div class="err"></div>
          <div class="tablescroll">
            <table>
              <thead><tr><th>Environment</th><th>Description</th><th>Created</th></tr></thead>
              <tbody class="list" part="rows"></tbody>
            </table>
          </div>
          <arazzo-pager class="pager" part="pager"></arazzo-pager>
        </div>
        <div class="detail-pane"></div>
      </div>
      <dialog part="dialog">
        <div class="dlg-head">New environment</div>
        <div class="content"></div>
        <div class="foot">
          <button class="cancel ghost" type="button">Cancel</button>
          <button class="confirm primary" type="button">Create</button>
        </div>
      </dialog>
    `;
    this.$('.refresh').addEventListener('click', () => this.reload());
    this.$('.new').addEventListener('click', () => this.openCreate());
    this.$('arazzo-pager').addEventListener('prev', () => this.prevPage());
    this.$('arazzo-pager').addEventListener('next', () => this.nextPage());
    this.$('.cancel').addEventListener('click', () => this.closeEditor());
    this.$('.confirm').addEventListener('click', () => this.submitForm());
    this.$('dialog').addEventListener('close', () => { this._form = null; });
    this.$('dialog').addEventListener('cancel', (e) => { e.preventDefault(); this.closeEditor(); });
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

    if (this._loading && this._envs.length === 0) {
      list.innerHTML = `<tr><td colspan="3"><div class="skl"></div><div class="skl"></div></td></tr>`;
    } else if (this._envs.length === 0) {
      list.innerHTML = `<tr><td colspan="3"><div class="empty">No environments defined.</div></td></tr>`;
    } else {
      list.innerHTML = this._envs.map((e) => `
        <tr class="erow selectable" part="row" data-name="${escapeHtml(e.name)}" aria-selected="${String(e.name === this._selected)}">
          <td part="cell"><span class="ename">${escapeHtml(e.displayName || e.name)}</span> <span class="ecode">${escapeHtml(e.name)}</span></td>
          <td part="cell" class="edesc">${e.description ? escapeHtml(e.description) : '<span class="muted">—</span>'}</td>
          <td part="cell" class="etime" title="${escapeHtml(absoluteTime(e.createdAt))}">${escapeHtml(relativeTime(e.createdAt))}</td>
        </tr>`).join('');
      this.$$('.erow').forEach((b) => b.addEventListener('click', () => this.select(b.dataset.name)));
    }

    this.renderFoot();
    this.renderDetail();
  }

  renderFoot() {
    // The footer shows the bounded grand total the caller's reach admits (with "+" when the server capped it), not just
    // the current page's length; it falls back to the visible count if the count query was unavailable.
    const shown = this._total != null ? `${this._total}${this._totalCapped ? '+' : ''}` : `${this._envs.length}`;
    const noun = (this._total != null ? this._total : this._envs.length) === 1 ? 'environment' : 'environments';
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

    const e = this._detail;
    const writable = this.canWrite;
    const auditUpdated = e.lastUpdatedAt
      ? ` · updated ${escapeHtml(relativeTime(e.lastUpdatedAt))}${e.lastUpdatedBy ? ` by ${actorLabel(e.lastUpdatedBy)}` : ''}`
      : '';
    pane.innerHTML = `
      <div class="panel detail" part="detail">
        <div class="section">
          <div class="dhead"><span class="dtitle">${escapeHtml(e.displayName || e.name)}</span><span class="dname">${escapeHtml(e.name)}</span><button class="d-close" type="button" title="Close" aria-label="Close">✕</button></div>
          <div class="audit">Created ${escapeHtml(relativeTime(e.createdAt))}${e.createdBy ? ` by ${actorLabel(e.createdBy)}` : ''}${auditUpdated}</div>
        </div>
        <div class="section">
          <h4>Details</h4>
          ${writable ? `
            <div class="field"><span>Display name</span><input class="d-displayName" value="${escapeHtml(e.displayName || '')}" placeholder="${escapeHtml(e.name)}"></div>
            <div class="field"><span>Description</span><textarea class="d-description" placeholder="(optional)">${escapeHtml(e.description || '')}</textarea></div>
            <div class="opt">
              <label class="check"><input type="checkbox" class="d-requireEvidence"${e.requireEvidence ? ' checked' : ''}> Require publish evidence for promotion</label>
              <div class="hint">When set, a workflow version may be made available here only if its server-attested scenario suite passed at publish.</div>
            </div>
            <div class="opt">
              <label class="check"><input type="checkbox" class="d-allowsDraftRuns"${e.allowsDraftRuns ? ' checked' : ''}> Allow draft debug runs</label>
              <div class="hint">A development-class posture: the designer may run a working copy's draft here. Leave off for shared or production-class environments.</div>
            </div>
            <div class="field"><span>Management tags</span><arazzo-tag-editor class="d-mgmt-editor"></arazzo-tag-editor></div>
            <div class="hint">Who may manage and see this environment. An administrator may re-tag; the deployment-internal tags are preserved and the reserved <code>sys:</code> prefix is not allowed.</div>
            <div class="row-actions"><button class="d-save primary" type="button">Save</button></div>
          ` : `
            <div class="field"><span>Description</span><div>${e.description ? escapeHtml(e.description) : '<span class="muted">—</span>'}</div></div>
            ${e.requireEvidence ? '<div class="field"><span>Promotion</span><div>Requires green publish evidence.</div></div>' : ''}
            ${e.allowsDraftRuns ? '<div class="field"><span>Draft runs</span><div>Allows draft debug runs.</div></div>' : ''}
            <div class="field"><span>Management tags</span><div>${Array.isArray(e.managementTags) && e.managementTags.length
              ? `<span class="mtags">${e.managementTags.map((t) => `<code>${escapeHtml(t.key)}=${escapeHtml(t.value)}</code>`).join(' ')}</span>`
              : '<span class="muted">None — visible to everyone within reach.</span>'}</div></div>
          `}
        </div>
        <div class="section">
          <h4>Administrators</h4>
          <arazzo-administrators-panel class="env-admins" environment="${escapeHtml(e.name)}" scopes="${escapeHtml(this.getAttribute('scopes') || '')}"></arazzo-administrators-panel>
        </div>
        ${this.canReadAvailability ? `
        <div class="section">
          <h4>Available workflow versions</h4>
          <div class="avail-list">${this.availabilityHtml()}</div>
        </div>` : ''}
        ${writable ? `
        <div class="section">
          <div class="row-actions"><button class="d-delete danger" type="button">Delete environment…</button></div>
        </div>` : ''}
      </div>
    `;

    const admins = pane.querySelector('.env-admins');
    if (admins) admins.client = this.buildClient();
    // Seed the management-tags editor once it is in the DOM (its .tags setter re-renders the rows) —
    // with the USER-owned labels only. A deployed environment also carries deployment-internal
    // sys:* tags (reach plumbing the server owns and preserves); echoing those back on save is a
    // 400 (reserved prefix), so seeding them would make every subsequent save of that environment
    // fail. They are not the operator's to edit.
    const mgmtEd = pane.querySelector('.d-mgmt-editor');
    if (mgmtEd) {
      mgmtEd.tags = (Array.isArray(e.managementTags) ? e.managementTags : [])
        .filter((t) => !String(t.key || '').startsWith('sys:'));
    }
    const saveBtn = pane.querySelector('.d-save');
    if (saveBtn) saveBtn.addEventListener('click', () => this.saveMetadata());
    const delBtn = pane.querySelector('.d-delete');
    if (delBtn) delBtn.addEventListener('click', () => this.deleteEnvironment(e.name));
    pane.querySelector('.d-close')?.addEventListener('click', () => { this.clearDetail(); this.renderBody(); });
  }

  availabilityHtml() {
    if (this._availability.length === 0) return '<div class="empty">No workflow versions are available in this environment yet.</div>';
    return this._availability.map((a) => `
      <div class="avail-row">
        <span class="avail-wf">${escapeHtml(a.baseWorkflowId)}</span>
        <span class="avail-ver">v${escapeHtml(a.versionNumber)}</span>
        <span class="avail-when" title="${escapeHtml(absoluteTime(a.createdAt))}">made available ${escapeHtml(relativeTime(a.createdAt))}${a.createdBy ? ` by ${escapeHtml(a.createdBy)}` : ''}</span>
      </div>`).join('');
  }

  renderEditor() {
    const content = this.$('.content');
    const f = this._form;
    if (!content || !f) return;
    content.innerHTML = `
      <div class="field"><span>Name</span><input class="f-name" placeholder="qa" value="${escapeHtml(f.name)}"></div>
      <div class="field"><span>Display name</span><input class="f-displayName" placeholder="(optional)" value="${escapeHtml(f.displayName)}"></div>
      <div class="field"><span>Description</span><textarea class="f-description" placeholder="(optional)">${escapeHtml(f.description)}</textarea></div>
      <div class="opt"><label class="check"><input type="checkbox" class="f-requireEvidence"${f.requireEvidence ? ' checked' : ''}> Require publish evidence for promotion</label></div>
      <div class="opt"><label class="check"><input type="checkbox" class="f-allowsDraftRuns"${f.allowsDraftRuns ? ' checked' : ''}> Allow draft debug runs (development-class environments only)</label></div>
      <div class="field"><span>Management tags</span><arazzo-tag-editor class="f-mgmt-editor"></arazzo-tag-editor></div>
      <div class="hint">Scope who may manage and see this environment; an administrator may re-tag it later. The reserved <code>sys:</code> prefix is not allowed.</div>
      <div class="form-err">${f.formError ? `<div class="error-banner"><span><strong>${escapeHtml(f.formError.title || 'Request failed')}</strong>${f.formError.detail ? ' — ' + escapeHtml(f.formError.detail) : ''}</span></div>` : ''}</div>
    `;
    content.querySelector('.f-name').addEventListener('input', (ev) => { f.name = ev.target.value; });
    content.querySelector('.f-displayName').addEventListener('input', (ev) => { f.displayName = ev.target.value; });
    content.querySelector('.f-description').addEventListener('input', (ev) => { f.description = ev.target.value; });
    content.querySelector('.f-requireEvidence').addEventListener('change', (ev) => { f.requireEvidence = ev.target.checked; });
    content.querySelector('.f-allowsDraftRuns').addEventListener('change', (ev) => { f.allowsDraftRuns = ev.target.checked; });
    const mgmtEd = content.querySelector('.f-mgmt-editor');
    mgmtEd.tags = Array.isArray(f.managementTags) ? f.managementTags : [];
    mgmtEd.addEventListener('tags-changed', () => { f.managementTags = mgmtEd.tags; });
  }
}

define('arazzo-environments', ArazzoEnvironments);
export { ArazzoEnvironments };