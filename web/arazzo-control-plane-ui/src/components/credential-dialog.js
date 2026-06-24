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
//
// The secret references are DRIVEN BY THE AUTH KIND: each kind resolves a fixed set of role-named secrets (the
// runner-side `SourceCredentialProviderFactory`), so the form shows exactly those slots, labelled, with the role
// fixed — not a free-text role and an open-ended "+ Add". And because the control plane never connects to a
// secret store (§13.5), the UI cannot browse it; each slot is entered through GUIDED, per-store fields (the
// locator shape differs per store) that compose the canonical `secretRef` and preview exactly what is stored,
// with a "Raw reference…" escape hatch for a value that does not fit the guided shape.

import { ArazzoElement, SHARED_CSS, GRANTEE_CHIP_CSS, granteeChip, escapeHtml, define } from './base.js';
import './grantee-picker.js';

const SECRET_REF = /^(keyvault|awssm|vault|env|file):\/\/.+/;

// The supported auth kinds and the secret slot(s) each one consumes (role = the `secretRefs[].name` the runner
// resolves; label = plain-language name for the slot). mTLS is intentionally absent — it is not implemented (the
// durability `SourceCredentialKind` would reject it) and needs two slots (certificate + key); see design §13.1.
const SLOTS = {
  apiKey: [{ role: 'value', label: 'API key' }],
  bearer: [{ role: 'value', label: 'Bearer token' }],
  basic: [{ role: 'password', label: 'Password' }],
  oauth2ClientCredentials: [{ role: 'clientSecret', label: 'Client secret' }],
};
const AUTH_KINDS = Object.keys(SLOTS);

// The non-secret config each auth kind reads (the runner-side SourceCredentialProviderFactory). Like the secret
// slots, the Config section is driven by the auth kind — exactly these labelled fields — with an "additional
// config" escape hatch for anything else. `options` renders a <select> (empty = the runner's default).
const CONFIG = {
  apiKey: [
    { key: 'parameterName', label: 'Header / parameter name', placeholder: 'X-API-Key', required: false },
    { key: 'location', label: 'Location', options: ['header', 'query', 'cookie'], required: false },
  ],
  bearer: [],
  basic: [
    { key: 'username', label: 'Username', placeholder: 'svc-account', required: true },
  ],
  oauth2ClientCredentials: [
    { key: 'tokenUrl', label: 'Token URL', placeholder: 'https://idp.example.com/oauth/token', required: true },
    { key: 'clientId', label: 'Client id', placeholder: 'my-client', required: true },
    { key: 'scope', label: 'Scope', placeholder: 'optional', required: false },
    { key: 'clientAuthentication', label: 'Client authentication', options: ['body', 'basic'], required: false },
  ],
};

// The per-store reference grammar (authoritative — matches the runner-side resolvers). Each scheme declares the
// fields a user fills, how they compose into `scheme://locator[#version]`, how an existing reference parses back
// into those fields (null ⇒ cannot be represented, fall back to raw), and a worked example.
const SCHEMES = {
  keyvault: {
    label: 'Azure Key Vault',
    fields: [
      { key: 'host', label: 'Vault host', placeholder: 'petstore-kv (or petstore-kv.vault.azure.net)', required: true },
      { key: 'name', label: 'Secret name', placeholder: 'api-key', required: true },
      { key: 'version', label: 'Version', placeholder: 'optional', required: false },
    ],
    compose: (v) => `keyvault://${v.host}/${v.name}${v.version ? `#${v.version}` : ''}`,
    parse: (locator, version) => {
      const slash = locator.indexOf('/');
      return slash > 0 && slash < locator.length - 1
        ? { host: locator.slice(0, slash), name: locator.slice(slash + 1), version }
        : null;
    },
    example: 'keyvault://petstore-kv/api-key#a1b2c3',
  },
  awssm: {
    label: 'AWS Secrets Manager',
    fields: [
      { key: 'id', label: 'Secret id or ARN', placeholder: 'petstore/api-key  (or arn:aws:secretsmanager:…)', required: true },
      { key: 'version', label: 'Version id', placeholder: 'optional', required: false },
    ],
    compose: (v) => `awssm://${v.id}${v.version ? `#${v.version}` : ''}`,
    parse: (locator, version) => (locator ? { id: locator, version } : null),
    example: 'awssm://petstore/api-key',
  },
  vault: {
    label: 'HashiCorp Vault (KV v2)',
    fields: [
      { key: 'mount', label: 'Mount', placeholder: 'secret', required: true },
      { key: 'path', label: 'Path', placeholder: 'arazzo/petstore', required: true },
      { key: 'field', label: 'Field', placeholder: 'api-key', required: true },
    ],
    compose: (v) => `vault://${v.mount}/${v.path}#${v.field}`,
    parse: (locator, version) => {
      const slash = locator.indexOf('/');
      return slash > 0 && slash < locator.length - 1 && version
        ? { mount: locator.slice(0, slash), path: locator.slice(slash + 1), field: version }
        : null;
    },
    example: 'vault://secret/arazzo/petstore#api-key',
  },
  env: {
    label: 'Environment variable',
    fields: [{ key: 'var', label: 'Variable name', placeholder: 'PETSTORE_API_KEY', required: true }],
    compose: (v) => `env://${v.var}`,
    parse: (locator) => (locator ? { var: locator } : null),
    example: 'env://PETSTORE_API_KEY',
  },
  file: {
    label: 'File path',
    fields: [{ key: 'path', label: 'File path', placeholder: '/var/run/secrets/petstore', required: true }],
    compose: (v) => `file://${v.path}`,
    parse: (locator) => (locator ? { path: locator } : null),
    example: 'file:///var/run/secrets/petstore',
  },
};

