// <arazzo-catalog-add-dialog> — a guided, multi-step wizard for adding a workflow to the catalog.
//
//   const dlg = document.querySelector('arazzo-catalog-add-dialog');
//   dlg.client = client;
//   dlg.open();
//
// Properties : .client
// Methods    : open(), close()
// Events     : workflow-added {version}, error {problem}
//
// You add a *workflow* (an Arazzo workflow document + the source documents it references). The catalog assigns the
// version (a new id starts at v1; an existing one gets the next — submit the bare id, NOT a `-vN`). The wizard walks:
//
//   1. Details          — build-from-documents or upload a pre-built package; owner (governance); tags.
//   2. Sources & creds  — (build only) for each `sourceDescriptions` entry: an ALREADY-REGISTERED source (§7.6) is
//                         resolved & immutable (no re-upload); a genuinely new one asks for its document and is
//                         registered on commit. Each source also shows the credentials it already has (a binding
//                         serves every workflow that references the source — reuse) or nudges to set one up (§13).
//   3. Administrators   — the creator always administers (§15, established on the first version); add OPTIONAL further
//                         administrators by resolving a real person/team/role (never a hand-typed tuple, never a
//                         workflow — a workflow does not administer a workflow).
//   4. Review & commit  — everything is gathered first; one commit registers new sources, adds the version, applies
//                         the administrators, and opens the guided credential dialog for each source flagged for setup.

import { ArazzoElement, SHARED_CSS, GRANTEE_CHIP_CSS, granteeChip, escapeHtml, define } from './base.js';
import { packWorkflowPackage } from '../workflow-package.js';
import './credential-dialog.js';
import './grantee-picker.js';

const STEP_FLOW = {
  build: ['details', 'sources', 'admins', 'review'],
  upload: ['details', 'admins', 'review'],
};
const STEP_TITLES = { details: 'Details', sources: 'Sources & credentials', admins: 'Administrators', review: 'Review' };

/**
 * Whether a credential binding is usable by a workflow's runs — a client-side approximation of the backend's §13
 * `IsUsableBy` gate. An unscoped binding (no usage grant) is shared and usable by any run; a usage-scoped binding is
 * usable here only when its usage names exactly this workflow (the tag a run is known to carry). The server does the
 * full label-superset check over the run's resolved identity.
 */
function usableByWorkflow(binding, baseWorkflowId) {
  const identity = binding?.usageGrantee?.identity;
  if (!identity || identity.length === 0) return true;
  return identity.every((t) => t.dimension === 'workflow' && t.value === baseWorkflowId);
}

class ArazzoCatalogAddDialog extends ArazzoElement {
  connectedCallback() {
    if (!this._built) this.renderShell();
  }

  /** Open the wizard (reset to a blank first step). */
  open() {
    if (!this._built) this.renderShell();
    this._mode = 'build';
    this._stepIndex = 0;
    this._workflowDoc = null;
    this._workflowText = '';
    this._packageFile = null;
    this._declaredSources = [];
    this._sourceState = new Map();     // name → { name, type, registered, uploadedDoc, uploadedText }
    this._registryByName = new Map();
    this._environments = [];           // governed environments (for the readiness view)
    this._credentialedEnvBySource = new Map(); // sourceName → Set<environment that has a credential>
    this._sourceDocs = {};             // sourceName → its document (registry- or upload-sourced; for credential derivation)
    this._stagedAdmins = [];           // resolved grantees (person/team/role)
    this._owner = { name: '', email: '', team: '', url: '' };
    this._tags = [];
    this._error = '';
    this.renderStep();
    this.$('dialog').showModal();
  }

  close() {
    this.$('dialog')?.close();
  }

  // ---- step model -------------------------------------------------------------------------------

  steps() { return STEP_FLOW[this._mode]; }
  currentStep() { return this.steps()[this._stepIndex]; }
  isLast() { return this._stepIndex === this.steps().length - 1; }

  goNext() {
    const step = this.currentStep();
    this.saveStep(step);
    const err = this.validateStep(step);
    if (err) { this._error = err; this.renderError(); return; }
    this._error = '';
    if (this.isLast()) { this.commit(); return; }
    this._stepIndex += 1;
    this.renderStep();
    this.onEnterStep(this.currentStep());
  }

