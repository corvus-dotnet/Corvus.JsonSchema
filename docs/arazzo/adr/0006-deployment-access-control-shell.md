# ADR 0006. The deployment access-control shell and ambient identity

Date: 2026-07-21. Status: **Accepted**. Scope: the deployment-wide constraint every principal's reach is
wrapped in, and how request-context dimensions enter the identity. Builds on
[0002](0002-grant-verbs-are-reach-not-scopes.md) and [0004](0004-fail-closed-non-disclosing-enforcement.md).
This records why a deployment imposes a mandated wrapper rule and a reserved tag namespace that no user grant
can widen or forge, and why an ambient dimension derived from the request overrides a token-supplied claim of
the same name.

## Context

A multi-tenant deployment needs a boundary that no authored grant can cross. If a tenant boundary were just
another user rule, a mistaken or malicious grant could widen past it. The boundary has to sit outside the
grant model, wrapping it, so a user rule can only ever narrow within it.

The boundary is expressed against identity tags such as `sys:tenant`. Two things then have to be true of
those tags. A user must not be able to author or forge one (a caller who could write their own
`sys:tenant=acme` tag would cross the boundary), and where a dimension is derived from the request context
rather than the token (a vanity host, a route prefix, an API-gateway header), the derived value must win over
any value the token carries for the same dimension, or the boundary could be widened by a forged claim.

### Grounded architectural facts

- **The shell is a mandated wrapper plus a reserved prefix.** `SecurityShell`
  (`src/Corvus.Text.Json.Arazzo.Durability/SecurityShell.cs`) holds the mandated wrapper rules every principal
  is constrained by and the reserved internal-tag prefix, `DefaultInternalPrefix = "sys:"`. The mandated rules
  typically reference internal tags against claims, for example `sys:tenant == $claim.tenant`.
- **The wrapper ANDs the user rule.** `SecurityShell.BuildFilter(userRules, claims)` builds the effective
  filter as the mandated wrapper rules ANDed with the principal's resolved user rules. If both the wrapper and
  the user rules are empty the filter admits nothing (the unrestricted case must be an explicit grant, per
  [0004](0004-fail-closed-non-disclosing-enforcement.md)).
- **Internal tags are deployment-owned.** `SecurityShell.ValidateUserTags` rejects a user tag whose key
  carries the internal prefix, and `SecurityShell.StripInternal` removes internal tags from client responses,
  so a `sys:` tag can be neither authored nor observed by an end user.
- **Ambient dimensions are authoritative over token claims.** `PersistentRowSecurityPolicy.AddAmbientClaims`
  (`ControlPlane.Server/PersistentRowSecurityPolicy.cs`) stamps request-context dimensions
  (`IAmbientIdentityDimensions.Resolve`, `HttpRequestAmbientIdentityDimensions`) into the reach claim map, and
  the ambient value **replaces** any token-supplied claim of the same name. It is a no-op when no provider is
  configured.

## Decision

Every deployment has an access-control shell.

- A set of **mandated wrapper rules** is ANDed around every principal's resolved reach. A user grant narrows
  within the shell and can never widen past it. A tenant boundary the shell imposes therefore holds for every
  principal regardless of the grants authored.
- A **reserved tag namespace** (`sys:` by default) marks deployment-owned identity dimensions. A user may not
  author a tag in it (`ValidateUserTags` rejects), and the namespace is stripped from client responses
  (`StripInternal`), so internal identity tags can be neither forged nor observed.
- An **ambient dimension** derived from the request context is authoritative over a token-supplied claim of
  the same name. A `sys:` dimension need not come from the token, and when it is derived from the request it
  overrides any conflicting token claim, so a forged claim cannot widen context-derived reach.

The shell (its wrapper rules and its prefix) is per-deployment configuration, alongside the authentication
scheme and the security mode ([0016](0016-control-plane-security-mode.md)).

## Consequences

- Tenant isolation is enforced structurally. It does not depend on every grant being authored correctly,
  because the shell wraps and constrains all of them.
- Authoring a grant that references an ambient dimension carries a consistency requirement: the grant only
  matches a caller whose stamped identity will contain that dimension's value, so the value has to be authored
  as it will be stamped (this is the trap called out in execution-host-design §16.5.5).
- The unrestricted (operator) case cannot fall out of an empty shell and empty rules, which
  [0004](0004-fail-closed-non-disclosing-enforcement.md) requires: it must be an explicit grant.
- Because internal tags are stripped from responses, the access overview and any client-facing identity view
  present the operator-facing form, and the server re-derives the internal form when it needs to match
  (this is why the overview must be server-aggregated, ADR 0015).