/** Split a reference into its guided scheme + field values, or `{ scheme: 'raw', raw }` when it cannot be represented. */
function parseRef(ref) {
  const m = /^([a-z0-9]+):\/\/(.*)$/i.exec(ref || '');
  const spec = m && SCHEMES[m[1].toLowerCase()];
  if (!spec) return { scheme: 'raw', raw: ref || '' };
  const rest = m[2];
  const hash = rest.indexOf('#');
  const values = spec.parse(hash >= 0 ? rest.slice(0, hash) : rest, hash >= 0 ? rest.slice(hash + 1) : '');
  return values ? { scheme: m[1].toLowerCase(), values } : { scheme: 'raw', raw: ref };
}

class ArazzoCredentialDialog extends ArazzoElement {
  connectedCallback() {
    if (!this._built) this.render();
  }

  /**
   * Open to create (no argument), edit/rotate (a {@link CredentialBindingSummary}), or create for a fixed source
   * (`open(null, { sourceName, lockSource: true })`) — the source-credential setup launched from adding a workflow.
   */
  open(binding = null, { sourceName, lockSource = false } = {}) {
    if (!this._built) this.render();
    this._editing = binding || null;
    this._prefillSource = binding ? null : (sourceName || null);
    this._lockSource = !binding && lockSource && !!sourceName;
    this._originalRefs = binding ? (binding.secretRefs || []).map((r) => `${r.name}=${r.ref}`).sort().join('\n') : '';
    this.$('form').reset();
    this.$('.error-banner').hidden = true;
    this.fill(binding);
    // The usage picker resolves real grantees via the client (§16.5.4); share the dialog's client and clear any prior pick.
    const picker = this.$('.usage-grantee');
    if (picker) {
      if (this.client) picker.client = this.client;
      picker.reset?.();
    }
    this.$('.title').textContent = binding
      ? `Edit ${binding.sourceName}@${binding.environment}`
      : (this._prefillSource ? `New credential for ${this._prefillSource}` : 'New credential binding');
    this.$('.confirm').textContent = binding ? 'Save' : 'Create';
    this.$('dialog').showModal();
  }

  close() {
    this.$('dialog')?.close();
  }

  get authKind() {
    return this.$('#authKind').value.trim();
  }

  fill(b) {
    const ro = !!b; // source/env are the immutable identity when editing
    this.$('#sourceName').value = b?.sourceName || this._prefillSource || '';
    this.$('#environment').value = b?.environment || '';
    this.$('#sourceName').readOnly = ro || this._lockSource; // locked when creating for a fixed source
    this.$('#environment').readOnly = ro;
    this.$('#authKind').value = b?.authKind || 'apiKey';
    this.$('#description').value = b?.description || '';
    this.$('#expiresAt').value = b?.expiresAt ? String(b.expiresAt).slice(0, 10) : '';

    const refsByRole = new Map();
    for (const r of b?.secretRefs || []) refsByRole.set(r.name, r.ref);
    this.renderRefs(refsByRole);

    this.renderConfig(new Map((b?.config || []).map((c) => [c.key, c.value])));

    // Usage grants + management tags are set at create and immutable on update — show read-only when editing.
    this.$('fieldset.create-only').hidden = ro;
    this.$('.scopes-readonly').hidden = !ro;
    if (ro) {
      const usage = b.usageGrantee && Array.isArray(b.usageGrantee.identity) && b.usageGrantee.identity.length
        ? granteeChip(b.usageGrantee)
        : '—';
      const mgmt = (b.managementTags || []).map((t) => `${t.key}=${t.value}`).join(', ') || '—';
      this.$('.scopes-readonly').innerHTML = `<div><span class="ro-label">Usage</span> ${usage}</div><div><span class="ro-label">Management tags</span> ${escapeHtml(mgmt)}</div>`;
    } else {
      this.$('.mgmt').innerHTML = '';
    }
  }

