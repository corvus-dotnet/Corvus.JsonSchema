// <arazzo-environment-picker> — a type-to-search dropdown picker for a deployment environment (the shared
// search-picker UX; the environment sibling of <arazzo-workflow-picker>).
//
//   <arazzo-environment-picker placeholder="Filter by environment…"></arazzo-environment-picker>
//   el.client = client;                 // an ArazzoControlPlaneClient (environments:read)
//   el.addEventListener('environment-picked', (e) => use(e.detail.name));
//   const name = el.value;              // the chosen environment name, or '' when nothing is picked
//
// Attributes : placeholder, value, base-url
// Properties : .client, .value (the chosen environment name | ''), .reset()
// Events     : environment-picked {name}; also a bubbling `change` on pick/clear so a host can react.
//
// Same dropdown UX + shared PICKER_CSS styling as the workflow/grantee pickers, popping an initial suggestions
// list the moment it's focused. It resolves to a REAL environment within the caller's reach — you pick from the
// registry rather than free-typing a name that may match nothing. The list endpoint is already reach-scoped
// (§14.2), so an approver-scoped host simply sees the environments the server admits for them.

import { ArazzoElement, SHARED_CSS, PICKER_CSS, escapeHtml, define } from './base.js';

const SEARCH_LIMIT = 8;
const DEBOUNCE_MS = 200;

class ArazzoEnvironmentPicker extends ArazzoElement {
  static get observedAttributes() {
    return ['placeholder', 'value', 'base-url'];
  }

  constructor() {
    super();
    /** @private */ this._timer = null;
    /** @private */ this._seq = 0;
    /** @private @type {Array<{name:string,displayName?:string}>} */ this._results = [];
    /** @private @type {string|null} */ this._selected = null;
    /** @private */ this._searched = false;
    /** @private */ this._active = -1;
    /** @private */ this._built = false;
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    const v = this.getAttribute('value');
    if (v) { this._selected = v; this.renderSelection(); }
  }

  disconnectedCallback() {
    clearTimeout(this._timer);
    if (this._onDocDown) document.removeEventListener('pointerdown', this._onDocDown);
  }

  attributeChangedCallback(name, _old, value) {
    if (!this._built) return;
    if (name === 'base-url') this._client = undefined;
    else if (name === 'placeholder') { const i = this.$('.q'); if (i) i.placeholder = value || ''; }
    else if (name === 'value') { this._selected = value || null; this.renderSelection(); }
  }

  requestRender() { /* client is read lazily on the next search */ }

  /** The chosen environment name, or '' when nothing is picked. */
  get value() { return this._selected || ''; }

  set value(v) { this._selected = v || null; if (this._built) this.renderSelection(); }

  reset() {
    this._selected = null;
    this._results = [];
    this._searched = false;
    const input = this.$('.q');
    if (input) input.value = '';
    this.renderSelection();
    this.hideResults();
  }

