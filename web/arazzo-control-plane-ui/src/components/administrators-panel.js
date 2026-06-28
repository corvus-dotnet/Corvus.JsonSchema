// <arazzo-administrators-panel> — manage a workflow's administrator set (§15).
//
//   <arazzo-administrators-panel base-url="/arazzo/v1" base-workflow-id="nightly-reconcile"
//                                scopes="administrators:write"></arazzo-administrators-panel>
//
// Attributes : base-url, base-workflow-id, scopes (gates the mutating controls)
// Properties : .client, .baseWorkflowId
// Events     : administrators-changed {administrators}, error {problem}
// Parts      : panel, list, row, add
//
// An administrator is a resolved identity (§16.5.4): the deployment-mapped {dimension, value} grants it resolves
// from (never a raw internal tag), an optional resolved kind/label for display, and a stable digest that is the
// removal key. Adding names a real person/team/role/workflow grantee via the picker (which resolves it to its exact
// sys: identity), not a hand-typed tuple. The set is governed by current-administrator membership: a caller who is
// not an administrator is refused (403), shown here as a plain banner, never a disclosure of who is. The set is
// never empty — removing the last administrator is refused (409).

import { ArazzoElement, SHARED_CSS, GRANTEE_CHIP_CSS, granteeChip, escapeHtml, confirmDialog, define } from './base.js';
import './grantee-picker.js';

class ArazzoAdministratorsPanel extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'base-workflow-id', 'scopes'];
  }

  constructor() {
    super();
    /** @private */ this._admins = [];
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
    if (name === 'scopes') this.renderBody();
    else this.load();
  }

  get baseWorkflowId() {
    return this.getAttribute('base-workflow-id') || '';
  }

  set baseWorkflowId(value) {
    if (value) this.setAttribute('base-workflow-id', value);
    else this.removeAttribute('base-workflow-id');
  }

  requestRender() {
    this.load();
  }

  refresh() {
    this.load();
  }

  get canWrite() {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes('administrators:write');
  }

  async load() {
    const client = this.client;
    const base = this.baseWorkflowId;
    const grantIn = this.$('.grant-in');
    if (grantIn && client && grantIn.client !== client) grantIn.client = client;
    if (!client || !base) {
      this._error = !base ? { title: 'No workflow selected', detail: 'Set a base-workflow-id.' } : { title: 'Not configured', detail: 'Set a base-url or .client.' };
      this._admins = [];
      this.renderBody();
      return;
    }
    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      const { administrators } = await client.listAdministrators(base);
      if (seq !== this._reqSeq) return;
      this._admins = administrators;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._admins.length });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  // ---- mutations --------------------------------------------------------------------------------

  async add() {
    const grantee = this.$('.grant-in').grant;
    if (!grantee) { this.showError({ title: 'Select a grantee to add as an administrator.' }); return; }
    // The picker resolved the grantee to its exact identity; carry kind/value/identity/label/complete straight through.
    const member = { kind: grantee.kind, value: grantee.value, identity: grantee.identity, label: grantee.label, complete: grantee.complete };
    await this.mutate(() => this.client.addAdministrator(this.baseWorkflowId, member), () => this.$('.grant-in').reset());
  }

  async removeMember(digest, describe) {
    const confirmed = await confirmDialog(this, {
      title: 'Remove administrator',
      message: `Remove ${describe} from the administrators of '${this.baseWorkflowId}'?`,
      confirmLabel: 'Remove', danger: true,
    });
    if (!confirmed) return;
    await this.mutate(() => this.client.removeAdministrator(this.baseWorkflowId, digest));
  }

  async mutate(action, onOk) {
    try {
      const { administrators } = await action();
      this._admins = administrators ?? this._admins;
      this._error = null;
      onOk?.();
      this.renderBody();
      this.emit('administrators-changed', { administrators: this._admins });
    } catch (err) {
      this.showError(err.problem || { title: err.message }, err);
    }
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
        ${GRANTEE_CHIP_CSS}
        /* overflow stays visible so the add-row's grantee-picker results dropdown isn't clipped by the card. */
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); }
        .head { padding: 10px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); border-radius: var(--_radius) var(--_radius) 0 0; display: flex; align-items: baseline; gap: 8px; }
        .head .title { font-weight: 700; }
        .head .base { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); }
        .list { display: grid; }
        .arow { display: flex; align-items: center; gap: 8px; padding: 9px 12px; border-bottom: 1px solid var(--_border); }
        .arow:last-child { border-bottom: none; }
        .grow { flex: 1; }
        .add { display: flex; gap: 8px; align-items: center; padding: 10px 12px; border-top: 1px solid var(--_border); background: var(--_surface); border-radius: 0 0 var(--_radius) var(--_radius); }
        .add .grant-in { flex: 1; }
        .err { margin: 10px 12px; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }
      </style>
      <div class="panel" part="panel">
        <div class="head"><span class="title">Administrators</span><span class="base" part="base"></span></div>
        <div class="err"></div>
        <div class="list" part="list"></div>
        <div class="add" part="add" hidden>
          <arazzo-grantee-picker class="grant-in"></arazzo-grantee-picker>
          <button class="addbtn primary" type="button">Add</button>
        </div>
      </div>
    `;
    this.$('.addbtn').addEventListener('click', () => this.add());
  }

  renderBody() {
    const base = this.$('.base');
    if (base) base.textContent = this.baseWorkflowId || '';
    const err = this.$('.err');
    const list = this.$('.list');
    const add = this.$('.add');
    if (!list) return;

    err.innerHTML = this._error
      ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
      : '';

    add.hidden = !this.canWrite || !this.baseWorkflowId;

    if (this._loading && this._admins.length === 0) {
      list.innerHTML = '<div class="skl"></div><div class="skl"></div>';
      return;
    }
    if (this._admins.length === 0) {
      list.innerHTML = `<div class="empty">${this.baseWorkflowId ? 'No administration established for this workflow.' : 'Select a workflow to manage its administrators.'}</div>`;
      return;
    }

    list.innerHTML = this._admins.map((a) => {
      const describe = a.label || (a.identity || []).map((g) => `${g.dimension}=${g.value}`).join(', ') || 'this administrator';
      return `
      <div class="arow" part="row">
        ${granteeChip(a)}
        <span class="grow"></span>
        ${this.canWrite ? `<button class="rm ghost" type="button" data-digest="${escapeHtml(a.digest)}" data-describe="${escapeHtml(describe)}">Remove</button>` : ''}
      </div>`;
    }).join('');
    this.$$('.rm').forEach((btn) => btn.addEventListener('click', () => this.removeMember(btn.dataset.digest, btn.dataset.describe)));
  }
}

define('arazzo-administrators-panel', ArazzoAdministratorsPanel);
export { ArazzoAdministratorsPanel };