  /** Render the reference slots for the current auth kind, filling each from `refsByRole` and preserving extras. */
  renderRefs(refsByRole = new Map()) {
    const kind = this.authKind;
    const slots = SLOTS[kind];
    const refs = this.$('.refs');
    refs.innerHTML = '';

    if (!slots) {
      refs.innerHTML = `<div class="muted refhint">Choose a supported auth kind (${AUTH_KINDS.join(', ')}) to enter its secret.</div>`;
      return;
    }

    const used = new Set();
    for (const slot of slots) {
      this.addRefRow({ role: slot.role, label: slot.label, ref: refsByRole.get(slot.role) || '', required: true });
      used.add(slot.role);
    }
    // Preserve any existing references this kind does not consume (legacy / wrong-role) so a save never drops them.
    for (const [role, ref] of refsByRole) {
      if (role && !used.has(role)) {
        this.addRefRow({ role, label: role, ref, removable: true, note: '· not used by this auth kind' });
      }
    }
  }

  /** Append a guided reference slot: a fixed role + label, a per-store scheme picker, and a live canonical preview. */
  addRefRow({ role, label, ref = '', required = false, removable = false, note = '' }) {
    const parsed = ref ? parseRef(ref) : { scheme: 'keyvault', values: {} };
    const row = document.createElement('div');
    row.className = 'refrow';
    row.dataset.role = role;
    row.dataset.label = label;
    if (required) row.dataset.required = 'true';
    const schemeOptions = Object.entries(SCHEMES)
      .map(([k, s]) => `<option value="${k}"${k === parsed.scheme ? ' selected' : ''}>${escapeHtml(s.label)}</option>`)
      .join('') + `<option value="raw"${parsed.scheme === 'raw' ? ' selected' : ''}>Raw reference…</option>`;
    row.innerHTML = `
      <div class="reftop">
        <div class="slot-label">${escapeHtml(label)}${required ? ' *' : ''}${note ? ` <span class="muted">${escapeHtml(note)}</span>` : ''}</div>
        <select class="scheme" aria-label="secret store for ${escapeHtml(label)}">${schemeOptions}</select>
        ${removable ? `<button class="rm ghost" type="button" title="Remove" aria-label="Remove">✕</button>` : '<span></span>'}
      </div>
      <div class="reffields"></div>
      <div class="refpreview muted"></div>`;
    if (removable) row.querySelector('.rm').addEventListener('click', () => row.remove());
    row.querySelector('.scheme').addEventListener('change', () => this.renderRefFields(row));
    this.$('.refs').appendChild(row);
    this.renderRefFields(row, parsed);
  }

  /** (Re)build a reference row's fields for its selected scheme, wire live preview, and seed values when provided. */
  renderRefFields(row, seed = null) {
    const scheme = row.querySelector('.scheme').value;
    const fields = row.querySelector('.reffields');

    if (scheme === 'raw') {
      const raw = seed?.scheme === 'raw' ? seed.raw : '';
      fields.innerHTML = `<div class="reffield wide"><label>Reference</label><input class="rawref" type="text" placeholder="scheme://locator[#version]" aria-label="raw reference" value="${escapeHtml(raw)}"></div>`;
      fields.querySelector('.rawref').addEventListener('input', () => this.updatePreview(row));
      this.updatePreview(row);
      return;
    }

    const spec = SCHEMES[scheme];
    const values = seed?.scheme === scheme ? seed.values : {};
    fields.innerHTML = spec.fields.map((f) => `
      <div class="reffield">
        <label>${escapeHtml(f.label)}${f.required ? ' *' : ''}</label>
        <input data-key="${f.key}" type="text" placeholder="${escapeHtml(f.placeholder)}" aria-label="${escapeHtml(f.label)}" value="${escapeHtml(values[f.key] || '')}">
      </div>`).join('');
    fields.querySelectorAll('input').forEach((i) => i.addEventListener('input', () => this.updatePreview(row)));
    this.updatePreview(row);
  }

