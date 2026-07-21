# Integrating an identity provider

The control plane has no user registry and issues no credentials. It authorizes the claims an identity provider
(IdP) asserts. To wire your IdP (the demo runs Keycloak; Entra ID and Okta work the same way), you do three
things: map its groups and roles to capability scopes, supply the identity claims that become the reach identity,
and wire OIDC for each kind of caller. This guide is the how-to that ties those together. The model behind it is
[identity and authorization](identity-and-authorization.md) section 16, and the configuration seam is
[deployment bootstrap](deployment-bootstrap.md).

## The two planes, in one page

Authorization has two independent planes ([ADR 0001](../adr/0001-two-plane-access-model.md)).

- **Capability** answers "what is this action". It is a set of scopes (for example `runs:write`,
  `catalog:read`), resolved from the caller's token claims combined with any stored per-principal entitlements.
- **Reach** answers "which rows may it touch". It is row-level security over the caller's canonical `sys:`
  identity (a tag set such as `sys:sub=…`, `sys:team=payments`), matched by membership against the security
  bindings that grant reach.

An action needs both: the scope to perform the verb, and reach over the specific rows. The
[authorization and identity guide](auth-and-authorization.md) covers wiring and administering them; this guide
covers where they come from in your IdP.

## What the IdP owns, and what Arazzo owns

The IdP owns identity: registration, credentials, MFA, and coarse, slow-changing org and team membership
(groups, roles, attributes). Arazzo owns the claim-to-entitlement bindings. Granting access in Arazzo means
writing a rule or binding in the security-policy store, never mutating the IdP. Adding a person to a team is an
IdP operation; granting that team reach over a workflow is an Arazzo operation.

## Mapping groups and roles to capability scopes

The deployment policy maps token claims to capability scopes. The genesis case is configured in
`DeploymentBootstrapOptions` (see [deployment bootstrap](deployment-bootstrap.md)): a genesis-admin group claim
(for example `arazzo-admins`) maps to the full scope set and unrestricted reach, so the first administrator holds
admin the moment they sign in. Finer-grained, in-domain entitlement is then written into the security-policy
store as bindings keyed to claims, which is where day-to-day granting happens.

So a new capability for a team is usually a binding that keys the team's group claim to the scopes it needs, not
a change to the IdP or a redeploy.

## Supplying the identity claims that become reach tags

The deployment shell resolves the authenticated principal into the `sys:` identity that row security matches on.
Configure which claim identifies the subject (the identity claim type, for example `sub`) and which claims become
reach dimensions (for example a `team` claim becomes `sys:team=…`). A claim your workflows reach on must be
present in the token, so the realm has to emit it. Ambient dimensions that are not IdP claims (for example a
tenant derived from request context) are stamped by the shell rather than the token; see section 16.5.5 of the
[identity and authorization guide](identity-and-authorization.md) for the split.

## Wiring OIDC for each caller

Every caller is a principal with claims. Only the authentication flow differs.

- **Web UI.** OIDC Authorization Code with PKCE through a backend-for-frontend. The host runs the flow and holds
  tokens in an `HttpOnly` cookie session; the SPA calls the API same-origin with no tokens in JavaScript, guarded
  by `SameSite=Lax`, a required `X-CSRF` header, and a POST-only logout.
- **CLI.** OIDC for native apps: Authorization Code with PKCE on a loopback redirect by default (it opens the
  browser), or the Device Authorization Flow (`--use-device-code`) for headless or over-SSH use. Tokens are
  cached and silently refreshed.
- **Machine.** A confidential client using client-credentials with private-key-JWT or mTLS (not a shared secret),
  with workload-identity federation (Kubernetes ServiceAccount, cloud IAM, SPIFFE) as the target so no secret is
  stored. See the [runner guide](running-a-runner.md) for how a runner authenticates as a machine principal.

## Bootstrapping the first administrator

The first-admin-into-an-empty-system problem is solved with configuration, not an interactive screen, in three
tiers (section 16.2 of the [identity and authorization guide](identity-and-authorization.md)): the IdP's own
super-admin at first boot, a declarative realm import that seeds the Arazzo realm and its admin group and a seed
admin principal, and the deployment policy that maps that admin group to all scopes and unrestricted reach. A
break-glass path (a one-time bootstrap token disabled after first use) covers recovery when the IdP or its config
is unavailable.

## Per-provider notes

The wiring is standard OIDC, so any compliant provider works. The three that ship directory adapters (for
resolving a person, team, or role to its exact `sys:` identity when authoring a grant) are Keycloak, Entra ID,
and Okta. For each, the same three steps apply: emit the group and role claims your bindings key on, emit the
subject and dimension claims your reach matches on, and register a confidential client per machine caller. The
demo uses Keycloak because it is the locally-runnable stand-in; nothing in the control plane is Keycloak-specific.

## See also

- [Identity and authorization](identity-and-authorization.md) for the full model, row security, and the
  entitlement lifecycle.
- [Authorization wiring](auth-and-authorization.md) for authentication schemes and machine identities.
- [Deployment bootstrap](deployment-bootstrap.md) for the `DeploymentBootstrapOptions` that configure the
  genesis admin, the identity claim type, and the OIDC and secret-store wiring.
- [ADR 0001](../adr/0001-two-plane-access-model.md) for the two-plane decision.
