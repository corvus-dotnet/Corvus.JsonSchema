// Arazzo Control Plane — shared component base (Layer 1 internals).
//
// A tiny base class and a handful of helpers shared by every kit component, so each element file stays
// focused on its own markup. No framework — just HTMLElement + Shadow DOM.

import { ArazzoControlPlaneClient } from '../arazzo-client.js';

/**
 * Common Shadow-DOM CSS prepended by every component. It never *sets* `--arazzo-*` tokens on `:host`
 * (that would block inheritance from a themed ancestor such as `<arazzo-control-plane theme="dark">`);
 * instead each declaration reads a token with a light-mode fallback. A host themes the whole kit by
 * setting the tokens on `:root` (see arazzo-kit.css) or on the panel element.
 */
export const SHARED_CSS = `
  :host {
    --_font: var(--arazzo-font, system-ui, -apple-system, Segoe UI, Roboto, sans-serif);
    --_radius: var(--arazzo-radius, 8px);
    --_bg: var(--arazzo-bg, #ffffff);
    --_surface: var(--arazzo-surface, #f7f8fa);
    --_border: var(--arazzo-border, #e3e6ea);
    --_text: var(--arazzo-text, #1c2024);
    --_muted: var(--arazzo-muted, #6b7280);
    --_accent: var(--arazzo-accent, #3b6cf6);
    --_danger: var(--arazzo-danger, #d4351c);
    box-sizing: border-box;
    font-family: var(--_font);
    color: var(--_text);
    font-size: 14px;
    line-height: 1.45;
  }
  *, *::before, *::after { box-sizing: border-box; }
  button {
    font: inherit;
    color: inherit;
    cursor: pointer;
    border: 1px solid var(--_border);
    background: var(--_bg);
    border-radius: var(--_radius);
    padding: 6px 12px;
  }
  button:hover:not(:disabled) { border-color: var(--_accent); }
  button:disabled { opacity: 0.5; cursor: not-allowed; }
  button.primary { background: var(--_accent); border-color: var(--_accent); color: #fff; }
  button.danger { background: var(--_danger); border-color: var(--_danger); color: #fff; }
  button.ghost { background: transparent; border-color: transparent; }
  a { color: var(--_accent); }
  .muted { color: var(--_muted); }
  /* Replace the native dropdown chevron (which ignores the kit theme and vanishes on dark surfaces)
     with a neutral-grey one that reads on both light and dark backgrounds. Components must use
     'background-color' (never the 'background' shorthand) on selects so they don't reset this image. */
  select {
    appearance: none;
    -webkit-appearance: none;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='8' viewBox='0 0 12 8'%3E%3Cpath d='M1 1.5 6 6.5 11 1.5' fill='none' stroke='%23808a99' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'/%3E%3C/svg%3E");
    background-repeat: no-repeat;
    background-position: right 10px center;
    background-size: 11px;
    padding-right: 30px;
  }
  .error-banner {
    border: 1px solid var(--_danger);
    background: color-mix(in srgb, var(--_danger) 8%, transparent);
    color: var(--_text);
    border-radius: var(--_radius);
    padding: 10px 12px;
    display: flex; gap: 10px; align-items: center; justify-content: space-between;
  }
  .empty { color: var(--_muted); text-align: center; padding: 28px 12px; }
  [hidden] { display: none !important; }
`;

/** Maps a {@link WorkflowRunStatus} to its themeable colour token (with a fallback). */
export function statusColor(status) {
  const map = {
    Pending: 'var(--arazzo-status-pending, #9aa1ab)',
    Running: 'var(--arazzo-status-running, #2f74d0)',
    Suspended: 'var(--arazzo-status-suspended, #b07d18)',
    Completed: 'var(--arazzo-status-completed, #2a8a4a)',
    Cancelled: 'var(--arazzo-status-cancelled, #6b7280)',
    Faulted: 'var(--arazzo-status-faulted, #d4351c)',
  };
  return map[status] || 'var(--arazzo-muted, #6b7280)';
}

/** HTML-escape a string for safe interpolation into innerHTML. */
export function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

/** A compact relative-time string for a past timestamp, e.g. `3m ago`. */
export function relativeTime(iso, now = Date.now()) {
  if (!iso) return '';
  const then = Date.parse(iso);
  if (Number.isNaN(then)) return String(iso);
  return `${humanizeDelta(now - then)} ago`;
}

/** A compact countdown for a future timestamp, e.g. `in 2m` (or `overdue` once past). */
export function countdown(iso, now = Date.now()) {
  if (!iso) return '';
  const then = Date.parse(iso);
  if (Number.isNaN(then)) return String(iso);
  const delta = then - now;
  return delta <= 0 ? 'overdue' : `in ${humanizeDelta(delta)}`;
}

/** A locale absolute timestamp (for `title=` tooltips). */
export function absoluteTime(iso) {
  if (!iso) return '';
  const t = Date.parse(iso);
  return Number.isNaN(t) ? String(iso) : new Date(t).toLocaleString();
}

/**
 * Show a themed, focus-trapped confirmation dialog inside `host`'s shadow root (never the browser's
 * built-in `confirm`), returning a promise that resolves `true` if confirmed, `false` otherwise. The
 * dialog reads the kit's `--arazzo-*` theme tokens (inherited) and removes itself when dismissed.
 *
 * @param {HTMLElement} host The element whose shadow root hosts the dialog (so it inherits the theme).
 * @param {{ title?: string, message?: string, confirmLabel?: string, cancelLabel?: string, danger?: boolean }} [options]
 * @returns {Promise<boolean>}
 */
