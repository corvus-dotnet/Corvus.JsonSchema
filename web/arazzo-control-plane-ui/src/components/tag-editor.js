// <arazzo-tag-editor> — a reusable key/value tag editor for §14.2 security / management tags.
//
// Renders each tag as a removable row (key input + value input + a ✕ button) plus an "Add tag" button, so ADD, EDIT and
// DELETE are all explicit affordances (unlike a single free-text field). Seed it by setting `.tags` to an
// [{ key, value }] array; read the current non-empty rows back from the `.tags` getter. Emits `tags-changed` on any edit.
//
// Properties : .tags ([{ key, value }] — get returns the current non-empty rows; set re-seeds and re-renders)
// Events     : tags-changed (on add / remove / input)
import { ArazzoElement, escapeHtml, define } from './base.js';

class ArazzoTagEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._tags = [];
  }

  /** Seed the editor's rows. @param {Array<{ key: string, value: string }>} value */
  set tags(value) {
    this._tags = Array.isArray(value) ? value.map((t) => ({ key: t.key ?? '', value: t.value ?? '' })) : [];
    if (this.isConnected) this.render();
  }

  /** The current tags as [{ key, value }], excluding wholly-empty rows. */
  get tags() {
    return this.collect().filter((t) => t.key || t.value);
  }

  connectedCallback() {
    this.render();
  }

  // Every row's current input values (including partially-filled ones — the getter filters wholly-empty rows).
  collect() {
    return this.$$('.tag-row').map((row) => ({
      key: row.querySelector('.tk').value.trim(),
      value: row.querySelector('.tv').value.trim(),
    }));
  }

  addRow() {
    this._tags = this.collect();
    this._tags.push({ key: '', value: '' });
    this.render();
    const rows = this.$$('.tag-row');
    rows[rows.length - 1]?.querySelector('.tk')?.focus();
    this.emit('tags-changed');
  }

  removeRow(index) {
    this._tags = this.collect();
    this._tags.splice(index, 1);
    this.render();
    this.emit('tags-changed');
  }

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        :host { display: block; }
        .tag-row { display: flex; align-items: center; gap: 6px; margin-bottom: 6px; }
        .tag-row input { flex: 1 1 0; min-width: 0; padding: 4px 8px; border: 1px solid var(--_border, #ccc); border-radius: 6px; background: var(--_bg, #fff); color: var(--_text, #111); font: inherit; }
        .eq { color: var(--_muted, #888); }
        .rm { border: 1px solid var(--_border, #ccc); background: transparent; color: var(--_muted, #888); border-radius: 6px; cursor: pointer; padding: 2px 9px; line-height: 1.4; }
        .rm:hover { color: var(--_text, #111); border-color: var(--_muted, #888); }
        .add { border: 1px dashed var(--_border, #ccc); background: transparent; color: var(--_text, #111); border-radius: 6px; cursor: pointer; padding: 4px 10px; font: inherit; }
        .add:hover { border-style: solid; }
        .empty { color: var(--_muted, #888); margin-bottom: 6px; font-style: italic; }
      </style>
      ${this._tags.length ? '' : '<div class="empty">No tags yet.</div>'}
      <div class="rows">
        ${this._tags.map((t, i) => `
          <div class="tag-row" data-i="${i}">
            <input class="tk" type="text" placeholder="key" value="${escapeHtml(t.key)}" aria-label="Tag key">
            <span class="eq">=</span>
            <input class="tv" type="text" placeholder="value" value="${escapeHtml(t.value)}" aria-label="Tag value">
            <button class="rm" type="button" title="Remove tag" aria-label="Remove tag">✕</button>
          </div>`).join('')}
      </div>
      <button class="add" type="button">+ Add tag</button>
    `;
    this.$('.add').addEventListener('click', () => this.addRow());
    this.$$('.rm').forEach((btn, i) => btn.addEventListener('click', () => this.removeRow(i)));
    this.$$('.tk, .tv').forEach((inp) => inp.addEventListener('input', () => this.emit('tags-changed')));
  }
}

define('arazzo-tag-editor', ArazzoTagEditor);
export { ArazzoTagEditor };