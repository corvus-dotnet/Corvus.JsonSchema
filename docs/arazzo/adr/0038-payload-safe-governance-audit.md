# ADR 0038. A payload-safe governance-audit primitive

Date: 2026-07-21. Status: **Accepted**. Scope: how a governed action is audited. This records why every
governed action is audited through one primitive that emits a span and an audit log carrying only controlled
vocabulary and identifiers, never a payload or a secret.

## Context

A control plane governs access, and governance actions (grant, revoke, approve, deny, publish, delete) have to
leave an audit trail: who did what to which resource, and how it turned out. Two things can go wrong. The
audit can be inconsistent, each action logging in its own shape, so an operator cannot rely on a uniform trail.
Or the audit can leak: if an action logs its inputs, a step output, a credential value, or a request payload
can end up in a log or a trace, which is exactly the sensitive data the disclosure tier
([ADR 0013](0013-step-output-disclosure-tier.md)) works to protect.

### Grounded architectural facts

- **One primitive audits every governed action.** `GovernanceAudit.Mutation(logger, action, actor,
  targetKind, targetId, outcome)` (`ControlPlane.Server/GovernanceAudit.cs`, design §850) emits a span named
  for the action on `ArazzoTelemetry.ActivitySource` plus an audit-grade structured log, so who changed what,
  and the outcome, are recorded uniformly.
- **Its inputs are controlled vocabulary and identifiers only.** The action name, actor, target kind, target
  id, and outcome are all a stable controlled vocabulary or an identifier, never a workflow payload or a
  secret. A caller cannot route a step output or a credential value through it, because there is no parameter
  that takes one.
- **It is zero-cost when unobserved.** The span is zero-cost when no listener is attached, and the log is
  emitted only if the host wired an audit logger, so the primitive costs nothing when nobody is watching.

## Decision

Every governed action is audited through **one payload-safe primitive**, `GovernanceAudit`, which emits a span
named for the action and an audit-grade structured log. Its inputs are only controlled vocabulary and
identifiers (the action, the actor, the target kind and id, the outcome). It has no parameter that could carry
a payload or a secret, so an action cannot leak its inputs through the audit. The span is zero-cost when
unobserved.

## Consequences

- The audit trail is uniform. Every governed action records the same shape, so an operator reads one trail,
  not a different log per action.
- The audit cannot leak. There is no way to pass a payload or a secret to the primitive, so a step output or a
  credential value cannot reach a log or a trace through governance auditing, upholding the disclosure boundary
  ([ADR 0013](0013-step-output-disclosure-tier.md)).
- Auditing is free when nobody listens, so it can be applied to every governed action without a cost on
  deployments that do not collect it.
- Because the primitive is shared, adding a new governed action means calling it, not inventing a new audit
  shape, which keeps the trail consistent as the surface grows.
