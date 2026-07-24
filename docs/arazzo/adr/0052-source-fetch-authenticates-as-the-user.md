# ADR 0052. Acquiring a source document is matched to how the document is protected

Date: 2026-07-24. Status: **Accepted**. Scope: how the acquisition dialog's Fetch URL path (and the
other acquisition modes beside it) bring a third-party API description document (OpenAPI, AsyncAPI,
Arazzo) into a working copy or the registry, against endpoints that may be unsecured or secured. This
record corrects an earlier framing of the same decision that put a brokered OAuth "connect as
yourself" flow first and treated everything else as a fallback. That framing mapped the lightest need
(retrieve a document the author can already see) onto the heaviest, most security-critical mechanism,
and mis-described the common secured case. The corrected decision matches the acquisition mechanism to
how the document is actually protected.

## Context

`POST /sources/fetch` performs the document fetch server-side (`SourceDocumentFetcher.FetchAsync`).
It is server-side deliberately: a browser cannot make a credentialed cross-origin read of a
third-party document endpoint, because that endpoint does not return the CORS headers that would
permit it, and many public endpoints are not CORS-permissive either. One server-side fetch, one
parser, no browser CORS.

The governing question is what "secured" means for a document endpoint, because the acquisition
mechanism has to match the protection. The author's own description of the common case is the anchor:
*a document I could retrieve in my own browser once I have logged into the site.* That is almost
always a **browser session**: an origin-scoped cookie (commonly HttpOnly), or an identity-aware-proxy
session, established by an interactive login. The session is held by the browser. It is not a secret
the author can hand over, and it is not available to the control-plane server. A server-side fetch
cannot present it, and a browser-side fetch cannot send it cross-origin. A document behind a browser
session can therefore only be brought in **through the browser, by the author's own hand**.

That is distinct from two other protections that a document endpoint may use, and which the server
*can* present:

- **A header credential.** The endpoint accepts a static secret in a request header: a bearer token
  or PAT (`Authorization: Bearer`), an API key (a named header or query parameter), or HTTP Basic.
  The author holds the secret, or it is a registered workload credential. The §13 credential
  machinery already models exactly these (`SourceCredentialKind` is `ApiKey`, `Bearer`, `Basic`,
  `OAuth2ClientCredentials`, `Mtls`). This is the common *secured-API* case: an API's own
  `openapi.json` behind the same token as the API, a gateway subscription key, a portal download API.
- **An OAuth/OIDC access token that the endpoint's content API accepts.** An authorization-code flow
  yields a token about and for the user. It retrieves the document only where the target is a
  resource server that accepts that token on its content endpoint, with the audience and scope that
  endpoint requires. This holds for git hosts and API platforms whose content APIs are
  bearer-authenticated (GitHub contents, GitLab repository files, Azure DevOps items) and for
  bearer-fronted API catalogs. It does **not** hold for a portal whose document endpoint is
  cookie-protected, even when that portal is itself an OIDC provider: the access token authenticates
  the user to the identity provider, but it is not what the portal's document endpoint checks.

The earlier framing conflated the last two. "Interactive OAuth" was read as "the way I log into the
portal" and built as "an authorization-code flow that mints an API token for the document endpoint."
For a git host that is correct. For a portal the author merely logs into, it is usually inapplicable:
the flow completes, a token is custodied, and the fetch still fails because the endpoint wanted the
session cookie, not our token.

## Options

**A. A brokered OAuth "connect as yourself" flow as the primary model.** A deployment registers OAuth
providers; the author connects per provider through a brokered popup; the server holds the user token
and presents it on the fetch. The header credential is demoted to a filterable fallback, and
browser-mediated acquisition is not a first-class fetch answer. This is what was first shipped.

**B. An acquisition ladder matched to the protection.** Anonymous fetch for public URLs;
browser-mediated acquisition (paste the document, or upload the saved file) for documents behind a
browser session; a server-presented header credential (one-shot or a registered binding) for
secured-API endpoints; the connected-provider broker for providers whose content API accepts a
brokered token. The pane's order follows how commonly each protection occurs.

**C. Replay the browser session server-side.** Have the author (or the browser) hand the third-party
session to the server so the server can fetch as the browser would, by extracting the cookie or
proxying the authenticated session.

## Antagonistic review

