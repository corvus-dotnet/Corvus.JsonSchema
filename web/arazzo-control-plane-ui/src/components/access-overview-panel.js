// <arazzo-access-overview> — the "who can do what, where" overview for one resolved grantee (design §6.1). Pick a
// grantee with the shared picker, then see all of their access in one place: their reach GRANTS (a claim to per-verb
// reach, with inline Revoke), the CAPABILITY scopes those grants confer (active vs PIM-eligible, server-resolved),
// the base workflows and ENVIRONMENTS they ADMINISTER, and the source credentials their runs may USE. The
// server aggregates it (GET /access/grants), so the client stays thin. Read-first; the only mutation is revoking a
// grant (security:write), which deletes the underlying claim binding for every principal it matches.
//
//   <arazzo-access-overview base-url="/arazzo/v1" scopes="security:read security:write"></arazzo-access-overview>
//   el.client = client;
//
// Attributes : base-url, scopes (gates Revoke)
// Properties : .client
// Events     : grantee-selected {grantee}, revoked {bindingId}, open-workflow {baseWorkflowId}, open-environment {environment},
//              open-credential {sourceName, environment}, error {problem}

import { ArazzoElement, SHARED_CSS, GRANTEE_CHIP_CSS, granteeChip, escapeHtml, confirmDialog, define } from './base.js';
import './grantee-picker.js';

const VERBS = ['read', 'write', 'purge'];
const grantMode = (g) => (g?.unrestricted ? 'unrestricted' : (Array.isArray(g?.ruleNames) && g.ruleNames.length > 0 ? 'scopes' : 'denied'));
const grantSummary = (g) => {
  const mode = grantMode(g);
  if (mode === 'unrestricted') return 'Unrestricted';
  if (mode === 'scopes') return g.ruleNames.join(', ');
  return 'Denied';
};

const EMPTY = 'Pick a grantee to see everything they can do, and where.';

