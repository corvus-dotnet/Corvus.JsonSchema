// <arazzo-availability-matrix> — the (workflow version × environment) promotion matrix for one base workflow (§7.8).
//
//   <arazzo-availability-matrix base-url="/arazzo/v1" base-workflow-id="nightly-reconcile"
//                               scopes="availability:read availability:write"></arazzo-availability-matrix>
//
// Attributes : base-url, base-workflow-id, selected-version (highlights a row), scopes (gates the direct make/withdraw)
// Properties : .client, .baseWorkflowId, .selectedVersion
// Events     : availability-changed {baseWorkflowId, versionNumber, environment, available}, promotion-requested {request},
//              loaded {versions, environments}, error {problem}
// Parts      : panel, table, row, cell
//
// The rollout grid for a base workflow: rows = its versions (newest first), columns = the deployment environments, each
// cell the availability of that (version, environment) pair (§7.8 — additive, many-to-many). A cell is **available**
// (with a Withdraw action), **promotable** (ready — every source the version references has a usable credential in the
// environment, §7.7/§13 — offering a direct *Make available* with `availability:write`, or *Request promotion* through
// the §16.5-shaped approver inbox otherwise), or **not ready** (no usable credential set; no action). Direct make/
// withdraw is environment-administrator-gated server-side (a `403`/`409` is surfaced); the request path needs no scope.

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { ArazzoElement, SHARED_CSS, escapeHtml, confirmDialog, define } from './base.js';
import './availability-request-dialog.js';

// A binding is usable by a workflow when it is unscoped (shared) or its usage names exactly this workflow — the
// client-side approximation of §13 IsUsableBy the catalog detail uses for the same readiness computation.
function usableByWorkflow(binding, baseWorkflowId) {
  const id = binding.usageGrantee?.identity;
  return !id || id.length === 0 || id.every((t) => t.dimension === 'workflow' && t.value === baseWorkflowId);
}