**A (broker-first).** Its strength is real for the case it fits: a git host with a token-authenticated
content API gives a genuine per-user reach story, and the brokered popup keeps every credential off
the browser. Its failure is that it was cast as the answer to "a document I can see in my browser,"
which it usually is not. It requires a deployment operator to pre-register an OAuth client at each
provider, it stands up a security-critical authorization-code client with server-side refresh-token
custody, and against a cookie-protected portal it does not work at all while looking as though it
should. It leads with the narrowest, heaviest mechanism and buries the ones that actually serve the
common cases.

**B (the ladder).** Its strength is that each tier matches a real protection and no tier pretends to
serve a protection it cannot. The common cases (public, browser session, header credential) are
served by light mechanisms, and the heavy broker is reserved for where it genuinely applies. Its cost
is honesty about tier 2: for a document behind a pure browser session there is no server-side
shortcut, so the author does a manual step (paste or upload). That manual step is not a defect of the
design; it is the true shape of "only my browser is authenticated here."

**C (replay the session).** Rejected as both impossible cleanly and a security anti-pattern. A session
cookie is HttpOnly and origin-scoped precisely so that no script and no other origin can read it; the
control plane is neither the browser nor that origin. Extracting and forwarding a user's third-party
session to a server would manufacture a credential-exfiltration path and a custody liability for
exactly the material the browser is built to protect. The browser is the right place for a browser
session to stay.

## Decision

**Acquisition is a ladder, matched to how the document is protected, ordered by how commonly each
protection occurs.**

1. **Public.** An anonymous server-side fetch.
2. **Behind a browser session (cookie or SSO), with no extractable header credential.** The browser
   is the authenticated agent, so the bytes come in through the browser: the author pastes the
   document text, or uploads the file they saved from the authenticated page. No server-side
   credential custody is involved. A best-effort same-origin or CORS-permissive credentialed browser
   fetch may be offered as a convenience, falling back silently to paste or upload. This is the
   answer to "a document I could retrieve in my own browser once logged in."
3. **Behind a header credential the author holds.** A server-side fetch presenting it: a one-shot
   secret (used for the single fetch, never stored or logged) or a registered §13 workload binding.
   The schemes are those the §13 machinery already models (bearer, API key by header or query name,
   Basic), not bearer alone.
4. **Behind an OAuth/OIDC provider whose content API accepts a brokered user token.** The
   connected-provider broker: an authorization-code flow through a deployment-registered client,
   per-`(principal, provider)` server-side token custody, host-coverage-gated so a user's token is
   only ever attached to a host the provider covers. This is the git-host and API-platform class
   (GitHub, GitLab, Azure DevOps, and bearer-fronted API catalogs), not a GitHub special case. It is
   the specialized tier: it requires a client registration at the provider and applies only where the
   endpoint accepts the token.

The pane states this order. Public and browser-session acquisition come first because they cover the
common cases and carry no server-side credential surface. The header credential and the connected
provider follow, for the endpoints that genuinely present those protections.

## Consequences

- The pane's order changes from "connect as yourself, one-shot secret, workload binding" to the
  protection-matched ladder above. Browser-mediated acquisition (paste beside upload) becomes a
  first-class acquisition answer rather than an afterthought, and is what the pane offers when a URL
  sits behind a browser session.
- The claim that adding an SSO'd portal is "configuration, not code" is corrected. A portal whose
  document endpoint is cookie-protected is served by browser-mediated acquisition, not by a provider
  registration. Only a provider whose content API accepts a brokered token is served by a
  registration.
- The connected-provider broker is retained and generalized, not GitHub-special: it serves any
  provider whose content API accepts a brokered bearer (GitHub, GitLab, Azure DevOps, bearer-fronted
  catalogs). GitHub is provider #1 of that class; its repos and browse surface stay GitHub-specific
  while the authorize, callback, exchange, and custody machinery is the shared provider broker.
- The one-shot secret must present the header scheme the endpoint wants (bearer, API key by header
  name, Basic), matching the §13 kinds, rather than being bearer-only. This is a known narrowing in
  the current implementation to widen.
- The broker's custody posture is unchanged: in-process per `(principal, provider)`, so a multi-node
  deployment needs sticky sessions or a shared custody store before horizontal scale.
- A live broker test needs a provider whose content endpoint actually accepts the brokered token (a
  git host, or an API whose gateway checks the token). A demo endpoint that validates the
  deployment's own session or realm token is not, on its own, a faithful test of the broker against
  an independent third party, and must not be presented as one.
