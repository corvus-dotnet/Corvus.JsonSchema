// <arazzo-provider-connect> — one connected provider's session control (ADR 0052).
//
//   const connect = document.createElement('arazzo-provider-connect');
//   connect.client = client;                  // an ArazzoControlPlaneClient
//   connect.provider = { name, displayName, connected };   // a listProviders entry
//   host.appendChild(connect);
//
// Properties : .client, .provider (the listProviders entry it controls), .windowOpener (injectable
//              for tests), .pollIntervalMs
// Events     : provider-connected {provider} · provider-disconnected {provider} · error {problem}
//
// The sign-in is the same POPUP flow as the GitHub panel (the server machinery IS the same broker):
// beginProviderAuth mints the authorize URL (a bearer SPA cannot carry its token on a top-level
// navigation), the popup rides the provider's redirect to the control plane's callback, and THIS
// component polls listProviders until the provider flips to connected — then closes the popup. The
// kit never sees a provider credential.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

const CONNECT_TIMEOUT_MS = 120_000;

class ArazzoProviderConnect extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._provider = null;
    /** @private */ this._connecting = false;
    /** The popup opener — injectable so tests can complete the callback without a real window. */
    this.windowOpener = (url) => window.open(url, 'arazzo-provider-auth', 'popup,width=680,height=760');
    /** How often the connection state is polled while the popup is open. */
    this.pollIntervalMs = 800;
  }

  /** The Layer-0 client. */
  set client(value) { this._client = value; }
  get client() { return this._client; }

  /** The provider this control connects: a listProviders entry ({name, displayName?, connected}). */
  set provider(value) {
    this._provider = value;
    this.render();
  }

  get provider() { return this._provider; }

  connectedCallback() {
    this.render();
  }

  render() {
    if (!this.shadowRoot.firstChild) {
      this.shadowRoot.innerHTML = `
        <style>
          ${SHARED_CSS}
          :host { display: inline-block; }
          .row { display: flex; align-items: center; gap: 8px; font-size: 12px; }
          .chip { display: inline-flex; align-items: center; gap: 6px; border: 1px solid var(--_border); border-radius: 999px; padding: 2px 10px; }
          .chip .provider-name { font-weight: 600; }
          .muted-note { color: var(--_muted); }
          button { font-size: 12px; }
        </style>
        <div class="row" part="row"></div>`;
    }

    const row = this.$('.row');
    if (!row) return;
    const provider = this._provider;
    if (!provider) {
      row.innerHTML = '';
      return;
    }

    const display = provider.displayName || provider.name;
    if (this._connecting) {
      row.innerHTML = `<span class="muted-note">Waiting for ${escapeHtml(display)}…</span><button class="cancel" type="button">Cancel</button>`;
      this.$('.cancel').addEventListener('click', () => this.cancelConnect());
      return;
    }

    if (provider.connected) {
      row.innerHTML = `
        <span class="chip" part="chip" title="Signed in to ${escapeHtml(display)}; the fetch runs as you">
          <span class="muted">Connected to</span> <span class="provider-name">${escapeHtml(display)}</span>
        </span>
        <button class="disconnect" type="button" title="Drop the brokered ${escapeHtml(display)} session">Disconnect</button>`;
      this.$('.disconnect').addEventListener('click', () => this.disconnect());
      return;
    }

    row.innerHTML = `
      <span class="muted-note">This host is covered by ${escapeHtml(display)}.</span>
      <button class="connect" type="button" title="Sign in through ${escapeHtml(display)}; the fetch then runs as you">Connect ${escapeHtml(display)}…</button>`;
    this.$('.connect').addEventListener('click', () => this.connect());
  }

  // ---- the popup flow -----------------------------------------------------------------------------

  async connect() {
    if (!this._client || !this._provider || this._connecting) return;
    const name = this._provider.name;
    this._connecting = true;
    this.render();
    let popup = null;
    try {
      const { authorizeUrl } = await this._client.beginProviderAuth(name);
      popup = this.windowOpener(authorizeUrl);
      const deadline = Date.now() + CONNECT_TIMEOUT_MS;
      while (this._connecting && Date.now() < deadline) {
        await new Promise((resolve) => { this._pollTimer = setTimeout(resolve, this.pollIntervalMs); });
        if (!this._connecting) break;
        const { providers } = await this._client.listProviders();
        const current = (providers ?? []).find((p) => p.name === name);
        if (current?.connected) {
          this._provider = current;
          break;
        }

        // The user closed the popup without completing: one last poll has just run — stop waiting.
        if (popup && popup.closed) break;
      }
    } catch (err) {
      this.emit('error', { problem: err.problem, error: err });
    } finally {
      this._connecting = false;
      try { popup?.close(); } catch { /* cross-origin close refusals are harmless */ }
      this.render();
      if (this._provider?.connected) this.emit('provider-connected', { provider: this._provider });
    }
  }

  cancelConnect() {
    this._connecting = false;
    clearTimeout(this._pollTimer);
  }

  async disconnect() {
    if (!this._client || !this._provider) return;
    try {
      await this._client.deleteProviderSession(this._provider.name);
      this._provider = { ...this._provider, connected: false };
      this.render();
      this.emit('provider-disconnected', { provider: this._provider });
    } catch (err) {
      this.emit('error', { problem: err.problem, error: err });
    }
  }
}

define('arazzo-provider-connect', ArazzoProviderConnect);
export { ArazzoProviderConnect };