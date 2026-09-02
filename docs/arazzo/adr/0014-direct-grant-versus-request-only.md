# ADR 0014. Direct grant versus request-only, split by binding type

Date: 2026-07-21. Revised 2026-09-02: the guard covers read reach and capability scopes, not only write and
purge (P1-5 of the 2026-08-07 security audit). Status: **Accepted**. Scope: which grants may be authored
directly and which must go through the request-and-approve flow. Builds on
[0010](0010-access-requests-ceiling-bounded.md). This records why the authoring path is split by binding
type, with a server-side self-elevation guard that no direct author can cross.

## Context

There are two ways access reaches a person. An administrator authors a grant directly (a `security:write`
action), or a person requests access and an approver grants it ([0010](0010-access-requests-ceiling-bounded.md)).
Making everything direct removes the separation of duties that the request flow provides. Making everything
request-only makes coarse, standing policy (a whole team may read a domain) needlessly heavyweight. The split
has to follow the nature of the grant, and in both paths a privileged author must be unable to quietly
elevate themselves.

Read reach is a tenant boundary, not a courtesy: the security policy is reach-partitioned by tenant
([0067](0067-reach-enforced-by-the-store-proven-on-the-wire.md) and the P1-5 reach plane), so a read grant the author
matches is cross-tenant read in one call. A binding also carries `scopes`, capability the
identity provider never issued. Either, granted to the author themselves, is elevation.

### Grounded architectural facts

- **The split is by binding type.** The security UI (`guides/security-ui.md`) makes standing group or policy
  bindings (coarse reach for a team or a role) authorable directly with `security:write`, while a per-person
  (`sub`-scoped) binding stays request-and-approve only. The rationale is separation of duties: granting one
  named person elevated access is the case that most wants a second party.
- **A server-side self-elevation guard covers both authoring paths.** `SelfElevates`
  (`ControlPlane.Server/ArazzoControlPlaneSecurityHandler.cs`, defense in depth) refuses, on create and on
  update, a binding that confers anything on the caller themselves, decided by `CallerMatches` (membership: the caller's
  own stamped identity contains the binding's claim and every additional clause). A refusal is audited as `refused-self-elevation`.
- **The guard is inert where there is nothing to elevate.** In the unscoped or `Open` posture there is no
  caller identity and no row reach to elevate, so the guard is a no-op.

## Decision

The grant-authoring path is **split by binding type**, and both paths sit under a server-side self-elevation
guard.

- **Standing group or policy bindings** (coarse reach for a team or role) may be authored directly by a
  holder of `security:write`.
- **Per-person bindings** (a named `sub`) go through the request-and-approve flow
  ([0010](0010-access-requests-ceiling-bounded.md)), so granting one person elevated access always involves a
  second party.
- **The self-elevation guard** refuses any binding, direct or requested, that confers **anything** on the
  caller themselves: read, write or purge reach, or any capability scope, on create and on update. An
  `eligibleOnly` binding counts, since stored eligibility is honoured by the self-elevation strategy and
  would be the same elevation with a one-request detour. A binding that confers nothing is not elevation.
  The `*` wildcard primary contains every authenticated caller, the author included, so a wildcard binding
  with any grant is refused on the API path. Deployment-wide shell grants are the bootstrap's to seed.

The guard is strict, not comparative. A "would this widen what the author already holds" check was rejected.
Rule-expression containment is not cheaply decidable, and a standing grant of a scope the author holds only
by token still elevates in time, because it survives the provider revoking it.

## Consequences

- Separation of duties is preserved for the case that most needs it (elevating a named person), without
  making coarse standing policy heavyweight.
- An author writes standing policy for groups they are not a member of. Anything for their own, read included,
  goes through the request flow and its independent-decision rule
  ([0009](0009-eligible-versus-active-self-elevation.md)).
- The guard is defense in depth: it is a server check, not a UI affordance, so it cannot be bypassed by
  calling the API directly.
- In an `Open` or unscoped deployment the guard is inert, because there is no reach to elevate
  ([0016](0016-control-plane-security-mode.md)).
