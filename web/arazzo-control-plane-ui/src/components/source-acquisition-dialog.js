// <arazzo-source-acquisition-dialog> — attach a source to a working copy (design §3.2/§5.3):
// pick a registered source · fetch from a URL (server-side; authenticated as YOU via a connected
// provider, a one-shot secret, or a workload binding — ADR 0052) · upload JSON.
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
//
// Fetch authentication (ADR 0052) — the pane's order states the identity model: when the pasted
// URL's host is covered by a connected provider, the fetch runs AS the signed-in user (a Connect
// affordance appears when not yet connected); otherwise a one-shot secret (this fetch only, never
// stored) or a workload credential binding (the filterable §13 picker) applies; otherwise the
// fetch is anonymous. The effective mode is always named under the pickers.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './github-connect.js';
import './provider-connect.js';
import './filter-input.js';
import './git-tree.js';
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
    /** @private — races the branch-list load against a newer repo choice. */ this._branchSeq = 0;
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
    this._pasted = null;
    this._credsLoaded = false; // render() rebuilds the picker — reload the bindings on demand
    this._providersLoaded = false;
    this._providers = [];
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
    this._pasted = null;
    this._credsLoaded = false; // render() rebuilds the picker — reload the bindings on demand
    this._providersLoaded = false;
    this._providers = [];
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
        input, select, textarea, arazzo-filter-input { width: 100%; min-width: 0; box-sizing: border-box; }
        label { display: grid; gap: 4px; font-size: 12px; color: var(--_muted); }
        input, select { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: var(--arazzo-text, inherit); }
        .tabs { display: flex; gap: 4px; }
        .tabs button { font-size: 12px; padding: 4px 12px; opacity: 0.7; }
        .tabs button.active { opacity: 1; border-color: var(--_accent); font-weight: 600; }
        .mode[hidden] { display: none; }
        .mode { display: grid; gap: 10px; }
        .cred { display: grid; gap: 4px; }
        .cred .hint { font-size: 11px; color: var(--_muted); }
        .session-note { font-size: 11px; color: var(--_muted); border: 1px dashed var(--_border); border-radius: 6px; padding: 6px 8px; }
        .link { font: inherit; font-size: 11px; padding: 0; background: none; border: none; color: var(--_accent); text-decoration: underline; cursor: pointer; }
        textarea { font: inherit; font-size: 12px; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: var(--arazzo-text, inherit); resize: vertical; }
        .preview { font-size: 12px; border: 1px solid var(--_border); border-radius: 6px; padding: 8px 10px; display: grid; gap: 2px; }
        .crumb { display: flex; gap: 6px; align-items: center; font-size: 11px; color: var(--_muted); }
        .crumb button { font-size: 11px; padding: 0 8px; }
        .gh-tree { display: block; }
        .gh-repo-label[hidden], .gh-branch-label[hidden], .gh-browser[hidden] { display: none; }
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
            <button type="button" data-mode="paste">Paste</button>
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
            <div class="session-note">
              Behind a login you use in your browser? The server can't reuse your browser session, so
              <button type="button" class="to-paste link">paste</button> or
              <button type="button" class="to-upload link">upload</button> the document you can already open.
            </div>
            <div class="cred">
              <div class="provider-line" hidden>
                <arazzo-provider-connect class="provider-connect"></arazzo-provider-connect>
              </div>
              <label>Credential <span class="muted">(only if the endpoint takes one in a header — a public URL needs none)</span>
                <select class="secret-scheme">
                  <option value="bearer">Bearer token / PAT</option>
                  <option value="apiKey">API key (named header)</option>
                  <option value="basic">Basic (username + password)</option>
                </select>
              </label>
              <label class="secret-header-label" hidden>Header name
                <input class="secret-header" type="text" placeholder="X-API-Key" autocomplete="off">
              </label>
              <label class="secret-username-label" hidden>Username
                <input class="secret-username" type="text" autocomplete="off">
              </label>
              <label>Secret value <span class="muted">(this fetch only, never stored)</span>
                <input class="secret-in" type="password" placeholder="token / key / password" autocomplete="off">
              </label>
              <label>…or a saved credential binding <span class="muted">(optional)</span>
                <arazzo-filter-input class="cred-binding-in" aria-label="Saved credential binding" placeholder="type to filter (source · environment)"></arazzo-filter-input>
              </label>
              <span class="hint auth-hint">Fetches anonymously.</span>
            </div>
            <div><button class="fetch" type="button">Fetch &amp; preview</button></div>
            <div class="preview fetch-preview" hidden></div>
          </div>
          <div class="mode mode-paste" hidden>
            <label>Document text — paste the OpenAPI/AsyncAPI/Arazzo JSON you opened in your browser
              <textarea class="paste-in" rows="10" spellcheck="false" placeholder='{ "openapi": "3.1.0", … }'></textarea>
            </label>
            <span class="hint">The truest answer for a document behind your own browser login. YAML: save it and Upload, or convert to JSON.</span>
            <div class="preview paste-preview" hidden></div>
          </div>
          <div class="mode mode-upload" hidden>
            <label>Document file (JSON — for YAML, convert first or paste as JSON)
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
            <label class="gh-repo-label" hidden>Repository
              <arazzo-filter-input class="gh-repo-in" aria-label="Repository" placeholder="type to filter, or any owner/repo"></arazzo-filter-input>
            </label>
            <label class="gh-branch-label" hidden>Branch
              <arazzo-filter-input class="gh-branch-in" aria-label="Branch" placeholder="type to filter branches"></arazzo-filter-input>
            </label>
            <div class="gh-browser" hidden>
              <arazzo-git-tree class="gh-tree"></arazzo-git-tree>
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
    this.$('.url-in').addEventListener('input', () => this.updateFetchAuthUi());
    this.$('.secret-in').addEventListener('input', () => this.updateAuthHint());
    this.$('.secret-scheme').addEventListener('change', () => this.updateSecretScheme());
    this.$('.secret-header').addEventListener('input', () => this.updateAuthHint());
    this.$('.secret-username').addEventListener('input', () => this.updateAuthHint());
    this.$('.cred-binding-in').addEventListener('change', () => this.updateAuthHint());
    this.$('.provider-connect').addEventListener('provider-connected', () => this.refreshProviders());
    this.$('.provider-connect').addEventListener('provider-disconnected', () => this.refreshProviders());
    this.$('.to-paste').addEventListener('click', () => this.switchMode('paste'));
    this.$('.to-upload').addEventListener('click', () => this.switchMode('upload'));
    this.$('.paste-in').addEventListener('input', () => this.readPaste());
    this.$('.file-in').addEventListener('change', () => this.readUpload());
    this.$('.gh-connect').addEventListener('github-connected', () => this.renderGitHubRepos());
    this.$('.gh-connect').addEventListener('github-disconnected', () => this.renderGitHubRepos());
    this.$('.gh-repo-in').addEventListener('change', () => this.onRepoChosen());
    this.$('.gh-branch-in').addEventListener('change', () => this.browseGitHub(''));
  }

  switchMode(mode) {
    this._mode = mode;
    this.$$('.tabs button').forEach((tab) => tab.classList.toggle('active', tab.dataset.mode === mode));
    this.$('.mode-registry').hidden = mode !== 'registry';
    this.$('.mode-fetch').hidden = mode !== 'fetch';
    this.$('.mode-paste').hidden = mode !== 'paste';
    this.$('.mode-upload').hidden = mode !== 'upload';
    this.$('.mode-github').hidden = mode !== 'github';
    this.$('.mode-catalog').hidden = mode !== 'catalog';
    if (mode === 'github') this.renderGitHubRepos();
    if (mode === 'catalog') this.loadCatalog();
    if (mode === 'fetch') { this.loadCredentialBindings(); this.loadProviders(); }
    if (mode === 'paste') this.$('.paste-in').focus();
    this.updateAttachState();
  }

  /** @private — populates the workload-binding picker (the kit's filter combo — a deployment can
   *  hold very many bindings) with the registered credential bindings (source · environment pairs,
   *  non-secret metadata only). Loaded once per open; a deployment without the credentials surface
   *  just leaves the provider/secret options standing alone. */
  async loadCredentialBindings() {
    if (this._credsLoaded) return;
    this._credsLoaded = true;
    const picker = this.$('.cred-binding-in');
    this._bindingByLabel = new Map();
    try {
      const { credentials } = await this._client.listCredentials({ limit: 200 });
      picker.items = credentials.map((c) => {
        const label = `${c.sourceName} · ${c.environment}`;
        this._bindingByLabel.set(label, { sourceName: c.sourceName, environment: c.environment });
        return { value: label, sub: c.authKind ?? '' };
      });
    } catch {
      // No credentials surface (or no reach): the provider/secret options stand alone.
    }
  }

  /** @private — the connected-provider registry (ADR 0052), loaded once per open; the pasted URL's
   *  host resolves against each provider's hosts patterns to offer Connect / fetch-as-you. */
  async loadProviders() {
    if (this._providersLoaded) {
      this.updateFetchAuthUi();
      return;
    }

    this._providersLoaded = true;
    try {
      const { providers } = await this._client.listProviders();
      this._providers = providers ?? [];
    } catch {
      this._providers = []; // no providers surface (or no reach): the fallbacks stand alone
    }

    this.updateFetchAuthUi();
  }

  /** @private — re-reads the registry's connection state (after a connect/disconnect). */
  async refreshProviders() {
    try {
      this._providers = (await this._client.listProviders()).providers ?? [];
    } catch {
      // Keep the last known state; the next open re-reads.
    }

    this.updateFetchAuthUi();
  }

  /** @private — the provider (a listProviders entry) covering the URL's host, or null. Mirrors the
   *  server's gate (exact or `*.suffix`, case-insensitive) so the pane only OFFERS what the server
   *  would accept. */
  matchProvider(url) {
    if (!this._providers?.length || !url) return null;
    let host;
    try { host = new URL(url).hostname.toLowerCase(); } catch { return null; }
    const matches = (pattern) => (pattern.startsWith('*.')
      ? host.length > pattern.length - 1 && host.endsWith(pattern.slice(1).toLowerCase())
      : host === pattern.toLowerCase());
    return this._providers.find((p) => (p.hosts ?? []).some(matches)) ?? null;
  }

  /** @private — the fetch's effective authentication, in the pane's identity order (ADR 0052):
   *  a covered + connected provider fetches as the user; else a typed one-shot secret; else a
   *  picked workload binding; else anonymous. */
  effectiveAuth() {
    const provider = this.matchProvider(this.$('.url-in').value.trim());
    if (provider?.connected) {
      return { auth: { provider: provider.name }, describe: `Fetches as you via ${provider.displayName || provider.name}.` };
    }

    const value = this.$('.secret-in').value;
    if (value) {
      const scheme = this.$('.secret-scheme').value || 'bearer';
      const secret = { scheme, value };
      if (scheme === 'apiKey') secret.header = this.$('.secret-header').value.trim();
      if (scheme === 'basic') secret.username = this.$('.secret-username').value;
      return { auth: { secret }, describe: `Fetches with the one-shot ${scheme} credential (this fetch only, never stored).` };
    }

    const label = this.$('.cred-binding-in').value;
    const binding = this._bindingByLabel?.get(label);
    if (binding) {
      return { auth: { binding }, describe: `Fetches with the workload binding ${label}.` };
    }

    return {
      auth: null,
      describe: provider
        ? `Fetches anonymously — connect ${provider.displayName || provider.name} to fetch as yourself.`
        : 'Fetches anonymously.',
    };
  }

  /** @private — shows/hides the provider line for the current URL and refreshes the mode hint. */
  updateFetchAuthUi() {
    const line = this.$('.provider-line');
    if (!line) return;
    const provider = this.matchProvider(this.$('.url-in').value.trim());
    line.hidden = !provider;
    if (provider) {
      const connect = this.$('.provider-connect');
      connect.client = this._client;
      if (this.windowOpener) connect.windowOpener = this.windowOpener;
      connect.provider = provider;
    }

    this.updateAuthHint();
  }

  /** @private — shows the header/username fields for the chosen one-shot scheme. */
  updateSecretScheme() {
    const scheme = this.$('.secret-scheme').value;
    this.$('.secret-header-label').hidden = scheme !== 'apiKey';
    this.$('.secret-username-label').hidden = scheme !== 'basic';
    this.updateAuthHint();
  }

  /** @private */
  updateAuthHint() {
    const hint = this.$('.auth-hint');
    if (hint) hint.textContent = this.effectiveAuth().describe;
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
      || (this._mode === 'paste' && this._pasted)
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

    const { auth } = this.effectiveAuth();
    const preview = this.$('.fetch-preview');
    preview.hidden = false;
    preview.textContent = 'Fetching…';
    this._fetched = null;
    this.updateAttachState();
    const seq = ++this._seq;
    try {
      const fetched = await this._client.fetchSourceDocument({
        url,
        ...(auth ? { auth } : {}),
      });
      if (seq !== this._seq) return;
      this._fetched = fetched;

      // The one-shot secret authenticated its single fetch; it is not kept for another.
      if (auth?.secret) {
        this.$('.secret-in').value = '';
        this.updateAuthHint();
      }

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
      const type = ArazzoSourceAcquisitionDialog.classifyDocument(document);
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

  /** Paste mode (ADR 0052 tier 2): the author pastes the document they opened in their own
   *  authenticated browser session. Parsed client-side like upload — the bytes never round-trip a
   *  credential. Empty clears; a parse failure surfaces without disabling the pane. */
  readPaste() {
    this.clearError();
    const text = this.$('.paste-in').value.trim();
    const preview = this.$('.paste-preview');
    this._pasted = null;
    if (!text) {
      preview.hidden = true;
      this.updateAttachState();
      return;
    }

    try {
      const document = JSON.parse(text);
      const type = ArazzoSourceAcquisitionDialog.classifyDocument(document);
      this._pasted = { document, type };
      preview.hidden = false;
      preview.innerHTML = `<span><strong>${escapeHtml(type)}</strong> ${escapeHtml(document[type] ?? '')}</span>`;
    } catch (err) {
      preview.hidden = true;
      this.showError(`Not a usable JSON document: ${err.message}`);
    }

    this.updateAttachState();
  }

  /** The document type from its own marker, or `jsonschema` when it sniffs as a JSON Schema
   *  (#94: referenced by schemas/<name>#<pointer> from inputs schemas, never by sourceDescriptions).
   *  Throws when it is none. Shared by upload and paste. */
  static classifyDocument(document) {
    const looksLikeSchema = typeof document.$schema === 'string'
      || (document && typeof document === 'object' && !Array.isArray(document)
        && ('$defs' in document || 'properties' in document || 'type' in document));
    const type = document.openapi ? 'openapi' : document.asyncapi ? 'asyncapi' : document.arazzo ? 'arazzo'
      : looksLikeSchema ? 'jsonschema' : null;
    if (!type) {
      throw new Error('The document declares neither openapi, asyncapi, nor arazzo, and does not look like a JSON Schema.');
    }

    return type;
  }

  // ---- GitHub import (§4.7) ---------------------------------------------------------------------

  renderGitHubRepos() {
    const session = this.$('.gh-connect').session;
    const repos = session?.connected ? (session.repositories ?? []) : [];
    const label = this.$('.gh-repo-label');
    // The repo list is (re)built here, which resets the selection — so collapse the branch picker
    // and browser until a repo is chosen (again).
    this.$('.gh-branch-label').hidden = true;
    this.$('.gh-browser').hidden = true;
    this.$('.gh-preview').hidden = true;
    this._github = { picked: null, path: '' };
    label.hidden = repos.length === 0;
    if (repos.length === 0) {
      this._catalog = { picked: null };
      this.updateAttachState();
      return;
    }

    // The kit's filter combo (the shared picker design): the session's seed narrows as you type, an
    // owner-qualified query deepens through the server-side typeahead (reaching public repos the seed
    // never contains), and any visible owner/repo stays free-typable (the OAuth model).
    const repoItem = (r) => ({ value: r.fullName || `${r.owner}/${r.name}`, sub: [r.defaultBranch, r.private ? 'private' : ''].filter(Boolean).join(' · ') });
    const repoInput = this.$('.gh-repo-in');
    repoInput.items = repos.map(repoItem);
    repoInput.lookup = (q) => (q.includes('/') && this._client
      ? this._client.searchRepositories(q).then((r) => (r.repositories ?? []).map(repoItem)).catch(() => [])
      : []);
    repoInput.value = '';
    this.updateAttachState();
  }

  /** A repo choice: load its branches, then browse the chosen (default) branch from the root. */
  async onRepoChosen() {
    const sel = this.$('.gh-repo-in');
    this.$('.gh-preview').hidden = true;
    this._github = { picked: null, path: '' };
    this.updateAttachState();
    if (!sel.value) {
      this.$('.gh-branch-label').hidden = true;
      this.$('.gh-browser').hidden = true;
      return;
    }
    const [owner, repo] = [sel.value.slice(0, sel.value.indexOf('/')), sel.value.slice(sel.value.indexOf('/') + 1)];
    await this.loadGitHubBranches(owner, repo);
  }

  /** Populate the branch picker from the repo's real branches (default branch pre-selected), then
   *  browse it — so a file can be imported from a NON-default branch, not only the repo default. */
  async loadGitHubBranches(owner, repo) {
    const branchSel = this.$('.gh-branch-in');
    const branchLabel = this.$('.gh-branch-label');
    branchLabel.hidden = false;
    branchSel.disabled = true;
    branchSel.items = [];
    branchSel.value = '';
    this.$('.gh-browser').hidden = true;
    const seq = ++this._branchSeq;
    try {
      const list = await this._client.listRepoBranches(owner, repo);
      if (seq !== this._branchSeq || this._mode !== 'github') return; // a newer repo choice superseded this
      const names = (list.branches ?? []).map((b) => b.name);
      const picked = list.defaultBranch ?? names[0] ?? '';
      branchSel.items = names.map((name) => ({ value: name, sub: name === list.defaultBranch ? 'default' : '' }));
      branchSel.value = picked;
      branchSel.disabled = names.length === 0;
      this.browseGitHub();
    } catch (err) {
      if (seq !== this._branchSeq) return;
      branchSel.items = [];
      branchSel.value = '';
      branchSel.disabled = true;
      this.showError(err.problem?.detail || err.problem?.title || err.message);
    }
  }

  /** Mount the shared lazy tree (the same component the Git pane browses with) over the chosen
   *  repo+branch: directories load as they expand, JSON files pick into the preview. */
  browseGitHub() {
    const sel = this.$('.gh-repo-in');
    if (!sel.value) return;
    const [owner, repo] = [sel.value.slice(0, sel.value.indexOf('/')), sel.value.slice(sel.value.indexOf('/') + 1)];
    const branch = this.$('.gh-branch-in').value || undefined;
    this.clearError();
    this._github = { owner, repo, branch, picked: null };
    this.$('.gh-preview').hidden = true;
    this.updateAttachState();
    const browser = this.$('.gh-browser');
    browser.hidden = false;
    const tree = this.$('.gh-tree');
    tree.mode = 'file';
    tree.pickableFile = (entry) => entry.name.toLowerCase().endsWith('.json');
    tree.loader = async (path) => {
      const node = await this._client.browseRepo(owner, repo, { path: path || undefined, ref: branch });
      return node.kind === 'dir' ? node.entries : [];
    };
    if (!tree._picking) {
      tree._picking = true;
      tree.addEventListener('picked', (e) => this.pickGitHubFile(e.detail.path, e.detail.entry?.name ?? e.detail.path.split('/').pop()));
      tree.addEventListener('error', (e) => this.showError(e.detail.error?.problem?.detail || e.detail.error?.message || 'The repository could not be browsed.'));
    }
    tree.reload();
  }

  async pickGitHubFile(path, name) {
    this.clearError();
    const { owner, repo, branch } = this._github;
    const preview = this.$('.gh-preview');
    preview.hidden = false;
    preview.textContent = 'Loading…';
    const seq = ++this._seq;
    try {
      const result = await this._client.browseRepo(owner, repo, { path, ref: branch });
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
    } else if (this._mode === 'paste') {
      attachment = { document: this._pasted.document, type: this._pasted.type };
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
    else if (this._mode === 'paste') { type = this._pasted.type; document = this._pasted.document; }
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