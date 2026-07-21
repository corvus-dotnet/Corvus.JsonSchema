# ADR 0009. Eligible versus active self-elevation, and the independent-decision rule

Date: 2026-07-21. Status: **Accepted**. Scope: standing eligibility versus a live grant, and who may decide a
request. Builds on [0002](0002-grant-verbs-are-reach-not-scopes.md). This records why an eligibility confers
nothing until it is activated, and why a request is never decided by its own requester.

## Context

Privileged access should not be standing. A person who might need to purge runs next month should not carry
purge reach all month waiting for the need. The model has to distinguish a right a person holds now from a
right a person is entitled to activate when they need it, so the second grants nothing at rest. This is the
just-in-time, privileged-identity-management idea.

Separately, a decision on a request must not be made by the person who raised it. An administrator who could
approve their own access request would turn a request into a self-grant, which defeats the point of asking.

### Grounded architectural facts

- **An eligibleOnly binding confers nothing active.** The resolver skips it:
  `PersistentRowSecurityPolicy` excludes `binding.EligibleOnlyValue` bindings when it compiles a principal's
  reach (`ControlPlane.Server/PersistentRowSecurityPolicy.cs:113`). An eligibility sits in the store
  contributing no live reach and no live scope.
- **Self-elevation is claims eligibility unioned with stored eligibility.** On submit,
  `AccessRequestApprovalService.SubmitAsync` auto-approves when the requester is eligible, where eligibility
  is a deployment-supplied claims predicate unioned with a stored eligibleOnly binding for this subject,
  workflow, and scope (`ControlPlane.Server/AccessRequestApprovalService.cs`). Approving as eligible writes an
  eligibleOnly binding (`WriteBindingAsync(..., eligibleOnly: true)`), so the requester may thereafter
  self-elevate that access rather than carry it live.
- **The independent-decision rule is enforced at the touchpoint.** A request is never decided by its own
  requester, even an administrator: the access-request and availability-request handlers refuse it, and a
  denial of your own request is treated as a withdrawal wearing a decision's clothes
  (`ControlPlane.Server/ArazzoControlPlaneAvailabilityRequestsHandler.cs:274`, and the access-request handler;
  named in `GovernanceAudit.cs`).

## Decision

Eligibility and active grant are distinct.

- An **eligibleOnly** binding is standing eligibility. It confers nothing at rest, because the reach resolver
  excludes it. It records that a subject may activate a specific access, not that they hold it.
- **Self-elevation** activates an eligibility just in time. On submitting a request the requester is
  auto-approved when they are eligible, where eligibility is the claims predicate unioned with a stored
  eligibleOnly binding. There is no ambient privilege: a person holds the access only after activating it.
- The **independent-decision rule** forbids a requester from deciding their own request, even an
  administrator. The decision touchpoints refuse it.

## Consequences

- Privileged reach is not standing. A person who is eligible for purge holds no purge reach until they
  activate it, so a compromised session cannot use a right the person never activated.
- Eligibility and grant are authored the same way (a binding) and differ only in the `eligibleOnly` flag,
  which the resolver reads, so the two travel one code path with one branch, not two mechanisms
  ([0002](0002-grant-verbs-are-reach-not-scopes.md)).
- The approver inbox only ever shows requests the approver did not raise, because the independent-decision
  rule removes a caller's own requests from the set they can act on.
- Approving as eligible and approving live are the two decision outcomes, one writing an eligibleOnly binding
  and the other a live one, both bounded by the request ceiling
  ([0010](0010-access-requests-ceiling-bounded.md)).