class ArazzoAvailabilityMatrix extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'base-workflow-id', 'selected-version', 'single-version', 'scopes'];
  }

  constructor() {
    super();
    /** @private */ this._versions = [];
    /** @private */ this._environments = [];
    /** @private */ this._availability = new Map();   // versionNumber → Set(environment)
    /** @private */ this._credByEnv = null;           // Map(environment → Set(sourceName)) usable by this workflow, or null if unreadable
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.load();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'selected-version' || name === 'single-version' || name === 'scopes') this.renderBody();
    else { this._client = undefined; this.load(); }
  }

  get baseWorkflowId() { return this.getAttribute('base-workflow-id') || ''; }
  set baseWorkflowId(value) { value ? this.setAttribute('base-workflow-id', value) : this.removeAttribute('base-workflow-id'); }

  get selectedVersion() {
    const v = this.getAttribute('selected-version');
    return v == null || v === '' ? null : Number(v);
  }
  set selectedVersion(value) { value == null ? this.removeAttribute('selected-version') : this.setAttribute('selected-version', String(value)); }

  requestRender() { this.load(); }
  refresh() { this.load(); }

  get canWrite() {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes('availability:write');
  }

  // ---- data -------------------------------------------------------------------------------------

  async load() {
    const client = this.client;
    const base = this.baseWorkflowId;
    if (!client || !base) {
      this._error = !base ? { title: 'No workflow selected', detail: 'Set a base-workflow-id.' } : { title: 'Not configured', detail: 'Set a base-url or .client.' };
      this._versions = [];
      this.renderBody();
      return;
    }
    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      const [versions, environments] = await Promise.all([
        client.listCatalogVersions(base, { limit: 200 }).then((r) => r.versions),
        this.drainEnvironments(),
      ]);
      // Per-version availability (a small fan-out — a base workflow has few versions) + the usable-credential map.
      const [availPairs, credByEnv] = await Promise.all([
        Promise.all(versions.map((v) => client
          .listVersionAvailability(base, v.versionNumber, { limit: 200 })
          .then((r) => [v.versionNumber, new Set(r.availability.map((a) => a.environment))])
          .catch(() => [v.versionNumber, new Set()]))),
        this.loadCredByEnv(base).catch(() => null),
      ]);
      if (seq !== this._reqSeq) return;
      this._versions = [...versions].sort((a, b) => b.versionNumber - a.versionNumber);
      this._environments = environments.sort((a, b) => a.name.localeCompare(b.name));
      this._availability = new Map(availPairs);
      this._credByEnv = credByEnv;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { versions: this._versions.length, environments: this._environments.length });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  async drainEnvironments() {
    const envs = [];
    for await (const page of this.client.listEnvironmentsPaged()) envs.push(...page.environments);
    return envs;
  }

  /** The usable-credential map (environment → set of source names credentialed there, usable by this workflow). */
  async loadCredByEnv(base) {
    const byEnv = new Map();
    for await (const page of this.client.listCredentialsPaged({ limit: 200 })) {
      for (const c of page.credentials) {
        if (!usableByWorkflow(c, base)) continue;
        if (!byEnv.has(c.environment)) byEnv.set(c.environment, new Set());
        byEnv.get(c.environment).add(c.sourceName);
      }
    }
    return byEnv;
  }

  isAvailable(versionNumber, env) {
    return this._availability.get(versionNumber)?.has(env) === true;
  }

  /** A version is ready in an environment when every source it references has a usable credential there (§7.7). */
  isReady(version, env) {
    if (!this._credByEnv) return false; // credentials unreadable → readiness unknown, offer no promote action
    const needed = (Array.isArray(version.sources) ? version.sources : []).map((s) => s.name);
    const have = this._credByEnv.get(env);
    return needed.every((n) => have?.has(n));
  }

  // ---- actions ----------------------------------------------------------------------------------

  async makeAvailable(versionNumber, env) {
    try {
      await this.client.makeVersionAvailable(this.baseWorkflowId, versionNumber, env);
      let set = this._availability.get(versionNumber);
      if (!set) { set = new Set(); this._availability.set(versionNumber, set); }
      set.add(env);
      this._error = null;
      this.renderBody();
      this.emit('availability-changed', { baseWorkflowId: this.baseWorkflowId, versionNumber, environment: env, available: true });
    } catch (err) {
      this.showError(err.problem || { title: err.message }, err);
    }
  }

  async withdraw(versionNumber, env) {
    const confirmed = await confirmDialog(this, {
      title: 'Withdraw availability',
      message: `Withdraw v${versionNumber} of '${this.baseWorkflowId}' from '${env}'? Runs already executing on it continue; new runs can no longer target it there.`,
      confirmLabel: 'Withdraw', danger: true,
    });
    if (!confirmed) return;
    try {
      await this.client.deleteVersionAvailability(this.baseWorkflowId, versionNumber, env);
      this._availability.get(versionNumber)?.delete(env);
      this._error = null;
      this.renderBody();
      this.emit('availability-changed', { baseWorkflowId: this.baseWorkflowId, versionNumber, environment: env, available: false });
    } catch (err) {
      this.showError(err.problem || { title: err.message }, err);
    }
  }

  requestPromotion(versionNumber, env) {
    let dlg = this.$('arazzo-availability-request-dialog');
    if (!dlg) {
      dlg = document.createElement('arazzo-availability-request-dialog');
      dlg.addEventListener('availability-request-submitted', (e) => this.emit('promotion-requested', e.detail));
      this.$('.panel').appendChild(dlg);
    }
    dlg.client = this.client;
    dlg.open({ baseWorkflowId: this.baseWorkflowId, versionNumber, environment: env, lockWorkflow: true });
  }

  showError(problem, error) {
    this._error = problem;
    this.renderBody();
    this.emit('error', { problem, error });
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        .head { padding: 10px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); display: flex; align-items: center; gap: 8px; }
        .head .title { font-weight: 700; }
        .head .grow { flex: 1; }
        .err { margin: 10px 12px; }
        .scroll { overflow-x: auto; }
        table { border-collapse: collapse; width: 100%; font-size: 13px; }
        th, td { border-bottom: 1px solid var(--_border); padding: 8px 10px; text-align: left; vertical-align: top; }
        thead th { background: var(--_surface); font-weight: 600; position: sticky; top: 0; }
        th.env { white-space: nowrap; }
        td.ver { white-space: nowrap; font-weight: 600; }
        td.ver .vstatus { display: block; font-weight: 400; font-size: 11px; color: var(--_muted); }
        tr.selected td { background: color-mix(in srgb, var(--_accent) 7%, transparent); }
        .cell { display: flex; flex-direction: column; gap: 4px; align-items: flex-start; min-width: 120px; }
        .badge { font-size: 11px; padding: 1px 8px; border-radius: 999px; border: 1px solid var(--_border); }
        .badge.available { color: #1a7f37; border-color: currentColor; }
        .badge.notready { color: var(--_muted); }
        .cell button { font-size: 12px; padding: 3px 9px; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }
      </style>
      <div class="panel" part="panel">
        <div class="head"><span class="title">Promotion matrix</span><span class="grow"></span><button class="refresh ghost" type="button" title="Refresh">↻</button></div>
        <div class="err"></div>
        <div class="scroll"><div class="body"></div></div>
      </div>
    `;
    this.$('.refresh').addEventListener('click', () => this.load());
  }

  renderBody() {
    const err = this.$('.err');
    const body = this.$('.body');
    if (!body) return;

    err.innerHTML = this._error
      ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
      : '';

    if (this._loading && this._versions.length === 0) {
      body.innerHTML = '<div class="skl"></div><div class="skl"></div>';
      return;
    }
    if (this._versions.length === 0) {
      body.innerHTML = '<div class="empty">No versions to show.</div>';
      return;
    }
    if (this._environments.length === 0) {
      body.innerHTML = '<div class="empty">No environments defined — create one to promote into.</div>';
      return;
    }

    const selected = this.selectedVersion;
    // In single-version mode (embedded in the catalog detail) the matrix is scoped to the selected version's row only —
    // the (version × environment) grid for just that version, like the source/credential section it sits beside.
    const shown = (this.hasAttribute('single-version') && selected != null)
      ? this._versions.filter((v) => v.versionNumber === selected)
      : this._versions;
    if (shown.length === 0) {
      body.innerHTML = '<div class="empty">This version is not shown in the matrix.</div>';
      return;
    }
    const head = `<thead><tr><th>Version</th>${this._environments.map((e) => `<th class="env">${escapeHtml(e.displayName || e.name)}</th>`).join('')}</tr></thead>`;
    const rows = shown.map((v) => {
      const cells = this._environments.map((e) => `<td>${this.cellHtml(v, e.name)}</td>`).join('');
      const status = v.status && v.status !== 'Active' ? `<span class="vstatus">${escapeHtml(v.status)}</span>` : '';
      return `<tr part="row"${selected === v.versionNumber ? ' class="selected"' : ''}><td class="ver">v${escapeHtml(v.versionNumber)}${status}</td>${cells}</tr>`;
    }).join('');
    body.innerHTML = `<table part="table">${head}<tbody>${rows}</tbody></table>`;

    this.$$('button[data-action]').forEach((btn) => {
      const n = Number(btn.dataset.version);
      const env = btn.dataset.env;
      const action = btn.dataset.action;
      btn.addEventListener('click', () => {
        if (action === 'make') this.makeAvailable(n, env);
        else if (action === 'withdraw') this.withdraw(n, env);
        else if (action === 'request') this.requestPromotion(n, env);
      });
    });
  }

  cellHtml(version, env) {
    const n = version.versionNumber;
    const data = `data-version="${escapeHtml(n)}" data-env="${escapeHtml(env)}"`;
    if (this.isAvailable(n, env)) {
      return `<div class="cell"><span class="badge available" part="cell">✓ Available</span>${this.canWrite ? `<button class="ghost" type="button" data-action="withdraw" ${data}>Withdraw</button>` : ''}</div>`;
    }
    if (this.isReady(version, env)) {
      const action = this.canWrite
        ? `<button class="primary" type="button" data-action="make" ${data}>Make available</button>`
        : `<button class="ghost" type="button" data-action="request" ${data}>Request…</button>`;
      return `<div class="cell" part="cell"><span class="badge">Ready</span>${action}</div>`;
    }
    return `<div class="cell" part="cell"><span class="badge notready" title="No usable credential set for this version's sources in ${escapeHtml(env)}.">— not ready</span></div>`;
  }
}

define('arazzo-availability-matrix', ArazzoAvailabilityMatrix);
export { ArazzoAvailabilityMatrix };