# ADR 0010. Access requests are ceiling-bounded and subject-pinned

Date: 2026-07-21. Status: **Accepted**. Scope: what an approved access request may grant. Builds on
[0002](0002-grant-verbs-are-reach-not-scopes.md) and [0009](0009-eligible-versus-active-self-elevation.md).
This records why an approval can only ever grant a narrow, capped slice of access, so the request path can
never become a way to grant arbitrary reach or scope.

## Context

An access request lets a person ask for access they do not have, and an approver grant it. The danger is that
this path becomes a general-purpose grant mechanism. If an approval could write any binding, the request flow
would be a way to hand out security-write, purge, administration, or cross-tenant reach, which is exactly what
the two-plane model and the shell are meant to prevent. The request path needs a hard ceiling that holds no
matter what is requested or who approves.

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

## Decision

An approved access request grants a **ceiling-bounded, subject-pinned** slice of access, never an arbitrary
binding.

- **Scope ceiling.** At most the requested scopes intersected with a run-access allowlist (`runs:read`,
  `runs:write`). Security, purge, administration, and escalation are out of reach of the request path.
- **Subject-pinned.** The grant's subject is fixed to the requester. A request cannot grant access to someone
  else.
- **Reach-pinned.** The grant's reach is fixed to the target workflow.
- **TTL-capped.** The grant expires at or before the deployment maximum.

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
