// <arazzo-source-acquisition-dialog> — attach a source to a working copy (design §3.2/§5.3):
// pick a registered source · fetch from a URL (server-side, optionally credentialed) · upload JSON.
//
//   const dlg = document.createElement('arazzo-source-acquisition-dialog');
//   dlg.client = client;                      // an ArazzoControlPlaneClient
//   host.appendChild(dlg);
//   dlg.open({ workingCopyId: 'wc-…', suggestedName: 'payments' });
//
// It also registers a NEW source into the sources registry (§7.6), reusing the same Fetch/Upload/GitHub acquisition:
//   dlg.openForRegister({ suggestedName: 'payments' });   // Registry + Catalog modes are hidden; commits via createSource
//
// Properties : .client
// Methods    : open({ workingCopyId, suggestedName? }) — attach to a working copy
//              openForRegister({ suggestedName? })      — register a new source in the registry
//              close()
// Events     : source-attached {attachment}    (attach: carries the working copy's NEW etag — hosts refresh their save token)
//              source-registered {source}       (register: the created source record)
//              error {problem}
//
// The four acquisition modes end at the same seam: attachWorkingCopySource with a registry
// reference or an inline document. Fetch previews what the server detected (type/version/digest)
// before attaching; upload accepts a JSON document file (YAML endpoints go through Fetch, where the
// server parses YAML); GitHub (§4.7) browses a repository through the caller's brokered session and
// imports a JSON spec as an inline document.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './github-connect.js';
import './catalog-table.js';
import './tag-editor.js';

class ArazzoSourceAcquisitionDialog extends ArazzoElement {
  constructor() {
    super();
    /** @private The commit target: 'attach' (to a working copy) or 'register' (a new source in the registry). */
    this._target = 'attach';
    /** @private */ this._workingCopyId = null;
    /** @private */ this._mode = 'registry';
    /** @private */ this._registry = [];
    /** @private */ this._fetched = null;   // the fetchSourceDocument result awaiting attach
    /** @private */ this._uploaded = null;  // { document, type } parsed from the chosen file
    /** @private */ this._seq = 0;
  }

  /** The Layer-0 client used to load choices, fetch, and attach. */
  set client(value) { this._client = value; }
  get client() { return this._client; }

  /** Open the dialog for a working copy; `suggestedName` seeds the sourceDescriptions-name input. */
  open({ workingCopyId, suggestedName = '' } = {}) {
    this._target = 'attach';
    this._workingCopyId = workingCopyId;
    this._mode = 'registry';
    this._registry = [];
    this._fetched = null;
    this._uploaded = null;
    this._github = { picked: null, path: '' };
    this.render();
    this.$('.name-in').value = suggestedName;
    this.$('.gh-connect').client = this._client;
    if (this.windowOpener) this.$('.gh-connect').windowOpener = this.windowOpener;
    this.$('dialog').showModal();
    this.loadRegistry();
  }

  /**
   * Open the dialog to REGISTER a new source in the sources registry (§7.6) rather than attach one to a working copy.
   * Reuses the same Fetch-URL / Upload / GitHub acquisition modes and preview; the Registry and Catalog modes (which
   * pick an existing source or a workflow trigger) do not apply and are hidden. Commits with `createSource` and emits
   * `source-registered {source}`. `suggestedName` seeds the source-name input.
   */
  openForRegister({ suggestedName = '' } = {}) {
    this._target = 'register';
    this._workingCopyId = null;
    this._mode = 'fetch';
    this._registry = [];
    this._fetched = null;
    this._uploaded = null;
    this._github = { picked: null, path: '' };
    this.render();
    this.$('.name-in').value = suggestedName;
    this.$('.gh-connect').client = this._client;
    if (this.windowOpener) this.$('.gh-connect').windowOpener = this.windowOpener;
    this.$('dialog').showModal();
    this.switchMode('fetch');
  }

  close() {
    this.$('dialog')?.close();
  }

