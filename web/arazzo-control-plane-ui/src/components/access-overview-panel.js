// <arazzo-access-overview> — the "who can do what, where" overview for one resolved grantee (design §6.1). Pick a
// grantee with the shared picker, then see all of their access in one place: their reach GRANTS (a claim to per-verb
// reach, with inline Revoke), the CAPABILITY scopes those grants confer (active vs PIM-eligible, server-resolved),
// the base workflows and ENVIRONMENTS they ADMINISTER, and the source credentials their runs may USE.
//
// The BOUNDED summary (grantee, capabilities, administered environments) loads in one call (GET /access/grants); the
// three UNBOUNDED lists — reach grants, administered workflows, credential usage — are keyset-paged sub-resources
// (GET /access/grants/{reach,administered,credentials}), each with its own Prev/Next pager. Read-first; the only
// mutation is revoking a grant (security:write), which deletes the underlying claim binding for every principal it matches.
//
//   <arazzo-access-overview base-url="/arazzo/v1" scopes="security:read security:write"></arazzo-access-overview>
//   el.client = client;
//
// Attributes : base-url, scopes (gates Revoke)
// Properties : .client
// Events     : grantee-selected {grantee}, revoked {bindingId}, open-workflow {baseWorkflowId}, open-environment {environment},
//              open-credential {sourceName, environment}, error {problem}

import { ArazzoElement, SHARED_CSS, PAGER_CSS, GRANTEE_CHIP_CSS, granteeChip, escapeHtml, confirmDialog, define } from './base.js';
import './grantee-picker.js';
import './pager.js';

const VERBS = ['read', 'write', 'purge'];
const grantMode = (g) => (g?.unrestricted ? 'unrestricted' : (Array.isArray(g?.ruleNames) && g.ruleNames.length > 0 ? 'scopes' : 'denied'));
const grantSummary = (g) => {
  const mode = grantMode(g);
  if (mode === 'unrestricted') return 'Unrestricted';
  if (mode === 'scopes') return g.ruleNames.join(', ');
  return 'Denied';
};

const EMPTY = 'Pick a grantee to see everything they can do, and where.';
const PAGE_SIZE = 25;