  goBack() {
    this.saveStep(this.currentStep());
    this._error = '';
    if (this._stepIndex > 0) this._stepIndex -= 1;
    this.renderStep();
    this.onEnterStep(this.currentStep());
  }

  /** Persist the current step's editable fields into state, so values survive Back/Next. */
  saveStep(step) {
    if (step === 'details') {
      const mode = this.$('input[name="mode"]:checked')?.value;
      if (mode) this._mode = mode;
      this._owner = {
        name: this.$('#ownerName')?.value.trim() || '',
        email: this.$('#ownerEmail')?.value.trim() || '',
        team: this.$('#ownerTeam')?.value.trim() || '',
        url: this.$('#ownerUrl')?.value.trim() || '',
      };
      this._tags = (this.$('#tags')?.value || '').split(/[,\s]+/).map((t) => t.trim()).filter(Boolean);
    }
    // The sources step has no deferred fields — uploads parse on change and credentials are set up inline.
  }

  validateStep(step) {
    if (step === 'details') {
      if (this._mode === 'build') {
        if (!this._workflowDoc) return 'Choose a valid Arazzo workflow document (.json).';
        if (!this._workflowDoc.workflows?.[0]?.workflowId) return 'The workflow document has no workflowId.';
        if (/-v\d+$/.test(this._workflowDoc.workflows[0].workflowId)) return 'Submit the bare workflow id (no -vN suffix); the catalog assigns the version.';
      } else if (!this._packageFile) {
        return 'Choose a package file (.awp) to upload.';
      }
      if (!this._owner.name || !this._owner.email) return 'Owner name and email are required.';
    } else if (step === 'sources') {
      for (const decl of this._declaredSources) {
        const st = this._sourceState.get(decl.name);
        if (!st?.registered && !st?.uploadedDoc) return `Provide the source document for "${decl.name}" (or it must already be a registered source).`;
      }
      // A workflow must be deployable: refuse to continue unless EVERY source has a credential in the SAME environment
      // (so it can actually run somewhere). Set up credentials inline until the readiness banner turns green.
      if (this._declaredSources.length && this.readyEnvironments().length === 0) {
        return 'This workflow isn’t ready in any environment yet — give every source a credential in the same environment (Set up credential…) before continuing.';
      }
    }
    return null;
  }

  onEnterStep(step) {
    if (step === 'sources') this.loadSourcesStep();
    else if (step === 'admins') { const p = this.$('.admin-picker'); if (p && this.client) p.client = this.client; }
  }

  // ---- details ----------------------------------------------------------------------------------

  /** Parse the chosen workflow document → declared sources, and surface a confirmation or parse error. */
  async deriveWorkflow() {
    const status = this.$('.wf-status');
    const file = this.$('#workflowFile')?.files?.[0];
    if (!file) { this._workflowDoc = null; this._workflowText = ''; this._declaredSources = []; if (status) status.textContent = ''; return; }
    try {
      this._workflowText = await file.text();
      this._workflowDoc = JSON.parse(this._workflowText);
    } catch {
      this._workflowDoc = null; this._workflowText = ''; this._declaredSources = [];
      if (status) { status.textContent = 'The workflow document is not valid JSON.'; status.className = 'wf-status err'; }
      return;
    }
    const wfId = this._workflowDoc?.workflows?.[0]?.workflowId || '';
    this._declaredSources = (Array.isArray(this._workflowDoc?.sourceDescriptions) ? this._workflowDoc.sourceDescriptions : [])
      .filter((s) => s?.name).map((s) => ({ name: s.name, type: s.type || 'openapi', url: s.url }));
    this._sourceState = new Map(); // re-derive cleanly on a new document
    if (status) { status.textContent = `✓ ${wfId || 'workflow'} — ${this._declaredSources.length} source(s) declared`; status.className = 'wf-status ok'; }
  }

  // ---- sources & credentials --------------------------------------------------------------------

