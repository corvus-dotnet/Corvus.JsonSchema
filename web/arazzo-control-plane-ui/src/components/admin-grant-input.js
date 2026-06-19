// <arazzo-admin-grant-input> — pick an administrator identity as a deployment-mapped {dimension, value} grant.
//
//   <arazzo-admin-grant-input></arazzo-admin-grant-input>
//   el.client = client;                       // for the workflow-id autocomplete (catalog:read)
//   el.setAttribute('fixed-workflow', id);    // lock the workflow dimension's value to this id (Add-workflow context)
//   const grant = el.grant;                    // { dimension, value } | null
//
// Attributes : fixed-workflow (lock the workflow value to this id), base-url
// Properties : .client, .grant ({dimension,value}|null), .dimension, .reset()
//
// The dimension is one of the well-known reach dimensions the deployment maps to an unforgeable sys: identity —
// `workflow` (the immutable per-workflow identity) or `tenant`. The value control adapts: workflow → the catalog
// workflow-id autocomplete; tenant → a plain text input. With fixed-workflow set, the workflow value is locked to
// that id (read-only) — the Add-workflow context, where the only workflow you can name is the one being created.
// This surface only *names* which deployment-stamped identity administers; it cannot stamp sys: tags onto a
// principal (that is the shell's job, §14.3), so the choice of dimension is not a forgery vector.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './workflow-id-input.js';

const DIMENSIONS = ['workflow', 'tenant'];

class ArazzoAdminGrantInput extends ArazzoElement {
  static get observedAttributes() {
    return ['fixed-workflow', 'base-url'];
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.renderValue();
  }

  attributeChangedCallback() {
    if (this._built) this.renderValue();
  }

  /** Re-pass the client to the inner autocomplete when the host sets it. */
  requestRender() {
    this.renderValue();
  }

  get dimension() {
    return this.$('.dim')?.value || DIMENSIONS[0];
  }

  get fixedWorkflow() {
    return this.getAttribute('fixed-workflow') || '';
  }

  /** The chosen identity as { dimension, value }, or null when the value is blank. */
  get grant() {
    const value = this.currentValue().trim();
    return value ? { dimension: this.dimension, value } : null;
  }

  /** @private */
  currentValue() {
    if (this.dimension === 'workflow') {
      return this.fixedWorkflow || this.$('arazzo-workflow-id-input')?.value || '';
    }
    return this.$('.val-text')?.value || '';
  }

  reset() {
    const wf = this.$('arazzo-workflow-id-input');
    if (wf) wf.value = '';
    const text = this.$('.val-text');
    if (text) text.value = '';
  }

  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: inline-flex; gap: 8px; align-items: center; width: 100%; }
        select, input { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
        select.dim { width: 120px; flex: none; }
        .val-slot { display: inline-flex; flex: 1; min-width: 0; }
        .val-slot > * { flex: 1; min-width: 0; }
        input.val-fixed { color: var(--_muted); cursor: not-allowed; }
      </style>
      <select class="dim" aria-label="dimension">
        ${DIMENSIONS.map((d) => `<option value="${d}">${d}</option>`).join('')}
      </select>
      <span class="val-slot"></span>
    `;
    this.$('.dim').addEventListener('change', () => this.renderValue());
  }

  /** (Re)build the value control to match the selected dimension. */
  renderValue() {
    const slot = this.$('.val-slot');
    if (!slot) return;
    const dim = this.dimension;
    if (dim === 'workflow' && this.fixedWorkflow) {
      slot.innerHTML = `<input class="val-fixed" type="text" value="${escapeHtml(this.fixedWorkflow)}" readonly aria-label="workflow" title="Locked to the workflow being added">`;
      return;
    }
    if (dim === 'workflow') {
      slot.innerHTML = '<arazzo-workflow-id-input placeholder="workflow id…"></arazzo-workflow-id-input>';
      const wf = slot.querySelector('arazzo-workflow-id-input');
      const client = this.client;
      if (client) wf.client = client;
      const baseUrl = this.getAttribute('base-url');
      if (baseUrl) wf.setAttribute('base-url', baseUrl);
      return;
    }
    slot.innerHTML = `<input class="val-text" type="text" placeholder="${escapeHtml(dim)} value" aria-label="${escapeHtml(dim)} value">`;
  }
}

define('arazzo-admin-grant-input', ArazzoAdminGrantInput);
export { ArazzoAdminGrantInput };