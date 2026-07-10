// <copyright file="auth-status.js" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The shared sign-in/sign-out chrome (§16.3 BFF). One element, used by BOTH the control-plane dashboard and the
// workflow designer, so "who am I / sign out" looks and behaves identically everywhere. It self-discovers the auth
// state from /me: signed in → the identity + a Sign-out that POSTs /logout (a form navigation, so the OIDC
// end-session redirect chain runs — a fetch would not navigate); signed out → a Sign-in link that returns here;
// auth disabled (the endpoint 404s or the fetch fails, e.g. the standalone demo) → the element stays invisible so
// hosts can drop it in unconditionally. Styled from the --arazzo-* custom properties that pierce the shadow boundary.
class ArazzoAuthStatus extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
  }

  get meUrl() { return this.getAttribute('me-url') || '/me'; }

  get loginUrl() { return this.getAttribute('login-url') || '/login'; }

  get logoutUrl() { return this.getAttribute('logout-url') || '/logout'; }

  connectedCallback() {
    this.shadowRoot.innerHTML = `
      <style>
        :host { display: none; }              /* invisible until /me resolves — auth-off hosts show nothing */
        :host([data-state]) { display: inline-flex; align-items: center; gap: 8px; font-size: 13px;
                              color: var(--arazzo-muted, #6b7280); }
        strong { color: var(--arazzo-text, inherit); font-weight: 600; }
        .linklike { font: inherit; font-size: 13px; cursor: pointer; border: none; background: none; padding: 0;
                    color: var(--arazzo-accent, #3b6cf6); text-decoration: underline; }
        .linklike:hover { text-decoration: none; }
        .linklike:focus-visible { outline: 2px solid var(--arazzo-accent, #3b6cf6); outline-offset: 2px; border-radius: 2px; }
      </style>
      <span class="content"></span>`;
    void this.refresh();
  }

  /** The return target for sign-in — back to wherever the user is now. */
  returnHere() { return this.loginUrl + '?returnUrl=' + encodeURIComponent(location.pathname + location.search); }

  async refresh() {
    let me;
    try {
      const r = await fetch(this.meUrl, { credentials: 'include' });
      if (r.status === 404) { return; }        // authorization disabled — no BFF endpoints; stay hidden
      me = r.ok ? await r.json() : null;        // ok → identity; 401/other → signed out
    } catch {
      return;                                   // no BFF reachable (standalone demo) — stay hidden
    }

    const content = this.shadowRoot.querySelector('.content');
    if (me) {
      this.setAttribute('data-state', 'in');
      const who = document.createElement('span');
      who.append('Signed in as ');
      const name = document.createElement('strong');
      name.textContent = me.name ?? 'you';
      who.append(name);
      if (me.groups?.length) { who.title = me.groups.join(', '); }
      const out = document.createElement('button');
      out.className = 'linklike';
      out.type = 'button';
      out.textContent = 'Sign out';
      out.addEventListener('click', () => this.signOut());
      content.replaceChildren(who, out);
    } else {
      this.setAttribute('data-state', 'out');
      const link = document.createElement('a');
      link.className = 'linklike';
      link.href = this.returnHere();
      link.textContent = 'Sign in';
      content.replaceChildren(link);
    }
  }

  /** POST /logout as a form navigation so the OIDC end-session redirect chain runs (a fetch would not navigate). */
  signOut() {
    const form = document.createElement('form');
    form.method = 'post';
    form.action = this.logoutUrl;
    document.body.appendChild(form);
    form.submit();
  }
}

customElements.define('arazzo-auth-status', ArazzoAuthStatus);
