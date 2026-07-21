# ADR 0042. Auth-agnostic: the host owns the session

Date: 2026-07-21. Status: **Accepted**. Scope: how the web kit authenticates. Builds on
[ADR 0040](0040-three-layer-web-kit.md). This records why the kit takes credentials from its host rather than
embedding an identity-provider flow.

## Context

The control plane is authentication-agnostic: a host owns authentication with any scheme
([ADR 0001](0001-two-plane-access-model.md), the wiring guide). The kit has to fit that. If the kit bundled an
OAuth or OIDC client, it would wed every adopter to one identity provider and one login flow, which is the
single biggest thing that stops a UI kit being reused. The kit should present the UI and let the host supply
the credential.

### Grounded architectural facts

- **The host owns the session; the kit never embeds an IdP flow.** The kit is auth-agnostic
  (design `ui-design.md`): the host owns its OAuth2, OIDC, or mTLS session and hands the kit credentials, and
  the kit never embeds an IdP flow. There is no bundled OAuth or OIDC client, called out as the single biggest
  reuse-killer because it weds the kit to one IdP.
- **Credentials arrive through a hook.** A Layer 2 screen exposes an `authProvider`, a function the host sets
  that returns the `Authorization` header (for example a bearer token from the host's own session), shared with
  its children, so the host supplies the header the kit sends.
- **The sign-in chrome is optional.** A shared `arazzo-auth-status` element provides BFF sign-in and sign-out
  chrome that self-discovers and stays invisible when auth is disabled, so it is an opt-in convenience, not a
  required IdP integration.

## Decision

The web kit is **auth-agnostic**: the host owns the session and hands the kit credentials through an
`authProvider` hook, and the kit never embeds an identity-provider flow. There is no bundled OAuth or OIDC
client. A host using any scheme supplies the credential; the kit renders the UI.

## Consequences

- The kit is adoptable regardless of identity provider, because it does not carry one. A host on Entra, Okta,
  a custom OIDC, or mTLS supplies its own credential.
- Authentication is configured once, on the client or the Layer 2 screen's `authProvider`
  ([ADR 0040](0040-three-layer-web-kit.md)), not per component.
- The kit stays out of the login flow, so it does not date as identity providers or flows change; the host
  owns that.
- The optional sign-in chrome exists for BFF deployments that want it, but it is not on the critical path, so a
  deployment with its own session UI does not use it.
