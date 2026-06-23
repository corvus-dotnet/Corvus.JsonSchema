// <arazzo-grantee-picker> — resolve a real grantee to its exact sys: identity (§16.5.4).
//
//   <arazzo-grantee-picker placeholder="Find a person, team, role…"></arazzo-grantee-picker>
//   el.client = client;                 // an ArazzoControlPlaneClient (administrators:read)
//   el.addEventListener('grantee-selected', (e) => use(e.detail.grantee));
//   const grant = el.grant;             // the resolved grantee { kind, value, label, identity[], complete } | null
//
// Attributes : placeholder, kind (lock the search to one kind), source (observed|directory), base-url
// Properties : .client, .grant (resolved grantee | null), .reset()
// Events     : grantee-selected {detail:{grantee}}, grantee-cleared, error {detail:{problem}}
//
// This is the correct-by-construction replacement for the interim <arazzo-admin-grant-input>: instead of making
// an operator hand-assemble a {dimension,value} tuple and guess the deployment's grain (a wrong value silently
// matches no one, over-grants a tenant, or locks the caller out — the §16.5.4 hazard), the operator names a
// *real* person/team/role/workflow and the server (GET /identity/grantees, directory + observed sources,
// reach-filtered) resolves it to the exact sys: identity. The value this element yields is always a
// server-resolved grantee, never a typed tuple. A grantee whose identity is not `complete` is flagged, because
// exact-set-equality membership would not match a partial identity.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

const SEARCH_LIMIT = 8;
const DEBOUNCE_MS = 200;
// The well-known grantee kinds §16.5.4 names; a deployment maps each to an unforgeable sys: identity. (A future
// increment can source the live set from GET /identity/capabilities' SupportedGranteeKinds.)
const KINDS = ['person', 'team', 'role', 'workflow', 'tenant'];

class ArazzoGranteePicker extends ArazzoElement {
  static get observedAttributes() {
    return ['placeholder', 'kind', 'source', 'base-url'];
  }

  constructor() {
    super();
    /** @private */ this._timer = null;
    /** @private */ this._seq = 0;
    /** @private @type {object[]} */ this._results = [];
    /** @private @type {object|null} */ this._selected = null;
    /** @private */ this._built = false;
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
  }

  attributeChangedCallback(name) {
    if (!this._built) return;
    if (name === 'base-url') this._client = undefined;
    if (name === 'placeholder') {
      const input = this.$('.q');
      if (input) input.placeholder = this.getAttribute('placeholder') || 'Find a person, team, role…';
    }
    if (name === 'kind') this.renderKindFilter();
  }

  requestRender() { /* the client is read lazily on the next search; nothing to redraw eagerly. */ }

  /** The locked kind (search is fixed to it) when the `kind` attribute is set, else the dropdown selection. */
  get kind() {
    return this.getAttribute('kind') || this.$('.kind')?.value || '';
  }

  /** The chosen, server-resolved grantee, or null when nothing is selected. */
  get grant() {
    return this._selected;
  }

  /** Clear the selection and the query — back to an empty picker. */
  reset() {
    this._selected = null;
    this._results = [];
    const input = this.$('.q');
    if (input) input.value = '';
    this.renderSelection();
    this.hideResults();
  }

