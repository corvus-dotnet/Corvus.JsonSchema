// <arazzo-filter-input> — a text input with the kit's themed, filtered dropdown (the shared PICKER_CSS look —
// the same design as the environment, source, and grantee pickers) over caller-supplied items. The generic
// combo behind the GitHub repository and branch pickers (§4.7), whose lists can be very long (a real account's
// repositories; dotnet/runtime's branches).
//
//   <arazzo-filter-input placeholder="type to filter…" aria-label="Repository"></arazzo-filter-input>
//   el.items = [{ value: 'acme/specs', sub: 'main · private' }, …];   // label defaults to value
//   el.lookup = async (q) => [{ value, sub }, …];                       // optional deepening typeahead
//   el.value;                                                          // the chosen (or typed) value
//   el.addEventListener('change', (e) => doSomething(e.target.value));
//
// Attributes : placeholder, value, aria-label
// Properties : .items, .value, .readOnly, .disabled
// Events     : input / change bubble out (composed) retargeted to this element, so `e.target.value` works.
//
// Type-to-filter over the items; a value outside the list stays typable (free entry) — for GitHub that is the
// OAuth model's reach (any visible owner/repo works whether listed or not), and for a new branch it is the name
// about to be created. An optional async `lookup` deepens the list while typing (debounced, stale-guarded):
// its results render after the instant static matches, deduplicated by value — the GitHub pickers use it so an
// owner-qualified query ('dotnet/run') reaches repositories the session's seed never contains.

import { ArazzoElement, SHARED_CSS, PICKER_CSS, escapeHtml, define } from './base.js';

class ArazzoFilterInput extends ArazzoElement {
  static get observedAttributes() {
    return ['placeholder', 'value', 'aria-label'];
  }

  constructor() {
    super();
    /** @private */ this._items = [];
    /** @private */ this._active = -1;
    /** @private */ this._lookupSeq = 0;
    /** @private */ this._lookupTimer = 0;
  }

