// <arazzo-workflow-picker> — a type-to-search dropdown picker for a catalogued workflow (the shared search-picker UX).
//
//   <arazzo-workflow-picker placeholder="Find a workflow…"></arazzo-workflow-picker>
//   el.client = client;                 // an ArazzoControlPlaneClient (catalog:read)
//   el.addEventListener('workflow-picked', (e) => use(e.detail.baseWorkflowId));
//   const id = el.value;                // the chosen base workflow id, or '' when nothing is picked
//
// Attributes : placeholder, value, base-url
// Properties : .client, .value (the chosen base workflow id | ''), .reset()
// Events     : workflow-picked {baseWorkflowId}; also a bubbling `change` on pick/clear so a host can react.
//
// The workflow sibling of <arazzo-grantee-picker>: same dropdown UX + shared PICKER_CSS styling, and it pops an initial
// suggestions list the moment it's focused (no need to type first). It resolves to a REAL catalogued base workflow —
// you pick from the catalog rather than free-typing an id that may match nothing. `.value` mirrors the old
// <arazzo-workflow-id-input> so hosts read the chosen id the same way.

import { ArazzoElement, SHARED_CSS, PICKER_CSS, escapeHtml, define } from './base.js';

const SEARCH_LIMIT = 8;
const DEBOUNCE_MS = 200;

class ArazzoWorkflowPicker extends ArazzoElement {
  static get observedAttributes() {
    return ['placeholder', 'value', 'base-url'];
  }

  constructor() {
    super();
    /** @private */ this._timer = null;
    /** @private */ this._seq = 0;
    /** @private @type {Array<{id:string,title?:string}>} */ this._results = [];
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

  /** The chosen base workflow id, or '' when nothing is picked (mirrors the old workflow-id-input). */
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
    const placeholder = this.getAttribute('placeholder') || 'Find a workflow…';
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${PICKER_CSS}
        :host { display: block; position: relative; }
        .chip { display: flex; gap: 8px; align-items: center; padding: 7px 10px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); }
        .chip .wf { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
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
      // Free-text find over title/description/id (substring), not an anchored id prefix — so "pet" finds "adopt-pet".
      // Wider fetch + client-side dedupe to distinct base ids (the catalog returns versions).
      const { versions } = await client.searchCatalog({ q: q || undefined, limit: SEARCH_LIMIT * 3 });
      if (seq !== this._seq) return;
      const seen = new Set();
      const out = [];
      for (const v of versions) {
        const id = v.baseWorkflowId;
        if (id && !seen.has(id)) { seen.add(id); out.push({ id, title: v.title }); }
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
      list.innerHTML = `<li role="option" aria-disabled="true">No matching workflows</li>`;
      this.showResults();
      return;
    }
    list.innerHTML = this._results.map((r, i) => `
      <li role="option" data-index="${i}" aria-selected="false">
        <span>
          <span class="label">${escapeHtml(r.title || r.id)}</span>
          ${r.title && r.title !== r.id ? `<div class="ident">${escapeHtml(r.id)}</div>` : ''}
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
    this._selected = r.id;
    const input = this.$('.q');
    if (input) input.value = '';
    this.hideResults();
    this.renderSelection();
    this.emit('workflow-picked', { baseWorkflowId: r.id });
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
        <span class="wf" title="${escapeHtml(this._selected)}">${escapeHtml(this._selected)}</span>
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

define('arazzo-workflow-picker', ArazzoWorkflowPicker);
export { ArazzoWorkflowPicker };