  // ---- rendering --------------------------------------------------------------------------------

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: 10px; background: var(--_bg); color: inherit; padding: 0;
                 width: min(560px, 92vw); max-height: 88vh; display: flex; flex-direction: column; overflow: hidden; }
        dialog:not([open]) { display: none; }
        dialog::backdrop { background: rgb(0 0 0 / 0.35); }
        .head { display: flex; justify-content: space-between; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--_border); }
        .head h2 { margin: 0; font-size: 14px; }
        .body { padding: 12px 14px; display: grid; gap: 10px; overflow-y: auto; overflow-x: hidden; flex: 1; min-height: 0; }
        .body > * { min-width: 0; }
        label { display: grid; gap: 4px; font-size: 12px; color: var(--_muted); }
        input, select { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: var(--arazzo-text, inherit); }
        .tabs { display: flex; gap: 4px; }
        .tabs button { font-size: 12px; padding: 4px 12px; opacity: 0.7; }
        .tabs button.active { opacity: 1; border-color: var(--_accent); font-weight: 600; }
        .mode[hidden] { display: none; }
        .mode { display: grid; gap: 10px; }
        .cred { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
        .preview { font-size: 12px; border: 1px solid var(--_border); border-radius: 6px; padding: 8px 10px; display: grid; gap: 2px; }
        .crumb { display: flex; gap: 6px; align-items: center; font-size: 11px; color: var(--_muted); }
        .crumb button { font-size: 11px; padding: 0 8px; }
        .gh-list { display: grid; max-height: 200px; overflow: auto; border: 1px solid var(--_border); border-radius: 6px; }
        .gh-list button { text-align: left; border: none; background: none; padding: 6px 10px; font-size: 12px; cursor: pointer; color: inherit; border-radius: 0; }
        .gh-list button:hover:not(:disabled) { background: rgb(127 127 127 / 0.12); }
        .gh-list button:disabled { opacity: 0.45; cursor: default; }
        .gh-repo-label[hidden], .gh-browser[hidden] { display: none; }
        .preview .digest { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 10px; overflow-wrap: anywhere; color: var(--_muted); }
        .foot { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 14px; border-top: 1px solid var(--_border); }
        .error-banner[hidden] { display: none; }
      </style>
      <dialog part="dialog">
        <div class="head"><h2>${this._target === 'register' ? 'Register a source' : 'Attach a source'}</h2><button class="x" type="button" title="Close">✕</button></div>
        <div class="body">
          <div class="error-banner" hidden></div>
          <label>Name — matches the document's <code>sourceDescriptions</code>
            <input class="name-in" type="text" placeholder="e.g. payments" autocomplete="off">
          </label>
          <div class="tabs" role="tablist">
            <button type="button" data-mode="registry" class="active" ${this._target === 'register' ? 'hidden' : ''}>Registry</button>
            <button type="button" data-mode="fetch" ${this._target === 'register' ? 'class="active"' : ''}>Fetch URL</button>
            <button type="button" data-mode="upload">Upload</button>
            <button type="button" data-mode="catalog" ${this._target === 'register' ? 'hidden' : ''}>Catalog</button>
            <button type="button" data-mode="github">GitHub</button>
          </div>
          <div class="mode mode-registry">
            <label>Registered source
              <select class="registry-in"><option value="">Loading…</option></select>
            </label>
          </div>
          <div class="mode mode-fetch" hidden>
            <label>Document URL
              <input class="url-in" type="url" placeholder="https://api.example.com/openapi.json">
            </label>
            <div class="cred">
              <label>Credential source <span class="muted">(optional)</span>
                <input class="cred-source-in" type="text" autocomplete="off">
              </label>
              <label>Environment
                <input class="cred-env-in" type="text" autocomplete="off">
              </label>
            </div>
            <div><button class="fetch" type="button">Fetch &amp; preview</button></div>
            <div class="preview fetch-preview" hidden></div>
          </div>
          <div class="mode mode-upload" hidden>
            <label>Document file (JSON — YAML endpoints go through Fetch)
              <input class="file-in" type="file" accept=".json,application/json">
            </label>
            <div class="preview upload-preview" hidden></div>
          </div>
          <div class="mode mode-catalog" hidden>
            <div class="hint">Trigger a published workflow over HTTP: attaching adds the control plane's run-trigger operation for the picked version — request body typed by ITS inputs schema. Drag the operation onto the canvas like any other.</div>
            <label>Search the catalog
              <input class="cat-q" type="text" placeholder="name, tag, owner…" spellcheck="false">
            </label>
            <arazzo-catalog-table class="cat-table" selectable status="Active" page-size="6"></arazzo-catalog-table>
            <div class="cat-preview hint" hidden></div>
          </div>
          <div class="mode mode-github" hidden>
            <arazzo-github-connect class="gh-connect"></arazzo-github-connect>
            <label class="gh-repo-label" hidden>Repository (the user ∩ installation intersection)
              <select class="gh-repo-in"></select>
            </label>
            <div class="gh-browser" hidden>
              <div class="crumb"><button class="gh-up ghost" type="button" title="Up" hidden>↑</button><span class="gh-path"></span></div>
              <div class="gh-list"></div>
            </div>
            <div class="preview gh-preview" hidden></div>
          </div>
          ${this._target === 'register' ? `
          <label>Display name <span class="muted">(optional)</span>
            <input class="displayname-in" type="text" autocomplete="off">
          </label>
          <label>Description <span class="muted">(optional)</span>
            <input class="description-in" type="text" autocomplete="off">
          </label>
          <label>Management tags <span class="muted">(optional)</span></label>
          <arazzo-tag-editor class="reg-mgmt-editor"></arazzo-tag-editor>
          ` : ''}
        </div>
        <div class="foot">
          <button class="cancel" type="button">Cancel</button>
          <button class="attach" type="button" disabled>${this._target === 'register' ? 'Register' : 'Attach'}</button>
        </div>
      </dialog>
    `;

    this.$('button.x').addEventListener('click', () => this.close());
    this.$('button.cancel').addEventListener('click', () => this.close());
    this.$('button.attach').addEventListener('click', () => this.attach());
    this.$$('.tabs button').forEach((tab) => tab.addEventListener('click', () => this.switchMode(tab.dataset.mode)));
    this.$('.name-in').addEventListener('input', () => this.updateAttachState());
    this.$('.registry-in').addEventListener('change', () => this.updateAttachState());
    this.$('button.fetch').addEventListener('click', () => this.fetchPreview());
    this.$('.file-in').addEventListener('change', () => this.readUpload());
    this.$('.gh-connect').addEventListener('github-connected', () => this.renderGitHubRepos());
    this.$('.gh-connect').addEventListener('github-disconnected', () => this.renderGitHubRepos());
    this.$('.gh-repo-in').addEventListener('change', () => this.browseGitHub(''));
    this.$('.gh-up').addEventListener('click', () => this.browseGitHub(this._github.path.split('/').slice(0, -1).join('/')));
  }

  switchMode(mode) {
    this._mode = mode;
    this.$$('.tabs button').forEach((tab) => tab.classList.toggle('active', tab.dataset.mode === mode));
    this.$('.mode-registry').hidden = mode !== 'registry';
    this.$('.mode-fetch').hidden = mode !== 'fetch';
    this.$('.mode-upload').hidden = mode !== 'upload';
    this.$('.mode-github').hidden = mode !== 'github';
    this.$('.mode-catalog').hidden = mode !== 'catalog';
    if (mode === 'github') this.renderGitHubRepos();
    if (mode === 'catalog') this.loadCatalog();
    this.updateAttachState();
  }

  showError(message) {
    const banner = this.$('.error-banner');
    banner.textContent = message;
    banner.hidden = false;
  }

  clearError() {
    this.$('.error-banner').hidden = true;
  }

  updateAttachState() {
    const name = this.$('.name-in').value.trim();
    const ready = name.length > 0 && (
      (this._mode === 'registry' && this.$('.registry-in').value)
      || (this._mode === 'fetch' && this._fetched)
      || (this._mode === 'upload' && this._uploaded)
      || (this._mode === 'github' && this._github?.picked)
      || (this._mode === 'catalog' && this._catalog?.picked));
    this.$('button.attach').disabled = !ready;
  }

  // ---- modes ------------------------------------------------------------------------------------

  /** Catalog mode: the REAL catalog table — server-side filtered, keyset-paged (hundreds of
   *  workflows stay browsable); picking a row synthesizes the §6.2 trigger source. */
  loadCatalog() {
    const table = this.$('.cat-table');
    if (table.client !== this._client) {
      table.client = this._client;
      table.addEventListener('version-selected', (e) => void this.pickCatalogVersion(e.detail.version));
      let debounce = 0;
      this.$('.cat-q').addEventListener('input', (e) => {
        clearTimeout(debounce);
        debounce = setTimeout(() => { table.filters = { ...table.filters, q: e.target.value.trim() || undefined }; }, 250);
      });
    }
  }

  /** @private — the synthesized source: ONE operation, the version's HTTP trigger, inputs-typed. */
  async pickCatalogVersion(version) {
    this.clearError();
    this._catalog = { picked: null };
    const preview = this.$('.cat-preview');
    preview.hidden = true;
    if (!version) { this.updateAttachState(); return; }
    let inputs = { type: 'object' };
    try {
      const schemas = await this._client.getCatalogWorkflowSchemas(version.baseWorkflowId, version.versionNumber);
      inputs = schemas.workflows?.[version.workflowId]?.inputs ?? Object.values(schemas.workflows ?? {})[0]?.inputs ?? inputs;
    } catch {
      // No baked metadata: the trigger still attaches, body untyped.
    }

    const path = `/catalog/${version.baseWorkflowId}/versions/${version.versionNumber}/runs`;
    this._catalog.picked = {
      version,
      document: {
        openapi: '3.1.0',
        info: { title: `Control plane — run ${version.title ?? version.baseWorkflowId} v${version.versionNumber}`, version: '1.0.0' },
        paths: { [path]: { post: {
          operationId: `start-${version.baseWorkflowId}-v${version.versionNumber}`,
          summary: `Start a run of ${version.title ?? version.baseWorkflowId} v${version.versionNumber} (the control plane's HTTP trigger; 202 = the run was created, it executes asynchronously)`,
          requestBody: { content: { 'application/json': { schema: inputs } } },
          responses: {
            202: { description: 'run created', content: { 'application/json': { schema: { type: 'object', properties: { runId: { type: 'string' }, status: { type: 'string' } } } } } },
            404: { description: 'unknown version, or outside your reach' },
            409: { description: 'no live runner hosts this version in the environment' },
          },
        } } },
      },
    };
    if (!this.$('.name-in').value.trim()) this.$('.name-in').value = `run-${version.baseWorkflowId}`;
    preview.textContent = `POST ${path} — body: the workflow's typed inputs; 202 → { runId }`;
    preview.hidden = false;
    this.updateAttachState();
  }


  async loadRegistry() {
    const seq = ++this._seq;
    try {
      const { sources } = await this._client.listSources({ limit: 200 });
      if (seq !== this._seq) return;
      this._registry = sources;
      const sel = this.$('.registry-in');
      sel.innerHTML = sources.length
        ? `<option value="">Choose a source…</option>` + sources.map((s) =>
            `<option value="${escapeHtml(s.name)}">${escapeHtml(s.name)}${s.type ? ` · ${escapeHtml(s.type)}` : ''}</option>`).join('')
        : `<option value="">No registered sources</option>`;

      // A natural default: name the attachment after the picked registry source when the name is empty.
      sel.addEventListener('change', () => {
        const nameIn = this.$('.name-in');
        if (!nameIn.value.trim() && sel.value) {
          nameIn.value = sel.value;
        }

        this.updateAttachState();
      });
    } catch (err) {
      if (seq !== this._seq) return;
      this.$('.registry-in').innerHTML = `<option value="">Could not load the registry</option>`;
    }
  }

  async fetchPreview() {
    this.clearError();
    const url = this.$('.url-in').value.trim();
    if (!url) {
      this.showError('Enter a document URL to fetch.');
      return;
    }

    const credentialSource = this.$('.cred-source-in').value.trim();
    const credentialEnvironment = this.$('.cred-env-in').value.trim();
    const preview = this.$('.fetch-preview');
    preview.hidden = false;
    preview.textContent = 'Fetching…';
    this._fetched = null;
    this.updateAttachState();
    const seq = ++this._seq;
    try {
      const fetched = await this._client.fetchSourceDocument({
        url,
        ...(credentialSource ? { credential: { sourceName: credentialSource, environment: credentialEnvironment } } : {}),
      });
      if (seq !== this._seq) return;
      this._fetched = fetched;
      preview.innerHTML = `
        <span><strong>${escapeHtml(fetched.type)}</strong>${fetched.version ? ` ${escapeHtml(fetched.version)}` : ''}${fetched.contentType ? ` · ${escapeHtml(fetched.contentType)}` : ''}</span>
        <span class="digest">sha256: ${escapeHtml(fetched.digest)}</span>`;
      const nameIn = this.$('.name-in');
      if (!nameIn.value.trim()) {
        // A natural default from the URL's file stem (e.g. …/payments.openapi.json → payments).
        const stem = url.split('/').pop()?.split('.')[0];
        if (stem) nameIn.value = stem;
      }

      this.updateAttachState();
    } catch (err) {
      if (seq !== this._seq) return;
      preview.hidden = true;
      this.showError(err.problem?.detail || err.problem?.title || err.message);
      this.updateAttachState();
    }
  }

  async readUpload() {
    this.clearError();
    const file = this.$('.file-in').files?.[0];
    const preview = this.$('.upload-preview');
    this._uploaded = null;
    if (!file) {
      preview.hidden = true;
      this.updateAttachState();
      return;
    }

    try {
      const document = JSON.parse(await file.text());
      // A JSON Schema document declares no openapi/asyncapi/arazzo marker — sniff its own shape ($schema, or
      // schema-ish keywords) and attach it as a jsonschema document (#94: referenced by schemas/<name>#<pointer>
      // from inputs schemas, never by sourceDescriptions).
      const looksLikeSchema = typeof document.$schema === 'string'
        || (document && typeof document === 'object' && !Array.isArray(document)
          && ('$defs' in document || 'properties' in document || 'type' in document));
      const type = document.openapi ? 'openapi' : document.asyncapi ? 'asyncapi' : document.arazzo ? 'arazzo'
        : looksLikeSchema ? 'jsonschema' : null;
      if (!type) {
        throw new Error('The document declares neither openapi, asyncapi, nor arazzo, and does not look like a JSON Schema.');
      }

      this._uploaded = { document, type };
      preview.hidden = false;
      preview.innerHTML = `<span><strong>${escapeHtml(type)}</strong> ${escapeHtml(document[type] ?? '')} · ${escapeHtml(file.name)}</span>`;
      const nameIn = this.$('.name-in');
      if (!nameIn.value.trim()) {
        const stem = file.name.split('.')[0];
        if (stem) nameIn.value = stem;
      }
    } catch (err) {
      preview.hidden = true;
      this.showError(`Not a usable JSON document: ${err.message}`);
    }

    this.updateAttachState();
  }

  // ---- GitHub import (§4.7) ---------------------------------------------------------------------

  renderGitHubRepos() {
    const session = this.$('.gh-connect').session;
    const repos = session?.connected ? (session.installations ?? []).flatMap((i) => i.repositories ?? []) : [];
    const label = this.$('.gh-repo-label');
    const browser = this.$('.gh-browser');
    label.hidden = repos.length === 0;
    if (repos.length === 0) {
      browser.hidden = true;
      this._github = { picked: null, path: '' };
      this._catalog = { picked: null };
      this.$('.gh-preview').hidden = true;
      this.updateAttachState();
      return;
    }

    const sel = this.$('.gh-repo-in');
    sel.innerHTML = `<option value="">Choose a repository…</option>` + repos.map((r) =>
      `<option value="${escapeHtml(`${r.owner}/${r.name}`)}">${escapeHtml(r.fullName)}</option>`).join('');
  }

  async browseGitHub(path) {
    const sel = this.$('.gh-repo-in');
    if (!sel.value) return;
    const [owner, repo] = [sel.value.slice(0, sel.value.indexOf('/')), sel.value.slice(sel.value.indexOf('/') + 1)];
    this.clearError();
    this._github = { owner, repo, path, picked: null };
    this.$('.gh-preview').hidden = true;
    this.updateAttachState();
    const seq = ++this._seq;
    try {
      const result = await this._client.browseRepo(owner, repo, { path: path || undefined });
      if (seq !== this._seq || this._mode !== 'github') return;
      const browser = this.$('.gh-browser');
      browser.hidden = false;
      this.$('.gh-path').textContent = `${owner}/${repo}/${path}`;
      this.$('.gh-up').hidden = !path;
      const list = this.$('.gh-list');
      const entries = (result.entries ?? []).slice().sort((a, b) => (a.type === b.type ? (a.name < b.name ? -1 : 1) : a.type === 'dir' ? -1 : 1));
      list.innerHTML = entries.length === 0
        ? `<button type="button" disabled>(empty)</button>`
        : entries.map((e) => {
            const importable = e.type === 'dir' || e.name.toLowerCase().endsWith('.json');
            return `<button type="button" data-type="${e.type}" data-path="${escapeHtml(e.path)}" data-name="${escapeHtml(e.name)}"${importable ? '' : ' disabled title="Only JSON imports directly; YAML goes through Fetch URL."'}>${e.type === 'dir' ? '📁' : '📄'} ${escapeHtml(e.name)}</button>`;
          }).join('');
      list.querySelectorAll('button[data-path]').forEach((entry) => entry.addEventListener('click', () =>
        entry.dataset.type === 'dir' ? this.browseGitHub(entry.dataset.path) : this.pickGitHubFile(entry.dataset.path, entry.dataset.name)));
    } catch (err) {
      if (seq !== this._seq) return;
      this.showError(err.problem?.detail || err.problem?.title || err.message);
    }
  }

  async pickGitHubFile(path, name) {
    this.clearError();
    const { owner, repo } = this._github;
    const preview = this.$('.gh-preview');
    preview.hidden = false;
    preview.textContent = 'Loading…';
    const seq = ++this._seq;
    try {
      const result = await this._client.browseRepo(owner, repo, { path });
      if (seq !== this._seq || this._mode !== 'github') return;
      const text = new TextDecoder().decode(Uint8Array.from(atob((result.file?.content ?? '').replace(/\s/g, '')), (c) => c.charCodeAt(0)));
      const document = JSON.parse(text);
      const type = document.openapi ? 'openapi' : document.asyncapi ? 'asyncapi' : document.arazzo ? 'arazzo' : null;
      if (!type) {
        throw new Error('The document declares neither openapi, asyncapi, nor arazzo.');
      }

      this._github.picked = { document, type, path };
      preview.innerHTML = `<span><strong>${escapeHtml(type)}</strong> ${escapeHtml(document[type] ?? '')} · ${escapeHtml(path)}</span>`;
      const nameIn = this.$('.name-in');
      if (!nameIn.value.trim()) {
        const stem = name.split('.')[0];
        if (stem) nameIn.value = stem;
      }
    } catch (err) {
      preview.hidden = true;
      this._github.picked = null;
      this.showError(`Not a usable JSON document: ${err.problem?.detail || err.message}`);
    }

    this.updateAttachState();
  }

  // ---- attach -----------------------------------------------------------------------------------

  async attach() {
    this.clearError();
    const name = this.$('.name-in').value.trim();
    if (this._target === 'register') {
      await this.register(name);
      return;
    }

    let attachment;
    if (this._mode === 'registry') {
      attachment = { sourceName: this.$('.registry-in').value };
    } else if (this._mode === 'fetch') {
      attachment = { document: this._fetched.document, type: this._fetched.type };
    } else if (this._mode === 'github') {
      attachment = { document: this._github.picked.document, type: this._github.picked.type };
    } else if (this._mode === 'catalog') {
      attachment = { document: this._catalog.picked.document, type: 'openapi' };
    } else {
      attachment = { document: this._uploaded.document, type: this._uploaded.type };
    }

    this.$('button.attach').disabled = true;
    try {
      const attached = await this._client.attachWorkingCopySource(this._workingCopyId, name, attachment);
      this.emit('source-attached', { attachment: attached });
      this.close();
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
      this.updateAttachState();
      this.emit('error', { problem: err.problem, error: err });
    }
  }

  /** Register the acquired document as a new source in the registry (the 'register' target). */
  async register(name) {
    let type;
    let document;
    if (this._mode === 'fetch') { type = this._fetched.type; document = this._fetched.document; }
    else if (this._mode === 'github') { type = this._github.picked.type; document = this._github.picked.document; }
    else { type = this._uploaded.type; document = this._uploaded.document; }

    this.$('button.attach').disabled = true;
    try {
      const managementTags = this.$('.reg-mgmt-editor')?.tags ?? [];
      const created = await this._client.createSource({
        name,
        type,
        document,
        displayName: this.$('.displayname-in')?.value.trim() || undefined,
        description: this.$('.description-in')?.value.trim() || undefined,
        managementTags: managementTags.length ? managementTags : undefined,
      });
      this.emit('source-registered', { source: created });
      this.close();
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
      this.updateAttachState();
      this.emit('error', { problem: err.problem, error: err });
    }
  }
}

define('arazzo-source-acquisition-dialog', ArazzoSourceAcquisitionDialog);
export { ArazzoSourceAcquisitionDialog };