// The three keyset-paged sections: each names the client method that fetches a page, the response array field, and the
// per-section renderer. A fresh page-state object is minted for each section on every grantee selection.
const PAGED = [
  { key: 'reach', title: 'Reach (grants)', method: 'getAccessGrantsReach', field: 'bindings' },
  { key: 'administered', title: 'Administers workflows', method: 'getAccessGrantsAdministered', field: 'administers' },
  { key: 'credentials', title: 'Credential usage', method: 'getAccessGrantsCredentials', field: 'credentialUsage' },
];

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
        ${PAGER_CSS}
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
        /* A section's pager is hidden when the list fits on one page (no prev, no next) so the overview stays quiet. */
        .pager { flex: none; margin-top: 4px; }
        .pager[hidden] { display: none; }
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
    this._summary = null;
    this._pages = null;
    this.$('.body').innerHTML = `<div class="empty">${EMPTY}</div>`;
  }

  async loadOverview(grantee) {
    this._grantee = grantee;
    const seq = (this._seq = (this._seq || 0) + 1);
    this.emit('grantee-selected', { grantee });
    this.$('.body').innerHTML = '<div class="empty">Loading…</div>';

    // A fresh page-state per section (items on the current page, the keyset cursor of THIS page, the next-page cursor,
    // and the stack of prior-page cursors for Prev). The bounded summary and the first page of each section load together.
    this._pages = Object.fromEntries(PAGED.map((s) => [s.key, { items: [], token: null, next: null, history: [], loading: false, error: null }]));
    try {
      const [summary] = await Promise.all([
        this.client.getAccessGrants(grantee),
        ...PAGED.map((s) => this.loadPage(s.key, seq)),
      ]);
      if (seq !== this._seq) return;
      this._summary = summary;
      this.renderOverview();
    } catch (err) {
      if (seq !== this._seq) return;
      const problem = err?.problem || { title: 'Failed to load access' };
      this.$('.body').innerHTML = `<div class="empty">${escapeHtml(problem.title || 'Failed to load access')}</div>`;
      this.emit('error', { problem, error: err });
    }
  }

  // Fetch one keyset page for a section into its page-state (no re-render — the caller renders once the state is set).
  async loadPage(key, seq) {
    const section = PAGED.find((s) => s.key === key);
    const state = this._pages[key];
    state.loading = true;
    state.error = null;
    try {
      const page = await this.client[section.method](this._grantee, { pageToken: state.token || undefined, limit: PAGE_SIZE });
      if (seq !== this._seq) return;
      state.items = Array.isArray(page[section.field]) ? page[section.field] : [];
      state.next = page.nextPageToken || null;
      state.loading = false;
    } catch (err) {
      if (seq !== this._seq) return;
      state.items = [];
      state.next = null;
      state.loading = false;
      state.error = err?.problem || { title: err?.message || 'Failed to load' };
      this.emit('error', { problem: state.error, error: err });
    }
  }

  // Turn a section's page: push the current cursor for Prev on next, pop it on prev, then reload just that section.
  async pageSection(key, dir) {
    const state = this._pages?.[key];
    if (!state) return;
    if (dir === 'next') {
      if (!state.next) return;
      state.history.push(state.token);
      state.token = state.next;
    } else {
      if (state.history.length === 0) return;
      state.token = state.history.pop();
    }
    const seq = this._seq;
    state.loading = true; // disable the pager while the page turn is in flight
    this.renderSection(key);
    await this.loadPage(key, seq);
    if (seq !== this._seq) return;
    this.renderSection(key);
  }

  renderOverview() {
    const s = this._summary || {};
    const capabilities = Array.isArray(s.capabilities) ? s.capabilities : [];
    const environments = Array.isArray(s.administersEnvironments) ? s.administersEnvironments : [];

    // Capability chips: the server resolves eligibility + expiry (runtime-resolver semantics), so the client only
    // renders — a dashed chip is a PIM eligibility (self-elevation required), a solid one is held actively.
    const caps = capabilities.length
      ? `<div class="caps">${capabilities.map((c) => {
        const until = c.expiresAt ? ` <span class="until">until ${escapeHtml(new Date(c.expiresAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' }))}</span>` : '';
        return `<span class="cap${c.eligible ? ' eligible' : ''}">${escapeHtml(c.scope)}${c.eligible ? ' (eligible)' : ''}${until}</span>`;
      }).join('')}</div>`
      : '<div class="empty">No capability scopes.</div>';

    const envAdmin = environments.length
      ? environments.map((e) => `<div class="row"><span class="grow"><span class="name">${escapeHtml(e.environment)}</span>${this.envMeta(e)}</span><button class="link" type="button" data-environment="${escapeHtml(e.environment)}">Open</button></div>`).join('')
      : '<div class="empty">Administers no environments.</div>';

    // Skeleton: the summary sections (capabilities, environments) render inline; the three paged sections get an empty
    // rows container + a pager that renderSection() fills. Pager prev/next are wired ONCE here (delegated by data-pager).
    this.$('.body').innerHTML = `
      <div class="who">${granteeChip(s.grantee || this._grantee)}</div>
      <div class="section" data-section="reach"><h4>Reach (grants)</h4><div class="rows"></div><arazzo-pager class="pager" data-pager="reach" hidden></arazzo-pager></div>
      <div class="section"><h4>Capabilities</h4>${caps}</div>
      <div class="section" data-section="administered"><h4>Administers workflows</h4><div class="rows"></div><arazzo-pager class="pager" data-pager="administered" hidden></arazzo-pager></div>
      <div class="section"><h4>Administers environments</h4>${envAdmin}</div>
      <div class="section" data-section="credentials"><h4>Credential usage</h4><div class="rows"></div><arazzo-pager class="pager" data-pager="credentials" hidden></arazzo-pager></div>
    `;

    this.shadowRoot.querySelectorAll('arazzo-pager[data-pager]').forEach((pager) => {
      const key = pager.dataset.pager;
      pager.addEventListener('prev', () => this.pageSection(key, 'prev'));
      pager.addEventListener('next', () => this.pageSection(key, 'next'));
    });

    // Environment "Open" is a summary-section button (not inside a paged section), so wire it here.
    this.$('.body').querySelectorAll('[data-environment]').forEach((btn) => btn.addEventListener('click', () => this.emit('open-environment', { environment: btn.dataset.environment })));

    for (const section of PAGED) this.renderSection(section.key);
  }

  // §849: the paged rows carry a server-side summary — the workflow's representative version. Assemble only what the
  // server supplied.
  workflowMeta(a) {
    const parts = [];
    if (a.title) parts.push(escapeHtml(a.title));
    if (Number.isInteger(a.latestVersion)) parts.push(`v${a.latestVersion}`);
    if (a.status) parts.push(escapeHtml(a.status));
    if (a.owner) parts.push(escapeHtml(a.owner));
    return parts.length ? `<div class="sub">${parts.join(' · ')}</div>` : '';
  }

  // The environment's draft-run policy and a bounded count of the versions available in it (§849, the count-API pattern).
  envMeta(e) {
    const parts = [];
    if (e.displayName) parts.push(escapeHtml(e.displayName));
    if (e.availability && Number.isInteger(e.availability.count)) {
      parts.push(`${e.availability.count}${e.availability.capped ? '+' : ''} available`);
    }
    if (typeof e.allowsDraftRuns === 'boolean') parts.push(e.allowsDraftRuns ? 'drafts allowed' : 'no drafts');
    return parts.length ? `<div class="sub">${parts.join(' · ')}</div>` : '';
  }

  reachRows(bindings) {
    const canWrite = this.hasScope('security:write');
    return bindings.map((b) => {
      const claim = b.claimType === '*'
        ? 'every principal (*)'
        : `${escapeHtml(b.claimType)}${b.claimValue ? ` = ${escapeHtml(b.claimValue)}` : ''}`;
      // An eligible-only binding confers NOTHING until self-elevation — rendering its (all-denied) reach rows would read
      // as an explicit deny record, which misstates its nature. Say what it is instead; the capabilities section carries
      // its dashed eligible chip.
      const body = b.eligibleOnly
        ? '<div class="pim">PIM eligibility — confers nothing until elevated (see Capabilities below).</div>'
        : VERBS.map((v) => {
          const denied = grantMode(b[v]) === 'denied';
          return `<div class="verb"><span class="v">${v}</span><span class="${denied ? 'denied' : (grantMode(b[v]) === 'unrestricted' ? 'wide' : '')}">${escapeHtml(grantSummary(b[v]))}</span></div>`;
        }).join('');
      return `<div class="grant${b.eligibleOnly ? ' eligible' : ''}"><div class="grant-head"><span class="claim">${claim}</span><span class="grow"></span>`
        + (canWrite ? `<button class="revoke" type="button" data-revoke="${escapeHtml(b.id)}" title="Revoke this grant">Revoke</button>` : '')
        + `</div>${body}</div>`;
    }).join('');
  }

  administeredRows(administers) {
    return administers.map((a) => `<div class="row"><span class="grow"><span class="name">${escapeHtml(a.baseWorkflowId)}</span>${this.workflowMeta(a)}</span><button class="link" type="button" data-workflow="${escapeHtml(a.baseWorkflowId)}">Open</button></div>`).join('');
  }

  credentialRows(usage) {
    return usage.map((u) => `<div class="row"><span class="grow">${escapeHtml(u.sourceName)} <span class="sub">/ ${escapeHtml(u.environment)}</span></span><button class="link" type="button" data-cred="${escapeHtml(u.sourceName)}@${escapeHtml(u.environment)}">Open</button></div>`).join('');
  }

  // Render one paged section's rows + pager into its container, then wire that section's row buttons.
  renderSection(key) {
    const host = this.$(`.section[data-section="${key}"]`);
    if (!host) return;
    const state = this._pages[key];
    const rows = host.querySelector('.rows');
    const pager = host.querySelector('arazzo-pager');
    const hasPrev = state.history.length > 0;
    const hasNext = !!state.next;

    if (state.loading && state.items.length === 0) {
      rows.innerHTML = '<div class="empty">Loading…</div>';
    } else if (state.error) {
      rows.innerHTML = `<div class="empty">${escapeHtml(state.error.title || 'Failed to load')}</div>`;
    } else if (state.items.length === 0) {
      rows.innerHTML = `<div class="empty">${EMPTY_SECTION[key]}</div>`;
    } else if (key === 'reach') {
      rows.innerHTML = this.reachRows(state.items);
    } else if (key === 'administered') {
      rows.innerHTML = this.administeredRows(state.items);
    } else {
      rows.innerHTML = this.credentialRows(state.items);
    }

    // The pager only appears once a section actually spans pages; a single-page section stays quiet.
    const paging = hasPrev || hasNext;
    pager.hidden = !paging;
    if (paging) pager.update({ hasPrev, hasNext, loading: state.loading, info: `page ${state.history.length + 1}` });

    this.wireSection(host, key);
  }

  wireSection(host, key) {
    if (key === 'reach') {
      host.querySelectorAll('[data-revoke]').forEach((btn) => btn.addEventListener('click', () => this.revoke(btn.dataset.revoke)));
    } else if (key === 'administered') {
      host.querySelectorAll('[data-workflow]').forEach((btn) => btn.addEventListener('click', () => this.emit('open-workflow', { baseWorkflowId: btn.dataset.workflow })));
    } else {
      host.querySelectorAll('[data-cred]').forEach((btn) => btn.addEventListener('click', () => {
        const [sourceName, environment] = btn.dataset.cred.split('@');
        this.emit('open-credential', { sourceName, environment });
      }));
    }
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
      // A revoke changes both the reach list AND the conferred capabilities, so reload the whole overview.
      if (this._grantee) await this.loadOverview(this._grantee);
    } catch (err) {
      this.emit('error', { problem: err?.problem, error: err });
    }
  }
}

const EMPTY_SECTION = {
  reach: 'No reach grants match this grantee’s identity.',
  administered: 'Administers nothing.',
  credentials: 'No usable source credentials.',
};

define('arazzo-access-overview', ArazzoAccessOverview);
export { ArazzoAccessOverview };
