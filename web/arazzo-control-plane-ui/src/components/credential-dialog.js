// <arazzo-credential-dialog> — create or edit a source credential binding (§13).
//
//   const dlg = document.querySelector('arazzo-credential-dialog');
//   dlg.client = client;
//   dlg.open();                 // create
//   dlg.open(bindingSummary);   // edit / rotate
//
// Properties : .client
// Methods    : open(binding?), close()
// Events     : credential-saved {binding}, error {problem}
//
// The form edits REFERENCES and non-secret metadata only — a `secretRef` (scheme://locator[#version]) plus
// auth kind, config, usage grants, and lifecycle dates. It never accepts secret material: a value without a
// known scheme is refused before the request (the same boundary the server enforces). On edit it is a merge
// over the current binding — re-pointing a reference is a rotation, so it stamps `rotatedAt`. Management tags
// and usage grants are immutable across updates, so they are shown read-only when editing.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

const SECRET_REF = /^(keyvault|awssm|vault|env|file):\/\/.+/;
const AUTH_KINDS = ['apiKey', 'bearer', 'basic', 'oauth2ClientCredentials', 'mtls'];

class ArazzoCredentialDialog extends ArazzoElement {
  connectedCallback() {
    if (!this._built) this.render();
  }

  /** Open to create (no argument) or edit/rotate (a {@link CredentialBindingSummary}). */
  open(binding = null) {
    if (!this._built) this.render();
    this._editing = binding || null;
    this._originalRefs = binding ? (binding.secretRefs || []).map((r) => `${r.name}=${r.ref}`).sort().join('\n') : '';
    this.$('form').reset();
    this.$('.error-banner').hidden = true;
    this.fill(binding);
    this.$('.title').textContent = binding ? `Edit ${binding.sourceName}@${binding.environment}` : 'New credential binding';
    this.$('.confirm').textContent = binding ? 'Save' : 'Create';
    this.$('dialog').showModal();
  }

  close() {
    this.$('dialog')?.close();
  }

  fill(b) {
    const ro = !!b; // source/env are the immutable identity when editing
    this.$('#sourceName').value = b?.sourceName || '';
    this.$('#environment').value = b?.environment || '';
    this.$('#sourceName').readOnly = ro;
    this.$('#environment').readOnly = ro;
    this.$('#authKind').value = b?.authKind || 'apiKey';
    this.$('#description').value = b?.description || '';
    this.$('#expiresAt').value = b?.expiresAt ? String(b.expiresAt).slice(0, 10) : '';

    this.$('.refs').innerHTML = '';
    const refs = b?.secretRefs?.length ? b.secretRefs : [{ name: 'value', ref: '' }];
    for (const r of refs) this.addRow('.refs', 'ref', r.name, r.ref);

    this.$('.config').innerHTML = '';
    for (const c of b?.config || []) this.addRow('.config', 'cfg', c.key, c.value);

    // Usage grants + management tags are set at create and immutable on update — show read-only when editing.
    this.$('fieldset.create-only').hidden = ro;
    this.$('.scopes-readonly').hidden = !ro;
    if (ro) {
      const grants = (b.usageGrants || []).map((g) => `${g.dimension}=${g.value}`).join(', ') || '—';
      const mgmt = (b.managementTags || []).map((t) => `${t.key}=${t.value}`).join(', ') || '—';
      this.$('.scopes-readonly').innerHTML = `<div><span class="ro-label">Usage grants</span> ${escapeHtml(grants)}</div><div><span class="ro-label">Management tags</span> ${escapeHtml(mgmt)}</div>`;
    } else {
      this.$('.grants').innerHTML = '';
      this.$('.mgmt').innerHTML = '';
    }
  }

