// <arazzo-credentials> — the Sources management screen: the credential-rotation worklist as a master-detail surface.
//
//   <arazzo-credentials base-url="/arazzo/v1" scopes="credentials:read credentials:write"></arazzo-credentials>
//   <script type="module">document.querySelector('arazzo-credentials').client = client;</script>
//
// Attributes : base-url, scopes (space-separated)
// Properties : .client (Layer-0 client), .authProvider (() => Authorization header), .fetch
// Events     : re-emits credential-selected / credential-saved / credential-deleted / error
//
// The Sources sibling of <arazzo-catalog>: same master-detail shell (a selectable list on the left, a detail pane on
// the right) over the credential-binding surface. Selecting a row opens its record in the pane (view / rotate / edit /
// duplicate / revoke — scope-gated), NOT a modal. Creating a credential is rooted where the source + its auth are known
// (the catalog's per-workflow Sources panel, §7.5), so this surface has no "New" — only manage-in-place; the guided
// editor dialog is reused for the Edit and Duplicate actions the detail pane raises.

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { ArazzoElement, SHARED_CSS, define } from './base.js';
import './credentials-table.js';
import './credential-detail.js';
import './credential-dialog.js';

class ArazzoCredentials extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'scopes'];
  }

  constructor() {
    super();
    /** @private */ this._authProvider = undefined;
    /** @private */ this._fetch = undefined;
  }

  connectedCallback() {
    this.render();
    this.wire();
    this.applyClient();
    this.applyScopes();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'base-url') { this._client = undefined; this.applyClient(); }
    else if (name === 'scopes') this.applyScopes();
  }

  set authProvider(fn) { this._authProvider = fn; this._client = undefined; this.applyClient(); }
  get authProvider() { return this._authProvider; }

  set fetch(fn) { this._fetch = fn; this._client = undefined; this.applyClient(); }

  set client(value) { this._client = value; if (this.isConnected) this.applyClient(); }
  get client() {
    if (!this._client) {
      const baseUrl = this.getAttribute('base-url');
      if (baseUrl) this._client = new ArazzoControlPlaneClient({ baseUrl, fetch: this._fetch, getAuthHeader: this._authProvider });
    }
    return this._client;
  }

  get scopeList() {
    return (this.getAttribute('scopes') || '').split(/\s+/).filter(Boolean);
  }

  applyClient() {
    const client = this.client;
    if (!client) return;
    const table = this.$('arazzo-credentials-table');
    if (table) table.client = client;
    const dialog = this.$('arazzo-credential-dialog');
    if (dialog) dialog.client = client;
    const detail = this._pane?.querySelector('arazzo-credential-detail');
    if (detail) detail.client = client;
  }

  applyScopes() {
    const scopes = this.getAttribute('scopes') || '';
    const table = this.$('arazzo-credentials-table');
    if (table) table.setAttribute('scopes', scopes);
    const dialog = this.$('arazzo-credential-dialog');
    if (dialog) dialog.setAttribute('scopes', scopes);
    const detail = this._pane?.querySelector('arazzo-credential-detail');
    if (detail) detail.setAttribute('scopes', scopes);
  }

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .layout { display: grid; grid-template-columns: minmax(0, 1fr); gap: 14px; }
        @media (min-width: 880px) { .layout.has-selection { grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr); align-items: start; } }
      </style>
      <div class="layout" part="layout">
        <arazzo-credentials-table selectable part="table"></arazzo-credentials-table>
        <div class="detail-pane"></div>
      </div>
      <arazzo-credential-dialog></arazzo-credential-dialog>
    `;
    this._layout = this.$('.layout');
    this._pane = this.$('.detail-pane');
  }

  wire() {
    const table = this.$('arazzo-credentials-table');
    const dialog = this.$('arazzo-credential-dialog');

    table.addEventListener('credential-selected', (e) => { this.showDetail(e.detail.binding); this.emit('credential-selected', e.detail); });
    table.addEventListener('error', (e) => this.emit('error', e.detail));

    // The guided editor dialog is reused for the detail pane's Edit and Duplicate actions; on save, refresh the list and
    // re-show the (possibly rotated) binding in the pane.
    dialog.addEventListener('credential-saved', (e) => {
      table.refresh();
      if (e.detail?.binding) this.showDetail(e.detail.binding);
      this.emit('credential-saved', e.detail);
    });
    dialog.addEventListener('error', (e) => this.emit('error', e.detail));
  }

  showDetail(binding) {
    if (!binding) return this.clearDetail();
    let detail = this._pane.querySelector('arazzo-credential-detail');
    if (!detail) {
      detail = document.createElement('arazzo-credential-detail');
      const scopes = this.getAttribute('scopes');
      if (scopes) detail.setAttribute('scopes', scopes);
      detail.client = this.client;
      detail.addEventListener('credential-edit', (e) => this.openDialog(e.detail.binding));
      detail.addEventListener('credential-duplicate', (e) => this.openDialog(e.detail.binding, { duplicate: true }));
      detail.addEventListener('credential-deleted', (e) => { this.$('arazzo-credentials-table').refresh(); this.clearDetail(); this.emit('credential-deleted', e.detail); });
      detail.addEventListener('close', () => this.clearDetail());
      detail.addEventListener('error', (e) => this.emit('error', e.detail));
      this._pane.replaceChildren(detail);
    }
    detail.binding = binding;
    this._layout.classList.add('has-selection');
  }

  openDialog(binding, opts = {}) {
    const dialog = this.$('arazzo-credential-dialog');
    dialog.client = this.client;
    dialog.open(binding, opts);
  }

  clearDetail() {
    this._pane.replaceChildren();
    this._layout.classList.remove('has-selection');
  }

  /** Reload the list from page 1 and clear any open detail — the hook the demo calls on a persona change (reach/scopes
   *  change the visible bindings, and an open detail may now be out of reach / forbidden). */
  reload() {
    this.$('arazzo-credentials-table')?.reload();
    this.clearDetail();
  }

  refresh() { this.reload(); }

  requestRender() { this.applyClient(); this.applyScopes(); }
}

define('arazzo-credentials', ArazzoCredentials);
export { ArazzoCredentials };