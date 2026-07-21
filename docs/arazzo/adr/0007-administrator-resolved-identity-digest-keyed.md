# ADR 0007. Administration is a set of resolved identities, digest-keyed, with a reverse index

Date: 2026-07-21. Status: **Accepted**. Scope: how a workflow's (and an environment's) administrators are
modelled, matched, and queried. Builds on [0003](0003-membership-matching-over-canonical-identity.md). This
records why administration is a set of resolved identities rather than principals, keyed by a canonical
digest, with a reverse index that answers "what do I administer" as an indexed lookup.

## Context

Some rights follow the resource, not a capability scope. Publishing a further version of a workflow, and
managing who administers it, belong to that workflow's administrators. The same holds for an environment.
This administration has to survive the people involved: an administrator seat is not "the person who is
logged in", it is an identity that a person may satisfy. It also has to answer two questions cheaply. The
forward question, "who administers workflow X?", and the reverse, "which workflows does this caller
administer?", which the approver inbox needs so an approver sees every request they can act on without naming
a workflow first.

### Grounded architectural facts

- **Administration is a set of identities.** `WorkflowAdministrators` (`Durability/Security/`) holds a set of
  administrator identities, governed through `SecuredWorkflowCatalog` and persisted by
  `IWorkflowAdministratorStore`. The environment twin is `EnvironmentAdministrators` /
  `IEnvironmentAdministratorStore`.
- **Membership is the subset test of [0003](0003-membership-matching-over-canonical-identity.md).**
  `WorkflowAdministrators.IsAdministeredBy(candidate)` is true when a stored administrator identity is a
  subset of the caller's whole stamped identity (founder subset-of candidate).
- **Keyed by a canonical digest.** `SecurityIdentityDigest` (`Durability/SecurityIdentityDigest.cs`) is the
  canonical digest of an identity, so an identity's stored key is stable and order-independent.
- **The reverse question is an indexed lookup, not a scan.** `IWorkflowAdministratorStore.ListAdministeredAsync`
  looks up every non-empty subset-digest of the caller's identity (`SecurityIdentityDigest.SubsetDigests`)
  against the founder-digest-keyed index, verified across the in-memory store and nine backends
  (`WorkflowAdministeredPaging`, `WorkflowAdministeredContinuationToken`).
- **Workflow identity is immutable and materialised at creation.** Version 1 establishes the base id's
  administrator set (design §15.2), so `sys:workflow` cannot be squatted by a later submitter, and the reverse
  index has an entry for every workflow from birth with no implicit-version-1 blind spot.

## Decision

Administration is a **set of resolved identities**, not a set of principals. A caller administers a target
when their stamped identity contains one of the target's administrator identities
([0003](0003-membership-matching-over-canonical-identity.md)). The set is keyed by each identity's canonical
`SecurityIdentityDigest`, and a reverse index maps administrator-digest to the base ids it administers so the
reverse question is an indexed subset-digest lookup, never a scan. A workflow's administrator set is
established at creation (version 1) and cannot be squatted thereafter.

## Consequences

- An administrator seat outlives the people who fill it. Adding or removing a person from a group changes who
  satisfies a group-keyed seat, without editing the seat.
- The approver inbox is answerable cheaply. "Which requests may I act on" reduces to "which workflows do I
  administer", which is the indexed reverse lookup.
- The write path stays founder-digest-keyed and only the read side fans out over subset-digests, so
  publishing and re-administration do not pay for the reverse-index convenience
  ([0003](0003-membership-matching-over-canonical-identity.md)).
- Environment administration reuses the same model, which is why promotion into an environment is gated on
  the target environment's administrator set.
