// <arazzo-availability-request-dialog> — the §7.8 "request promotion" submit form, reusable as a standalone dialog.
//
//   const dlg = document.createElement('arazzo-availability-request-dialog');
//   dlg.client = client;                       // an ArazzoControlPlaneClient
//   host.shadowRoot.appendChild(dlg);
//   dlg.open();                                // pick any workflow (autocomplete)
//   dlg.open({ baseWorkflowId: 'onboard-customer', versionNumber: 1, environment: 'staging', lockWorkflow: true });
//
// Properties : .client
// Methods    : open({ baseWorkflowId?, versionNumber?, environment?, lockWorkflow? }), close()
// Events     : availability-request-submitted {request}, error {problem}
//
// Asks for a workflow version to be made available in an environment (a "promotion request", §7.8). The form is
// constrained to valid choices: the Version dropdown offers only the chosen workflow's actual catalogued versions,
// and the Environment dropdown offers only the environments where that version is READY — every source it references
// has a credential there (§7.7). There is no readiness query endpoint, so the dialog computes that gate client-side
// from the same inputs the server uses (GET /environments + the version's sources + GET /credentials); it is an
// advisory pre-filter — the server still enforces readiness at approval time. Reusable by <arazzo-availability-requests>
// (the "Request promotion…" button) and by a catalog-version entry (locked to that workflow + version).

import { ArazzoElement, adoptStyles, SHARED_CSS, escapeHtml, define } from './base.js';
import { versionReadiness, restrictionSummary } from '../readiness.js';
import './workflow-picker.js';

class ArazzoAvailabilityRequestDialog extends ArazzoElement {
  /** The Layer-0 client used to load choices and submit. */
  set client(value) { this._client = value; }
  get client() { return this._client; }

  /** Open the form. With `{ ..., lockWorkflow: true }` the workflow + version are fixed (a catalog-version entry). */
  open({ baseWorkflowId = '', versionNumber = '', environment = '', lockWorkflow = false } = {}) {
    this._baseWorkflowId = baseWorkflowId || '';
    this._versionNumber = versionNumber === '' || versionNumber == null ? '' : String(versionNumber);
    this._environment = environment || '';
    this._lock = !!(lockWorkflow && baseWorkflowId && this._versionNumber);
    this._versions = [];
    this._readyEnvs = [];
    this._verSeq = 0;
    this._envSeq = 0;
    this.render();
    this.$('dialog').showModal();
    // Seed the choices: a fixed/known workflow loads its versions straight away; otherwise wait for a pick.
    if (this._baseWorkflowId) this.loadVersions(this._baseWorkflowId);
    if (!this._lock) this.$('.sub-wf')?.shadowRoot?.querySelector('input')?.focus();
    this.updateSubmitState();
  }

  close() {
    this.$('dialog')?.close();
  }

  showError(message) {
    const banner = this.$('.error-banner');
    if (banner) { banner.textContent = message; banner.hidden = false; }
  }

  // ---- choices ----------------------------------------------------------------------------------

  /** The version number currently chosen (the locked value, or the version dropdown's selection). */
  selectedVersionNumber() {
    return this._lock ? this._versionNumber : (this.$('.ver-in')?.value || '');
  }

  /** The full version object for the current selection (carries its `sources`), or null. */
  selectedVersion() {
    const n = Number(this.selectedVersionNumber());
    return this._versions.find((v) => v.versionNumber === n) || null;
  }

