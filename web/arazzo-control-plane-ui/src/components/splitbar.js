// <arazzo-splitbar> — a draggable resize bar between two panes. The bar owns NO layout: it drives
// one CSS custom property (a pixel size) on a target element, and the host's grid consumes it —
// `grid-template-columns: minmax(0, 1fr) auto var(--inspector-w, 380px)` — so the layout stays
// declarative and the bar stays reusable.
//
//   <arazzo-splitbar orientation="vertical" target="main" prop="--inspector-w"
//                    min="260" max="720" invert storage-key="designer.split.inspector"
//                    aria-label="Resize the inspector"></arazzo-splitbar>
//
// Attributes : orientation — 'vertical' (a column bar; horizontal drags resize) or 'horizontal'
//                (a row bar; vertical drags resize)
//              target      — selector for the element carrying the property, resolved in the
//                            bar's root node (page or shadow tree)
//              prop        — the CSS custom property to set (a px length)
//              min / max   — clamp bounds in px (default 120 / 900)
//              invert      — the sized pane sits on the LEFT/TOP of the drag axis inverse: set it
//                            when dragging left/up should GROW the pane (pane after the bar)
//              storage-key — persists the size in localStorage and restores it on connect
//              aria-label  — announced name; the bar is a keyboard-focusable role="separator"
//                            (arrows nudge by 16px, double-click or Enter resets to the default)
// Events     : split-changed {prop, value|null} (null = reset to the stylesheet default)
//
// Pointer capture + component-scoped listeners only (the kit's shadow-DOM discipline); dragging
// sets `body { user-select: none }` for its duration so text panes don't smear-select.

import { define } from './base.js';

const NUDGE = 16;

class ArazzoSplitbar extends HTMLElement {
  connectedCallback() {
    if (this._built) return;
    this._built = true;
    const vertical = (this.getAttribute('orientation') ?? 'vertical') === 'vertical';
    this._vertical = vertical;
    this.setAttribute('role', 'separator');
    this.setAttribute('aria-orientation', vertical ? 'vertical' : 'horizontal');
    if (!this.hasAttribute('tabindex')) this.setAttribute('tabindex', '0');
    this.style.cssText = `
      display: block; flex: none; touch-action: none; z-index: 5;
      ${vertical ? 'width: 6px; cursor: col-resize;' : 'height: 6px; cursor: row-resize;'}
      background: transparent; transition: background 120ms;`;
    this.addEventListener('pointerenter', () => { this.style.background = 'color-mix(in srgb, var(--arazzo-accent, #2563eb) 35%, transparent)'; });
    this.addEventListener('pointerleave', () => { if (!this._dragging) this.style.background = 'transparent'; });

    const stored = this.storageKey && localStorage.getItem(this.storageKey);
    if (stored !== null && stored !== '' && Number.isFinite(Number(stored))) {
      this.apply(Number(stored), { persist: false });
    }

    this.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return;
      const start = this.current() ?? this.measured();
      if (start === null) return;
      this._dragging = { start, x: e.clientX, y: e.clientY };
      try { this.setPointerCapture(e.pointerId); } catch { /* synthetic pointers (tests) have no capture */ }
      document.body.style.userSelect = 'none';
    });
    this.addEventListener('pointermove', (e) => {
      const g = this._dragging;
      if (!g) return;
      const delta = this._vertical ? e.clientX - g.x : e.clientY - g.y;
      this.apply(g.start + (this.hasAttribute('invert') ? -delta : delta), { persist: false });
    });
    const end = () => {
      if (!this._dragging) return;
      this._dragging = null;
      document.body.style.userSelect = '';
      this.style.background = 'transparent';
      this.persist();
    };
    this.addEventListener('pointerup', end);
    this.addEventListener('pointercancel', end);
    this.addEventListener('dblclick', () => this.reset());
    this.addEventListener('keydown', (e) => {
      const grow = this._vertical ? 'ArrowRight' : 'ArrowDown';
      const shrink = this._vertical ? 'ArrowLeft' : 'ArrowUp';
      if (e.key !== grow && e.key !== shrink && e.key !== 'Enter') return;
      e.preventDefault();
      if (e.key === 'Enter') { this.reset(); return; }
      const sign = (e.key === grow ? 1 : -1) * (this.hasAttribute('invert') ? -1 : 1);
      const from = this.current() ?? this.measured();
      if (from === null) return;
      this.apply(from + sign * NUDGE);
    });
  }

  get storageKey() { return this.getAttribute('storage-key'); }

  /** @private — the property's current override in px, or null when the stylesheet default rules. */
  current() {
    const target = this.targetEl();
    const raw = target?.style.getPropertyValue(this.getAttribute('prop'))?.trim();
    return raw ? Number.parseFloat(raw) : null;
  }

  /** @private — no override yet: measure the sized pane so the first drag starts from reality.
   *  The sized pane is the bar's next (or, with `invert` semantics reversed panes, previous)
   *  sibling; measuring the resolved custom property covers grids that consume it elsewhere. */
  measured() {
    const target = this.targetEl();
    if (!target) return null;
    const resolved = getComputedStyle(target).getPropertyValue(this.getAttribute('prop'))?.trim();
    if (resolved) return Number.parseFloat(resolved);
    const pane = this.hasAttribute('invert') ? this.nextElementSibling : this.previousElementSibling;
    if (!pane) return null;
    const box = pane.getBoundingClientRect();
    return this._vertical ? box.width : box.height;
  }

  /** @private */
  targetEl() {
    const selector = this.getAttribute('target');
    if (!selector) return this.parentElement;
    const root = this.getRootNode();
    return (root.querySelector ? root.querySelector(selector) : null) ?? document.querySelector(selector);
  }

  /** @private */
  apply(value, { persist = true } = {}) {
    const min = Number(this.getAttribute('min') ?? 120);
    const max = Number(this.getAttribute('max') ?? 900);
    const clamped = Math.round(Math.max(min, Math.min(max, value)));
    this.targetEl()?.style.setProperty(this.getAttribute('prop'), `${clamped}px`);
    this.setAttribute('aria-valuenow', String(clamped));
    if (persist) this.persist();
    this.dispatchEvent(new CustomEvent('split-changed', {
      bubbles: true, composed: true, detail: { prop: this.getAttribute('prop'), value: clamped },
    }));
  }

  /** Clears the override — the stylesheet default takes back over. */
  reset() {
    this.targetEl()?.style.removeProperty(this.getAttribute('prop'));
    this.removeAttribute('aria-valuenow');
    if (this.storageKey) localStorage.removeItem(this.storageKey);
    this.dispatchEvent(new CustomEvent('split-changed', {
      bubbles: true, composed: true, detail: { prop: this.getAttribute('prop'), value: null },
    }));
  }

  /** @private */
  persist() {
    const value = this.current();
    if (this.storageKey && value !== null) localStorage.setItem(this.storageKey, String(value));
  }
}

define('arazzo-splitbar', ArazzoSplitbar);