class ArazzoAccessOverview extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes'];
  }

  connectedCallback() {
    this.render();
  }

  attributeChangedCallback(name) {
    if (name === 'scopes' && this._built) {
      const picker = this.$('arazzo-grantee-picker');
      if (picker) picker.setAttribute('scopes', this.getAttribute('scopes') || '');
    }
  }

  hasScope(scope) {
    // Kit-wide convention (same as the grants/rules/environments panels): an ABSENT scopes
    // attribute means "do not gate here — the server is the authority". Treating absent as
    // nothing-held hid the inline Revoke from privileged callers in hosts that set no scopes
    // attributes (the sample shell does this by design).
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes(scope);
  }

  /** Forward a late-set client to the nested grantee picker (the base .client setter calls this when it changes). */
  requestRender() {
    const picker = this.$('arazzo-grantee-picker');
    if (picker && picker.client !== this.client) picker.client = this.client;
  }

  /**
   * Re-run the aggregation for the selected grantee. The demo host calls refresh() on every panel in
   * a view when its tab is activated, so a grants change made under another tab (revoke, approval,
   * delete) is reflected here rather than leaving a stale overview on screen.
   */
  refresh() {
    if (this._grantee) this.loadOverview(this._grantee);
  }

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${GRANTEE_CHIP_CSS}
        :host { display: flex; flex-direction: column; min-height: 0; height: 100%; }
        .find { flex: none; display: flex; gap: 10px; align-items: center; margin-bottom: 16px; }
        .find label { font-weight: 600; white-space: nowrap; }
        .find arazzo-grantee-picker { flex: 1; min-width: 0; }
        .body { flex: 1; min-height: 0; overflow: auto; }
        .empty { color: var(--_muted); padding: 12px 0; }
        .who { display: flex; align-items: center; gap: 10px; margin-bottom: 16px; }
        h4 { margin: 20px 0 8px; font-size: 12px; letter-spacing: 0.04em; text-transform: uppercase; color: var(--_muted); }
        .section:first-of-type h4 { margin-top: 0; }
        .grant { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 9px 11px; margin-bottom: 8px; }
        .grant-head { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
        .grant-head .claim { font-weight: 600; }
        .grant-head .grow { flex: 1; }
        .verb { display: flex; gap: 10px; font-size: 13px; padding: 2px 0; }
        .verb .v { width: 46px; color: var(--_muted); }
        .verb .denied { color: var(--_muted); }
        /* Unrestricted reach is the value a reviewer should slow down on — same amber alert as the grants list. */
        .verb .wide { color: var(--arazzo-status-suspended, #b07d18); font-weight: 700; }
        .row { display: flex; align-items: center; gap: 8px; font-size: 13px; padding: 6px 0; border-bottom: 1px solid var(--_border); }
        .row:last-child { border-bottom: none; }
        .row .grow { flex: 1; }
        .row .sub { color: var(--_muted); font-size: 12px; }
        .caps { display: flex; flex-wrap: wrap; gap: 6px; padding: 4px 0; }
        .cap { border: 1px solid var(--_border); border-radius: 999px; padding: 3px 10px; font-size: 12px; }
        .cap.eligible { border-style: dashed; color: var(--_muted); }
        .cap .until { color: var(--_muted); }
        .grant.eligible { border-style: dashed; }
        .pim { color: var(--_muted); font-size: 12px; padding: 2px 0; }
        button.link { background: transparent; border-color: transparent; color: var(--_accent); padding: 3px 8px; }
        button.link:hover { text-decoration: underline; }
        button.revoke { color: var(--_danger); font-size: 12px; padding: 3px 9px; }
      </style>
      <div class="find">
        <label for="who">Find a grantee</label>
        <arazzo-grantee-picker id="who" placeholder="Find a person, team, role…"></arazzo-grantee-picker>
      </div>
      <div class="body"><div class="empty">${EMPTY}</div></div>
    `;
    this._built = true;
    const picker = this.$('#who');
    picker.client = this.client;
    picker.setAttribute('scopes', this.getAttribute('scopes') || '');
    picker.addEventListener('grantee-selected', (e) => this.loadOverview(e.detail.grantee));
    picker.addEventListener('grantee-cleared', () => this.clear());
    picker.addEventListener('error', (e) => this.emit('error', e.detail));
  }

  clear() {
    this._grantee = null;
    this._overview = null;
    this.$('.body').innerHTML = `<div class="empty">${EMPTY}</div>`;
  }

  async loadOverview(grantee) {
    this._grantee = grantee;
    this.emit('grantee-selected', { grantee });
    this.$('.body').innerHTML = '<div class="empty">Loading…</div>';
    try {
      this._overview = await this.client.getAccessGrants(grantee);
      this.renderOverview();
    } catch (err) {
      const problem = err?.problem || { title: 'Failed to load access' };
      this.$('.body').innerHTML = `<div class="empty">${escapeHtml(problem.title || 'Failed to load access')}</div>`;
      this.emit('error', { problem, error: err });
    }
  }

  renderOverview() {
    const o = this._overview || {};
    const canWrite = this.hasScope('security:write');
    const bindings = Array.isArray(o.bindings) ? o.bindings : [];
    const capabilities = Array.isArray(o.capabilities) ? o.capabilities : [];
    const administers = Array.isArray(o.administers) ? o.administers : [];
    const environments = Array.isArray(o.administersEnvironments) ? o.administersEnvironments : [];
    const usage = Array.isArray(o.credentialUsage) ? o.credentialUsage : [];

    const reach = bindings.length
      ? bindings.map((b) => {
        const claim = b.claimType === '*'
          ? 'every principal (*)'
          : `${escapeHtml(b.claimType)}${b.claimValue ? ` = ${escapeHtml(b.claimValue)}` : ''}`;
        // An eligible-only binding confers NOTHING until self-elevation — rendering its (all-denied)
        // reach rows would read as an explicit deny record, which misstates its nature. Say what it
        // is instead; the capabilities section below carries its dashed eligible chip.
        const body = b.eligibleOnly
          ? '<div class="pim">PIM eligibility — confers nothing until elevated (see Capabilities below).</div>'
          : VERBS.map((v) => {
            const denied = grantMode(b[v]) === 'denied';
            return `<div class="verb"><span class="v">${v}</span><span class="${denied ? 'denied' : (grantMode(b[v]) === 'unrestricted' ? 'wide' : '')}">${escapeHtml(grantSummary(b[v]))}</span></div>`;
          }).join('');
        return `<div class="grant${b.eligibleOnly ? ' eligible' : ''}"><div class="grant-head"><span class="claim">${claim}</span><span class="grow"></span>`
          + (canWrite ? `<button class="revoke" type="button" data-revoke="${escapeHtml(b.id)}" title="Revoke this grant">Revoke</button>` : '')
          + `</div>${body}</div>`;
      }).join('')
      : '<div class="empty">No reach grants match this grantee’s identity.</div>';

    // Capability chips: the server resolves eligibility + expiry (runtime-resolver semantics), so the client only
    // renders — a dashed chip is a PIM eligibility (self-elevation required), a solid one is held actively.
    const caps = capabilities.length
      ? `<div class="caps">${capabilities.map((c) => {
        const until = c.expiresAt ? ` <span class="until">until ${escapeHtml(new Date(c.expiresAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' }))}</span>` : '';
        return `<span class="cap${c.eligible ? ' eligible' : ''}">${escapeHtml(c.scope)}${c.eligible ? ' (eligible)' : ''}${until}</span>`;
      }).join('')}</div>`
      : '<div class="empty">No capability scopes.</div>';

    // The rows carry a server-side summary (§849): the workflow's representative version, and the environment's draft-run
    // policy and a bounded count of the versions available in it. Assemble only the parts the server supplied.
    const workflowMeta = (a) => {
      const parts = [];
      if (a.title) parts.push(escapeHtml(a.title));
      if (Number.isInteger(a.latestVersion)) parts.push(`v${a.latestVersion}`);
      if (a.status) parts.push(escapeHtml(a.status));
      if (a.owner) parts.push(escapeHtml(a.owner));
      return parts.length ? `<div class="sub">${parts.join(' · ')}</div>` : '';
    };
    const envMeta = (e) => {
      const parts = [];
      if (e.displayName) parts.push(escapeHtml(e.displayName));
      if (e.availability && Number.isInteger(e.availability.count)) {
        parts.push(`${e.availability.count}${e.availability.capped ? '+' : ''} available`);
      }
      if (typeof e.allowsDraftRuns === 'boolean') parts.push(e.allowsDraftRuns ? 'drafts allowed' : 'no drafts');
      return parts.length ? `<div class="sub">${parts.join(' · ')}</div>` : '';
    };

    const admin = administers.length
      ? administers.map((a) => `<div class="row"><span class="grow"><span class="name">${escapeHtml(a.baseWorkflowId)}</span>${workflowMeta(a)}</span><button class="link" type="button" data-workflow="${escapeHtml(a.baseWorkflowId)}">Open</button></div>`).join('')
      : '<div class="empty">Administers nothing.</div>';

    const envAdmin = environments.length
      ? environments.map((e) => `<div class="row"><span class="grow"><span class="name">${escapeHtml(e.environment)}</span>${envMeta(e)}</span><button class="link" type="button" data-environment="${escapeHtml(e.environment)}">Open</button></div>`).join('')
      : '<div class="empty">Administers no environments.</div>';

    const creds = usage.length
      ? usage.map((u) => `<div class="row"><span class="grow">${escapeHtml(u.sourceName)} <span class="sub">/ ${escapeHtml(u.environment)}</span></span><button class="link" type="button" data-cred="${escapeHtml(u.sourceName)}@${escapeHtml(u.environment)}">Open</button></div>`).join('')
      : '<div class="empty">No usable source credentials.</div>';

    this.$('.body').innerHTML = `
      <div class="who">${granteeChip(o.grantee || this._grantee)}</div>
      <div class="section"><h4>Reach (grants)</h4>${reach}</div>
      <div class="section"><h4>Capabilities</h4>${caps}</div>
      <div class="section"><h4>Administers workflows</h4>${admin}</div>
      <div class="section"><h4>Administers environments</h4>${envAdmin}</div>
      <div class="section"><h4>Credential usage</h4>${creds}</div>
    `;
    this.wire();
  }

  wire() {
    const body = this.$('.body');
    body.querySelectorAll('[data-revoke]').forEach((btn) => btn.addEventListener('click', () => this.revoke(btn.dataset.revoke)));
    body.querySelectorAll('[data-workflow]').forEach((btn) => btn.addEventListener('click', () => this.emit('open-workflow', { baseWorkflowId: btn.dataset.workflow })));
    body.querySelectorAll('[data-environment]').forEach((btn) => btn.addEventListener('click', () => this.emit('open-environment', { environment: btn.dataset.environment })));
    body.querySelectorAll('[data-cred]').forEach((btn) => btn.addEventListener('click', () => {
      const [sourceName, environment] = btn.dataset.cred.split('@');
      this.emit('open-credential', { sourceName, environment });
    }));
  }

  async revoke(bindingId) {
    const ok = await confirmDialog(this, {
      title: 'Revoke grant?',
      message: 'This deletes the claim binding for every principal it matches, not just this grantee.',
      confirmLabel: 'Revoke',
      danger: true,
    });
    if (!ok) return;
    try {
      await this.client.deleteSecurityBinding(bindingId);
      this.emit('revoked', { bindingId });
      if (this._grantee) await this.loadOverview(this._grantee);
    } catch (err) {
      this.emit('error', { problem: err?.problem, error: err });
    }
  }
}

define('arazzo-access-overview', ArazzoAccessOverview);
export { ArazzoAccessOverview };
