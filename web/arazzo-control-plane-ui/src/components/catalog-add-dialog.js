// <arazzo-catalog-add-dialog> — a modal for adding a workflow to the catalog.
//
//   const dlg = document.querySelector('arazzo-catalog-add-dialog');
//   dlg.client = client;
//   dlg.open();
//
// Properties : .client
// Methods    : open(), close()
// Events     : workflow-added {version}, error {problem}
//
// You add a *workflow* (an Arazzo workflow document + the source documents it references); the catalog
// assigns the version (a new workflow id starts at v1, an existing one gets the next version — submit the
// bare workflow id, NOT a `-vN`). Two ways to supply it: BUILD it in-browser from the workflow + its
// sources (the dialog reads the workflow's `sourceDescriptions` and requires a document for each), or
// UPLOAD a pre-built package archive (e.g. from the `arazzo catalog pack` CLI).

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import { packWorkflowPackage } from '../workflow-package.js';

const SOURCES_HINT = 'Choose the workflow document above to see the source documents it requires.';

class ArazzoCatalogAddDialog extends ArazzoElement {
  connectedCallback() {
    if (!this._built) this.render();
  }

  /** Open the dialog (reset to a blank form). */
  open() {
    if (!this._built) this.render();
    this.$('form').reset();
    this.$('.error-banner').hidden = true;
    this.setMode('build');
    this.$('.sources').innerHTML = `<div class="hint">${SOURCES_HINT}</div>`;
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
        .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
        input[type="text"], input[type="email"], input[type="url"] {
          width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background: var(--_bg); color: var(--_text);
        }
        input[type="file"] { font: inherit; font-size: 12px; }
        .sources { display: grid; gap: 8px; }
        .source-row { display: grid; grid-template-columns: 1fr 1.3fr; gap: 10px; align-items: center; border: 1px solid var(--_border); border-radius: var(--_radius); padding: 8px 10px; }
        .src-meta { display: grid; gap: 1px; }
        .src-label { font-weight: 600; font-size: 13px; color: var(--_text); }
        .src-type { font-size: 11px; color: var(--_muted); text-transform: uppercase; letter-spacing: 0.03em; }
        .hint { font-size: 11px; color: var(--_muted); }
        .hint.err { color: var(--_danger); }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <dialog part="dialog">
        <form method="dialog">
          <div class="head">
            <div class="title">Add workflow</div>
            <div class="subhead">The catalog assigns the version — a new workflow id starts at v1, an existing one gets the next version. Submit the bare id (no <code>-vN</code>).</div>
          </div>
          <div class="content">
            <div class="modes" part="modes">
              <label class="mode"><input type="radio" name="mode" value="build" checked> Build from documents</label>
              <label class="mode"><input type="radio" name="mode" value="upload"> Upload package (.zip)</label>
            </div>

            <fieldset class="mode-fields" data-mode="build">
              <legend>Workflow &amp; sources</legend>
              <div><label for="workflowFile">Arazzo workflow document (.json)</label><input id="workflowFile" type="file" accept=".json,application/json"></div>
              <div>
                <label>Source documents (required — one per <code>sourceDescriptions</code> entry)</label>
                <div class="sources"><div class="hint">${SOURCES_HINT}</div></div>
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
            <button value="confirm" class="primary confirm" type="submit">Add workflow</button>
          </div>
        </form>
      </dialog>
    `;

    this.$$('input[name="mode"]').forEach((r) => r.addEventListener('change', () => this.setMode(r.value)));
    this.$('#workflowFile').addEventListener('change', () => this.deriveSources());
    this.$('form').addEventListener('submit', (e) => {
      if (e.submitter?.value === 'confirm') {
        e.preventDefault();
        this.submit();
      }
    });
  }

  /** Read the chosen workflow and render a required file input for each `sourceDescriptions` entry. */
  async deriveSources() {
    const area = this.$('.sources');
    const file = this.$('#workflowFile').files?.[0];
    if (!file) { area.innerHTML = `<div class="hint">${SOURCES_HINT}</div>`; return; }
    let doc;
    try {
      doc = JSON.parse(await file.text());
    } catch {
      area.innerHTML = '<div class="hint err">The workflow document is not valid JSON.</div>';
      return;
    }
    const decls = Array.isArray(doc?.sourceDescriptions) ? doc.sourceDescriptions.filter((s) => s?.name) : [];
    if (decls.length === 0) {
      area.innerHTML = '<div class="hint">This workflow declares no source documents.</div>';
      return;
    }
    area.innerHTML = decls.map((s) => `
      <div class="source-row">
        <div class="src-meta">
          <span class="src-label">${escapeHtml(s.name)}</span>
          ${s.type ? `<span class="src-type">${escapeHtml(s.type)}</span>` : ''}
          ${s.url ? `<span class="hint">${escapeHtml(s.url)}</span>` : ''}
        </div>
        <input type="file" class="src-file" data-name="${escapeHtml(s.name)}" accept=".json,application/json">
      </div>`).join('');
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
      // Every source the workflow declares must have a document.
      const sources = [];
      for (const input of this.$$('.src-file')) {
        const name = input.dataset.name;
        const file = input.files?.[0];
        if (!file) throw new Error(`Provide the source document for "${name}".`);
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
      this.emit('workflow-added', { version });
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
