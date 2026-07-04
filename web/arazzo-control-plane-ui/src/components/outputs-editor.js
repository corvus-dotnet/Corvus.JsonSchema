// <arazzo-outputs-editor> — a name → runtime-expression map editor (step `outputs` and workflow
// `outputs` share this shape). Values are edited through <arazzo-expression-input>.
//
//   const ed = document.createElement('arazzo-outputs-editor');
//   ed.completionContext = { … };
//   ed.value = step.outputs;                     // { name: '$response.body#/…', … }
//   ed.addEventListener('outputs-changed', (e) => { step.outputs = e.detail.outputs; });
//
// External `.value` sets rebuild; internal edits mutate and emit (focus preserved).

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './expression-input.js';

class ArazzoOutputsEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private — [{name, expression}] preserves order and tolerates in-progress renames. */
    this._entries = [];
    /** @private */ this._completionContext = {};
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.renderRows();
  }

  /** The outputs map. Setting rebuilds the rows. */
  get value() {
    return Object.fromEntries(this._entries.filter((e) => e.name).map((e) => [e.name, e.expression]));
  }

  set value(map) {
    this._entries = Object.entries(map || {}).map(([name, expression]) => ({ name, expression }));
    if (this.isConnected) { if (!this._built) this.renderShell(); this.renderRows(); }
  }

  /** Schema context forwarded to the expression inputs. */
  get completionContext() { return this._completionContext; }
  set completionContext(ctx) {
    this._completionContext = ctx || {};
    for (const x of this.$$('arazzo-expression-input')) x.completionContext = this._completionContext;
  }

  /** @private */
  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .rows { display: grid; gap: 6px; }
        .orow { display: grid; grid-template-columns: minmax(90px, 2fr) 3fr auto; gap: 6px; align-items: center; }
        input.oname {
          width: 100%; box-sizing: border-box; font: 12.5px ui-monospace, SFMono-Regular, Menlo, monospace;
          padding: 7px 9px; border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text);
        }
        .add { margin-top: 6px; font-size: 12px; }
        .empty { padding: 6px 0; font-size: 12px; text-align: left; }
      </style>
      <div class="rows" part="rows"></div>
      <button class="add ghost" type="button">+ Add output</button>
    `;
    this.$('.add').addEventListener('click', () => {
      this._entries.push({ name: '', expression: '' });
      this.renderRows();
      this.$$('.orow .oname').at(-1)?.focus();
      // No emit yet — an unnamed row is not an output until it gets a name.
    });
  }

  /** @private */
  renderRows() {
    const rows = this.$('.rows');
    rows.replaceChildren();
    if (!this._entries.length) {
      rows.innerHTML = '<div class="empty muted">No outputs.</div>';
      return;
    }
    this._entries.forEach((entry, i) => {
      const row = document.createElement('div');
      row.className = 'orow';
      row.innerHTML = `
        <input class="oname" type="text" placeholder="name" value="${escapeHtml(entry.name)}">
        <span class="slot"></span>
        <button class="close" type="button" title="Remove output">✕</button>
      `;
      const exprInput = document.createElement('arazzo-expression-input');
      exprInput.setAttribute('placeholder', '$response.body#/…');
      exprInput.value = entry.expression || '';
      exprInput.completionContext = this._completionContext;
      row.querySelector('.slot').replaceWith(exprInput);

      row.querySelector('.oname').addEventListener('input', (e) => {
        entry.name = e.target.value.trim();
        this._emit();
      });
      exprInput.addEventListener('value-changed', (e) => {
        e.stopPropagation();
        entry.expression = e.detail.value;
        this._emit();
      });
      row.querySelector('.close').addEventListener('click', () => {
        this._entries.splice(i, 1);
        this.renderRows();
        this._emit();
      });
      rows.append(row);
    });
  }

  /** @private */
  _emit() {
    this.emit('outputs-changed', { outputs: this.value });
  }
}

define('arazzo-outputs-editor', ArazzoOutputsEditor);
export { ArazzoOutputsEditor };