  renderShell() {
    this._built = true;
    const placeholder = this.getAttribute('placeholder') || 'Find a person, team, role…';
    const locked = this.getAttribute('kind');
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; position: relative; }
        .row { display: flex; gap: 8px; align-items: stretch; }
        .kind { width: 130px; flex: none; font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text); }
        .q { flex: 1; min-width: 0; font: inherit; padding: 6px 10px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
        .results { position: absolute; z-index: 20; left: 0; right: 0; margin-top: 4px; list-style: none; padding: 4px; max-height: 280px; overflow-y: auto; background: var(--_bg); border: 1px solid var(--_border); border-radius: var(--_radius); box-shadow: 0 6px 24px rgba(0,0,0,0.12); }
        .results li { padding: 7px 9px; border-radius: var(--_radius); cursor: pointer; display: flex; gap: 8px; align-items: baseline; }
        .results li:hover, .results li[aria-selected="true"] { background: var(--_surface); }
        .badge { font-size: 11px; padding: 1px 6px; border-radius: 999px; border: 1px solid var(--_border); color: var(--_muted); flex: none; }
        .label { font-weight: 600; }
        .ident { color: var(--_muted); font-size: 12px; }
        .src { color: var(--_muted); font-size: 11px; margin-left: auto; flex: none; }
        .partial { color: var(--_danger); border-color: var(--_danger); }
        .chip { display: flex; gap: 8px; align-items: center; padding: 7px 10px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); }
        .chip .label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .chip .ident { white-space: nowrap; }
        .warn { color: var(--_danger); font-size: 12px; margin-top: 6px; }
        .hint { color: var(--_muted); font-size: 12px; margin-top: 6px; }
      </style>
      <div class="picker">
        <div class="row">
          ${locked ? '' : `<select class="kind" aria-label="grantee kind"></select>`}
          <input class="q" type="search" part="input" autocomplete="off" role="combobox" aria-expanded="false"
                 aria-autocomplete="list" placeholder="${escapeHtml(placeholder)}" aria-label="${escapeHtml(placeholder)}">
        </div>
        <ul class="results" role="listbox" hidden></ul>
        <div class="selected" hidden></div>
        <div class="msg" hidden></div>
      </div>
    `;
    this.renderKindFilter();
    const input = this.$('.q');
    input.addEventListener('input', () => {
      clearTimeout(this._timer);
      this._timer = setTimeout(() => this.runSearch(input.value.trim()), DEBOUNCE_MS);
    });
    input.addEventListener('keydown', (e) => this.onKeyDown(e));
    input.addEventListener('focus', () => { if (this._results.length) this.showResults(); });
    document.addEventListener('pointerdown', this._onDocDown ??= (e) => {
      if (!this.contains(e.target) && !this.shadowRoot.contains(e.composedPath()[0])) this.hideResults();
    });
  }

  disconnectedCallback() {
    clearTimeout(this._timer);
    if (this._onDocDown) document.removeEventListener('pointerdown', this._onDocDown);
  }

  /** @private */
  renderKindFilter() {
    const select = this.$('.kind');
    if (!select) return; // locked to a single kind via the attribute → no selector
    const current = select.value || '';
    select.innerHTML = `<option value="">Any kind</option>` + KINDS.map((k) => `<option value="${k}">${k}</option>`).join('');
    select.value = current;
    if (!select._wired) {
      select._wired = true;
      select.addEventListener('change', () => this.runSearch(this.$('.q')?.value.trim() ?? ''));
    }
  }

  /** @private */
  async runSearch(q) {
    const client = this.client;
    if (!client) return;
    const seq = ++this._seq;
    this.setMessage('');
    try {
      const { grantees } = await client.searchGrantees({
        q: q || undefined,
        kind: this.kind || undefined,
        source: this.getAttribute('source') || undefined,
        limit: SEARCH_LIMIT,
      });
      if (seq !== this._seq) return; // a newer keystroke superseded this response
      this._results = grantees;
      this.renderResults();
    } catch (problem) {
      if (seq !== this._seq) return;
      this._results = [];
      this.hideResults();
      this.setMessage('Could not search grantees.', true);
      this.emit('error', { problem });
    }
  }

  /** @private */
  renderResults() {
    const list = this.$('.results');
    if (!list) return;
    if (!this._results.length) {
      list.innerHTML = `<li role="option" aria-disabled="true" class="muted" style="cursor:default">No matches</li>`;
      this.showResults();
      return;
    }
    list.innerHTML = this._results.map((g, i) => {
      const ident = (g.identity || []).map((t) => `${t.dimension}=${t.value}`).join(' · ');
      const partial = g.complete === false;
      return `<li role="option" data-index="${i}" aria-selected="false" title="${escapeHtml(ident)}">
        <span class="badge${partial ? ' partial' : ''}">${escapeHtml(g.kind || '?')}</span>
        <span>
          <span class="label">${escapeHtml(g.label || g.value || '')}</span>
          <div class="ident">${escapeHtml(ident)}${partial ? ' — partial identity' : ''}</div>
        </span>
        <span class="src">${escapeHtml(g.source || '')}</span>
      </li>`;
    }).join('');
    for (const li of this.$$('.results li[data-index]')) {
      li.addEventListener('click', () => this.selectGrantee(Number(li.dataset.index)));
    }
    this._active = -1;
    this.showResults();
  }

  /** @private */
  selectGrantee(index) {
    const grantee = this._results[index];
    if (!grantee) return;
    this._selected = grantee;
    const input = this.$('.q');
    if (input) input.value = '';
    this.hideResults();
    this.renderSelection();
    this.emit('grantee-selected', { grantee });
  }

  /** @private */
  renderSelection() {
    const box = this.$('.selected');
    const picker = this.$('.row');
    if (!box) return;
    if (!this._selected) {
      box.hidden = true;
      box.innerHTML = '';
      if (picker) picker.hidden = false;
      this.setMessage('');
      return;
    }
    const g = this._selected;
    const ident = (g.identity || []).map((t) => `${t.dimension}=${t.value}`).join(' · ');
    const partial = g.complete === false;
    box.hidden = false;
    if (picker) picker.hidden = true;
    box.innerHTML = `
      <div class="chip">
        <span class="badge${partial ? ' partial' : ''}">${escapeHtml(g.kind || '?')}</span>
        <span class="label" title="${escapeHtml(g.label || g.value || '')}">${escapeHtml(g.label || g.value || '')}</span>
        <span class="ident">${escapeHtml(ident)}</span>
        <button type="button" class="ghost clear" aria-label="Clear selection">✕</button>
      </div>
      ${partial ? `<div class="warn">This is a partial identity — a grant matches by exact identity, so it may match no one. Prefer a complete identity.</div>` : ''}`;
    box.querySelector('.clear').addEventListener('click', () => this.clearSelection());
  }

  /** @private */
  clearSelection() {
    this._selected = null;
    this.renderSelection();
    const input = this.$('.q');
    input?.focus();
    this.emit('grantee-cleared', {});
  }

  /** @private */
  onKeyDown(e) {
    const list = this.$('.results');
    if (!list || list.hidden) return;
    const items = this.$$('.results li[data-index]');
    if (!items.length) { if (e.key === 'Escape') this.hideResults(); return; }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      this._active = (this._active ?? -1) + (e.key === 'ArrowDown' ? 1 : -1);
      if (this._active < 0) this._active = items.length - 1;
      if (this._active >= items.length) this._active = 0;
      items.forEach((li, i) => li.setAttribute('aria-selected', String(i === this._active)));
      items[this._active].scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'Enter') {
      e.preventDefault();
      this.selectGrantee(this._active >= 0 ? this._active : 0);
    } else if (e.key === 'Escape') {
      this.hideResults();
    }
  }

  /** @private */
  showResults() {
    const list = this.$('.results');
    if (list) { list.hidden = false; this.$('.q')?.setAttribute('aria-expanded', 'true'); }
  }

  /** @private */
  hideResults() {
    const list = this.$('.results');
    if (list) { list.hidden = true; this.$('.q')?.setAttribute('aria-expanded', 'false'); }
  }

  /** @private */
  setMessage(text, isError = false) {
    const msg = this.$('.msg');
    if (!msg) return;
    msg.textContent = text || '';
    msg.hidden = !text;
    msg.className = `msg ${isError ? 'warn' : 'hint'}`;
  }
}

define('arazzo-grantee-picker', ArazzoGranteePicker);
export { ArazzoGranteePicker };