  /** Append a removable two-input row (name/ref or key/value) to a list container. */
  addRow(container, kind, a = '', b = '') {
    const placeholders = {
      ref: ['role (e.g. value)', 'scheme://locator[#version]'],
      cfg: ['key', 'value'],
      grant: ['dimension (e.g. workflow)', 'value'],
      mgmt: ['key', 'value'],
    }[kind];
    const row = document.createElement('div');
    row.className = 'row';
    row.dataset.kind = kind;
    row.innerHTML = `
      <input class="a" type="text" placeholder="${placeholders[0]}" value="${escapeHtml(a)}">
      <input class="b" type="text" placeholder="${placeholders[1]}" value="${escapeHtml(b)}">
      <button class="rm ghost" type="button" title="Remove" aria-label="Remove">✕</button>`;
    row.querySelector('.rm').addEventListener('click', () => row.remove());
    this.$(container).appendChild(row);
  }

  collect(container) {
    const out = [];
    for (const row of this.$$(`${container} .row`)) {
      const a = row.querySelector('.a').value.trim();
      const b = row.querySelector('.b').value.trim();
      if (a || b) out.push([a, b]);
    }
    return out;
  }

  buildBody() {
    const sourceName = this.$('#sourceName').value.trim();
    const environment = this.$('#environment').value.trim();
    if (!sourceName || !environment) throw new Error('A source name and environment are required.');
    const authKind = this.$('#authKind').value.trim();
    if (!authKind) throw new Error('An auth kind is required.');

    const refs = this.collect('.refs');
    if (refs.length === 0) throw new Error('At least one secret reference is required.');
    const secretRefs = refs.map(([name, ref]) => {
      if (!name) throw new Error('Each secret reference needs a role name.');
      if (!SECRET_REF.test(ref)) throw new Error(`'${ref || '(empty)'}' is not a reference. Use scheme://locator[#version] (keyvault, awssm, vault, env, file) — never an inline secret.`);
      return { name, ref };
    });

    const config = this.collect('.config').map(([key, value]) => ({ key, value }));
    const description = this.$('#description').value.trim() || undefined;
    const expiresAtDate = this.$('#expiresAt').value;
    const expiresAt = expiresAtDate ? new Date(`${expiresAtDate}T00:00:00Z`).toISOString() : undefined;

    const body = { sourceName, environment, authKind, secretRefs };
    if (config.length) body.config = config;
    if (description) body.description = description;
    if (expiresAt) body.expiresAt = expiresAt;

    if (this._editing) {
      // A reference change is a rotation — stamp rotatedAt unless the operator's binding already had one moved.
      const nowRefs = secretRefs.map((r) => `${r.name}=${r.ref}`).sort().join('\n');
      if (nowRefs !== this._originalRefs) body.rotatedAt = new Date().toISOString();
      else if (this._editing.rotatedAt) body.rotatedAt = this._editing.rotatedAt;
    } else {
      const grants = this.collect('.grants').map(([dimension, value]) => ({ dimension, value }));
      const mgmt = this.collect('.mgmt').map(([key, value]) => ({ key, value }));
      if (grants.length) body.usageGrants = grants;
      if (mgmt.length) body.managementTags = mgmt;
    }
    return body;
  }

  async submit() {
    const banner = this.$('.error-banner');
    banner.hidden = true;
    let body;
    try {
      body = this.buildBody();
    } catch (err) {
      banner.textContent = err.message;
      banner.hidden = false;
      return;
    }

    const confirmBtn = this.$('.confirm');
    confirmBtn.disabled = true;
    try {
      const binding = this._editing
        ? await this.client.updateCredential(this._editing.sourceName, this._editing.environment, body)
        : await this.client.createCredential(body);
      this.close();
      this.emit('credential-saved', { binding });
    } catch (err) {
      const problem = err.problem || { title: err.message };
      banner.textContent = `${problem.title || 'Save failed'}${problem.detail ? ' — ' + problem.detail : ''}`;
      banner.hidden = false;
      this.emit('error', { problem, error: err });
    } finally {
      confirmBtn.disabled = false;
    }
  }

