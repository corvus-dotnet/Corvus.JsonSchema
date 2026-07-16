// <arazzo-grants-panel> — author access grants (design §14.2/§16.5): WHO (a claim) → WHERE (per-verb REACH — which
// rows read/write/purge may touch, narrowed by reusable rules). "Grant" is the user-facing term for what the API calls
// a security binding; "rule" for a security rule. NB: reach is NOT capability — which operations a caller may perform
// (e.g. write runs vs. environments) comes from their role's token scopes (§14.1), not from a grant.
//
//   <arazzo-grants-panel base-url="/arazzo/v1" scopes="security:read security:write"></arazzo-grants-panel>
//
// Attributes : base-url, scopes (gates the mutating controls)
// Properties : .client
// Events     : grants-changed {grants}, loaded {count}, error {problem}
// Parts      : panel, list, row, detail
//
// A master-detail over the grant list (the same shape as the Catalog / Sources / Environments surfaces): a selectable
// list on the left, a detail pane on the right that AUTHORS the selected grant in place — no modal. A grant keys a
// principal claim to per-action access:
//   WHO   — name a group/role grantee with the picker (its resolved identity derives the canonical claim), or enter a
//           raw claim. A *person* grantee is NOT granted here: per-person elevation goes through the access-request
//           flow, so the picker steers it there. The claim→identity mapping is issuer-sensitive, so the multi-IdP
//           caveat is surfaced; the server also rejects self-elevation (403).
//   WHERE — each of read/write/purge is Denied | Unrestricted | scoped (one or more scopes, chosen by typeahead so it
//           stays usable at hundreds — server-paged search). The claim is the immutable key, so it is read-only on edit.
// The list is searched + paged server-side; "New grant" opens a blank pane; editing/deleting happen in the pane.

import { ArazzoElement, SHARED_CSS, PAGER_CSS, PICKER_CSS, escapeHtml, confirmDialog, define } from './base.js';
import './grantee-picker.js';
import './pager.js';

// How long to wait after the last keystroke before issuing a server-side search (the grant list and the scope typeahead
// are both paged + searched on the server, so they scale to any number of grants/scopes).
const SEARCH_DEBOUNCE_MS = 250;

const VERBS = ['read', 'write', 'purge'];

const grantMode = (g) => (g?.unrestricted ? 'unrestricted' : (Array.isArray(g?.ruleNames) && g.ruleNames.length > 0 ? 'scopes' : 'denied'));
const grantSummary = (g) => {
  const mode = grantMode(g);
  if (mode === 'unrestricted') return 'Unrestricted';
  // A grant's rules are a CONJUNCTION (§14.2 — every rule must admit the row), so the summary joins
  // with '+' exactly like the claim column's ANDed identity clauses; a comma list would read as
  // alternatives, which is the opposite of what the server enforces.
  if (mode === 'scopes') return g.ruleNames.join(' + ');
  return 'Denied';
};

// A simple-grammar equality rule ("dim == 'value'") — the shape the provably-empty-conjunction
// advisory can reason about. Anything richer is left to the server's evaluation.
const EQ_RULE = /^\s*([A-Za-z_][\w.:-]*)\s*==\s*'([^']*)'\s*$/;