  /** Resolve which declared sources are registered (§7.6), preload their documents, and load readiness inputs. */
  async loadSourcesStep() {
    if (!this.client) { this.renderStep(); return; }
    this._registryByName = new Map();
    try {
      for await (const page of this.client.listSourcesPaged({ limit: 200 })) {
        for (const s of page.sources) this._registryByName.set(s.name, s);
      }
    } catch { /* no sources:read → treat all as new */ }
    for (const decl of this._declaredSources) {
      const registered = this._registryByName.has(decl.name);
      const existing = this._sourceState.get(decl.name);
      this._sourceState.set(decl.name, {
        name: decl.name, type: decl.type, registered,
        uploadedDoc: existing?.uploadedDoc ?? null, uploadedText: existing?.uploadedText ?? null,
      });
      // Preload a registered source's document so the inline credential dialog can derive its auth.
      if (registered && !this._sourceDocs[decl.name]) {
        try { this._sourceDocs[decl.name] = (await this.client.getSource(decl.name)).document; } catch { /* leave undefined */ }
      }
    }
    await this.loadReadiness();
    this.renderStep();
  }

  /** The base workflow id being created (from the parsed document); '' in upload mode. */
  get workflowId() {
    return this._workflowDoc?.workflows?.[0]?.workflowId || '';
  }

  /** Load the governed environments + which (source, environment) has a credential USABLE by this workflow. */
  async loadReadiness() {
    this._environments = [];
    this._credentialedEnvBySource = new Map();
    if (!this.client) return;
    try {
      for await (const page of this.client.listEnvironmentsPaged()) this._environments.push(...page.environments);
    } catch { /* no environments:read */ }
    try {
      for await (const page of this.client.listCredentialsPaged({ limit: 200 })) {
        for (const b of page.credentials) {
          // A binding only counts toward readiness if this workflow's runs could actually USE it (§13).
          if (!usableByWorkflow(b, this.workflowId)) continue;
          if (!this._credentialedEnvBySource.has(b.sourceName)) this._credentialedEnvBySource.set(b.sourceName, new Set());
          this._credentialedEnvBySource.get(b.sourceName).add(b.environment);
        }
      }
    } catch { /* no credentials:read */ }
  }

  /** The environments the source has a credential in. */
  credentialedEnvironments(sourceName) {
    return [...(this._credentialedEnvBySource.get(sourceName) ?? new Set())].sort();
  }

  /** The environments where EVERY declared source has a credential — the workflow is "ready" (runnable) there (§7.7). */
  readyEnvironments() {
    const needed = this._declaredSources.map((d) => d.name);
    const envNames = this._environments.map((e) => e.name);
    if (!needed.length) return envNames; // a sourceless workflow is ready everywhere
    return envNames.filter((env) => needed.every((n) => this._credentialedEnvBySource.get(n)?.has(env)));
  }

  /** Parse + stage an uploaded document for a new source. */
  async onSourceFile(input) {
    const name = input.dataset.name;
    const st = this._sourceState.get(name);
    const hint = input.closest('.source-row')?.querySelector('.src-upload-status');
    const file = input.files?.[0];
    if (!st) return;
    if (!file) { st.uploadedDoc = null; st.uploadedText = null; delete this._sourceDocs[name]; if (hint) hint.textContent = ''; return; }
    try {
      st.uploadedText = await file.text();
      st.uploadedDoc = JSON.parse(st.uploadedText);
      this._sourceDocs[name] = st.uploadedDoc; // so the inline credential dialog can derive its auth
      if (hint) { hint.textContent = '✓ document staged — will be registered on commit'; hint.className = 'src-upload-status ok'; }
    } catch {
      st.uploadedDoc = null; st.uploadedText = null; delete this._sourceDocs[name];
      if (hint) { hint.textContent = 'Not valid JSON.'; hint.className = 'src-upload-status err'; }
    }
  }

  // ---- administrators ---------------------------------------------------------------------------

  stageAdministrator() {
    const grantee = this.$('.admin-picker')?.grant;
    if (!grantee) return;
    const dup = this._stagedAdmins.some((a) => a.kind === grantee.kind && a.value === grantee.value);
    if (!dup) {
      this._stagedAdmins.push({ kind: grantee.kind, value: grantee.value, identity: grantee.identity, label: grantee.label, complete: grantee.complete });
      this.renderStagedAdmins();
    }
    this.$('.admin-picker')?.reset();
  }

