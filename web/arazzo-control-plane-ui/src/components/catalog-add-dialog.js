// <arazzo-catalog-add-dialog> — a modal for adding a new catalog version.
//
//   const dlg = document.querySelector('arazzo-catalog-add-dialog');
//   dlg.client = client;
//   dlg.open();
//
// Properties : .client
// Methods    : open(), close()
// Events     : version-added {version}, error {problem}
//
// Two ways to supply the package: BUILD it in-browser from a workflow document + its source documents (the
// catalog assigns the version number — submit the bare base workflow id, NOT a `-vN`), or UPLOAD a pre-built
// package archive (e.g. from the `arazzo catalog pack` CLI). Owner is the governance contact.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import { packWorkflowPackage } from '../workflow-package.js';

class ArazzoCatalogAddDialog extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._sourceRows = 0;
  }

  connectedCallback() {
    if (!this._built) this.render();
  }

  /** Open the dialog (reset to a blank form). */
  open() {
    if (!this._built) this.render();
    this.$('form').reset();
    this.$('.error-banner').hidden = true;
    this.setMode('build');
    this.$('.sources').replaceChildren();
    this._sourceRows = 0;
    this.addSourceRow();
    this.$('dialog').showModal();
  }

  close() {
    this.$('dialog')?.close();
  }

  setMode(mode) {
    this.$$('input[name="mode"]').forEach((r) => { r.checked = r.value === mode; });
    this.$$('.mode-fields').forEach((el) => { el.hidden = el.dataset.mode !== mode; });
  }

  get mode() {
    return this.$('input[name="mode"]:checked')?.value || 'build';
  }

  render() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog {
          border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg);
          color: var(--_text); padding: 0; width: min(620px, 94vw);
        }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .head { padding: 14px 16px; border-bottom: 1px solid var(--_border); }
        .head .title { font-weight: 700; font-size: 15px; }
        .subhead { color: var(--_muted); font-size: 12px; margin-top: 2px; }
        .content { padding: 16px; display: grid; gap: 14px; max-height: 64vh; overflow: auto; }
        fieldset { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 12px; margin: 0; display: grid; gap: 10px; }
        legend { font-size: 12px; font-weight: 600; color: var(--_muted); padding: 0 4px; }
        .modes { display: flex; gap: 8px; flex-wrap: wrap; }
        .mode { display: inline-flex; gap: 6px; align-items: center; border: 1px solid var(--_border); border-radius: var(--_radius); padding: 6px 10px; cursor: pointer; font-size: 13px; }
        .mode:has(input:checked) { border-color: var(--_accent); background: color-mix(in srgb, var(--_accent) 8%, transparent); }
        label { font-size: 12px; color: var(--_muted); display: block; margin-bottom: 4px; }
        .row { display: grid; gap: 10px; }
        .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
        input[type="text"], input[type="email"], input[type="url"] {
          width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background: var(--_bg); color: var(--_text);
        }
        input[type="file"] { font: inherit; font-size: 12px; }
        .source-row { display: grid; grid-template-columns: 1fr 1.4fr auto; gap: 8px; align-items: center; }
        .source-row .rm { padding: 4px 8px; }
        .hint { font-size: 11px; color: var(--_muted); }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <dialog part="dialog">
        <form method="dialog">
          <div class="head">
            <div class="title">Add catalog version</div>
            <div class="subhead">The catalog assigns the version number — submit the bare workflow id (no <code>-vN</code>).</div>
          </div>
          <div class="content">
            <div class="modes" part="modes">
              <label class="mode"><input type="radio" name="mode" value="build" checked> Build from documents</label>
              <label class="mode"><input type="radio" name="mode" value="upload"> Upload package (.zip)</label>
            </div>

            <fieldset class="mode-fields" data-mode="build">
              <legend>Documents</legend>
              <div><label for="workflowFile">Arazzo workflow document (.json)</label><input id="workflowFile" type="file" accept=".json,application/json"></div>
              <div>
                <label>Source documents (name → file)</label>
                <div class="sources"></div>
                <button type="button" class="add-source ghost" style="margin-top:8px">+ Add source</button>
                <div class="hint">Each name must match a <code>sourceDescriptions[].name</code> in the workflow.</div>
              </div>
            </fieldset>

            <fieldset class="mode-fields" data-mode="upload" hidden>
              <legend>Package</legend>
              <div><label for="packageFile">Package archive (.zip from <code>arazzo catalog pack</code>)</label><input id="packageFile" type="file" accept=".zip,application/zip,application/octet-stream"></div>
            </fieldset>

            <fieldset>
              <legend>Owner (governance)</legend>
              <div class="grid2">
                <div><label for="ownerName">Name *</label><input id="ownerName" type="text" placeholder="Reconciliation Team"></div>
                <div><label for="ownerEmail">Email *</label><input id="ownerEmail" type="email" placeholder="team@example.com"></div>
                <div><label for="ownerTeam">Team</label><input id="ownerTeam" type="text" placeholder="Platform"></div>
                <div><label for="ownerUrl">URL</label><input id="ownerUrl" type="url" placeholder="https://runbooks.example.com/…"></div>
              </div>
            </fieldset>

            <div><label for="tags">Tags (space or comma separated)</label><input id="tags" type="text" placeholder="prod billing"></div>

            <div class="error-banner" hidden></div>
          </div>
          <div class="foot">
            <button value="dismiss" class="ghost" type="submit">Cancel</button>
            <button value="confirm" class="primary confirm" type="submit">Add version</button>
          </div>
        </form>
      </dialog>
    `;

    this.$$('input[name="mode"]').forEach((r) => r.addEventListener('change', () => this.setMode(r.value)));
    this.$('.add-source').addEventListener('click', () => this.addSourceRow());
    this.$('#workflowFile').addEventListener('change', () => this.suggestSourcesFromWorkflow());
    this.$('form').addEventListener('submit', (e) => {
      if (e.submitter?.value === 'confirm') {
        e.preventDefault();
        this.submit();
      }
    });
  }

  addSourceRow(name = '') {
    const row = document.createElement('div');
    row.className = 'source-row';
    row.innerHTML = `
      <input type="text" class="src-name" placeholder="name" value="${escapeHtml(name)}">
      <input type="file" class="src-file" accept=".json,application/json">
      <button type="button" class="rm ghost danger" title="Remove">✕</button>`;
    row.querySelector('.rm').addEventListener('click', () => row.remove());
    this.$('.sources').appendChild(row);
    this._sourceRows++;
  }

  /** When a workflow file is picked, pre-add a row for each declared sourceDescriptions name not already present. */
  async suggestSourcesFromWorkflow() {
    const file = this.$('#workflowFile').files?.[0];
    if (!file) return;
    let doc;
    try {
      doc = JSON.parse(await file.text());
    } catch {
      return; // invalid JSON is reported on submit
    }
    const declared = Array.isArray(doc?.sourceDescriptions) ? doc.sourceDescriptions.map((s) => s?.name).filter(Boolean) : [];
    const present = new Set(this.$$('.src-name').map((i) => i.value.trim()).filter(Boolean));
    const empties = this.$$('.source-row').filter((r) => !r.querySelector('.src-name').value.trim());
    for (const name of declared) {
      if (present.has(name)) continue;
      const empty = empties.shift();
      if (empty) empty.querySelector('.src-name').value = name;
      else this.addSourceRow(name);
      present.add(name);
    }
  }

  /** Build the multipart request (package blob + owner + tags), or throw a friendly Error for invalid input. */
  async buildRequest() {
    const owner = {
      name: this.$('#ownerName').value.trim(),
      email: this.$('#ownerEmail').value.trim(),
    };
    if (!owner.name || !owner.email) throw new Error('Owner name and email are required.');
    const team = this.$('#ownerTeam').value.trim();
    const url = this.$('#ownerUrl').value.trim();
    if (team) owner.team = team;
    if (url) owner.url = url;

    const tags = this.$('#tags').value.split(/[,\s]+/).map((t) => t.trim()).filter(Boolean);

    let pkg;
    if (this.mode === 'upload') {
      const file = this.$('#packageFile').files?.[0];
      if (!file) throw new Error('Choose a package archive (.zip) to upload.');
      pkg = file;
    } else {
      const workflowFile = this.$('#workflowFile').files?.[0];
      if (!workflowFile) throw new Error('Choose the Arazzo workflow document.');
      const workflowText = await workflowFile.text();
      try {
        JSON.parse(workflowText);
      } catch {
        throw new Error('The workflow document is not valid JSON.');
      }
      const sources = [];
      for (const row of this.$$('.source-row')) {
        const name = row.querySelector('.src-name').value.trim();
        const file = row.querySelector('.src-file').files?.[0];
        if (!name && !file) continue;
        if (!name) throw new Error('Every source document needs a name.');
        if (!file) throw new Error(`Choose a file for source "${name}".`);
        const content = await file.text();
        try {
          JSON.parse(content);
        } catch {
          throw new Error(`Source "${name}" is not valid JSON.`);
        }
        sources.push({ name, content });
      }
      pkg = packWorkflowPackage(workflowText, sources);
    }

    return { package: pkg, owner, tags };
  }

  async submit() {
    const banner = this.$('.error-banner');
    banner.hidden = true;
    let request;
    try {
      request = await this.buildRequest();
    } catch (err) {
      banner.textContent = err.message;
      banner.hidden = false;
      return;
    }

    const confirmBtn = this.$('.confirm');
    confirmBtn.disabled = true;
    try {
      const version = await this.client.addCatalogVersion(request);
      this.close();
      this.emit('version-added', { version });
    } catch (err) {
      const problem = err.problem || { title: err.message };
      banner.textContent = `${problem.title || 'Add failed'}${problem.detail ? ' — ' + problem.detail : ''}`;
      banner.hidden = false;
      this.emit('error', { problem, error: err });
    } finally {
      confirmBtn.disabled = false;
    }
  }
}

define('arazzo-catalog-add-dialog', ArazzoCatalogAddDialog);
export { ArazzoCatalogAddDialog };
