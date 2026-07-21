# ADR 0014. Direct grant versus request-only, split by binding type

Date: 2026-07-21. Status: **Accepted**. Scope: which grants may be authored directly and which must go through
the request-and-approve flow. Builds on [0010](0010-access-requests-ceiling-bounded.md). This records why the
authoring path is split by binding type, with a server-side self-elevation guard that no direct author can
cross.

## Context

There are two ways access reaches a person. An administrator authors a grant directly (a `security:write`
action), or a person requests access and an approver grants it ([0010](0010-access-requests-ceiling-bounded.md)).
Making everything direct removes the separation of duties that the request flow provides. Making everything
request-only makes coarse, standing policy (a whole team may read a domain) needlessly heavyweight. The split
has to follow the nature of the grant, and in both paths a privileged author must be unable to quietly
elevate themselves.

### Grounded architectural facts

- **The split is by binding type.** The security UI (design `security-ui-design.md` §6.5 / §8) makes standing
  group or policy bindings (coarse reach for a team or a role) authorable directly with `security:write`,
  while a per-person (`sub`-scoped) binding stays request-and-approve only. The rationale is separation of
  duties: granting one named person elevated access is the case that most wants a second party.
- **A server-side self-elevation guard covers both authoring paths.** `SelfElevates`
  (`ControlPlane.Server/ArazzoControlPlaneSecurityHandler.cs`, design §16.5.3, defense in depth) refuses a
  binding, on both create and update, that would grant the caller itself elevated write or purge reach
  (`CallerMatches` against the caller's own stamped identity). A refusal is audited as
  `refused-self-elevation`.
- **The guard is inert where there is nothing to elevate.** In the unscoped or `Open` posture there is no row
  reach to elevate, so the guard is a no-op.

## Decision

The grant-authoring path is **split by binding type**, and both paths sit under a server-side self-elevation
guard.

- **Standing group or policy bindings** (coarse reach for a team or role) may be authored directly by a
  holder of `security:write`.
- **Per-person bindings** (a named `sub`) go through the request-and-approve flow
  ([0010](0010-access-requests-ceiling-bounded.md)), so granting one person elevated access always involves a
  second party.
- **The self-elevation guard** refuses any binding, direct or requested, that would grant the caller itself
  elevated write or purge reach, on create and on update. It is enforced on the server, so it holds regardless
  of the UI.

## Consequences

- Separation of duties is preserved for the case that most needs it (elevating a named person), without making
  coarse standing policy heavyweight.
- A privileged author cannot grant themselves elevated reach, because the server refuses a self-elevating
  binding even from a `security:write` holder. Elevating oneself goes through the request flow and its
  independent-decision rule ([0009](0009-eligible-versus-active-self-elevation.md)) instead.
- The guard is defense in depth: it is a server check, not a UI affordance, so it cannot be bypassed by
  calling the API directly.
- In an `Open` or unscoped deployment the guard is inert, because there is no reach to elevate
  ([0016](0016-control-plane-security-mode.md)).
