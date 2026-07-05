// <arazzo-source-acquisition-dialog> — attach a source to a working copy (design §3.2/§5.3):
// pick a registered source · fetch from a URL (server-side, optionally credentialed) · upload JSON.
//
//   const dlg = document.createElement('arazzo-source-acquisition-dialog');
//   dlg.client = client;                      // an ArazzoControlPlaneClient
//   host.appendChild(dlg);
//   dlg.open({ workingCopyId: 'wc-…', suggestedName: 'payments' });
//
// Properties : .client
// Methods    : open({ workingCopyId, suggestedName? }), close()
// Events     : source-attached {attachment}   (the attachment carries the working copy's NEW etag —
//                                              hosts refresh their save token from it)
//              error {problem}
//
// The three acquisition modes end at the same seam: attachWorkingCopySource with a registry
// reference or an inline document. Fetch previews what the server detected (type/version/digest)
// before attaching; upload accepts a JSON document file (YAML endpoints go through Fetch, where the
// server parses YAML). GitHub import arrives with the §4.7 integration.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

class ArazzoSourceAcquisitionDialog extends ArazzoElement {
  constructor() {
    super();
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
    this._workingCopyId = workingCopyId;
    this._mode = 'registry';
    this._registry = [];
    this._fetched = null;
    this._uploaded = null;
    this.render();
    this.$('.name-in').value = suggestedName;
    this.$('dialog').showModal();
    this.loadRegistry();
  }

  close() {
    this.$('dialog')?.close();
  }

  // ---- rendering --------------------------------------------------------------------------------

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: 10px; background: var(--_bg); color: inherit; padding: 0; width: min(480px, 92vw); }
        dialog::backdrop { background: rgb(0 0 0 / 0.35); }
        .head { display: flex; justify-content: space-between; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--_border); }
        .head h2 { margin: 0; font-size: 14px; }
        .body { padding: 12px 14px; display: grid; gap: 10px; }
        label { display: grid; gap: 4px; font-size: 12px; color: var(--_muted); }
        input, select { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: var(--arazzo-text, inherit); }
        .tabs { display: flex; gap: 4px; }
        .tabs button { font-size: 12px; padding: 4px 12px; opacity: 0.7; }
        .tabs button.active { opacity: 1; border-color: var(--_accent); font-weight: 600; }
        .mode[hidden] { display: none; }
        .mode { display: grid; gap: 10px; }
        .cred { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
        .preview { font-size: 12px; border: 1px solid var(--_border); border-radius: 6px; padding: 8px 10px; display: grid; gap: 2px; }
        .preview .digest { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 10px; overflow-wrap: anywhere; color: var(--_muted); }
        .foot { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 14px; border-top: 1px solid var(--_border); }
        .error-banner[hidden] { display: none; }
      </style>
      <dialog part="dialog">
        <div class="head"><h2>Attach a source</h2><button class="x" type="button" title="Close">✕</button></div>
        <div class="body">
          <div class="error-banner" hidden></div>
          <label>Attach as (the <code>sourceDescriptions</code> name)
            <input class="name-in" type="text" placeholder="e.g. payments" autocomplete="off">
          </label>
          <div class="tabs" role="tablist">
            <button type="button" data-mode="registry" class="active">Registry</button>
            <button type="button" data-mode="fetch">Fetch URL</button>
            <button type="button" data-mode="upload">Upload</button>
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
        </div>
        <div class="foot">
          <button class="cancel" type="button">Cancel</button>
          <button class="attach" type="button" disabled>Attach</button>
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
  }

  switchMode(mode) {
    this._mode = mode;
    this.$$('.tabs button').forEach((tab) => tab.classList.toggle('active', tab.dataset.mode === mode));
    this.$('.mode-registry').hidden = mode !== 'registry';
    this.$('.mode-fetch').hidden = mode !== 'fetch';
    this.$('.mode-upload').hidden = mode !== 'upload';
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
      || (this._mode === 'upload' && this._uploaded));
    this.$('button.attach').disabled = !ready;
  }

  // ---- modes ------------------------------------------------------------------------------------

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
      const type = document.openapi ? 'openapi' : document.asyncapi ? 'asyncapi' : document.arazzo ? 'arazzo' : null;
      if (!type) {
        throw new Error('The document declares neither openapi, asyncapi, nor arazzo.');
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

  // ---- attach -----------------------------------------------------------------------------------

  async attach() {
    this.clearError();
    const name = this.$('.name-in').value.trim();
    let attachment;
    if (this._mode === 'registry') {
      attachment = { sourceName: this.$('.registry-in').value };
    } else if (this._mode === 'fetch') {
      attachment = { document: this._fetched.document, type: this._fetched.type };
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
}

define('arazzo-source-acquisition-dialog', ArazzoSourceAcquisitionDialog);
export { ArazzoSourceAcquisitionDialog };