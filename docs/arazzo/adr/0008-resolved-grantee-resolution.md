# ADR 0008. Resolved-grantee resolution: no guessing

Date: 2026-07-21. Status: **Accepted**. Scope: how the identity a grant, an administrator seat, or a
credential usage applies to is chosen. Supports [0003](0003-membership-matching-over-canonical-identity.md).
This records why a grantee is always resolved to an exact `sys:` identity before it is stored, rather than
authored as a free-form `{dimension, value}` tuple the system later guesses at.

## Context

[0003](0003-membership-matching-over-canonical-identity.md) matches a principal against a named identity by
membership. That is only safe if the named identity is exact. If an author could type a free-form tuple, say
`sub=alice`, the system would have to guess which issuer, which tenant, and which of several people named
alice it meant, and a coarse or ambiguous name under membership matching could apply far more widely than the
author intended. The worry that membership lets a coarse founder over-reach is answered here: the grantee is
pinned to the exact grain at authoring time, so there is no coarse identity to expand.

### Grounded architectural facts

- **Resolution draws on two indexed sources plus a validated fallback.** `ArazzoControlPlaneIdentityHandler`
  (`ControlPlane.Server/ArazzoControlPlaneIdentityHandler.cs`) resolves grantees from the store-indexed
  observed-identity typeahead (`IObservedIdentityStore`, the identities the deployment has actually seen) and
  an optional pluggable directory (`IPrincipalDirectory`, people, teams, and roles from the IdP or an external
  directory). Where neither is configured, a raw subject may be authored and validated, issuer-sensitively.
- **A workflow is never directory-resolved.** Workflow is a control-plane concept, not a directory principal,
  so `sys:workflow` is resolved by the control plane and never fetched from a directory.
- **The directory resolves to the full membership-expanded identity.** A person is resolved to their complete
  `sys:` identity including group and team memberships (the directory adapters,
  `src/Corvus.Text.Json.Arazzo.Directories/`), so the pinned grantee carries the rich identity that
  [0003](0003-membership-matching-over-canonical-identity.md) matches against.

## Decision

A grantee is **resolved to an exact `sys:` identity before it is stored**, never authored as a free-form
tuple the system later guesses at. The picker resolves a grantee from the observed-identity typeahead or a
configured directory, or from a raw subject that is validated, and pins the full resolved identity. Only that
resolved identity is written to a grant, an administrator seat, or a credential usage. Membership matching
([0003](0003-membership-matching-over-canonical-identity.md)) then runs against exact, pre-resolved
identities on both sides.

## Consequences

- Membership matching is safe from coarse over-reach. Because every stored grantee is resolved to the exact
  grain, there is no coarse founder identity for a subset match to expand past what the author intended.
- The authoring surface presents real, resolved grantees (people, teams, roles the deployment knows), not a
  free-form form the author has to get exactly right, which is the correct-by-construction posture the
  security UI takes.
- The picker is issuer-sensitive. Where a raw subject is authored it is validated against the configured
  issuer, because the same `sub` under a different issuer is a different principal.
- Because the directory resolves a person to their full membership-expanded identity, a grant keyed on a
  team applies to any person the directory places in that team, without the grant naming the person.
