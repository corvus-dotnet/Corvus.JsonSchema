// <arazzo-pager> — the shared list footer: Prev / Next over a keyset cursor plus a free-form info area. One component,
// so every list (Runs, Catalog, Sources, Environments, Grants, Scopes, the request inboxes, Runners) gets the SAME
// footer instead of each hand-rolling its own.
//
//   <arazzo-pager class="pager" part="pager"></arazzo-pager>
//   pager.addEventListener('prev', () => table.prevPage());
//   pager.addEventListener('next', () => table.nextPage());
//   pager.update({ hasPrev, hasNext, loading, info: '12 runs · page 2' });   // info is trusted HTML
//
// Events     : prev, next (bubbling + composed, so the host hears them across shadow boundaries)
// Method     : update({ hasPrev?, hasNext?, loading?, info? })
//
// It renders into LIGHT DOM (no shadow root) ON PURPOSE: placed inside a host component's shadow tree, its `.prev` /
// `.next` / `.count` stay reachable via `host.shadowRoot.querySelector(...)` and it inherits the host's theme + the
// shared PAGER_CSS (from base.js) — so it is a real reusable component without a styling/selector boundary to cross.

import { define } from './base.js';

class ArazzoPager extends HTMLElement {
  connectedCallback() {
    if (this._built) return;
    this._built = true;
    this.innerHTML = `
      <button class="prev ghost" type="button">‹ Prev</button>
      <button class="next ghost" type="button">Next ›</button>
      <span class="grow"></span>
      <span class="count info"></span>`;
    this.querySelector('.prev').addEventListener('click', () => {
      if (!this._prevDisabled) this.dispatchEvent(new CustomEvent('prev', { bubbles: true, composed: true }));
    });
    this.querySelector('.next').addEventListener('click', () => {
      if (!this._nextDisabled) this.dispatchEvent(new CustomEvent('next', { bubbles: true, composed: true }));
    });
    if (this._pending) { const p = this._pending; this._pending = null; this.update(p); }
  }

  /**
   * @param {{ hasPrev?: boolean, hasNext?: boolean, loading?: boolean, info?: string }} state
   *   hasPrev/hasNext enable the buttons; loading disables both; info is trusted HTML for the right-hand area.
   */
  update({ hasPrev = false, hasNext = false, loading = false, info = '' } = {}) {
    if (!this._built) { this._pending = { hasPrev, hasNext, loading, info }; return; }
    this._prevDisabled = !hasPrev || loading;
    this._nextDisabled = !hasNext || loading;
    this.querySelector('.prev').disabled = this._prevDisabled;
    this.querySelector('.next').disabled = this._nextDisabled;
    this.querySelector('.count').innerHTML = info;
  }
}

define('arazzo-pager', ArazzoPager);
export { ArazzoPager };