// <arazzo-github-connect> — the brokered GitHub session control (workflow-designer design §4.7).
//
//   const connect = document.createElement('arazzo-github-connect');
//   connect.client = client;                  // an ArazzoControlPlaneClient
//   host.appendChild(connect);
//
// Properties : .client, .session (the latest status, or null), .windowOpener (injectable for tests),
//              .pollIntervalMs
// Methods    : refresh()
// Events     : github-connected {session} · github-disconnected · error {problem}
//
// The sign-in is a POPUP flow: beginGitHubAuth mints the authorize URL (a bearer SPA cannot carry
// its token on a top-level navigation), the popup rides GitHub's redirect to the control plane's
// callback, and THIS component polls the session until it flips to connected — then closes the
// popup. The kit never sees a GitHub credential; identity comes resolved from the session, never
// typed into a box.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

const CONNECT_TIMEOUT_MS = 120_000;

class ArazzoGitHubConnect extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._session = null;
    /** @private */ this._connecting = false;
    /** @private */ this._seq = 0;
    /** The popup opener — injectable so tests can complete the callback without a real window. */
    this.windowOpener = (url) => window.open(url, 'arazzo-github-auth', 'popup,width=680,height=760');
    /** How often the session is polled while the popup is open. */
    this.pollIntervalMs = 800;
  }

  /** The Layer-0 client. Setting it refreshes the session state. */
  set client(value) {
    this._client = value;
    this.refresh();
  }

  get client() { return this._client; }

  /** The latest session status ({connected, login, …}) or null before the first load. */
  get session() { return this._session; }

  connectedCallback() {
    this.render();
  }

  /** Re-reads the session (e.g. after a host-driven change). */
  async refresh() {
    if (!this._client) return;
    const seq = ++this._seq;
    try {
      const session = await this._client.getGitHubStatus();
      if (seq !== this._seq) return;
      this._session = session;
      this.render();
      if (session.connected) this.emit('github-connected', { session });
    } catch (err) {
      if (seq !== this._seq) return;
      // A deployment that brokers no App: the control renders nothing rather than a dead button.
      this._session = null;
      this.render();
      this.emit('error', { problem: err.problem, error: err });
    }
  }

  render() {
    if (!this.shadowRoot.firstChild) {
      this.shadowRoot.innerHTML = `
        <style>
          ${SHARED_CSS}
          :host { display: inline-block; }
          .row { display: flex; align-items: center; gap: 8px; font-size: 12px; }
          .chip { display: inline-flex; align-items: center; gap: 6px; border: 1px solid var(--_border); border-radius: 999px; padding: 2px 10px; }
          .chip .login { font-weight: 600; }
          .muted-note { color: var(--_muted); }
          button { font-size: 12px; }
        </style>
        <div class="row" part="row"></div>`;
    }

    const row = this.$('.row');
    if (!row) return;
    if (this._connecting) {
      row.innerHTML = `<span class="muted-note">Waiting for GitHub…</span><button class="cancel" type="button">Cancel</button>`;
      this.$('.cancel').addEventListener('click', () => this.cancelConnect());
      return;
    }

    if (this._session?.connected) {
      row.innerHTML = `
        <span class="chip" part="chip" title="Signed in to GitHub${this._session.name ? ` as ${escapeHtml(this._session.name)}` : ''}">
          <span class="login">${escapeHtml(this._session.login ?? 'connected')}</span>
        </span>
        <button class="disconnect" type="button" title="Drop the brokered GitHub session">Disconnect</button>`;
      this.$('.disconnect').addEventListener('click', () => this.disconnect());
      return;
    }

    row.innerHTML = `<button class="connect" type="button" title="Sign in via the deployment's GitHub App (§4.7)">Connect GitHub…</button>`;
    this.$('.connect').addEventListener('click', () => this.connect());
  }

  // ---- the popup flow -----------------------------------------------------------------------------

  async connect() {
    if (!this._client || this._connecting) return;
    this._connecting = true;
    this.render();
    let popup = null;
    try {
      const { authorizeUrl } = await this._client.beginGitHubAuth();
      popup = this.windowOpener(authorizeUrl);
      const deadline = Date.now() + CONNECT_TIMEOUT_MS;
      while (this._connecting && Date.now() < deadline) {
        await new Promise((resolve) => { this._pollTimer = setTimeout(resolve, this.pollIntervalMs); });
        if (!this._connecting) break;
        const session = await this._client.getGitHubStatus();
        if (session.connected) {
          this._session = session;
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
      if (this._session?.connected) this.emit('github-connected', { session: this._session });
    }
  }

  cancelConnect() {
    this._connecting = false;
    clearTimeout(this._pollTimer);
  }

  async disconnect() {
    if (!this._client) return;
    try {
      await this._client.deleteGitHubSession();
      this._session = { connected: false };
      this.render();
      this.emit('github-disconnected');
    } catch (err) {
      this.emit('error', { problem: err.problem, error: err });
    }
  }
}

define('arazzo-github-connect', ArazzoGitHubConnect);
export { ArazzoGitHubConnect };