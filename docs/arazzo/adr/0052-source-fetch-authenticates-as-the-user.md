# ADR 0052. Source fetch authenticates as the user, through connected providers

Date: 2026-07-24. Status: **Accepted**. Scope: how the acquisition dialog's Fetch URL path (and any
future user-identity read of a remote source document) authenticates against a secured host. This
records why the fetch pane's credential model moved from "pick a registered workload binding" to
"connect as yourself through a configured provider", with a one-shot secret as the fallback and the
workload binding demoted to an explicit, filterable third option.

## Context

`POST /sources/fetch` performs the spec-document fetch server-side (no browser CORS, one fetch
implementation). Its only authentication affordance was a flat `<select>` over the first 200
registered §13 credential bindings (`source · environment`). That got the identity model wrong:

- A §13 binding is a **workload** credential — what a runner presents to a source's API when
  *executing* a workflow in an environment ([ADR 0048](0048-source-credentials-are-references.md),
  [ADR 0051](0051-channel-sources-bind-per-environment.md)). Fetching a spec document from a
  portal is a **user-identity** act: the author is reading documentation their SSO says they may
  see. The two populations barely overlap, and handing workload secrets to an authoring gesture
  inverts the custody story.
- Even where a binding *is* right, an unfiltered select over a possibly-huge binding list
  contradicts the established filterable-picker pattern (`arazzo-filter-input`).
- The typical secured spec host (an internal developer portal, SwaggerHub, a partner API portal)
  sits behind interactive OAuth/OIDC. The GitHub integration already solved exactly this for one
  host: a deployment-registered OAuth app, a brokered popup, server-side per-principal token
  custody, reach = whatever the user can see.

## Options

**Keep bindings as the model and add filtering.** Rejected: filtering fixes the ergonomics, not the
identity inversion — the list still holds workload secrets, and the common case (an SSO'd portal)
is simply absent.

**Ad-hoc interactive OAuth against any URL.** Rejected as impossible: OAuth requires the target to
be an authorization server *and* the platform to be a registered client (a `client_id`) there. No
dialog can improvise a client registration.

**Store user PATs per host.** Rejected: inventing long-lived custody for arbitrary hosts multiplies
secret liability for the tail case. The tail is served by a one-shot secret that is never stored.

## Decision

**A deployment registers connected providers; the fetch authenticates as the user through them.**

- **Connected provider registry (configuration).** A provider entry names the provider and carries
  either an OIDC `issuer` (endpoints resolved by discovery) or explicit `authorizeEndpoint` +
  `tokenEndpoint`, plus `clientId`, a secret reference (environment variable / local config, the
  `GitHubOAuth` seam generalized), `scopes`, and the `hosts` patterns it covers. GitHub folds in as
  provider #1 under the same seam — its repos/browse surface stays GitHub-specific, but its
  authorize/callback/custody machinery is the shared provider machinery.
- **Per-principal provider connections.** Connecting runs the brokered popup (authorize →
  callback → server-side token exchange); the user token is held server-side per
  `(principal, provider)`, exactly the GitHub custody model. The platform never hands the token to
  the browser.
- **Connection-aware fetch.** The fetch request names its authentication explicitly: a provider
  connection, a one-shot secret, or a workload binding. The pane resolves the pasted URL's host
  against the provider registry: covered + connected → fetch with the user's token; covered + not
  connected → offer Connect; not covered → anonymous, with the fallbacks available.
- **One-shot secret fallback.** For a host no provider covers (the user is authorized, the
  platform is not a registered client there): paste a bearer/PAT, it authenticates that single
  server-side fetch, and it is never stored or logged.
- **Workload binding, demoted and filterable.** The `(sourceName, environment)` binding remains an
  explicit third option (some spec endpoints genuinely are served by the runtime credential),
  rendered as the kit's filter combo, never a flat select.

## Consequences

- The pane's order states the identity model: *connect as yourself* → *one-shot secret* →
  *workload binding*.
- Adding an SSO'd internal portal is configuration, not code: issuer + client id + secret ref +
  hosts (OIDC discovery fills in the rest).
- The GitHub OAuth guide's registration/custody/rotation guidance generalizes to every provider;
  GitHub stops being a special case in the auth machinery.
- Provider connections are in-process custody (as GitHub's were): a multi-node deployment needs
  sticky sessions or a shared custody store before horizontal scale — unchanged from the GitHub
  posture, now stated once for all providers.
- The demo composition can register its own Keycloak as a provider over a secured sample spec
  endpoint, so interactive OAuth is live-testable without any external account.