  /** The composed reference for a row, or `null` if a guided row is missing a required field. */
  refRowRef(row) {
    const scheme = row.querySelector('.scheme').value;
    if (scheme === 'raw') return row.querySelector('.rawref')?.value.trim() || '';
    const spec = SCHEMES[scheme];
    const values = {};
    for (const f of spec.fields) values[f.key] = (row.querySelector(`[data-key="${f.key}"]`)?.value || '').trim();
    if (spec.fields.some((f) => f.required && !values[f.key])) return null;
    return spec.compose(values);
  }

  /** Show the exact reference a row will store (live), or a hint + example while it is incomplete. */
  updatePreview(row) {
    const preview = row.querySelector('.refpreview');
    if (!preview) return;
    const scheme = row.querySelector('.scheme').value;
    if (scheme === 'raw') { preview.textContent = ''; return; }
    const ref = this.refRowRef(row);
    preview.innerHTML = ref
      ? `→ <code>${escapeHtml(ref)}</code>`
      : `Fill the fields above — e.g. <code>${escapeHtml(SCHEMES[scheme].example)}</code>`;
  }

  /** Snapshot the current reference rows as role → composed reference, so values survive an auth-kind switch. */
  snapshotRefs() {
    const map = new Map();
    for (const row of this.$$('.refs .refrow')) {
      const role = row.dataset.role;
      const ref = this.refRowRef(row);
      if (role && ref) map.set(role, ref);
    }
    return map;
  }

  /** Render the non-secret config fields for the current auth kind, filling each from `configByKey`, plus any extras. */
  renderConfig(configByKey = new Map()) {
    const kind = this.authKind;
    const fields = CONFIG[kind] || [];
    const host = this.$('.config-fields');
    host.innerHTML = fields.map((f) => {
      const val = configByKey.get(f.key) || '';
      const control = f.options
        ? `<select data-cfg="${f.key}"><option value=""${val ? '' : ' selected'}>(default)</option>${f.options.map((o) => `<option value="${escapeHtml(o)}"${o === val ? ' selected' : ''}>${escapeHtml(o)}</option>`).join('')}</select>`
        : `<input data-cfg="${f.key}" type="text" placeholder="${escapeHtml(f.placeholder || '')}" aria-label="${escapeHtml(f.label)}" value="${escapeHtml(val)}">`;
      return `<div class="cfg-field"><label>${escapeHtml(f.label)}${f.required ? ' *' : ''}</label>${control}</div>`;
    }).join('') || '<div class="muted">This auth kind needs no extra config.</div>';

    // Preserve any config keys this kind does not define (legacy / custom) as editable key/value rows.
    const known = new Set(fields.map((f) => f.key));
    this.$('.config-extra').innerHTML = '';
    for (const [key, value] of configByKey) {
      if (!known.has(key)) this.addRow('.config-extra', 'cfg', key, value);
    }
  }

  /** Snapshot the current config (kind fields + extras) as key → value, so values survive an auth-kind switch. */
  snapshotConfig() {
    const map = new Map();
    for (const el of this.$$('.config-fields [data-cfg]')) {
      const v = el.value.trim();
      if (v) map.set(el.dataset.cfg, v);
    }
    for (const [key, value] of this.collect('.config-extra')) map.set(key, value);
    return map;
  }

  /** Gather the config entries, requiring the kind's required fields. */
  collectConfig() {
    const out = [];
    for (const f of CONFIG[this.authKind] || []) {
      const value = (this.$(`.config-fields [data-cfg="${f.key}"]`)?.value || '').trim();
      if (f.required && !value) throw new Error(`${f.label} is required for ${this.authKind}.`);
      if (value) out.push({ key: f.key, value });
    }
    for (const [key, value] of this.collect('.config-extra')) out.push({ key, value });
    return out;
  }

