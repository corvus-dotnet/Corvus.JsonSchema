# ADR 0003. Membership matching over one canonical `sys:` identity

Date: 2026-07-21. Status: **Accepted**. Scope: how every authorization facet matches a principal against a
named identity. Supersedes the exact set-equality rule formerly stated in execution-host-design §16.5.4 and
§15. This records why reach, administration, self-elevation, and usable-credential all match by membership
(subset) over one rich canonical identity, rather than by set-equality against a narrow one.

## Context

A principal has an identity. A grant, an administrator seat, and a usage-scoped credential each name an
identity they apply to. Authorization has to decide whether the principal's identity satisfies the named
one. The original rule (execution-host-design §16.5.4) was **exact set-equality**: the principal matched only
if their identity set equalled the named set exactly, so "a superset or partial match is not an
administrator".

Set-equality pulls the identity in two incompatible directions, and this is the tension that forced a
change.

- **Reach wants the identity rich.** To match many grants, a principal's identity should carry many
  dimensions (subject, issuer, tenant, groups, roles). A binding keyed on `team=payments` should apply to
  any principal who is in that team, regardless of what else they are.
- **Set-equality wants the identity narrow.** For a person to set-equal a group-keyed administrator seat,
  their identity would have to be exactly the group's identity, which a person is not. So the same person
  cannot both carry a rich identity for reach and set-equal a coarse administrator seat. The demo had to
  stamp a group-form identity `{sys:group, sys:iss}` so a group could set-equal its own founder, which is a
  workaround, not a model.

A worry about the alternative, membership, is that it might let a coarse "founder" identity administer
things it should not. That worry is answered by the resolved-grantee model
(ADR 0008 once written, execution-host §16.5.4): a grantee is always
resolved to the exact grain before it is stored, so there is no coarse founder to expand. Membership over a
rich identity, plus resolved grantees, holds together.

### Grounded architectural facts

All facets already resolve against one canonical `sys:` identity, and the membership form is a subset test.

- **Identity is `SecurityTagSet`** (`Durability/SecurityTagSet.cs`), a deferred JSON array of `{key, value}`
  tags with string-free `Contains` / `IsSubsetOf` / `SetEquals`. The `sys:` prefix marks deployment-owned
  tags. `Contains` is exactly the membership primitive.
- **Reach matcher.** `PersistentRowSecurityPolicy.Matches` applies a binding when the caller's canonical
  identity contains each clause (`dimension = value`); the wildcard `*` is unchanged. It matches the
  canonical identity, not raw OIDC claims.
- **Self-elevation guard.** `ArazzoControlPlaneSecurityHandler.CallerMatches` uses the same "caller identity
  contains the named identity" test.
- **Administration.** `WorkflowAdministrators.IsAdministeredBy(candidate)` (and
  `WorkflowIdentity.SameAdministrator`) makes the forward check `founder subset-of candidate`: a stored
  administrator identity administers a caller whose whole stamped identity contains it.
- **Reverse index.** "What does this identity administer" looks up every non-empty subset-digest of the
  caller's identity (`SecurityIdentityDigest.SubsetDigests`) against the founder-digest-keyed index
  (`IWorkflowAdministratorStore.ListAdministeredAsync`). The stored side stays founder-keyed; only the query
  fans out over subsets. Verified across the in-memory store and nine backends.

## Options

**Exact set-equality (the original rule, now superseded).** A principal matches only if their identity set
equals the named set. Rejected: it cannot let one rich person identity satisfy both a fine reach grant and a
coarse administrator seat, and it forced the demo's group-form-identity workaround.

**Membership, the named set is a subset of the principal's identity (chosen).** A principal matches a named
identity when the principal's canonical identity contains it.

## Decision

Every authorization facet matches by **membership over one canonical `sys:` identity**. A principal
administers, reaches, or may use a target when the principal's stamped canonical identity **contains** the
target's named identity, where contains means the named tag set is a subset of the principal's identity. The
canonical identity is resolved once (including membership expansion, ADR 0008)
and every facet, the reach matcher, the self-elevation guard, administration, and usable-credential, uses the
same subset test. The wildcard `*` is unchanged. Matching is against the canonical identity, never against
raw OIDC claims.

## Consequences

- The original set-equality statements in execution-host-design §16.5.4 and §15 are **superseded by this
  ADR**. The spec now points here for the matching rule.
- The identity may be, and should be, rich. There is no longer a reason to stamp a narrow group-form
  identity to make set-equality work.
- The reverse administration index stays founder-digest-keyed and only the read side fans out over
  subset-digests, so the write path is unchanged and the read stays an indexed lookup, not a scan.
- Coarse over-reach is prevented not by narrowing the identity but by resolving grantees to the exact grain
  before storing them (ADR 0008).
- Membership governs the **authorization** facets only. A distinct class of **identity operations** stays exact
  by design: add-idempotency and digest-keyed removal in the observed-identity and administrator stores, and the
  uniqueness collision probe (`FindIdentityConflictAsync`), whose 409 stays exact set-equality with membership
  added only as a non-blocking broadening advisory. When a new identity-matching surface is added it is
  classified first, an authorization facet matches by membership, an identity operation stays exact, so the
  asymmetry between the two that produced the earlier matching bugs cannot recur unrecorded.