class ArazzoGrantsPanel extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._grants = [];
    /** @private */ this._total = null;          // q-scoped bounded total for the footer (null until first load)
    /** @private */ this._totalCapped = false;   // true when the true total meets/exceeds the server cap ("N+")
    // The scopes currently offered in the authoring typeahead — the latest server result, not the whole vocabulary.
    /** @private */ this._scopes = [];
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._history = [];          // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
    /** @private */ this._reqSeq = 0;
    /** @private */ this._query = '';
    /** @private */ this._form = null;           // the detail-pane form state (null = nothing selected)
    /** @private */ this._selectedId = null;     // the selected grant id (null when creating / nothing selected)
    /** @private */ this._squelchFocusPop = false; // true only across addRule's programmatic refocus
    /** @private */ this._ruleInfo = new Map();     // rule name → expression, from every typeahead page seen
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'scopes') { this.renderBody(); this.renderDetail(); }
    else this.reload();
  }

  requestRender() { this.reload(); }

  refresh() { this.reload(); }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 50;
  }

  get canWrite() {
    const scopes = (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
    return scopes.length === 0 || scopes.includes('security:write');
  }

  /** Reload from page 1 (resets the keyset cursor). Every caller that wants page 1 — a search-term change, connect,
   *  refresh, or a create/edit/delete mutation — goes through here so the pager returns to the first page. */
  reload() {
    this._history = [];
    this._currentToken = undefined;
    return this.load();
  }

  async load() {
    const client = this.client;
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url or .client.' };
      this._grants = [];
      this.renderBody();
      return;
    }
    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();
    try {
      // One keyset page of grants, filtered server-side by q. Replaces the list (Prev/Next paging), not appended. The
      // scope vocabulary for the authoring typeahead is fetched on demand when the editor opens / as the user types
      // (loadScopeOptions), not loaded in full here.
      const q = this._query.trim() || undefined;
      const [page, total] = await Promise.all([
        client.searchSecurityBindings({ q, pageToken: this._currentToken, limit: this.pageSize }),
        client.countSecurityBindings({ q }).catch(() => null),
      ]);
      if (seq !== this._reqSeq) return;
      this._grants = page.bindings;
      this._nextPageToken = page.nextPageToken;
      this._total = total ? total.count : null;
      this._totalCapped = total ? total.capped : false;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._grants.length, hasMore: !!this._nextPageToken });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  nextPage() {
    if (!this._nextPageToken) return;
    this._history.push(this._currentToken);
    this._currentToken = this._nextPageToken;
    this.load();
  }

  prevPage() {
    if (this._history.length === 0) return;
    this._currentToken = this._history.pop();
    this.load();
  }

  /** Fetch a page of rules matching `q` for the authoring picker, and refresh the open dropdown in place. Best-effort. */
  async loadScopeOptions(q) {
    const client = this.client;
    if (!client) return;
    try {
      const page = await client.searchSecurityRules({ q: q || undefined });
      this._scopes = page.rules;
      // Remember every expression this session has seen (name → expression): the provably-empty
      // conjunction advisory reasons over them best-effort.
      for (const rule of page.rules) this._ruleInfo.set(rule.name, rule.expression || '');
      this.renderRuleDropdown();
    } catch {
      /* the picker is a convenience; a failed lookup just leaves the previous options */
    }
  }

  /**
   * Best-effort detection of a conjunction that can NEVER admit a row: two selected simple
   * equality rules pinning the SAME dimension to different values (a grant's rules all have to
   * match — §14.2). Only rules whose expressions this session has seen are considered; anything
   * richer than the simple grammar is left to the server.
   */
  conjunctionConflict(names) {
    const byDim = new Map();
    for (const name of names) {
      const match = EQ_RULE.exec(this._ruleInfo.get(name) || '');
      if (!match) continue;
      const prior = byDim.get(match[1]);
      if (prior && prior.value !== match[2]) return { dimension: match[1], first: prior.name, second: name };
      byDim.set(match[1], { name, value: match[2] });
    }
    return null;
  }

  /** Render the open rule dropdown (the one for `_activeRuleVerb`) from the latest results, excluding already-added rules. */
  renderRuleDropdown() {
    const verb = this._activeRuleVerb;
    if (!verb) return;
    const list = this._pane?.querySelector(`.results[data-verb="${verb}"]`);
    if (!list) return;
    const added = new Set(this._form?.verbs[verb]?.scopes || []);
    const options = this._scopes.filter((s) => !added.has(s.name));
    if (!options.length) {
      list.innerHTML = `<li role="option" aria-disabled="true">No matching rules</li>`;
    } else {
      list.innerHTML = options.map((s) => `<li role="option" data-name="${escapeHtml(s.name)}"><span><span class="label">${escapeHtml(s.name)}</span>${s.description ? `<div class="ident">${escapeHtml(s.description)}</div>` : ''}</span></li>`).join('');
      list.querySelectorAll('li[data-name]').forEach((li) => li.addEventListener('click', () => this.addRule(verb, li.dataset.name)));
    }
    list.hidden = false;
  }

  /** Add a rule to a verb's reach (from a dropdown click), then re-render the editor with the new chip. */
  addRule(verb, name) {
    const f = this._form;
    if (!f || !name || f.verbs[verb].scopes.includes(name)) return;
    f.verbs[verb].scopes.push(name);
    this._activeRuleVerb = null;
    this.renderDetail();
    // Refocus for further typing WITHOUT re-popping the suggestions: the dropdown overlays whatever
    // sits below the verb row (including the pane's footer), so a completed selection must leave it
    // closed — typing again re-opens it.
    this._squelchFocusPop = true;
    this._pane?.querySelector(`.scope-input[data-verb="${verb}"]`)?.focus();
    this._squelchFocusPop = false;
  }

  /** Hide any open rule dropdown. */
  hideRuleDropdowns() {
    this._activeRuleVerb = null;
    this._pane?.querySelectorAll('.results').forEach((el) => { el.hidden = true; });
  }

  // ---- detail-pane authoring --------------------------------------------------------------------

  blankForm(mode, editId) {
    return {
      mode, editId, personBlocked: false, granteeLabel: '',
      claimType: '', claimValue: '', additionalClauses: [], description: '', formError: null,
      verbs: { read: { mode: 'denied', scopes: [] }, write: { mode: 'denied', scopes: [] }, purge: { mode: 'denied', scopes: [] } },
    };
  }

  /** Open a blank detail pane to author a new grant. */
  openCreate() {
    this._selectedId = null;
    this._form = this.blankForm('create', null);
    this.renderBody(); // clear any selected-row highlight
    this.renderDetail();
    this.loadScopeOptions(''); // seed the scope typeahead with a first page of the vocabulary
  }

  /** Select a grant row: open it in the detail pane for editing (the claim is the immutable key). */
  select(id) {
    const g = this._grants.find((x) => x.id === id);
    if (!g) return;
    this._selectedId = id;
    const f = this.blankForm('edit', id);
    f.claimType = g.claimType || '';
    f.claimValue = g.claimValue || '';
    f.additionalClauses = (g.additionalClauses || []).map((c) => ({ dimension: c.dimension || '', value: c.value || '' }));
    for (const verb of VERBS) {
      f.verbs[verb] = { mode: grantMode(g[verb]), scopes: Array.isArray(g[verb]?.ruleNames) ? [...g[verb].ruleNames] : [] };
    }
    f.description = g.description || '';
    this._form = f;
    this.renderBody(); // refresh row highlight
    this.renderDetail();
    this.loadScopeOptions('');
  }

  clearDetail() {
    this._form = null;
    this._selectedId = null;
    this.renderBody();
    this.renderDetail();
  }

  onGranteeSelected(grantee) {
    const f = this._form;
    if (!f) return;
    if (grantee.kind === 'person') {
      f.personBlocked = true;
      f.granteeLabel = grantee.label || grantee.value || 'this person';
      f.claimType = '';
      f.claimValue = '';
      f.additionalClauses = [];
    } else {
      f.personBlocked = false;
      const identity = grantee.identity || [];
      const primary = identity[0];
      f.claimType = primary?.dimension || grantee.kind || '';
      f.claimValue = primary?.value || grantee.value || '';
      // The whole resolved identity is the selector: the first dimension is the primary claim, every remaining
      // dimension is an additional clause ANDed with it (§16.5.4). Pinning the full identity is what stops a grant
      // keyed on a bare name (a group/role) from also matching the same name under a different issuer or tenant.
      f.additionalClauses = identity.slice(1).map((t) => ({ dimension: t.dimension || '', value: t.value || '' }));
      f.granteeLabel = grantee.label || grantee.value || '';
    }
    this.renderDetail();
  }

  buildBody() {
    const f = this._form;
    const verb = (v) => (v.mode === 'unrestricted' ? { unrestricted: true } : v.mode === 'scopes' ? { ruleNames: v.scopes } : { unrestricted: false });
    const body = {
      claimType: f.claimType.trim(),
      read: verb(f.verbs.read),
      write: verb(f.verbs.write),
      purge: verb(f.verbs.purge),
    };
    if (f.claimValue.trim()) body.claimValue = f.claimValue.trim();
    const clauses = (f.additionalClauses || [])
      .filter((c) => c.dimension.trim())
      .map((c) => (c.value.trim() ? { dimension: c.dimension.trim(), value: c.value.trim() } : { dimension: c.dimension.trim() }));
    if (clauses.length) body.additionalClauses = clauses;
    if (f.description.trim()) body.description = f.description.trim();
    return body;
  }

  async submitForm() {
    const f = this._form;
    if (!f) return;
    if (f.personBlocked) {
      f.formError = { title: 'Use the access-request flow for a person', detail: `Per-person elevation for ${f.granteeLabel} is requested and approved, not granted directly.` };
      this.renderDetail();
      return;
    }
    if (!f.claimType.trim()) {
      f.formError = { title: 'A claim is required (pick a group/role grantee or enter a raw claim).' };
      this.renderDetail();
      return;
    }
    const body = this.buildBody();
    try {
      if (f.mode === 'edit') {
        await this.client.updateSecurityBinding(f.editId, body);
      } else {
        await this.client.createSecurityBinding(body);
      }
      this.clearDetail();
      await this.reloadAndEmit();
    } catch (err) {
      f.formError = err.problem || { title: err.message };
      this.renderDetail();
      this.emit('error', { problem: f.formError, error: err });
    }
  }

  async deleteGrant(id) {
    const g = this._grants.find((x) => x.id === id);
    const describe = g ? `${g.claimType}${g.claimValue ? '=' + g.claimValue : ''}` : id;
    const confirmed = await confirmDialog(this, {
      title: 'Delete grant',
      message: `Delete the grant for '${describe}'? The principals it matches will lose that access.`,
      confirmLabel: 'Delete', danger: true,
    });
    if (!confirmed) return;
    try {
      await this.client.deleteSecurityBinding(id);
      this.clearDetail();
      await this.reloadAndEmit();
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  async reloadAndEmit() {
    // Reload from page 1 under the current search term (a mutation may have added/removed a matching grant).
    await this.reload();
    this.emit('grants-changed', { grants: this._grants });
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${PAGER_CSS}
        ${PICKER_CSS}
        :host { display: flex; flex-direction: column; min-height: 0; height: 100%; }
        .layout { flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1fr); grid-auto-rows: minmax(0, 1fr); gap: 14px; }
        .layout > * { min-height: 0; }
        .detail-pane { min-height: 0; overflow: auto; }
        .detail-pane:empty { display: none; }
        @media (min-width: 880px) { .layout.has-selection { grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr); } }
        .wrap { flex: 1; min-height: 0; display: flex; flex-direction: column; border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; background: var(--_bg); }
        .toolbar { flex: none; display: flex; align-items: center; gap: 8px; padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); }
        .toolbar .title { font-weight: 600; color: var(--_muted); font-size: 12px; }
        .toolbar .grow { flex: 1; }
        .search { font: inherit; font-size: 13px; padding: 5px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: inherit; width: 160px; }
        .err { flex: none; margin: 10px 12px; }
        .err:empty { display: none; }
        .tablescroll { flex: 1; min-height: 0; overflow: auto; scrollbar-gutter: stable; }
        table { width: 100%; border-collapse: collapse; }
        thead th { text-align: left; font-size: 12px; font-weight: 600; color: var(--_muted); padding: 9px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); white-space: nowrap; position: sticky; top: 0; z-index: 1; }
        tbody td { padding: 9px 12px; border-bottom: 1px solid var(--_border); vertical-align: top; }
        tbody tr:last-child td { border-bottom: none; }
        tbody tr.selectable { cursor: pointer; }
        tbody tr.selectable:hover { background: var(--_surface); }
        tbody tr[aria-selected="true"] { background: color-mix(in srgb, var(--_accent) 12%, transparent); }
        .claim { font-weight: 600; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
        .claim-and { font-weight: 400; color: var(--_muted); }
        .verbs { color: var(--_muted); font-size: 12px; margin-top: 3px; display: flex; gap: 12px; flex-wrap: wrap; }
        .verbs b { color: var(--_text); font-weight: 600; text-transform: capitalize; }
        .gdesc { color: var(--_muted); font-size: 12px; margin-top: 2px; }
        .gscopes { color: var(--_muted); font-size: 12px; margin-top: 2px; font-style: italic; }
        .skl { height: 14px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 10px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }
        .pager { flex: none; }

        /* detail pane */
        .detail { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        .dhead { padding: 12px 14px; border-bottom: 1px solid var(--_border); background: var(--_surface); font-weight: 700; display: flex; align-items: center; gap: 8px; }
        .dhead .grow { flex: 1; }
        .dhead .close { font-size: 16px; line-height: 1; }
        .content { padding: 14px; display: grid; gap: 14px; }
        .section { display: grid; gap: 8px; }
        .section > .slabel { font-weight: 600; font-size: 13px; }
        .caveat { font-size: 12px; color: var(--_muted); border-left: 3px solid var(--_border); padding: 4px 0 4px 8px; }
        .field { display: grid; gap: 4px; }
        .field > span { font-size: 12px; color: var(--_muted); }
        .field input, .field select { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); box-sizing: border-box; }
        .field input[readonly] { background: var(--_surface); color: var(--_muted); }
        .verb-row { display: grid; grid-template-columns: 70px 1fr; gap: 8px; align-items: start; }
        .verb-row > .vname { text-transform: capitalize; font-weight: 600; font-size: 13px; padding-top: 8px; }
        .scope-pick { display: grid; gap: 6px; margin-top: 6px; }
        .chips { display: flex; gap: 6px; flex-wrap: wrap; align-items: center; }
        .conj { color: var(--_muted); font-weight: 700; }
        .conj-hint { font-size: 11.5px; color: var(--_muted); margin-top: 4px; }
        .conj-warn { font-size: 11.5px; color: var(--arazzo-status-suspended, #b45309); margin-top: 4px; }
        .chip { display: inline-flex; align-items: center; gap: 4px; font-size: 12px; background: var(--_surface); border: 1px solid var(--_border); border-radius: 999px; padding: 2px 8px; }
        .chip button { border: none; background: none; cursor: pointer; color: var(--_muted); font-size: 13px; line-height: 1; padding: 0; }
        .clauses { display: grid; gap: 6px; }
        .clause-row { display: grid; grid-template-columns: 1fr 1fr auto; gap: 6px; align-items: center; }
        .clause-row input { font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); box-sizing: border-box; min-width: 0; }
        .clause-del { border: 1px solid var(--_border); background: none; cursor: pointer; color: var(--_muted); border-radius: var(--_radius); padding: 6px 9px; line-height: 1; }
        .clause-del:hover { color: var(--_text); }
        .add-clause { justify-self: start; font: inherit; font-size: 12px; border: 1px dashed var(--_border); background: none; cursor: pointer; color: var(--_muted); border-radius: var(--_radius); padding: 6px 10px; }
        .add-clause:hover { color: var(--_text); border-color: var(--_muted); }
        .scope-empty { font-size: 12px; color: var(--_muted); }
        .verb-mode { font: inherit; padding: 7px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        .rule-search { position: relative; }
        .scope-input { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); box-sizing: border-box; }
        .dfoot { display: flex; gap: 8px; align-items: center; padding: 12px 14px; border-top: 1px solid var(--_border); }
        .dfoot .grow { flex: 1; }
        .placeholder { color: var(--_muted); padding: 24px 14px; text-align: center; }
      </style>
      <div class="layout" part="layout">
        <div class="wrap" part="panel">
          <div class="toolbar" part="toolbar">
            <span class="title">Grants</span>
            <span class="grow"></span>
            <input class="search" type="search" placeholder="Search grants…" aria-label="Search grants">
            <button class="refresh ghost" type="button" title="Refresh">↻</button>
            <button class="new primary" type="button" hidden>New grant</button>
          </div>
          <div class="err"></div>
          <div class="tablescroll">
            <table>
              <thead><tr><th>Claim</th><th>Per-action access</th></tr></thead>
              <tbody class="list" part="rows"></tbody>
            </table>
          </div>
          <arazzo-pager class="pager" part="pager"></arazzo-pager>
        </div>
        <div class="detail-pane"></div>
      </div>
    `;
    this.$('.new').addEventListener('click', () => this.openCreate());
    this.$('.refresh').addEventListener('click', () => this.reload());
    this.$('.search').addEventListener('input', (e) => {
      this._query = e.target.value;
      clearTimeout(this._searchTimer);
      // A search-term change must reset to page 1 — reload(), not load().
      this._searchTimer = setTimeout(() => this.reload(), SEARCH_DEBOUNCE_MS);
    });
    this.$('arazzo-pager').addEventListener('prev', () => this.prevPage());
    this.$('arazzo-pager').addEventListener('next', () => this.nextPage());
    // A pointerdown anywhere but the open dropdown's own input/list closes it — INCLUDING elsewhere
    // in this panel (the dropdown overlays the pane below it, so a click aimed at the footer must
    // dismiss rather than leave the overlay intercepting).
    document.addEventListener('pointerdown', this._onDocDown ??= (e) => {
      const verb = this._activeRuleVerb;
      if (!verb) return;
      const path = e.composedPath();
      const input = this._pane?.querySelector(`.scope-input[data-verb="${verb}"]`);
      const list = this._pane?.querySelector(`.results[data-verb="${verb}"]`);
      if ((input && path.includes(input)) || (list && path.includes(list))) return;
      this.hideRuleDropdowns();
    });
    this._pane = this.$('.detail-pane');
    this._layout = this.$('.layout');
  }

  disconnectedCallback() {
    if (this._onDocDown) document.removeEventListener('pointerdown', this._onDocDown);
  }

  renderBody() {
    const err = this.$('.err');
    const list = this.$('.list');
    if (!list) return;
    this.$('.new').hidden = !this.canWrite;

    err.innerHTML = this._error
      ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
      : '';

    if (this._loading && this._grants.length === 0) {
      list.innerHTML = `<tr><td colspan="2"><div class="skl"></div><div class="skl"></div></td></tr>`;
    } else if (this._grants.length === 0) {
      list.innerHTML = `<tr><td colspan="2"><div class="empty">${this._query.trim() ? `No grants match “${escapeHtml(this._query.trim())}”.` : 'No grants defined.'}</div></td></tr>`;
    } else {
      list.innerHTML = this._grants.map((g) => `
        <tr class="grow-row selectable" part="row" data-id="${escapeHtml(g.id)}" aria-selected="${String(g.id === this._selectedId)}">
          <td part="cell"><span class="claim">${escapeHtml(g.claimType)}${g.claimValue ? '=' + escapeHtml(g.claimValue) : ''}${(g.additionalClauses || []).map((c) => ` <span class="claim-and">+ ${escapeHtml(c.dimension)}${c.value ? '=' + escapeHtml(c.value) : ''}</span>`).join('')}</span>${(g.scopes || []).length ? `<div class="gscopes">${g.eligibleOnly ? 'eligible for' : 'confers'} ${escapeHtml(g.scopes.join(', '))}${g.expiresAt ? ` until ${escapeHtml(new Date(g.expiresAt).toLocaleString())}` : ''}</div>` : ''}${g.description ? `<div class="gdesc">${escapeHtml(g.description)}</div>` : ''}</td>
          <td part="cell"><div class="verbs">${VERBS.map((v) => `<span><b>${v}</b> ${escapeHtml(grantSummary(g[v]))}</span>`).join('')}</div></td>
        </tr>`).join('');
      this.$$('.grow-row').forEach((tr) => tr.addEventListener('click', () => this.select(tr.dataset.id)));
    }

    this.$('arazzo-pager')?.update({
      hasPrev: this._history.length > 0,
      hasNext: !!this._nextPageToken,
      loading: this._loading,
      info: this._loading ? 'Loading…' : `${this._total ?? this._grants.length}${this._totalCapped ? '+' : ''} grant${(this._total ?? this._grants.length) === 1 ? '' : 's'}${this._history.length ? ` · page ${this._history.length + 1}` : ''}`,
    });
  }

  verbRowHtml(verb) {
    const v = this._form.verbs[verb];
    const modes = [['denied', 'Denied'], ['unrestricted', 'Unrestricted'], ['scopes', 'Limited to rules']];
    let picker = '';
    if (v.mode === 'scopes') {
      // The '+' separators say what the server enforces: the rules are a conjunction, not
      // alternatives. With 2+ rules the hint spells it out — and when two simple equality rules
      // pin the same dimension to different values, the advisory names the impossible pair
      // (non-blocking: the server remains the authority).
      const chips = v.scopes.map((name) => `<span class="chip">${escapeHtml(name)}<button type="button" class="chip-rm" data-verb="${verb}" data-scope="${escapeHtml(name)}" aria-label="remove ${escapeHtml(name)}">×</button></span>`).join('<span class="conj">+</span>');
      const conflict = v.scopes.length > 1 ? this.conjunctionConflict(v.scopes) : null;
      const hint = conflict
        ? `<div class="conj-warn">⚠ '${escapeHtml(conflict.first)}' and '${escapeHtml(conflict.second)}' both pin <code>${escapeHtml(conflict.dimension)}</code> to different values — together they can never admit a row. For either/or reach, add a second grant for the same claim.</div>`
        : (v.scopes.length > 1
          ? `<div class="conj-hint">All rules must match (their intersection). For either/or reach, add a second grant for the same claim.</div>`
          : '');
      picker = `
        <div class="scope-pick">
          <div class="chips">${chips || '<span class="scope-empty">No rules yet — search to add.</span>'}</div>
          ${hint}
          <div class="rule-search">
            <input class="scope-input" data-verb="${verb}" type="search" autocomplete="off" placeholder="Search rules to add…" aria-label="add a rule to ${verb}">
            <ul class="results" data-verb="${verb}" role="listbox" hidden></ul>
          </div>
        </div>`;
    }
    return `
      <div class="verb-row">
        <span class="vname">${verb}</span>
        <div>
          <select class="verb-mode" data-verb="${verb}">${modes.map(([m, l]) => `<option value="${m}" ${v.mode === m ? 'selected' : ''}>${l}</option>`).join('')}</select>
          ${picker}
        </div>
      </div>`;
  }

  /**
   * The additional-clause selector. On create it is editable: a grant keys on one claim, and the operator can pin more
   * identity dimensions (issuer, tenant, …) the caller must ALSO carry, authoring the same tag-set selector (§16.5.4)
   * the grantee picker fills from a resolved identity. On edit it is the read-only chip summary — the claim selector is
   * fixed after creation. Empty clauses are dropped in buildBody, so a blank added row is harmless.
   */
  additionalClausesHtml(f, editable) {
    const clauses = f.additionalClauses || [];
    if (!editable) {
      if (!clauses.length) return '';
      return `
        <div class="field"><span>AND every one of</span><div class="chips">${clauses.map((c) => `<span class="chip">${escapeHtml(c.dimension)}${c.value ? ' = ' + escapeHtml(c.value) : ''}</span>`).join('')}</div></div>
        <div class="caveat">This grant pins the grantee's <strong>full resolved identity</strong> — it applies only to a caller who carries every one of these dimensions, so it will not match the same name under a different issuer or tenant.</div>`;
    }

    const rows = clauses.map((c, i) => `
      <div class="clause-row" data-idx="${i}">
        <input class="f-clause-dim" placeholder="dimension (e.g. iss)" value="${escapeHtml(c.dimension)}">
        <input class="f-clause-val" placeholder="(any value)" value="${escapeHtml(c.value)}">
        <button class="clause-del" type="button" title="Remove clause" aria-label="Remove clause">✕</button>
      </div>`).join('');
    return `
      <div class="field"><span>AND also${clauses.length ? '' : ' (optional)'}</span>
        <div class="clauses">
          ${rows}
          <button class="add-clause" type="button">+ Add identity clause</button>
        </div>
      </div>
      <div class="caveat">A grant keys on one claim. Add a clause to require <strong>another identity dimension</strong> (e.g. an issuer or tenant) the caller must <em>also</em> carry, so the grant matches only that exact identity and not the same name under a different issuer. Picking a grantee above fills these in from its resolved identity.</div>`;
  }

  renderDetail() {
    const pane = this._pane;
    if (!pane) return;
    const f = this._form;
    this._layout.classList.toggle('has-selection', !!f);
    if (!f) { pane.replaceChildren(); return; }

    const isEdit = f.mode === 'edit';
    pane.innerHTML = `
      <div class="detail" part="detail">
        <div class="dhead">
          <span class="dtitle">${isEdit ? `Edit grant '${escapeHtml(f.claimType)}${f.claimValue ? '=' + escapeHtml(f.claimValue) : ''}'` : 'New grant'}</span>
          <span class="grow"></span>
          <button class="close ghost" type="button" title="Close" aria-label="Close">✕</button>
        </div>
        <div class="content">
          <div class="section">
            <span class="slabel">WHO — the claim this grant keys on</span>
            ${isEdit ? '' : `
              <div class="field"><span>Grantee</span><arazzo-grantee-picker class="who-picker" placeholder="a team or role…"></arazzo-grantee-picker></div>
              ${f.personBlocked ? `<div class="error-banner steer"><span>Per-person elevation for <strong>${escapeHtml(f.granteeLabel)}</strong> goes through the <strong>access-request flow</strong>, not a direct grant — request and have it approved instead.</span></div>` : ''}`}
            <div class="field"><span>Claim type</span><input class="f-claimType" placeholder="team" value="${escapeHtml(f.claimType)}" ${isEdit ? 'readonly' : ''}></div>
            <div class="field"><span>Claim value</span><input class="f-claimValue" placeholder="(any value of the type)" value="${escapeHtml(f.claimValue)}" ${isEdit ? 'readonly' : ''}></div>
            ${this.additionalClausesHtml(f, !isEdit)}
            ${isEdit
              ? `<div class="caveat">The claim identifies <strong>who</strong> this grant applies to and is fixed after creation — to change who, delete this grant and create a new one via the picker.</div>`
              : `<div class="caveat">A claim is only as trustworthy as the issuer asserting it. With multiple semi-trusted identity providers, prefer an issuer-qualified claim over a bare one.</div>`}
          </div>
          <div class="section">
            <span class="slabel">WHERE — reach, per action</span>
            <div class="caveat">This grants <strong>reach</strong> — which <strong>rows</strong> this claim may read / write / purge, narrowed by the rules you attach. It does <em>not</em> grant capability: <em>which operations</em> a caller may perform (e.g. write runs vs. environments) comes from their role's token scopes, not here.</div>
            ${VERBS.map((v) => this.verbRowHtml(v)).join('')}
          </div>
          <div class="field"><span>Description</span><input class="f-description" placeholder="(optional)" value="${escapeHtml(f.description)}"></div>
          <div class="form-err">${f.formError ? `<div class="error-banner"><span><strong>${escapeHtml(f.formError.title || 'Request failed')}</strong>${f.formError.detail ? ' — ' + escapeHtml(f.formError.detail) : ''}</span></div>` : ''}</div>
        </div>
        <div class="dfoot">
          ${isEdit ? '<button class="del danger" type="button">Delete…</button>' : ''}
          <span class="grow"></span>
          <button class="cancel ghost" type="button">Cancel</button>
          <button class="confirm primary" type="button">${isEdit ? 'Save' : 'Create'}</button>
        </div>
      </div>
    `;

    const picker = pane.querySelector('.who-picker');
    if (picker) {
      picker.client = this.client;
      picker.addEventListener('grantee-selected', (e) => this.onGranteeSelected(e.detail.grantee));
      picker.addEventListener('grantee-cleared', () => { f.personBlocked = false; this.renderDetail(); });
    }

    pane.querySelector('.f-claimType')?.addEventListener('input', (e) => { f.claimType = e.target.value; });
    pane.querySelector('.f-claimValue')?.addEventListener('input', (e) => { f.claimValue = e.target.value; });
    pane.querySelector('.f-description')?.addEventListener('input', (e) => { f.description = e.target.value; });

    // Additional-clause rows: input mutates in place (no re-render, so the caret holds); add/remove re-render.
    const clauseIdx = (el) => Number(el.closest('.clause-row').dataset.idx);
    pane.querySelectorAll('.f-clause-dim').forEach((el) => el.addEventListener('input', (e) => { f.additionalClauses[clauseIdx(e.target)].dimension = e.target.value; }));
    pane.querySelectorAll('.f-clause-val').forEach((el) => el.addEventListener('input', (e) => { f.additionalClauses[clauseIdx(e.target)].value = e.target.value; }));
    pane.querySelectorAll('.clause-del').forEach((el) => el.addEventListener('click', (e) => { f.additionalClauses.splice(clauseIdx(e.target), 1); this.renderDetail(); }));
    pane.querySelector('.add-clause')?.addEventListener('click', () => { (f.additionalClauses ||= []).push({ dimension: '', value: '' }); this.renderDetail(); });

    pane.querySelectorAll('.verb-mode').forEach((sel) => sel.addEventListener('change', () => {
      f.verbs[sel.dataset.verb].mode = sel.value;
      this.renderDetail();
    }));

    // Rule picker: focus pops an initial suggestions dropdown; typing narrows it (server-paged); clicking a result
    // (renderRuleDropdown → addRule) adds a chip. `change` (exact-typed name + Enter/blur) is a keyboard fallback.
    pane.querySelectorAll('.scope-input').forEach((input) => {
      const verb = input.dataset.verb;
      input.addEventListener('focus', () => {
        if (this._squelchFocusPop) return; // the post-selection refocus (addRule) keeps the dropdown closed
        this.hideRuleDropdowns(); this._activeRuleVerb = verb; this.loadScopeOptions(input.value.trim());
      });
      input.addEventListener('keydown', (e) => {
        // Escape dismisses the dropdown and STAYS dismissed: hideRuleDropdowns clears the active
        // verb, so an in-flight search that resolves later has nowhere to render (renderRuleDropdown
        // no-ops without one). preventDefault keeps the typed filter text (search inputs clear on
        // Escape); a fresh keystroke or refocus re-opens the suggestions.
        if (e.key === 'Escape') { e.preventDefault(); this.hideRuleDropdowns(); }
      });
      input.addEventListener('input', () => {
        this._activeRuleVerb = verb;
        clearTimeout(this._scopeTimer);
        this._scopeTimer = setTimeout(() => this.loadScopeOptions(input.value.trim()), SEARCH_DEBOUNCE_MS);
      });
      input.addEventListener('change', () => {
        const name = input.value.trim();
        if (name && this._scopes.some((s) => s.name === name)) this.addRule(verb, name);
        else input.value = '';
      });
    });
    pane.querySelectorAll('.chip-rm').forEach((btn) => btn.addEventListener('click', () => {
      const list = f.verbs[btn.dataset.verb].scopes;
      const i = list.indexOf(btn.dataset.scope);
      if (i >= 0) list.splice(i, 1);
      this.renderDetail();
    }));

    pane.querySelector('.close').addEventListener('click', () => this.clearDetail());
    pane.querySelector('.cancel').addEventListener('click', () => this.clearDetail());
    pane.querySelector('.confirm').addEventListener('click', () => this.submitForm());
    pane.querySelector('.del')?.addEventListener('click', () => this.deleteGrant(f.editId));

    // Scope honesty: a caller without security:write views the grant read-only — inputs disabled, no Save/Delete (the
    // server's 403 remains the backstop). A read-only caller never reaches create (New is hidden), so this is view/edit.
    if (!this.canWrite) {
      pane.querySelectorAll('input, select').forEach((c) => { c.disabled = true; });
      pane.querySelector('.confirm')?.remove();
      pane.querySelector('.del')?.remove();
      pane.querySelector('.cancel').textContent = 'Close';
    }
  }
}

define('arazzo-grants-panel', ArazzoGrantsPanel);
export { ArazzoGrantsPanel };