  /** Load the chosen workflow's catalogued versions into the Version dropdown, newest first. */
  async loadVersions(base) {
    const client = this._client;
    const sel = this.$('.ver-in'); // absent when locked
    const seq = ++this._verSeq;
    if (!client || !base) {
      this._versions = [];
      if (sel) { sel.innerHTML = `<option value="">Choose a workflow first</option>`; sel.disabled = true; }
      this.populateEnvironments(null);
      return;
    }
    if (sel) { sel.disabled = true; sel.innerHTML = `<option value="">Loading versions…</option>`; }
    try {
      const { versions } = await client.listCatalogVersions(base);
      if (seq !== this._verSeq) return; // superseded by a newer workflow pick
      this._versions = versions;
      if (sel) {
        if (!versions.length) {
          sel.innerHTML = `<option value="">No versions for this workflow</option>`;
          sel.disabled = true;
          this.populateEnvironments(null);
          return;
        }
        const sorted = [...versions].sort((a, b) => b.versionNumber - a.versionNumber);
        const preselect = this._versionNumber || String(sorted[0].versionNumber);
        sel.innerHTML = sorted.map((v) =>
          `<option value="${v.versionNumber}"${String(v.versionNumber) === preselect ? ' selected' : ''}>v${v.versionNumber}${v.status ? ` · ${escapeHtml(v.status)}` : ''}</option>`).join('');
        sel.disabled = false;
      }
      this.onVersionChange();
    } catch (err) {
      if (seq !== this._verSeq) return;
      this._versions = [];
      if (sel) { sel.innerHTML = `<option value="">Could not load versions</option>`; sel.disabled = true; }
      this.populateEnvironments(null);
    }
  }

  onVersionChange() {
    this.populateEnvironments(this.selectedVersion());
  }

  /** Populate the Environment dropdown with only the environments where `version` is ready (every source credentialed). */
  async populateEnvironments(version) {
    const sel = this.$('.env-in');
    if (!sel) return;
    const seq = ++this._envSeq;
    if (!version || !this._client) {
      this._readyEnvs = [];
      sel.innerHTML = `<option value="">Choose a version first</option>`;
      sel.disabled = true;
      this.updateSubmitState();
      this.showRestriction();
      return;
    }
    sel.disabled = true;
    sel.innerHTML = `<option value="">Checking environments…</option>`;
    try {
      const ready = await this.readyEnvironments(version);
      if (seq !== this._envSeq) return; // superseded by a newer version pick
      this._readyEnvs = ready;
      if (!ready.length) {
        sel.innerHTML = `<option value="">No environment is ready for this version</option>`;
        sel.disabled = true;
      } else {
        const pre = this._environment;
        sel.innerHTML = ready.map((e) =>
          `<option value="${escapeHtml(e.name)}"${e.name === pre ? ' selected' : ''}>${escapeHtml(e.displayName || e.name)}</option>`).join('');
        sel.disabled = false;
      }
      this.updateSubmitState();
      this.showRestriction();
    } catch (err) {
      if (seq !== this._envSeq) return;
      this._readyEnvs = [];
      sel.innerHTML = `<option value="">Could not load environments</option>`;
      sel.disabled = true;
      this.updateSubmitState();
      this.showRestriction();
    }
  }

  /**
   * The environments in which `version` is ready (ADR 0074), as the server judges it: every source has a credential
   * there that the version's runs may use, and the environment's evidence rule holds. The environments are listed for
   * their display names; the readiness is kept so the selected environment's restrictions can be shown.
   */
  async readyEnvironments(version) {
    const client = this._client;
    const envs = [];
    for await (const page of client.listEnvironmentsPaged()) envs.push(...page.environments);
    this._readiness = await versionReadiness(client, version.baseWorkflowId, version.versionNumber);
    return envs.filter((e) => this._readiness.get(e.name)?.ready);
  }

  /** Say who the selected environment's usable credentials are restricted to, if anyone. */
  showRestriction() {
    const hint = this.$('.restriction');
    if (!hint) return;
    const entry = this._readiness?.get(this.$('.env-in')?.value || '');
    const summary = entry ? restrictionSummary(entry) : '';
    hint.textContent = summary ? `Credentials: ${summary}.` : '';
    hint.hidden = !summary;
  }

  updateSubmitState() {
    const ok = this.$('.ok');
    if (!ok) return;
    ok.disabled = !(this.selectedVersionNumber() && this._readyEnvs.length > 0 && this.$('.env-in')?.value);
  }

  // ---- submit -----------------------------------------------------------------------------------