  renderShell() {
    this._built = true;
    const placeholder = this.getAttribute('placeholder') || 'Find an environment…';
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${PICKER_CSS}
        :host { display: block; position: relative; }
        .chip { display: flex; gap: 8px; align-items: center; padding: 7px 10px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); }
        .chip .env { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
      </style>
      <div class="picker">
        <input class="q" type="search" part="input" autocomplete="off" role="combobox" aria-expanded="false"
               aria-autocomplete="list" placeholder="${escapeHtml(placeholder)}" aria-label="${escapeHtml(placeholder)}">
        <ul class="results" role="listbox" hidden></ul>
        <div class="selected" hidden></div>
      </div>
    `;
    const input = this.$('.q');
    input.addEventListener('input', () => {
      clearTimeout(this._timer);
      this._timer = setTimeout(() => this.runSearch(input.value.trim()), DEBOUNCE_MS);
    });
    // Pop an initial suggestions list the moment the field is focused — no need to type first.
    input.addEventListener('focus', () => { if (this._results.length) this.showResults(); else this.runSearch(''); });
    input.addEventListener('keydown', (e) => this.onKeyDown(e));
    document.addEventListener('pointerdown', this._onDocDown ??= (e) => {
      if (!this.contains(e.target) && !this.shadowRoot.contains(e.composedPath()[0])) this.hideResults();
    });
    const v = this.getAttribute('value');
    if (v) { this._selected = v; this.renderSelection(); }
  }

  async runSearch(q) {
    const client = this.client;
    if (!client) return;
    const seq = ++this._seq;
    try {
      // The registry is small and already reach-scoped server-side; fetch a page and filter by substring over
      // name/displayName so "prod" finds "production".
      const { environments } = await client.listEnvironments({ limit: SEARCH_LIMIT * 3 });
      if (seq !== this._seq) return;
      const needle = q.toLowerCase();
      const out = [];
      for (const e of environments) {
        if (needle && !e.name.toLowerCase().includes(needle) && !(e.displayName || '').toLowerCase().includes(needle)) continue;
        out.push({ name: e.name, displayName: e.displayName });
        if (out.length >= SEARCH_LIMIT) break;
      }
      this._results = out;
      this._searched = true;
      this.renderResults();
    } catch {
      this._results = [];
      this.hideResults();
    }
  }

  renderResults() {
    const list = this.$('.results');
    if (!list) return;
    if (!this._results.length) {
      list.innerHTML = `<li role="option" aria-disabled="true">No matching environments</li>`;
      this.showResults();
      return;
    }
    list.innerHTML = this._results.map((r, i) => `
      <li role="option" data-index="${i}" aria-selected="false">
        <span>
          <span class="label">${escapeHtml(r.displayName || r.name)}</span>
          ${r.displayName && r.displayName !== r.name ? `<div class="ident">${escapeHtml(r.name)}</div>` : ''}
        </span>
      </li>`).join('');
    for (const li of this.$$('.results li[data-index]')) {
      li.addEventListener('click', () => this.selectItem(Number(li.dataset.index)));
    }
    this._active = -1;
    this.showResults();
  }

  selectItem(index) {
    const r = this._results[index];
    if (!r) return;
    this._selected = r.name;
    const input = this.$('.q');
    if (input) input.value = '';
    this.hideResults();
    this.renderSelection();
    this.emit('environment-picked', { name: r.name });
    this.dispatchEvent(new CustomEvent('change', { bubbles: true, composed: true }));
  }

  renderSelection() {
    const box = this.$('.selected');
    const input = this.$('.q');
    if (!box) return;
    if (!this._selected) {
      box.hidden = true; box.innerHTML = '';
      if (input) input.hidden = false;
      return;
    }
    box.hidden = false;
    if (input) input.hidden = true;
    box.innerHTML = `
      <div class="chip">
        <span class="env" title="${escapeHtml(this._selected)}">${escapeHtml(this._selected)}</span>
        <button type="button" class="ghost clear" aria-label="Clear selection">✕</button>
      </div>`;
    box.querySelector('.clear').addEventListener('click', () => this.clearSelection());
  }

  clearSelection() {
    this._selected = null;
    this.renderSelection();
    this.$('.q')?.focus();
    this.dispatchEvent(new CustomEvent('change', { bubbles: true, composed: true }));
  }

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
      this.selectItem(this._active >= 0 ? this._active : 0);
    } else if (e.key === 'Escape') {
      this.hideResults();
    }
  }

  showResults() {
    const list = this.$('.results');
    if (list) { list.hidden = false; this.$('.q')?.setAttribute('aria-expanded', 'true'); }
  }

  hideResults() {
    const list = this.$('.results');
    if (list) { list.hidden = true; this.$('.q')?.setAttribute('aria-expanded', 'false'); }
  }
}

define('arazzo-environment-picker', ArazzoEnvironmentPicker);