  render() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(620px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .head { padding: 14px 16px; border-bottom: 1px solid var(--_border); }
        .title { font-weight: 700; font-size: 15px; }
        .subhead { color: var(--_muted); font-size: 12px; margin-top: 2px; }
        .content { padding: 16px; display: grid; gap: 14px; max-height: 64vh; overflow: auto; }
        fieldset { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 12px; margin: 0; display: grid; gap: 10px; }
        legend { font-size: 12px; font-weight: 600; color: var(--_muted); padding: 0 4px; }
        label { font-size: 12px; color: var(--_muted); display: block; margin-bottom: 4px; }
        .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
        input[type="text"], input[type="date"], select { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        input[readonly] { background: var(--_surface); color: var(--_muted); }
        .row { display: grid; grid-template-columns: 1fr 1.4fr auto; gap: 8px; align-items: center; }
        .row .rm { padding: 4px 10px; }
        .add { justify-self: start; font-size: 12px; }
        .ro-label { color: var(--_muted); font-size: 12px; margin-right: 6px; }
        .scopes-readonly { display: grid; gap: 4px; font-size: 13px; }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <dialog part="dialog">
        <form method="dialog">
          <div class="head">
            <div class="title">New credential binding</div>
            <div class="subhead">References and non-secret metadata only — a <code>secretRef</code> points at your secret store; secret material is never entered here.</div>
          </div>
          <div class="content">
            <fieldset>
              <legend>Identity</legend>
              <div class="grid2">
                <div><label for="sourceName">Source name *</label><input id="sourceName" type="text" placeholder="petstore"></div>
                <div><label for="environment">Environment *</label><input id="environment" type="text" placeholder="production"></div>
              </div>
              <div class="grid2">
                <div><label for="authKind">Auth kind *</label><input id="authKind" type="text" list="authkinds" placeholder="apiKey"><datalist id="authkinds">${AUTH_KINDS.map((k) => `<option value="${k}">`).join('')}</datalist></div>
                <div><label for="expiresAt">Expires</label><input id="expiresAt" type="date"></div>
              </div>
              <div><label for="description">Description</label><input id="description" type="text" placeholder="Petstore API key."></div>
            </fieldset>

            <fieldset>
              <legend>Secret references *</legend>
              <div class="refs"></div>
              <button class="add ghost addref" type="button">+ Add reference</button>
            </fieldset>

            <fieldset>
              <legend>Config (non-secret)</legend>
              <div class="config"></div>
              <button class="add ghost addcfg" type="button">+ Add config entry</button>
            </fieldset>

            <fieldset class="create-only">
              <legend>Scopes (set at create, immutable after)</legend>
              <div><label>Usage grants — which workflow identity may use the binding</label><div class="grants"></div><button class="add ghost addgrant" type="button">+ Add grant</button></div>
              <div><label>Management tags — who may administer the binding</label><div class="mgmt"></div><button class="add ghost addmgmt" type="button">+ Add tag</button></div>
            </fieldset>

            <div class="scopes-readonly" hidden></div>

            <div class="error-banner" hidden></div>
          </div>
          <div class="foot">
            <button value="dismiss" class="ghost" type="submit">Cancel</button>
            <button value="confirm" class="primary confirm" type="submit">Create</button>
          </div>
        </form>
      </dialog>
    `;
    this.$('.addref').addEventListener('click', () => this.addRow('.refs', 'ref'));
    this.$('.addcfg').addEventListener('click', () => this.addRow('.config', 'cfg'));
    this.$('.addgrant').addEventListener('click', () => this.addRow('.grants', 'grant'));
    this.$('.addmgmt').addEventListener('click', () => this.addRow('.mgmt', 'mgmt'));
    this.$('form').addEventListener('submit', (e) => {
      if (e.submitter?.value === 'confirm') { e.preventDefault(); this.submit(); }
    });
  }
}

define('arazzo-credential-dialog', ArazzoCredentialDialog);
export { ArazzoCredentialDialog };