export function confirmDialog(host, options = {}) {
  const { title = 'Confirm', message = '', confirmLabel = 'Confirm', cancelLabel = 'Cancel', danger = false } = options;
  const root = host?.shadowRoot ?? document.body;
  return new Promise((resolve) => {
    const dlg = document.createElement('dialog');
    dlg.className = 'arazzo-confirm';
    dlg.setAttribute('part', 'confirm');
    dlg.innerHTML = `
      <style>
        dialog.arazzo-confirm {
          border: 1px solid var(--arazzo-border, #e3e6ea); border-radius: var(--arazzo-radius, 8px);
          background: var(--arazzo-bg, #fff); color: var(--arazzo-text, #1c2024); padding: 0;
          width: min(420px, 92vw); font: 14px var(--arazzo-font, system-ui, sans-serif);
        }
        dialog.arazzo-confirm::backdrop { background: rgba(0,0,0,0.4); }
        dialog.arazzo-confirm .head { padding: 14px 16px; font-weight: 700; border-bottom: 1px solid var(--arazzo-border, #e3e6ea); }
        dialog.arazzo-confirm .content { padding: 16px; line-height: 1.45; }
        dialog.arazzo-confirm .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--arazzo-border, #e3e6ea); }
        dialog.arazzo-confirm button { font: inherit; cursor: pointer; border: 1px solid var(--arazzo-border, #e3e6ea); background: var(--arazzo-bg, #fff); color: inherit; border-radius: var(--arazzo-radius, 8px); padding: 6px 12px; }
        dialog.arazzo-confirm button.primary { background: var(--arazzo-accent, #3b6cf6); border-color: var(--arazzo-accent, #3b6cf6); color: #fff; }
        dialog.arazzo-confirm button.danger { background: var(--arazzo-danger, #d4351c); border-color: var(--arazzo-danger, #d4351c); color: #fff; }
        dialog.arazzo-confirm button.ghost { background: transparent; border-color: transparent; }
      </style>
      <div class="head" part="confirm-title">${escapeHtml(title)}</div>
      <div class="content">${escapeHtml(message)}</div>
      <div class="foot">
        <button class="cancel ghost" type="button">${escapeHtml(cancelLabel)}</button>
        <button class="ok ${danger ? 'danger' : 'primary'}" type="button">${escapeHtml(confirmLabel)}</button>
      </div>`;

    const done = (value) => {
      dlg.close();
      dlg.remove();
      resolve(value);
    };
    dlg.querySelector('.cancel').addEventListener('click', () => done(false));
    dlg.querySelector('.ok').addEventListener('click', () => done(true));
    dlg.addEventListener('cancel', (e) => { e.preventDefault(); done(false); }); // Esc / backdrop
    root.appendChild(dlg);
    dlg.showModal();
    dlg.querySelector('.ok').focus();
  });
}

/**
 * Copy text to the clipboard, returning whether it succeeded. Tolerates an absent/blocked Clipboard API
 * (older browsers, insecure contexts, test environments) by returning `false` rather than throwing.
 * @param {string} text
 * @returns {Promise<boolean>}
 */
export async function copyToClipboard(text) {
  try {
    await navigator.clipboard?.writeText(String(text ?? ''));
    return navigator.clipboard != null;
  } catch {
    return false;
  }
}

function humanizeDelta(ms) {
  const s = Math.round(Math.abs(ms) / 1000);
  if (s < 60) return `${s}s`;
  const m = Math.round(s / 60);
  if (m < 60) return `${m}m`;
  const h = Math.round(m / 60);
  if (h < 48) return `${h}h`;
  return `${Math.round(h / 24)}d`;
}

/**
 * Base class: open Shadow DOM, a query helper, a bubbling+composed event emitter, and lazy client
 * resolution from an explicit `.client` property or a `base-url` attribute.
 */
export class ArazzoElement extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    /** @private @type {ArazzoControlPlaneClient|undefined} */ this._client = undefined;
  }

  /** The Layer-0 client. Set it directly, or supply a `base-url` attribute to have one built lazily. */
  get client() {
    if (!this._client) {
      const baseUrl = this.getAttribute('base-url');
      if (baseUrl) this._client = new ArazzoControlPlaneClient({ baseUrl });
    }
    return this._client;
  }

  set client(value) {
    this._client = value;
    if (this.isConnected) this.requestRender?.();
  }

  /** Dispatch a bubbling, composed {@link CustomEvent} so hosts/Layer 2 can listen across shadow roots. */
  emit(type, detail) {
    this.dispatchEvent(new CustomEvent(type, { detail, bubbles: true, composed: true }));
  }

  /** @param {string} selector */
  $(selector) {
    return this.shadowRoot.querySelector(selector);
  }

  /** @param {string} selector */
  $$(selector) {
    return [...this.shadowRoot.querySelectorAll(selector)];
  }
}

/** Register a custom element once (idempotent — safe when several entry points import it). */
export function define(tagName, ctor) {
  if (!customElements.get(tagName)) {
    customElements.define(tagName, ctor);
  }
}
