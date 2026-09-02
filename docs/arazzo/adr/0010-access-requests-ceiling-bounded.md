# ADR 0010. Access requests are ceiling-bounded and subject-pinned

Date: 2026-07-21. Revised 2026-09-02: the per-workflow reach rule is reserved and expression-checked (P1-5 of the
2026-08-07 security audit). Status: **Accepted**. Scope: what an approved access request may grant. Builds on
[0002](0002-grant-verbs-are-reach-not-scopes.md) and [0009](0009-eligible-versus-active-self-elevation.md).
This records why an approval can only ever grant a narrow, capped slice of access, so the request path can
never become a way to grant arbitrary reach or scope.

## Context

An access request lets a person ask for access they do not have, and an approver grant it. The danger is that
this path becomes a general-purpose grant mechanism. If an approval could write any binding, the request flow
would be a way to hand out security-write, purge, administration, or cross-tenant reach, which is exactly what
the two-plane model and the shell are meant to prevent. The request path needs a hard ceiling that holds no
matter what is requested or who approves.

The reach half of the ceiling is a named rule, `workflow-access:<workflowId>`, written by the approval service
on the first grant for a workflow and reused thereafter. A name is only a pin if nobody else can write under
it and what it names cannot drift. Rule names are one deployment-global namespace, and any tenant's
`security:write` holder can author rules, so the name has to be reserved and the expression checked.

### Grounded architectural facts

- **The platform cap.** `AccessRequestApprovalService` (`ControlPlane.Server/AccessRequestApprovalService.cs`,
  design §16.5) grants at most the requested scopes intersected with the deployment allowlist, which is run
  access only (`runs:read` / `runs:write`). The subject is fixed to the requester, the reach is fixed to the
  target workflow, and the expiry is capped at the deployment maximum TTL (default eight hours). Security,
  purge, administration, and escalation are never grantable through a request.
- **The cap applies unconditionally.** `GrantAndDecideAsync` applies `CapScopes` (run access only), pins the
  subject to the requester, pins the reach to the workflow, and caps the TTL, regardless of the approving
  path. So even if the narrow system capability that enacts a grant ever leaked, the worst case is an
  auto-grant of run access to the requester on their own workflow.
- **A narrow system capability enacts the grant.** The bounded grant runs under `accessRequests:grant`
  (`ControlPlaneScopes`), a capability that can only enact what a decision authorised, distinct from
  `security:write` which authors arbitrary bindings.
- **The per-workflow reach rule is reserved and checked.** `WorkflowReachRule` (the durability library) is the single
  definition of the rule's name and expression (`sys:workflow == '<workflowId>'`). The security authoring API
  refuses to create or update a rule under the `workflow-access:` prefix (403 `reserved-rule-name`, audited
  `refused-reserved-name`), checked before the reach gate because the namespace is documented and refusing it
  discloses nothing. `EnsureWorkflowRuleAsync` reuses an existing rule only after checking its expression is
  exactly the workflow's, and refuses the grant on a mismatch before any binding is written.

## Decision

An approved access request grants a **ceiling-bounded, subject-pinned** slice of access, never an arbitrary
binding.

- **Scope ceiling.** At most the requested scopes intersected with a run-access allowlist (`runs:read`,
  `runs:write`). Security, purge, administration, and escalation are out of reach of the request path.
- **Subject-pinned.** The grant's subject is fixed to the requester. A request cannot grant access to someone
  else.
- **Reach-pinned.** The grant's reach is fixed to the target workflow, through the per-workflow reach rule.
- **TTL-capped.** The grant expires at or before the deployment maximum.
- **Reach-rule integrity.** The `workflow-access:` namespace is reserved to the approval service, and an
  existing rule is reused only when its expression is exactly the workflow's. Delete stays under the ordinary
  write-reach gate: the rule is system-owned, so only an unrestricted-write caller can remove it, and a grant
  naming a missing rule contributes nothing. A mismatched rule is never repaired silently. It is evidence, and
  the request stays pending.

The ceiling is enforced in the grant path itself, so it holds regardless of what was requested, who approved,
or which approval strategy ([0011](0011-approval-is-a-strategy-seam.md)) ran.

## Consequences

- The request path cannot escalate. It grants time-boxed run access to the requester on one workflow and
  nothing more, so it is safe to expose broadly.
- Arbitrary bindings still require `security:write` and go through the grant-authoring surface, not the
  request path. This is the direct-grant versus request-only split
  ([0014](0014-direct-grant-versus-request-only.md)).
- The system capability that enacts a grant (`accessRequests:grant`) is deliberately narrow, so the blast
  radius of a leaked system credential is bounded by the same ceiling.
- Because a grant is subject-pinned to the requester, and a requester cannot decide their own request
  ([0009](0009-eligible-versus-active-self-elevation.md)), a person can neither grant themselves access
  directly nor approve their own request into one.
- A squatted rule cannot widen the ceiling: neither one squatted under the reserved name ahead of the first
  approval, nor one with the right expression widened later by its squatter, since the API refuses both the
  create and the update, and the service checks the expression it finds.