  renderStagedAdmins() {
    const host = this.$('.admins');
    if (!host) return;
    if (!this._stagedAdmins.length) {
      host.innerHTML = '<div class="hint">No additional administrators — only you (the creator) will administer this workflow.</div>';
      return;
    }
    host.innerHTML = this._stagedAdmins.map((a, i) => `
      <div class="admin-row">${granteeChip(a)}<span class="grow"></span>
        <button class="rm-admin ghost" type="button" data-i="${i}" title="Remove" aria-label="Remove">✕</button>
      </div>`).join('');
    this.$$('.rm-admin').forEach((btn) => btn.addEventListener('click', () => {
      this._stagedAdmins.splice(Number(btn.dataset.i), 1);
      this.renderStagedAdmins();
    }));
  }

  // ---- commit -----------------------------------------------------------------------------------

  async buildRequest() {
    const owner = { name: this._owner.name, email: this._owner.email };
    if (this._owner.team) owner.team = this._owner.team;
    if (this._owner.url) owner.url = this._owner.url;
    const tags = this._tags;

    let pkg;
    this._sourceDocs = {};
    if (this._mode === 'upload') {
      if (!this._packageFile) throw new Error('Choose a package file (.awp) to upload.');
      pkg = this._packageFile;
    } else {
      if (!this._workflowText) throw new Error('Choose the Arazzo workflow document.');
      // Gather every source's document — a registered source's comes from the registry (no re-upload), a new one's
      // from the upload — and register the new ones so future workflows can reuse them.
      const sources = [];
      for (const decl of this._declaredSources) {
        const st = this._sourceState.get(decl.name);
        let content;
        if (st?.registered) {
          const reg = await this.client.getSource(decl.name);
          this._sourceDocs[decl.name] = reg.document;
          content = JSON.stringify(reg.document);
        } else {
          if (!st?.uploadedDoc) throw new Error(`Provide the source document for "${decl.name}".`);
          this._sourceDocs[decl.name] = st.uploadedDoc;
          content = st.uploadedText;
          try {
            await this.client.registerSource({ name: decl.name, type: decl.type, document: st.uploadedDoc });
          } catch (err) {
            if (err.problem?.status !== 409) throw err; // 409 = raced in / already registered → reuse it
          }
        }
        sources.push({ name: decl.name, content });
      }
      pkg = packWorkflowPackage(this._workflowText, sources);
    }
    return { package: pkg, owner, tags };
  }

  async commit() {
    this._error = '';
    this.renderError();
    const confirmBtn = this.$('.confirm');
    if (confirmBtn) confirmBtn.disabled = true;
    let request;
    try {
      request = await this.buildRequest();
    } catch (err) {
      this._error = err.message;
      this.renderError();
      if (confirmBtn) confirmBtn.disabled = false;
      return;
    }
    try {
      const version = await this.client.addCatalogVersion(request);
      await this.applyAdministrators(version.baseWorkflowId);
      this.close();
      this.emit('workflow-added', { version });
    } catch (err) {
      const problem = err.problem || { title: err.message };
      this._error = `${problem.title || 'Add failed'}${problem.detail ? ' — ' + problem.detail : ''}`;
      this.renderError();
      this.emit('error', { problem, error: err });
    } finally {
      if (this.$('.confirm')) this.$('.confirm').disabled = false;
    }
  }

  /** Apply the staged administrator set to the landed base id (best-effort, idempotent). */
  async applyAdministrators(baseWorkflowId) {
    for (const member of this._stagedAdmins || []) {
      try {
        await this.client.addAdministrator(baseWorkflowId, member);
      } catch (err) {
        this.emit('error', { problem: err.problem || { title: err.message }, error: err });
      }
    }
  }

