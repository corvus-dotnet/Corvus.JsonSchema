# ADR 0011. Approval is a strategy seam

Date: 2026-07-21. Status: **Accepted**. Scope: how an access-request decision is made. Builds on
[0010](0010-access-requests-ceiling-bounded.md). This records why the decision step is a replaceable strategy
behind one interface, with a built-in single-approver default and a workflow-backed alternative, rather than
a fixed approval mechanism baked into the request handler.

## Context

Organisations approve access differently. One wants a single named approver to click approve, another wants a
multi-party review, a change ticket, or a decision that runs as an ordinary workflow so it can notify, wait,
and escalate. The request handler should not commit to one of these. It should submit a request and let the
deployment decide how the decision is reached, as long as every strategy is bounded by the same ceiling
([0010](0010-access-requests-ceiling-bounded.md)).

### Grounded architectural facts

- **One interface, two implementations.** `IAccessRequestApprovalService`
  (`ControlPlane.Server/IAccessRequestApprovalService.cs`) is the strategy seam. The built-in
  `AccessRequestApprovalService` is the synchronous single-approver default. `WorkflowBackedAccessRequestApprovalService`
  makes the decision an ordinary Arazzo workflow, with the human decision enacted asynchronously.
- **Both are ceiling-bounded.** Every strategy grants through the same capped, subject-pinned, reach-pinned,
  TTL-limited path ([0010](0010-access-requests-ceiling-bounded.md)), so replacing the strategy cannot widen
  what a grant may confer.
- **The workflow-backed decision is delivered over a channel.** The approver's decision reaches the suspended
  approval run by being published to a channel, following the KYC verdict precedent, not by a bespoke
  control-plane call ([0012](0012-approval-decision-delivery.md)).

## Decision

The access-request decision is a **replaceable strategy behind `IAccessRequestApprovalService`**. The request
handler submits a request and delegates the decision to the configured strategy. A deployment runs the
built-in synchronous single-approver default, or the workflow-backed strategy where the decision is an
ordinary Arazzo workflow, or its own implementation. Every strategy grants through the ceiling of
[0010](0010-access-requests-ceiling-bounded.md), so the seam changes how a decision is reached, never what it
may grant.

## Consequences

- A deployment can adopt a richer approval process (notify, wait, escalate, multi-party) by running the
  workflow-backed strategy, without changing the request handler or the grant ceiling.
- The workflow-backed strategy is Arazzo governing Arazzo: the control plane's own approval flow is an
  editable workflow on a system runner, with the decision delivered over a channel
  ([0012](0012-approval-decision-delivery.md)).
- Because every strategy shares the ceiling, the security review of the request path is done once, at the
  ceiling, not per strategy.
- The built-in strategy returns synchronously; the workflow-backed strategy returns "decision submitted" and
  the request reaches `Approved` when the workflow completes the bounded grant
  ([0012](0012-approval-decision-delivery.md)).
