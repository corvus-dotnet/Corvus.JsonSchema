// <arazzo-source-input> — a text input with a themed, filtered dropdown of the registered sources (§7.6).
//
//   <arazzo-source-input placeholder="Choose or type a source…"></arazzo-source-input>
//   el.client = client;                 // an ArazzoControlPlaneClient (sources:read for the list)
//   el.value;                           // the chosen source name
//   el.addEventListener('input', (e) => doSomething(e.target.value));
//
// Attributes : placeholder, value, base-url
// Properties : .client, .value, .readOnly
// Events     : input / change bubble out (composed) retargeted to this element, so `e.target.value` works.
//
// It loads every source the caller can see and offers them as a type-to-filter listbox (the shared PICKER_CSS look —
// same as the environment input and the grantee picker), while still accepting a free-typed value (you may bind a
// credential to a source that is about to be registered). Best-effort: without sources:read the list is empty and it
// behaves as a plain free-text input. A direct analogue of <arazzo-environment-input>.

import { ArazzoElement, SHARED_CSS, PICKER_CSS, escapeHtml, define } from './base.js';

class ArazzoSourceInput extends ArazzoElement {
  static get observedAttributes() {
    return ['placeholder', 'value', 'base-url'];
  }

  constructor() {
    super();
    /** @private */ this._sources = [];
    /** @private */ this._seq = 0;
    /** @private */ this._active = -1;
  }

  connectedCallback() {
    this.renderShell();
    this.loadSources();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected) return;
    const input = this.$('input');
    if (name === 'placeholder' && input) input.placeholder = newValue || '';
    else if (name === 'value' && input && input.value !== (newValue || '')) input.value = newValue || '';
    else if (name === 'base-url') { this._client = undefined; this.loadSources(); }
  }

  get value() { return this.$('input')?.value ?? this.getAttribute('value') ?? ''; }

  set value(v) {
    const input = this.$('input');
    if (input) input.value = v ?? '';
    else this.setAttribute('value', v ?? '');
  }

  get readOnly() { return this.$('input')?.readOnly ?? false; }

  set readOnly(v) {
    const input = this.$('input');
    if (input) input.readOnly = !!v;
    if (v) this.close();
  }

  /** Reload the source list (e.g. after the client changes — the base setter calls this). */
  requestRender() { this.loadSources(); }

  renderShell() {
    const placeholder = this.getAttribute('placeholder') || 'Choose or type a source…';
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${PICKER_CSS}
        :host { display: block; position: relative; }
        input {
          width: 100%; font: inherit; padding: 6px 10px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background: var(--_bg); color: var(--_text);
        }
        input:read-only { opacity: 0.7; cursor: default; }
        /* Stack the source name over its type/description (the subtitle). */
        .results li .txt { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
        .results li .sub { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
      </style>
      <input type="text" part="input" role="combobox" aria-autocomplete="list" aria-expanded="false" autocomplete="off"
             placeholder="${escapeHtml(placeholder)}" aria-label="Source" value="${escapeHtml(this.getAttribute('value') || '')}">
      <ul class="results" role="listbox" hidden></ul>
    `;
    const input = this.$('input');
    input.addEventListener('input', () => this.open());
    input.addEventListener('focus', () => this.open());
    input.addEventListener('keydown', (e) => this.onKey(e));
    // A native 'change' is NOT composed, so a typed value committed by tab/click-away dies at the shadow
    // boundary; re-dispatch it from the host so listeners see the commit exactly like a listbox pick.
    input.addEventListener('change', (e) => {
      if (this._selecting) return; // a pick already dispatched the composed pair
      e.stopPropagation();
      this.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    });
    // A short delay so a click on a result (which blurs the input first) still registers before we close.
    input.addEventListener('blur', () => setTimeout(() => this.close(), 120));
  }

  /**
   * Load every source the caller can see (walking all pages). Best-effort: without sources:read the list stays empty
   * and the field is plain free text.
   */
  async loadSources() {
    const client = this.client;
    if (!client) return;
    const seq = ++this._seq;
    try {
      const sources = [];
      const seen = new Set();
      for await (const page of client.listSourcesPaged({ limit: 200 })) {
        for (const s of (page.sources || [])) {
          if (s?.name && !seen.has(s.name)) {
            seen.add(s.name);
            const sub = s.displayName || s.description || '';
            sources.push({ name: s.name, sub: s.type ? (sub ? `${s.type} · ${sub}` : s.type) : sub });
          }
        }
      }
      if (seq !== this._seq) return; // a newer load superseded this one
      this._sources = sources;
    } catch {
      this._sources = [];
    }
  }

  /** Show the sources matching what's typed (substring, case-insensitive) as a listbox. */
  open() {
    const input = this.$('input');
    const list = this.$('.results');
    if (!input || !list || input.readOnly || this._selecting) return;
    const q = input.value.trim().toLowerCase();
    const matches = this._sources.filter((s) => `${s.name} ${s.sub}`.toLowerCase().includes(q));
    if (!matches.length) { this.close(); return; }
    this._active = -1;
    list.innerHTML = matches
      .map((s) => `<li role="option" aria-selected="false" data-name="${escapeHtml(s.name)}"><span class="txt"><span class="label">${escapeHtml(s.name)}</span>${s.sub ? `<span class="sub">${escapeHtml(s.sub)}</span>` : ''}</span></li>`)
      .join('');
    // mousedown (not click) so selection runs before the input's blur closes the list.
    list.querySelectorAll('li').forEach((li) => li.addEventListener('mousedown', (e) => { e.preventDefault(); this.select(li.dataset.name); }));
    list.hidden = false;
    input.setAttribute('aria-expanded', 'true');
  }

  close() {
    const list = this.$('.results');
    if (list) list.hidden = true;
    this.$('input')?.setAttribute('aria-expanded', 'false');
    this._active = -1;
  }

  select(name) {
    this._selecting = true; // the input event we dispatch would otherwise re-open the list
    this.value = name;
    this.$('input').dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    this.$('input').dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    this._selecting = false;
    this.close();
  }

  onKey(e) {
    const list = this.$('.results');
    if (list.hidden) {
      if (e.key === 'ArrowDown') this.open();
      return;
    }
    const items = [...list.querySelectorAll('li')];
    if (!items.length) return;
    if (e.key === 'ArrowDown') { e.preventDefault(); this._active = (this._active + 1) % items.length; this.highlight(items); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); this._active = (this._active - 1 + items.length) % items.length; this.highlight(items); }
    else if (e.key === 'Enter' && this._active >= 0) { e.preventDefault(); this.select(items[this._active].dataset.name); }
    else if (e.key === 'Escape') { this.close(); }
  }

  highlight(items) {
    items.forEach((li, i) => li.setAttribute('aria-selected', i === this._active ? 'true' : 'false'));
    if (this._active >= 0) items[this._active].scrollIntoView({ block: 'nearest' });
  }
}

define('arazzo-source-input', ArazzoSourceInput);
export { ArazzoSourceInput };