  /** Open the guided credential dialog INLINE (during the wizard) for one source; refresh readiness when a binding is saved. */
  openCredentialSetup(sourceName) {
    let dlg = this.$('arazzo-credential-dialog');
    if (!dlg) {
      dlg = document.createElement('arazzo-credential-dialog');
      this.shadowRoot.appendChild(dlg);
      dlg.addEventListener('credential-saved', (e) => {
        const b = e.detail?.binding;
        if (b?.sourceName && b?.environment && usableByWorkflow(b, this.workflowId)) {
          if (!this._credentialedEnvBySource.has(b.sourceName)) this._credentialedEnvBySource.set(b.sourceName, new Set());
          this._credentialedEnvBySource.get(b.sourceName).add(b.environment);
        }
        this.renderStep(); // refresh the source's credential chips + the readiness banner
      });
      dlg.addEventListener('error', (e) => e.stopPropagation()); // the credential dialog surfaces its own banner
    }
    dlg.client = this.client;
    dlg.open(null, { sourceName, lockSource: true, sourceDoc: this._sourceDocs?.[sourceName] || null });
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${GRANTEE_CHIP_CSS}
        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(640px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .head { padding: 14px 16px; border-bottom: 1px solid var(--_border); }
        .head .title { font-weight: 700; font-size: 15px; }
        .steps { display: flex; gap: 6px; margin-top: 8px; flex-wrap: wrap; }
        .step-chip { font-size: 11px; padding: 2px 8px; border-radius: 999px; border: 1px solid var(--_border); color: var(--_muted); }
        .step-chip.active { border-color: var(--_accent); color: var(--_text); background: color-mix(in srgb, var(--_accent) 10%, transparent); font-weight: 600; }
        .step-chip.done { color: var(--arazzo-status-completed, #2a8a4a); border-color: color-mix(in srgb, var(--arazzo-status-completed, #2a8a4a) 50%, var(--_border)); }
        .content { padding: 16px; display: grid; gap: 14px; max-height: 60vh; overflow: auto; }
        fieldset { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 12px; margin: 0; display: grid; gap: 10px; }
        legend { font-size: 12px; font-weight: 600; color: var(--_muted); padding: 0 4px; }
        .modes { display: flex; gap: 8px; flex-wrap: wrap; }
        .mode { display: inline-flex; gap: 6px; align-items: center; border: 1px solid var(--_border); border-radius: var(--_radius); padding: 6px 10px; cursor: pointer; font-size: 13px; }
        .mode:has(input:checked) { border-color: var(--_accent); background: color-mix(in srgb, var(--_accent) 8%, transparent); }
        label { font-size: 12px; color: var(--_muted); display: block; margin-bottom: 4px; }
        .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
        input[type="text"], input[type="email"], input[type="url"] { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
        input[type="file"] { font: inherit; font-size: 12px; }
        .wf-status, .src-upload-status { font-size: 11px; color: var(--_muted); }
        .wf-status.ok, .src-upload-status.ok { color: var(--arazzo-status-completed, #2a8a4a); }
        .wf-status.err, .src-upload-status.err, .hint.err { color: var(--_danger); }
        .sources { display: grid; gap: 8px; }
        .source-row { display: grid; gap: 6px; border: 1px solid var(--_border); border-radius: var(--_radius); padding: 8px 10px; }
        .src-head { display: flex; align-items: baseline; gap: 8px; }
        .src-label { font-weight: 600; font-size: 13px; color: var(--_text); }
        .src-type { font-size: 11px; color: var(--_muted); text-transform: uppercase; letter-spacing: 0.03em; }
        .src-badge { font-size: 11px; padding: 1px 7px; border-radius: 999px; margin-left: auto; }
        .src-badge.registered { color: var(--arazzo-status-completed, #2a8a4a); border: 1px solid color-mix(in srgb, var(--arazzo-status-completed, #2a8a4a) 50%, var(--_border)); }
        .src-badge.new { color: var(--arazzo-status-suspended, #b07d18); border: 1px solid color-mix(in srgb, var(--arazzo-status-suspended, #b07d18) 50%, var(--_border)); }
        .env-chip { font-size: 11px; padding: 1px 7px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_text); }
        .readiness { font-size: 13px; padding: 8px 10px; border-radius: var(--_radius); display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
        .readiness.ok { color: var(--arazzo-status-completed, #2a8a4a); border: 1px solid color-mix(in srgb, var(--arazzo-status-completed, #2a8a4a) 50%, var(--_border)); background: color-mix(in srgb, var(--arazzo-status-completed, #2a8a4a) 8%, transparent); }
        .readiness.warn { color: var(--arazzo-status-suspended, #b07d18); border: 1px solid color-mix(in srgb, var(--arazzo-status-suspended, #b07d18) 50%, var(--_border)); background: color-mix(in srgb, var(--arazzo-status-suspended, #b07d18) 8%, transparent); }
        .src-creds { font-size: 12px; color: var(--_muted); display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
        .src-creds .ok { color: var(--arazzo-status-completed, #2a8a4a); font-weight: 600; }
        .src-creds .warn { color: var(--arazzo-status-suspended, #b07d18); font-weight: 600; }
        .setup-cred-btn { margin-left: auto; font-size: 12px; }
        .review-row .ok { color: var(--arazzo-status-completed, #2a8a4a); }
        .review-row .warn { color: var(--arazzo-status-suspended, #b07d18); }
        .hint { font-size: 11px; color: var(--_muted); }
        .admins { display: grid; gap: 6px; }
        .admin-row { display: flex; align-items: center; gap: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); padding: 6px 10px; }
        .admin-row .grow { flex: 1; }
        .admin-add { display: flex; gap: 8px; align-items: center; }
        .admin-add .admin-picker { flex: 1; }
        .review-grid { display: grid; gap: 10px; }
        .review-row { display: grid; grid-template-columns: 130px 1fr; gap: 10px; font-size: 13px; }
        .review-row .k { color: var(--_muted); font-size: 12px; }
        .creator-note { font-size: 12px; color: var(--_muted); }
        .foot { display: flex; gap: 8px; align-items: center; padding: 12px 16px; border-top: 1px solid var(--_border); }
        .foot .spacer { flex: 1; }
        .error-banner { margin: 0 16px 12px; }
      </style>
      <dialog part="dialog">
        <form method="dialog">
          <div class="head">
            <div class="title">Add workflow</div>
            <div class="steps"></div>
          </div>
          <div class="content"></div>
          <div class="error-banner" hidden></div>
          <div class="foot">
            <button value="dismiss" class="cancel ghost" type="button">Cancel</button>
            <span class="spacer"></span>
            <button class="back ghost" type="button" hidden>Back</button>
            <button class="next primary" type="button">Next</button>
          </div>
        </form>
      </dialog>
    `;
    this.$('.cancel').addEventListener('click', () => this.close());
    this.$('.back').addEventListener('click', () => this.goBack());
    this.$('.next').addEventListener('click', () => this.goNext());
  }

  renderStep() {
    const step = this.currentStep();
    // Step indicator.
    const steps = this.steps();
    this.$('.steps').innerHTML = steps.map((s, i) => {
      const cls = i === this._stepIndex ? 'active' : (i < this._stepIndex ? 'done' : '');
      return `<span class="step-chip ${cls}">${i + 1}. ${STEP_TITLES[s]}</span>`;
    }).join('');
    // Content.
    const content = this.$('.content');
    content.innerHTML = this[`render_${step}`]();
    this.wireStep(step);
    // Footer.
    this.$('.back').hidden = this._stepIndex === 0;
    this.$('.next').textContent = this.isLast() ? 'Add workflow' : 'Next';
    this.$('.next').classList.toggle('confirm', this.isLast());
    this.renderError();
  }

  renderError() {
    const banner = this.$('.error-banner');
    if (!banner) return;
    banner.textContent = this._error || '';
    banner.hidden = !this._error;
  }

  wireStep(step) {
    if (step === 'details') {
      this.$$('input[name="mode"]').forEach((r) => r.addEventListener('change', () => { this.saveStep('details'); this.renderStep(); }));
      this.$('#workflowFile')?.addEventListener('change', () => this.deriveWorkflow());
      this.$('#packageFile')?.addEventListener('change', () => { this._packageFile = this.$('#packageFile').files?.[0] || null; });
    } else if (step === 'sources') {
      this.$$('.src-file').forEach((i) => i.addEventListener('change', () => this.onSourceFile(i)));
      this.$$('.setup-cred-btn').forEach((b) => b.addEventListener('click', () => this.openCredentialSetup(b.dataset.name)));
    } else if (step === 'admins') {
      const picker = this.$('.admin-picker');
      if (picker && this.client) picker.client = this.client;
      this.$('.add-admin')?.addEventListener('click', () => this.stageAdministrator());
      this.renderStagedAdmins();
    }
  }

  render_details() {
    const o = this._owner;
    return `
      <div class="modes" part="modes">
        <label class="mode"><input type="radio" name="mode" value="build"${this._mode === 'build' ? ' checked' : ''}> Build from documents</label>
        <label class="mode"><input type="radio" name="mode" value="upload"${this._mode === 'upload' ? ' checked' : ''}> Upload package (.awp)</label>
      </div>
      ${this._mode === 'build' ? `
        <fieldset>
          <legend>Workflow</legend>
          <div><label for="workflowFile">Arazzo workflow document (.json)</label><input id="workflowFile" type="file" accept=".json,application/json"></div>
          <div class="wf-status ${this._workflowDoc ? 'ok' : ''}">${this._workflowDoc ? `✓ ${escapeHtml(this._workflowDoc.workflows?.[0]?.workflowId || 'workflow')} — ${this._declaredSources.length} source(s) declared` : ''}</div>
        </fieldset>` : `
        <fieldset>
          <legend>Package</legend>
          <div><label for="packageFile">Package file (.awp from <code>arazzo catalog pack</code>)</label><input id="packageFile" type="file" accept=".awp,application/octet-stream"></div>
          ${this._packageFile ? `<div class="wf-status ok">✓ ${escapeHtml(this._packageFile.name)}</div>` : ''}
        </fieldset>`}
      <fieldset>
        <legend>Owner (governance)</legend>
        <div class="grid2">
          <div><label for="ownerName">Name *</label><input id="ownerName" type="text" placeholder="Reconciliation Team" value="${escapeHtml(o.name)}"></div>
          <div><label for="ownerEmail">Email *</label><input id="ownerEmail" type="email" placeholder="team@example.com" value="${escapeHtml(o.email)}"></div>
          <div><label for="ownerTeam">Team</label><input id="ownerTeam" type="text" placeholder="Platform" value="${escapeHtml(o.team)}"></div>
          <div><label for="ownerUrl">URL</label><input id="ownerUrl" type="url" placeholder="https://runbooks.example.com/…" value="${escapeHtml(o.url)}"></div>
        </div>
      </fieldset>
      <div><label for="tags">Tags (space or comma separated)</label><input id="tags" type="text" placeholder="prod billing" value="${escapeHtml(this._tags.join(' '))}"></div>
      <div class="hint">The catalog assigns the version — a new workflow id starts at v1, an existing one gets the next. Submit the bare id (no <code>-vN</code>).</div>
    `;
  }

  render_sources() {
    if (!this._declaredSources.length) {
      return '<div class="hint">This workflow declares no source documents — nothing to configure here.</div>';
    }
    const known = this._sourceState.size > 0;
    const ready = this.readyEnvironments();
    const readiness = !known ? ''
      : ready.length
        ? `<div class="readiness ok">✓ This workflow will be ready to run in ${ready.map((e) => `<span class="env-chip">${escapeHtml(e)}</span>`).join(' ')} — every source has a credential there.</div>`
        : `<div class="readiness warn">⚠ Not ready in any environment yet. Give <strong>every</strong> source a credential in the <strong>same</strong> environment so this workflow can run there — <strong>required</strong> before you can add it.</div>`;
    return `
      <div class="hint">Each source is registered once (§7.6) and reused — a source already in the registry is resolved automatically; a new one needs its document. Set up each source's credential per environment: a workflow is ready in an environment when every source it references has a credential there.</div>
      ${readiness}
      <div class="sources">${this._declaredSources.map((decl) => {
        const st = this._sourceState.get(decl.name);
        const registered = st?.registered;
        const envs = this.credentialedEnvironments(decl.name);
        return `
          <div class="source-row">
            <div class="src-head">
              <span class="src-label">${escapeHtml(decl.name)}</span>
              <span class="src-type">${escapeHtml(decl.type)}</span>
              ${!known ? '' : registered
                ? `<span class="src-badge registered">✓ registered</span>`
                : `<span class="src-badge new">new — register</span>`}
            </div>
            ${!known ? '<div class="hint">Checking the source registry…</div>' : registered
              ? `<div class="hint">Resolved from the registry — its document is reused immutably. No upload needed.</div>`
              : `<div><input type="file" class="src-file" data-name="${escapeHtml(decl.name)}" accept=".json,application/json"><div class="src-upload-status ${st?.uploadedDoc ? 'ok' : ''}">${st?.uploadedDoc ? '✓ document staged — will be registered on commit' : ''}</div></div>`}
            ${!known ? '' : `<div class="src-creds">
              ${envs.length ? `<span class="ok">Credential in:</span> ${envs.map((e) => `<span class="env-chip">${escapeHtml(e)}</span>`).join(' ')}` : `<span class="warn">No credential yet</span>`}
              <button class="setup-cred-btn ghost" type="button" data-name="${escapeHtml(decl.name)}">Set up credential…</button>
            </div>`}
          </div>`;
      }).join('')}</div>
    `;
  }

  render_admins() {
    return `
      <div class="creator-note">✓ <strong>You (the creator)</strong> will administer this workflow — established automatically on its first version (§15). Add others below if you want; this is optional.</div>
      <fieldset>
        <legend>Additional administrators</legend>
        <label>Resolve a real person, team or role (a workflow can’t administer a workflow).</label>
        <div class="admins"></div>
        <div class="admin-add">
          <arazzo-grantee-picker class="admin-picker" kinds="person team role" placeholder="Find a person, team or role…"></arazzo-grantee-picker>
          <button class="add-admin ghost" type="button">+ Add</button>
        </div>
      </fieldset>
    `;
  }

  render_review() {
    const wfId = this._mode === 'build' ? (this._workflowDoc?.workflows?.[0]?.workflowId || '—') : '(from package)';
    const sourceLines = this._mode === 'build'
      ? (this._declaredSources.length ? this._declaredSources.map((d) => {
          const st = this._sourceState.get(d.name);
          const envs = this.credentialedEnvironments(d.name);
          const cred = envs.length ? `credential in ${envs.join(', ')}` : 'no credential yet';
          return `<div>${escapeHtml(d.name)} — ${st?.registered ? 'reuse registered source' : 'register new source'}; ${cred}</div>`;
        }).join('') : '<div class="hint">none</div>')
      : '<div class="hint">carried in the uploaded package</div>';
    const ready = this.readyEnvironments();
    const readyLine = this._mode === 'build'
      ? (ready.length ? `<span class="ok">✓ Ready in ${escapeHtml(ready.join(', '))}</span>` : `<span class="warn">⚠ Not ready in any environment yet — runs can’t use a source until it has a credential there</span>`)
      : '<span class="hint">determined by the package + its credentials</span>';
    const admins = ['<div>You (the creator)</div>', ...this._stagedAdmins.map((a) => `<div>${escapeHtml(a.label || a.value)} <span class="hint">(${escapeHtml(a.kind)})</span></div>`)].join('');
    return `
      <div class="hint">Review, then commit — this registers any new sources, adds the version, applies the administrators, and opens the credential setup for the sources you flagged.</div>
      <div class="review-grid">
        <div class="review-row"><span class="k">Workflow</span><span>${escapeHtml(wfId)} <span class="hint">— the catalog assigns the version</span></span></div>
        <div class="review-row"><span class="k">Mode</span><span>${this._mode === 'build' ? 'Build from documents' : 'Upload package'}</span></div>
        <div class="review-row"><span class="k">Owner</span><span>${escapeHtml(this._owner.name)} &lt;${escapeHtml(this._owner.email)}&gt;${this._owner.team ? ` · ${escapeHtml(this._owner.team)}` : ''}</span></div>
        <div class="review-row"><span class="k">Tags</span><span>${this._tags.length ? this._tags.map((t) => `<span class="env-chip">${escapeHtml(t)}</span>`).join(' ') : '<span class="hint">none</span>'}</span></div>
        <div class="review-row"><span class="k">Sources</span><span>${sourceLines}</span></div>
        <div class="review-row"><span class="k">Readiness</span><span>${readyLine}</span></div>
        <div class="review-row"><span class="k">Administrators</span><span>${admins}</span></div>
      </div>
    `;
  }
}

define('arazzo-catalog-add-dialog', ArazzoCatalogAddDialog);
export { ArazzoCatalogAddDialog };
