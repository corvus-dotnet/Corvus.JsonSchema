// <arazzo-status-badge> — the shared status pill and the canonical status -> colour mapping.
//
//   <arazzo-status-badge status="Faulted"></arazzo-status-badge>
//
// Purely presentational (no client). Reflects the `status` attribute.

import { ArazzoElement, adoptStyles, SHARED_CSS, STATUS_COLORS, toneCss, escapeHtml, define } from './base.js';

// The badge's colour, one tone per run status, carried in --_color.
const STATUS_TONES = toneCss('.badge', STATUS_COLORS, 'var(--arazzo-muted, #6b7280)', '--_color');

class ArazzoStatusBadge extends ArazzoElement {
  static get observedAttributes() {
    return ['status'];
  }

  connectedCallback() {
    this.render();
  }

  attributeChangedCallback() {
    if (this.isConnected) this.render();
  }

  render() {
    const status = this.getAttribute('status') || 'Pending';
    adoptStyles(this.shadowRoot, SHARED_CSS, `
      .badge {
        display: inline-flex; align-items: center; gap: 6px;
        padding: 2px 9px;
        border-radius: 999px;
        font-size: 12px; font-weight: 600;
        color: var(--_color);
        background: color-mix(in srgb, var(--_color) 14%, transparent);
        border: 1px solid color-mix(in srgb, var(--_color) 35%, transparent);
        white-space: nowrap;
      }
      .dot { width: 6px; height: 6px; border-radius: 50%; background: var(--_color); }
    `, STATUS_TONES);
    this.shadowRoot.innerHTML = `
      <span class="badge" part="badge" data-tone="${escapeHtml(status)}">
        <span class="dot" part="dot"></span>${escapeHtml(status)}
      </span>
    `;
  }
}

define('arazzo-status-badge', ArazzoStatusBadge);
export { ArazzoStatusBadge };
