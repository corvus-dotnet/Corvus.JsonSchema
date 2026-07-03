// <arazzo-environment-input> — a text input with a themed, filtered dropdown of the system's deployment
// environments (§7.7).
//
//   <arazzo-environment-input placeholder="Choose or type an environment…"></arazzo-environment-input>
//   el.client = client;                 // an ArazzoControlPlaneClient (environments:read for the list)
//   el.value;                           // the chosen environment name
//   el.addEventListener('input', (e) => doSomething(e.target.value));
//
// Attributes : placeholder, value, base-url
// Properties : .client, .value, .readOnly
// Events     : input / change bubble out (composed) retargeted to this element, so `e.target.value` works.
//
// It loads every environment the caller can see (one small governed set, §7.7) and offers them as a type-to-filter
// listbox (the shared PICKER_CSS look — same as the grantee picker), while still accepting a free-typed value (you
// may credential an environment that is about to be created). Best-effort: without environments:read the list is
// empty and it behaves as a plain free-text input.

import { ArazzoElement, SHARED_CSS, PICKER_CSS, escapeHtml, define } from './base.js';

class ArazzoEnvironmentInput extends ArazzoElement {
  static get observedAttributes() {
    return ['placeholder', 'value', 'base-url'];
  }

  constructor() {
    super();
    /** @private */ this._envs = [];
    /** @private */ this._seq = 0;
    /** @private */ this._active = -1;
  }

  connectedCallback() {
    this.renderShell();
    this.loadEnvironments();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected) return;
    const input = this.$('input');
    if (name === 'placeholder' && input) input.placeholder = newValue || '';
    else if (name === 'value' && input && input.value !== (newValue || '')) input.value = newValue || '';
    else if (name === 'base-url') { this._client = undefined; this.loadEnvironments(); }
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

  /** Reload the environment list (e.g. after the client changes — the base setter calls this). */
  requestRender() { this.loadEnvironments(); }

  renderShell() {
    const placeholder = this.getAttribute('placeholder') || 'Choose or type an environment…';
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
        /* Stack the environment name over its description (the subtitle). */
        .results li .txt { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
        .results li .sub { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
      </style>
      <input type="text" part="input" role="combobox" aria-autocomplete="list" aria-expanded="false" autocomplete="off"
             placeholder="${escapeHtml(placeholder)}" aria-label="Environment" value="${escapeHtml(this.getAttribute('value') || '')}">
      <ul class="results" role="listbox" hidden></ul>
    `;
    const input = this.$('input');
    input.addEventListener('input', () => this.open());
    input.addEventListener('focus', () => this.open());
    input.addEventListener('keydown', (e) => this.onKey(e));
    // A short delay so a click on a result (which blurs the input first) still registers before we close.
    input.addEventListener('blur', () => setTimeout(() => this.close(), 120));
  }

  /**
   * Load every environment the caller can see (walking all pages). Best-effort: without environments:read the list
   * stays empty and the field is plain free text.
   */
  async loadEnvironments() {
    const client = this.client;
    if (!client) return;
    const seq = ++this._seq;
    try {
      const envs = [];
      const seen = new Set();
      for await (const page of client.listEnvironmentsPaged({ limit: 200 })) {
        for (const e of (page.environments || [])) {
          if (e?.name && !seen.has(e.name)) { seen.add(e.name); envs.push({ name: e.name, description: e.description || '' }); }
        }
      }
      if (seq !== this._seq) return; // a newer load superseded this one
      this._envs = envs;
    } catch {
      this._envs = [];
    }
  }

  /** Show the environments matching what's typed (substring, case-insensitive) as a listbox. */
  open() {
    const input = this.$('input');
    const list = this.$('.results');
    if (!input || !list || input.readOnly || this._selecting) return;
    const q = input.value.trim().toLowerCase();
    const matches = this._envs.filter((e) => `${e.name} ${e.description}`.toLowerCase().includes(q));
    if (!matches.length) { this.close(); return; }
    this._active = -1;
    list.innerHTML = matches
      .map((e) => `<li role="option" aria-selected="false" data-name="${escapeHtml(e.name)}"><span class="txt"><span class="label">${escapeHtml(e.name)}</span>${e.description ? `<span class="sub">${escapeHtml(e.description)}</span>` : ''}</span></li>`)
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

define('arazzo-environment-input', ArazzoEnvironmentInput);
export { ArazzoEnvironmentInput };