  connectedCallback() {
    this.renderShell();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected) return;
    const input = this.$('input');
    if (!input) return;
    if (name === 'placeholder') input.placeholder = newValue || '';
    else if (name === 'value' && input.value !== (newValue || '')) input.value = newValue || '';
    else if (name === 'aria-label') input.setAttribute('aria-label', newValue || '');
  }

  /** The caller-supplied items ({value, label?, sub?}); setting them re-filters an open list. */
  get items() { return this._items; }

  set items(value) {
    this._items = Array.isArray(value) ? value : [];
    if (this.$('.results') && !this.$('.results').hidden) this.open();
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

  get disabled() { return this.$('input')?.disabled ?? false; }

  set disabled(v) {
    const input = this.$('input');
    if (input) input.disabled = !!v;
    if (v) this.close();
  }

  renderShell() {
    const placeholder = this.getAttribute('placeholder') || 'type to filter…';
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${PICKER_CSS}
        /* The listbox rides the top layer (popover) so a scrolling dialog or sidebar never clips it;
           position is set per open from the input's rect. The popover UA defaults are neutralized. */
        .results { position: fixed; inset: auto; margin: 0; }
        .results[popover]:not(:popover-open) { display: none; }
        :host { display: block; position: relative; }
        input {
          width: 100%; font: inherit; padding: 6px 10px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background: var(--_bg); color: var(--_text);
        }
        input:read-only, input:disabled { opacity: 0.7; cursor: default; }
        /* Stack the value over its detail line (e.g. a repo's default branch and visibility). */
        .results li .txt { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
        .results li .sub { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
      </style>
      <input type="text" part="input" role="combobox" aria-autocomplete="list" aria-expanded="false" autocomplete="off"
             placeholder="${escapeHtml(placeholder)}" aria-label="${escapeHtml(this.getAttribute('aria-label') || '')}"
             value="${escapeHtml(this.getAttribute('value') || '')}" spellcheck="false">
      <ul class="results" role="listbox" popover="manual" hidden></ul>
    `;
    const input = this.$('input');
    input.addEventListener('input', () => this.open());
    // Focus shows the FULL list (typing filters): a pre-filled combo — a bound branch, a chosen repo —
    // must offer its alternatives on click, not hide them behind the committed value.
    input.addEventListener('focus', () => this.open({ showAll: true }));
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

  /** Show the items matching what's typed (substring over value+label+sub, case-insensitive) as a listbox,
   *  then deepen with the async lookup's results when one is set. */
  open({ showAll = false } = {}) {
    const input = this.$('input');
    const list = this.$('.results');
    if (!input || !list || input.readOnly || input.disabled || this._selecting) return;
    const q = showAll ? '' : input.value.trim().toLowerCase();
    const matches = this._items.filter((i) => `${i.value} ${i.label ?? ''} ${i.sub ?? ''}`.toLowerCase().includes(q));
    this.renderList(matches);
    this.scheduleLookup(q, matches);
  }

  /** @private Render the listbox (closed when empty). */
  renderList(matches) {
    const input = this.$('input');
    const list = this.$('.results');
    if (!matches.length) { this.close(); return; }
    this._active = -1;
    list.innerHTML = matches
      .map((i) => `<li role="option" aria-selected="false" data-value="${escapeHtml(i.value)}"><span class="txt"><span class="label">${escapeHtml(i.label ?? i.value)}</span>${i.sub ? `<span class="sub">${escapeHtml(i.sub)}</span>` : ''}</span></li>`)
      .join('');
    // mousedown (not click) so selection runs before the input's blur closes the list.
    list.querySelectorAll('li').forEach((li) => li.addEventListener('mousedown', (e) => { e.preventDefault(); this.select(li.dataset.value); }));
    this.showList();
    input.setAttribute('aria-expanded', 'true');
  }

  /** @private Debounced, stale-guarded lookup: results append after the static matches, deduped by value. */
  scheduleLookup(q, statics) {
    if (typeof this.lookup !== 'function' || !q) return;
    clearTimeout(this._lookupTimer);
    this._lookupTimer = setTimeout(async () => {
      const seq = ++this._lookupSeq;
      let extra = [];
      try { extra = (await this.lookup(this.$('input').value.trim())) || []; } catch { extra = []; }
      const input = this.$('input');
      if (seq !== this._lookupSeq || !input || input.value.trim().toLowerCase() !== q) return; // superseded or stale
      const seen = new Set(statics.map((i) => i.value));
      const merged = [...statics, ...extra.filter((i) => i && i.value && !seen.has(i.value))];
      if (merged.length > statics.length || statics.length) this.renderList(merged);
    }, 250);
  }

  close() {
    clearTimeout(this._lookupTimer);
    this._lookupSeq++;
    this.hideList();
    this.$('input')?.setAttribute('aria-expanded', 'false');
    this._active = -1;
  }

  select(value) {
    this._selecting = true; // the input event we dispatch would otherwise re-open the list
    this.value = value;
    this.$('input').dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    this.$('input').dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    this._selecting = false;
    this.close();
  }

  
  /** @private Place the top-layer list at the input (fixed coordinates; flips above when cramped). */
  positionList() {
    const input = this.$('input');
    const list = this.$('.results');
    if (!input || !list) return;
    const r = input.getBoundingClientRect();
    const below = window.innerHeight - r.bottom - 8;
    const above = r.top - 8;
    const placeAbove = below < 160 && above > below;
    list.style.left = `${r.left}px`;
    list.style.width = `${r.width}px`;
    list.style.maxHeight = `${Math.min(280, Math.max(placeAbove ? above : below, 120))}px`;
    if (placeAbove) { list.style.top = 'auto'; list.style.bottom = `${window.innerHeight - r.top + 4}px`; }
    else { list.style.bottom = 'auto'; list.style.top = `${r.bottom + 4}px`; }
  }

  /** @private Show the list in the top layer (escapes dialog/sidebar clipping); no-op when already open. */
  showList() {
    const list = this.$('.results');
    list.hidden = false;
    this.positionList();
    if (list.showPopover && !list.matches(':popover-open')) list.showPopover();
  }

  /** @private */
  hideList() {
    const list = this.$('.results');
    if (!list) return;
    list.hidden = true;
    if (list.hidePopover && list.matches(':popover-open')) list.hidePopover();
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
    else if (e.key === 'Enter' && this._active >= 0) { e.preventDefault(); this.select(items[this._active].dataset.value); }
    else if (e.key === 'Escape') { this.close(); }
  }

  highlight(items) {
    items.forEach((li, i) => li.setAttribute('aria-selected', i === this._active ? 'true' : 'false'));
    if (this._active >= 0) items[this._active].scrollIntoView({ block: 'nearest' });
  }
}

define('arazzo-filter-input', ArazzoFilterInput);
export { ArazzoFilterInput };
