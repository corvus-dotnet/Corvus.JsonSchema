# ADR 0005. Capability entitlements union into token claims; the IdP is never mutated

Date: 2026-07-21. Status: **Accepted**. Scope: how a stored capability grant reaches the capability check.
Builds on [0001](0001-two-plane-access-model.md). This records why a scope granted inside the control plane
(by an access-request approval) is unioned into the principal's `scope` claim at authentication time, rather
than written back to the identity provider or read from a second place at authorization time.

## Context

[0001](0001-two-plane-access-model.md) puts capability on the token: the check reads the `scope` claim. But
some capability grants originate inside the control plane, not the IdP. An access-request approval
([0010](0010-access-requests-ceiling-bounded.md) once written) can write a time-boxed capability grant to a
principal. That grant has to take effect at the capability check, which reads the `scope` claim, and the
control plane cannot write to the IdP that issued the token.

Two placements were possible. Read granted scopes from a second source at authorization time (the check
consults both the token and the store), or fold the stored grants into the `scope` claim once, at
authentication time, so the existing check needs no change.

### Grounded architectural facts

- **One transformer unions stored scopes into the claim.** `ControlPlaneEntitlementClaimsTransformer`
  (`ControlPlane.Server/ControlPlaneEntitlementClaimsTransformer.cs`) is an `IClaimsTransformation` that folds
  `ControlPlaneRowSecurityPolicy.ResolveGrantedScopes` into the principal's `scope` claim at authentication
  time, before the scope-authorization layer reads it.
- **It is allocation-free on the common path.** A principal with no per-principal scope grant resolves the
  shared empty list and is returned unchanged, with no clone and no claim added. Only an actually-elevated
  principal clones in a scope claim. It is idempotent, guarded by an applied-marker
  (`arazzo:entitlement-scopes-applied`), because ASP.NET Core may invoke a transformer more than once.
- **The IdP is never written.** The transformer only shapes the in-memory `ClaimsPrincipal` for this request.
  The token, and the IdP that issued it, are untouched.

## Decision

Stored capability grants are **unioned into the `scope` claim at authentication time** by a claims
transformer, not read from a second source at authorization time and not written back to the identity
provider. The capability check stays simple: it reads the `scope` claim, which now already carries both the
token's scopes and the principal's stored grants. Identity and its coarse, slow-changing scopes live in the
IdP; the control plane only unions in the fine, control-plane-issued grants for the duration of the request.

## Consequences

- The capability check has one input (the `scope` claim) and needs no knowledge of stored entitlements, which
  keeps [0001](0001-two-plane-access-model.md)'s capability plane a single, simple read.
- An approval that grants a scope takes effect on the principal's next request, when the transformer folds it
  in, with no IdP round-trip and no token reissue.
- The common path, a principal with no control-plane grant, allocates nothing: the transformer returns the
  principal unchanged.
- This mirrors the union pattern reach uses (the resolver unions matched bindings,
  [0002](0002-grant-verbs-are-reach-not-scopes.md)), so both planes resolve stored grants the same way: claims
  unioned with stored entitlements, the IdP owning only the coarse baseline.