  /** Append a removable two-input row (key/value) to a list container (config / grants / management tags). */
  addRow(container, kind, a = '', b = '') {
    const placeholders = {
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

  /** Gather the secret references from the auth-kind slots, validating each composes to a well-formed reference. */
  collectRefs() {
    const out = [];
    for (const row of this.$$('.refs .refrow')) {
      const name = row.dataset.role;
      if (!name) continue;
      const ref = this.refRowRef(row);
      if ((ref === '' || ref === null) && row.dataset.required !== 'true') continue; // an emptied, optional extra
      if (ref === '' || ref === null) {
        const scheme = row.querySelector('.scheme').value;
        const missing = scheme === 'raw'
          ? 'a reference'
          : SCHEMES[scheme].fields.filter((f) => f.required).map((f) => f.label.toLowerCase()).join(', ');
        throw new Error(`The ${row.dataset.label} reference is incomplete — fill ${missing}.`);
      }
      if (!SECRET_REF.test(ref)) {
        throw new Error(`'${ref}' is not a reference. Use scheme://locator[#version] (keyvault, awssm, vault, env, file) — never an inline secret.`);
      }
      out.push({ name, ref });
    }
    return out;
  }

  buildBody() {
    const sourceName = this.$('#sourceName').value.trim();
    const environment = this.$('#environment').value.trim();
    if (!sourceName || !environment) throw new Error('A source name and environment are required.');
    const authKind = this.authKind;
    if (!SLOTS[authKind]) throw new Error(`Choose a supported auth kind: ${AUTH_KINDS.join(', ')}.`);

    const secretRefs = this.collectRefs();
    if (secretRefs.length === 0) throw new Error('At least one secret reference is required.');

    const config = this.collectConfig();
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
      // Usage is a single resolved grantee from the picker — its identity (AND-matched) scopes which runs may use the
      // binding; kind/label are carried for display. Management tags stay the {key,value} rows.
      const grantee = this.$('.usage-grantee')?.grant;
      if (grantee) {
        body.usageGrantee = { identity: grantee.identity, kind: grantee.kind, label: grantee.label };
      }

      const mgmt = this.collect('.mgmt').map(([key, value]) => ({ key, value }));
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
        ${GRANTEE_CHIP_CSS}
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
        .row .rm, .reftop .rm { padding: 4px 10px; }
        .refrow { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 10px; display: grid; gap: 8px; background: var(--_bg); }
        .reftop { display: grid; grid-template-columns: 1fr 1.2fr auto; gap: 8px; align-items: center; }
        .slot-label { font-weight: 600; font-size: 13px; }
        .slot-label .muted { font-weight: 400; }
        .reffields { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 8px; }
        .reffield.wide { grid-column: 1 / -1; }
        .reffield label { margin-bottom: 2px; }
        .refpreview { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; color: var(--_muted); word-break: break-all; }
        .refpreview code { background: var(--_surface); padding: 1px 4px; border-radius: 4px; }
        .refhint { padding: 8px 2px; }
        .config-fields { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
        .config-fields:empty { display: none; }
        .cfg-field { display: grid; gap: 4px; }
        .cfg-field label { margin-bottom: 0; }
        .config-extra { display: grid; gap: 8px; }
        .config-extra:empty { display: none; }
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
              <div class="subhead">The auth kind sets which secret(s) are needed. Pick each secret's store and fill its fields — the canonical <code>secretRef</code> is composed and previewed. The control plane stores the reference only; it never reads the secret.</div>
              <div class="refs"></div>
            </fieldset>

            <fieldset>
              <legend>Config (non-secret)</legend>
              <div class="subhead">The settings the auth kind needs alongside its secret — endpoint URLs, header names, usernames. Non-secret; stored as-is.</div>
              <div class="config-fields"></div>
              <div class="config-extra"></div>
              <button class="add ghost addcfg" type="button">+ Add another config entry</button>
            </fieldset>

            <fieldset class="create-only">
              <legend>Scopes (set at create, immutable after)</legend>
              <div><label>Usage — which grantee's runs may use the binding (resolve a real person/team/role/workflow)</label><arazzo-grantee-picker class="usage-grantee"></arazzo-grantee-picker></div>
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
    // The auth kind drives both the secret slots and the config fields; re-render on change, preserving entries.
    this.$('#authKind').addEventListener('change', () => {
      this.renderRefs(this.snapshotRefs());
      this.renderConfig(this.snapshotConfig());
    });
    this.$('.addcfg').addEventListener('click', () => this.addRow('.config-extra', 'cfg'));
    this.$('.addmgmt').addEventListener('click', () => this.addRow('.mgmt', 'mgmt'));
    this.$('form').addEventListener('submit', (e) => {
      if (e.submitter?.value === 'confirm') { e.preventDefault(); this.submit(); }
    });
    // A close signal (save or cancel) so a caller can sequence dialogs — e.g. set up one source's credentials
    // after another when adding a workflow.
    this.$('dialog').addEventListener('close', () => this.emit('credential-dialog-closed'));
  }
}

define('arazzo-credential-dialog', ArazzoCredentialDialog);
export { ArazzoCredentialDialog };