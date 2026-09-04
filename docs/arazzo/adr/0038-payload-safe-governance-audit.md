# ADR 0038. A payload-safe governance-audit primitive

Date: 2026-07-21. Revised 2026-09-02: the audited actor is the canonical subject, every record carries the
actor's tenant and, where the action is environment-scoped, the environment, and run start, the bootstrap
grants, the approval service's policy writes and self-elevation are on the trail (P1-6 of the 2026-08-07
security audit). Status: **Accepted**. Scope: how a governed action is audited. This records why every
governed action is audited through one primitive that emits a span and an audit log carrying only controlled
vocabulary and identifiers, never a payload or a secret, and how the actor and the tenant are named.

## Context

A control plane governs access, and governance actions (grant, revoke, approve, deny, publish, delete, start)
have to leave an audit trail: who did what to which resource, in which tenant, and how it turned out. Three
things can go wrong. The audit can be inconsistent, each action logging in its own shape, so an operator cannot
rely on a uniform trail. The audit can leak: if an action logs its inputs, a step output, a credential value, or
a request payload can end up in a log or a trace, which is exactly the sensitive data the disclosure tier
([ADR 0013](0013-step-output-disclosure-tier.md)) works to protect. And the audit can misattribute: a record
naming a display name rather than the identity authorization decided on, or naming the deployment for an action
nobody authenticated, is a trail that cannot be joined to a grant, a request, or a tenant.

### Grounded architectural facts

- **One primitive audits every governed action.** `GovernanceAudit.Mutation(logger, action, actor,
  targetKind, targetId, outcome, environment)` (`Durability/Security/GovernanceAudit.cs`) emits a span named
  for the action on `ArazzoTelemetry.ActivitySource` plus an audit-grade structured log, so who changed what,
  where, and the outcome, are recorded uniformly. It is public in the durability library so the deployment
  bootstrap and the domain services audit through the same primitive as the handlers.
- **Its inputs are controlled vocabulary and identifiers only.** The action name, actor, target kind, target
  id, outcome, tenant and environment are all a stable controlled vocabulary or an identifier, never a workflow
  payload or a secret. A caller cannot route a step output or a credential value through it, because there is no
  parameter that takes one.
- **The actor is the canonical subject.** `AuditSubject` carries the subject and its owner group. The subject
  is resolved from the deployment's configured subject claim (`sub` by default), the identity a grant keys on,
  then the authorized party or client id for a client-credentials token that names no subject, then the
  authentication name, and is `anonymous` when the request carries no principal. `ControlPlaneAccess` resolves
  it once per request, with the owner group read from the caller's own stamped internal tags under the
  owner-group key (`sys:tenant`), so every handler records the same identity authorization used. The OIDC
  `name` claim is a display label (`PrincipalDisplayName`, the request's `requesterLabel`) and never the
  audited actor.
- **Tenant and environment are first-class.** The span and the log carry `corvus.arazzo.tenant` (the actor's
  owner group, when the deployment stamps one) and `corvus.arazzo.environment` (for an action scoped to a
  deployment environment: a run start, a schedule run, an environment or runner mutation), and the decisions
  counter is dimensioned by them alongside action and outcome.
- **It is zero-cost when unobserved.** The span is zero-cost when no listener is attached, and the log is
  emitted only if the host wired an audit logger, so the primitive costs nothing when nobody is watching.
- **A refusal is audited too.** A refused governed action is audited with its refusal outcome (for example
  `refused-own-request`, `refused-self-elevation`, `refused-reserved-name`), because a security control firing
  is exactly what an audit wants to record.

## Decision

Every governed action is audited through **one payload-safe primitive**, `GovernanceAudit`, which emits a span
named for the action and an audit-grade structured log. Its inputs are only controlled vocabulary and
identifiers: the action, the actor as its canonical subject and tenant, the target kind and id, the outcome,
and the environment where the action is scoped to one. It has no parameter that could carry a payload or a
secret, so an action cannot leak its inputs through the audit. The span is zero-cost when unobserved.

The trail is complete for the actions that confer or exercise access:

- **Run start** is audited (`run.start`, outcome `started`, or `reused` when an idempotent start returned the
  existing run) with the starting actor and the environment.
- **The bootstrap's founding grants** are audited as the `bootstrap` actor (`security-binding.create`,
  `security-rule.create`), so the read-all shell and the genesis administrator are on the trail.
- **The approval service's policy writes** are audited with the deciding actor: the per-workflow reach rule it
  ensures (`security-rule.create`), the grant or eligibility binding it writes (`security-binding.create`,
  outcome `granted`, `eligible`, or `self-elevated`), and the binding it deletes on revoke
  (`security-binding.delete`, `revoked`). The API layer's decision audit and the service's policy-write audit are
  two records of one act, joined by the actor and the request.
- **Self-elevation is distinguished** in the outcome vocabulary: a submit that the requester's eligibility
  auto-approves audits as `self-elevated`, not `submitted`.

## Consequences

- The audit trail is uniform and attributable. Every governed action records the same shape, so an operator
  reads one trail, and every record names the identity a grant or a request would name, in its tenant.
- The audit cannot leak. There is no way to pass a payload or a secret to the primitive, so a step output or a
  credential value cannot reach a log or a trace through governance auditing, upholding the disclosure boundary
  ([ADR 0013](0013-step-output-disclosure-tier.md)).
- Auditing is free when nobody listens, so it can be applied to every governed action without a cost on
  deployments that do not collect it.
- Because the primitive is shared, adding a new governed action means calling it, not inventing a new audit
  shape, which keeps the trail consistent as the surface grows.
- Governance decisions are observable as a rate per tenant and environment, not only as individual spans, so a
  deployment can alert on a spike in denials or refusals in one tenant without instrumenting each action.
- The trail is still best-effort observability, not a durable store. The span rides a sampled activity source and
  the log is an information-level `ILogger` call. A durable, append-only, tamper-evident sink is the separate
  decision the audit's GAP-6 asks for.
