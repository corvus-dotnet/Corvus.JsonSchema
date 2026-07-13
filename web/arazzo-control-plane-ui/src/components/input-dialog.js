// <arazzo-input-dialog> — the kit's standard ask. System prompt/confirm/alert are banned in this
// UI: when a flow needs a value or a yes/no, it pops THIS modal instead — styled, theme-aware,
// promise-based, and drivable from tests.
//
//   const ask = document.createElement('arazzo-input-dialog');
//   document.body.appendChild(ask);
//   const name = await ask.ask({ title: 'New working copy', field: { label: 'Name', value: 'untitled' } });
//   const sure = await ask.ask({ title: 'Delete?', message: '…', confirmLabel: 'Delete', danger: true });
//
// ask(options) → Promise resolving to the field's string (single-field mode), an object keyed by each
// field's `key` (multi-field mode), true (confirm mode), or null (cancelled/dismissed). One ask at a
// time; a second ask while open rejects the first as null.
//
// Properties : (none) · Methods: ask({title, message?, field?: {label, value?, placeholder?},
//              fields?: [{key, label, value?, placeholder?}], confirmLabel?, cancelLabel?, danger?})
// Events     : (none; the promise is the contract)

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

class ArazzoInputDialog extends ArazzoElement {
  connectedCallback() {
    this.render();
  }

  /** Opens the modal; resolves with the value (single field), an object keyed by field (multi-field),
   *  true (confirm mode), or null (cancelled). */
  ask({ title, message, field, fields, confirmLabel = 'OK', cancelLabel = 'Cancel', danger = false } = {}) {
    this._resolve?.(null); // a second ask supersedes the first
    this.renderBody({ title, message, field, fields, confirmLabel, cancelLabel, danger });
    const dialog = this.$('dialog');
    dialog.showModal();
    this.$('.in-field')?.focus();
    this.$('.in-field')?.select?.();
    return new Promise((resolve) => { this._resolve = resolve; });
  }

  /** @private — the current field value(s): a string (single), an object keyed by field (multi), or
   *  true (no field = confirm mode). */
  collect() {
    const inputs = [...this.shadowRoot.querySelectorAll('.in-field')];
    if (!inputs.length) return true;
    if (this._multi) {
      const out = {};
      for (const i of inputs) out[i.dataset.key] = i.value;
      return out;
    }
    return inputs[0].value;
  }

  /** @private */
  settle(value) {
    const resolve = this._resolve;
    this._resolve = null;
    this.$('dialog').close();
    resolve?.(value);
  }

  /** @private */
  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: 10px; background: var(--_bg); color: var(--arazzo-text, inherit);
                 padding: 0; width: min(440px, 92vw); }
        dialog::backdrop { background: rgba(0, 0, 0, 0.35); }
        .head { display: flex; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--_border); }
        .head h2 { margin: 0; font-size: 14px; flex: 1; }
        .body { padding: 14px; display: grid; gap: 10px; font-size: 13px; }
        .message { color: var(--_muted); }
        label { display: grid; gap: 4px; font-size: 12px; color: var(--_muted); }
        input { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px;
                background: var(--_bg); color: var(--arazzo-text, inherit); }
        .foot { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 14px; border-top: 1px solid var(--_border); }
        button { font: inherit; font-size: 13px; padding: 5px 14px; border: 1px solid var(--_border); border-radius: 8px;
                 background: var(--_bg); color: inherit; cursor: pointer; }
        button.confirm { border-color: var(--_accent); font-weight: 600; }
        button.confirm.danger { background: var(--arazzo-danger, #d4351c); border-color: var(--arazzo-danger, #d4351c); color: #fff; }
      </style>
      <dialog part="dialog"><div class="frame"></div></dialog>`;
    this.$('dialog').addEventListener('cancel', (e) => { e.preventDefault(); this.settle(null); });
  }

  /** @private */
  renderBody({ title, message, field, fields, confirmLabel, cancelLabel, danger }) {
    if (!this.shadowRoot.firstElementChild) this.render();
    // Multi-field mode carries a `key` per field and resolves to an object; single `field` keeps the
    // string contract; no field is a plain confirm.
    const list = fields ?? (field ? [{ ...field, key: null }] : []);
    this._multi = !!fields;
    this.$('.frame').innerHTML = `
      <div class="head"><h2>${escapeHtml(title ?? 'Confirm')}</h2></div>
      <div class="body">
        ${message ? `<div class="message">${escapeHtml(message)}</div>` : ''}
        ${list.map((f) => `<label>${escapeHtml(f.label ?? 'Value')}
          <input class="in-field" data-key="${escapeHtml(f.key ?? '')}" type="text" value="${escapeHtml(f.value ?? '')}" placeholder="${escapeHtml(f.placeholder ?? '')}" spellcheck="false">
        </label>`).join('')}
      </div>
      <div class="foot">
        <button class="cancel" type="button">${escapeHtml(cancelLabel)}</button>
        <button class="confirm${danger ? ' danger' : ''}" type="button">${escapeHtml(confirmLabel)}</button>
      </div>`;
    this.$('.cancel').addEventListener('click', () => this.settle(null));
    this.$('.confirm').addEventListener('click', () => this.settle(this.collect()));
    for (const inp of this.shadowRoot.querySelectorAll('.in-field')) {
      inp.addEventListener('keydown', (e) => { if (e.key === 'Enter') this.settle(this.collect()); });
    }
  }
}

define('arazzo-input-dialog', ArazzoInputDialog);
export { ArazzoInputDialog };