  async submit() {
    const baseWorkflowId = this._lock ? this._baseWorkflowId : (this.$('.sub-wf')?.value.trim() || '');
    const versionNumber = Number(this.selectedVersionNumber());
    const environment = this.$('.env-in')?.value || '';
    const reason = this.$('.reason-in').value.trim() || undefined;
    if (!baseWorkflowId || !Number.isInteger(versionNumber) || versionNumber < 1 || !environment) {
      this.showError('Choose a workflow, a version, and a ready environment.');
      return;
    }
    try {
      const request = await this.client.submitAvailabilityRequest({ baseWorkflowId, versionNumber, environment, reason });
      this.emit('availability-request-submitted', { request });
      this.close();
    } catch (err) {
      const problem = err.problem || { title: err.message };
      this.showError(`${problem.title || 'Request failed'}${problem.detail ? ' — ' + problem.detail : ''}`);
      this.emit('error', { problem, error: err });
    }
  }

  render() {
    const wfRow = this._lock
      ? `<div><div class="sub caption">Version</div><div class="locked-wf">${escapeHtml(this._baseWorkflowId)} v${escapeHtml(this._versionNumber)}</div></div>`
      : `<label>Workflow<arazzo-workflow-picker class="sub-wf" placeholder="Find a workflow…"></arazzo-workflow-picker></label>
         <label>Version<select class="ver-in" disabled><option value="">Choose a workflow first</option></select></label>`;
    adoptStyles(this.shadowRoot, SHARED_CSS, `
      dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(480px, 94vw); }
      dialog::backdrop { background: rgba(0,0,0,0.4); }
      .dhead { padding: 14px 16px; font-weight: 700; border-bottom: 1px solid var(--_border); }
      .dbody { padding: 14px 16px; display: grid; gap: 12px; }
      .dfoot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      label { font-size: 12px; color: var(--_muted); display: grid; gap: 4px; }
      input, textarea, select { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
      select:disabled { background: var(--_surface); color: var(--_muted); cursor: default; }
      textarea { resize: vertical; min-height: 56px; }
      .locked-wf { font-weight: 600; font-size: 14px; }
      button[disabled] { opacity: 0.5; cursor: not-allowed; }
      .error-banner { margin: 0 16px 12px; }
      .sub.caption { margin-bottom: 4px; }
    `);
    this.shadowRoot.innerHTML = `
      <dialog part="dialog">
        <form method="dialog">
          <div class="dhead">Request promotion</div>
          <div class="dbody">
            ${wfRow}
            <label>Environment<select class="env-in" disabled><option value="">Choose a version first</option></select></label>
            <div class="sub restriction" hidden></div>
            <label>Reason (optional)
              <textarea class="reason-in" placeholder="Why this version should be available here…"></textarea>
            </label>
            <div class="sub">Only the environments where this version is ready are offered: every source it references has a credential there that its runs may use, and any evidence the environment requires is green. An environment administrator approves the request.</div>
          </div>
          <div class="error-banner" hidden></div>
          <div class="dfoot">
            <button class="cancel ghost" type="button">Cancel</button>
            <button class="ok primary" type="button">Submit request</button>
          </div>
        </form>
      </dialog>`;
    const wf = this.$('.sub-wf');
    if (wf) {
      wf.client = this._client;
      if (this._baseWorkflowId) wf.value = this._baseWorkflowId;
      // The picker fires `change` on pick/clear (not per keystroke), so load the chosen workflow's versions then.
      wf.addEventListener('change', () => this.loadVersions(wf.value.trim()));
    }
    this.$('.ver-in')?.addEventListener('change', () => this.onVersionChange());
    this.$('.env-in')?.addEventListener('change', () => { this.updateSubmitState(); this.showRestriction(); });
    this.$('.cancel').addEventListener('click', () => this.close());
    this.$('dialog').addEventListener('cancel', (e) => { e.preventDefault(); this.close(); });
    this.$('.ok').addEventListener('click', (e) => { void this.runAction(e.currentTarget, () => this.submit()); });
  }
}

define('arazzo-availability-request-dialog', ArazzoAvailabilityRequestDialog);
export { ArazzoAvailabilityRequestDialog };
