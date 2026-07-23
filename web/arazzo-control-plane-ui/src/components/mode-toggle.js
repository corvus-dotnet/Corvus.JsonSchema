// <arazzo-mode-toggle> — the one segmented control for switching a panel between named modes
// (Form | JSON, and anywhere else two or three views share a surface). One reusable component so the
// toggle looks and behaves identically across the designer and the control plane, rather than each
// editor hand-rolling its own (payload, schema, scenario had three different looks).
//
//   const t = document.createElement('arazzo-mode-toggle');
//   t.options = [{ value: 'form', label: 'Form' }, { value: 'json', label: 'JSON' }];
//   t.value = 'form';
//   t.addEventListener('mode-changed', (e) => switchTo(e.detail.value));
//
// Attributes : disabled
// Properties : .options ([{ value, label }]), .value (the selected value), .disabled
// Events     : mode-changed { value } — fired only on a user click that changes the value (not on set)

import { ArazzoElement, SHARED_CSS, define } from './base.js';

class ArazzoModeToggle extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._options = [];
    /** @private */ this._value = null;
  }

  connectedCallback() {
    if (!this._built) this._renderShell();
    this._render();
  }

  /** The available modes: `[{ value, label }]`. Setting rebuilds the control. */
  get options() { return this._options; }
  set options(v) { this._options = Array.isArray(v) ? v : []; if (this._built) this._render(); }

  /** The selected value. Setting reflects the pressed state WITHOUT firing mode-changed. */
  get value() { return this._value; }
  set value(v) { this._value = v; if (this._built) this._reflect(); }

  get disabled() { return this.hasAttribute('disabled'); }
  set disabled(v) { this.toggleAttribute('disabled', !!v); if (this._built) this._render(); }

  _renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: inline-block; }
        .group { display: inline-flex; border: 1px solid var(--_border); border-radius: 6px; overflow: hidden; }
        button { border: none; background: var(--_bg); color: inherit; font: 12px var(--_font); padding: 3px 12px; cursor: pointer; }
        button + button { border-left: 1px solid var(--_border); }
        button[aria-pressed="true"] { background: var(--_surface); font-weight: 600; }
        button:not([aria-pressed="true"]):hover:not(:disabled) { background: var(--_surface); }
        button:disabled { cursor: default; opacity: 0.6; }
      </style>
      <div class="group" role="group"></div>`;
  }

  _render() {
    const group = this.$('.group');
    if (!group) return;
    if (this._value == null && this._options.length) this._value = this._options[0].value;
    group.replaceChildren();
    for (const opt of this._options) {
      const b = document.createElement('button');
      b.type = 'button';
      b.dataset.value = opt.value;
      b.textContent = opt.label ?? opt.value;
      b.setAttribute('aria-pressed', String(opt.value === this._value));
      b.disabled = this.disabled;
      b.addEventListener('click', () => this._select(opt.value));
      group.appendChild(b);
    }
  }

  /** Update the pressed state to match `this._value` without rebuilding (no event). @private */
  _reflect() {
    for (const b of this.$$('button')) b.setAttribute('aria-pressed', String(b.dataset.value === this._value));
  }

  /** @private */
  _select(value) {
    if (value === this._value || this.disabled) return;
    this._value = value;
    this._reflect();
    this.emit('mode-changed', { value });
  }
}

define('arazzo-mode-toggle', ArazzoModeToggle);
export { ArazzoModeToggle };
