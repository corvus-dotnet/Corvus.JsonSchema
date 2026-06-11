// <arazzo-workflow-id-input> — a text input with server-side workflowId autocomplete.
//
//   <arazzo-workflow-id-input placeholder="Filter by workflowId…"></arazzo-workflow-id-input>
//   el.client = client;                 // an ArazzoControlPlaneClient (catalog:read for suggestions)
//   el.addEventListener('input', (e) => doSomething(e.target.value));
//
// Attributes : placeholder, value, base-url
// Properties : .client, .value
// Events     : input / change bubble out (composed) retargeted to this element, so `e.target.value` works.
//
// As you type, it queries the catalog by indexed prefix (searchCatalog `workflowIdPrefix`) and fills a native
// <datalist>, capped so the dropdown stays small at any catalog size. Suggestions are best-effort — if the
// catalog isn't reachable (e.g. no catalog:read) it silently stays a plain free-text input.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

const SUGGESTION_LIMIT = 10;

class ArazzoWorkflowIdInput extends ArazzoElement {
  static get observedAttributes() {
    return ['placeholder', 'value', 'base-url'];
  }

  constructor() {
    super();
    /** @private */ this._suggestTimer = null;
    /** @private */ this._suggestSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.loadSuggestions('');
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected) return;
    const input = this.$('input');
    if (name === 'placeholder' && input) input.placeholder = newValue || '';
    else if (name === 'value' && input && input.value !== (newValue || '')) input.value = newValue || '';
    else if (name === 'base-url') { this._client = undefined; this.loadSuggestions(this.value.trim()); }
  }

  get value() { return this.$('input')?.value ?? this.getAttribute('value') ?? ''; }

  set value(v) {
    const input = this.$('input');
    if (input) input.value = v ?? '';
    else this.setAttribute('value', v ?? '');
  }

  /** Re-seed suggestions (e.g. after the client changes). */
  requestRender() { this.loadSuggestions(this.value.trim()); }

  renderShell() {
    const placeholder = this.getAttribute('placeholder') || 'Filter by workflowId…';
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        input {
          width: 100%; font: inherit; padding: 6px 10px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background: var(--_bg); color: var(--_text);
        }
      </style>
      <input type="search" part="input" list="wf-id-options" placeholder="${escapeHtml(placeholder)}"
             aria-label="${escapeHtml(placeholder)}" value="${escapeHtml(this.getAttribute('value') || '')}">
      <datalist id="wf-id-options"></datalist>
    `;
    // The native input/change events bubble out (composed) retargeted to this element, so a host can read
    // e.target.value. We additionally refresh the datalist (debounced) from the server as the user types.
    this.$('input').addEventListener('input', (e) => {
      const value = e.target.value.trim();
      clearTimeout(this._suggestTimer);
      this._suggestTimer = setTimeout(() => this.loadSuggestions(value), 200);
    });
  }

  /**
   * Fetch up to {@link SUGGESTION_LIMIT} workflow ids matching `prefix` (server-side, indexed) and fill the
   * datalist with both base ids and the versioned ids. Best-effort — silently no-ops without catalog access.
   * @param {string} [prefix]
   */
  async loadSuggestions(prefix = '') {
    const client = this.client;
    const datalist = this.$('#wf-id-options');
    if (!client || !datalist) return;
    const seq = ++this._suggestSeq;
    try {
      const { versions } = await client.searchCatalog({ workflowIdPrefix: prefix || undefined, limit: SUGGESTION_LIMIT * 2 });
      if (seq !== this._suggestSeq) return;
      const ids = new Set();
      for (const v of versions) {
        if (v.baseWorkflowId) ids.add(v.baseWorkflowId);
        if (v.workflowId) ids.add(v.workflowId);
        if (ids.size >= SUGGESTION_LIMIT) break;
      }
      datalist.innerHTML = [...ids].slice(0, SUGGESTION_LIMIT).map((id) => `<option value="${escapeHtml(id)}"></option>`).join('');
    } catch {
      // No catalog access → no suggestions; stays a plain free-text input.
    }
  }
}

define('arazzo-workflow-id-input', ArazzoWorkflowIdInput);
export { ArazzoWorkflowIdInput };
