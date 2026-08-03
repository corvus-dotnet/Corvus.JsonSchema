# ADR 0027. Runner-to-environment binding, with the revocation fence in the store

Date: 2026-07-21. Status: **Accepted**. Scope: which runner may execute which runs. Builds on
[ADR 0023](0023-two-process-store-as-queue.md). This records why runs are pinned to an environment, why a
runner must be authorized for an environment before it may serve it, and why revocation is enforced in the
store rather than trusted to the runner.

## Context

A deployment has environments (development, staging, production), and a run belongs to one of them. Runners
carry credentials and reach appropriate to an environment, so a run must execute on a runner authorized for
its environment, never on an arbitrary runner. Authorization also has to be revocable: if a runner is
compromised or retired, it must stop being able to serve runs, and that must not depend on the runner
cooperating, because a compromised runner will not.

### Grounded architectural facts

- **Runs are environment-pinned; runners claim only their environment.** The design (execution-host §5.5)
  fixes runs as environment-pinned at creation, and a runner claims only its exact environment.
- **A runner registry tracks liveness.** `IRunnerRegistry` (`src/Corvus.Text.Json.Arazzo.Durability/`) holds
  the registered runners and their heartbeat health, so a trigger gates on a live host.
- **Authorization is a lifecycle.** `EnvironmentRunnerAuthorization`
  (`src/Corvus.Text.Json.Arazzo.Durability/RunnerAuthorization/`) moves a runner through Pending, Authorized,
  and Quarantined or Revoked for an environment, backed by
  `InMemoryEnvironmentRunnerAuthorizationStore` and its persistent siblings.
- **The revocation fence is in the store.** A revoked runner cannot claim a run, and that is enforced at the
  store (`IWorkflowLeaseAdministration`, the claim path), not by asking the runner to stand down. A runner
  registers as a machine principal, so the control plane binds the trusted principal from the runner's token
  rather than trusting a self-asserted identity (this is the machine-principal registration, #881).

## Decision

A run is **pinned to an environment**, and a runner must be **authorized for that environment** before it may
claim the run. Authorization is a lifecycle (Pending, Authorized, Quarantined, Revoked). Revocation is
enforced in the **store**: a revoked runner's claim is refused at the lease path, so a runner losing its
authorization cannot serve runs regardless of whether it cooperates.

**Stopping a runner and un-saying a decision are different operations, and only one of them removes the
record.** Implementing [ADR 0065](0065-control-plane-owns-store-runners-encrypt-payload.md) decision 2's
pre-authorization made this distinction load-bearing, because it introduced a decision that can be wrong in a
way no later decision can correct.

- **Revoke** is how a runner that has served is stopped. The record survives with status `Revoked`, so the
  decision stays auditable, and the store expires the leases the runner holds so its in-flight work is fenced
  at once. Nothing about the runner's history is lost.
- **Withdraw** removes the record entirely, and exists for exactly one situation: a pre-authorization that
  named the wrong machine principal. The principal binds when the record is created and never moves — a
  registration presenting a different one is refused — so without removal an administrator's typo would make
  that runner id permanently unusable by the runner it was meant for. Withdrawal is refused once a runner has
  registered for the environment under that id.

The asymmetry is deliberate. Withdrawal is the weaker operation in every respect that matters: it erases the
audit trail rather than adding to it, and it fences nothing, because there is nothing to fence. Allowing it to
stand in for revocation would let an administrator stop a runner in a way that leaves no evidence the runner
was ever authorized, and would leave that runner's leases live while its authorization vanished.

## Consequences

- A run executes only on a runner appropriate to its environment, so production runs cannot land on a
  development runner or the reverse.
- Revoking a runner takes effect immediately and unconditionally, because the fence is the store refusing the
  claim, not the runner choosing to stop. A compromised runner cannot ignore its revocation.
- A runner's identity is the trusted principal from its token, established at registration
  ([ADR 0023](0023-two-process-store-as-queue.md), #881), so a runner cannot claim an environment by asserting
  an identity it does not hold.
- Because runs are environment-pinned, resume and cancel carry the environment through the checkpoint, so a
  resumed run stays on an authorized runner for its environment.
- A pre-authorization is correctable. That matters because binding the expected principal is what stops a
  runner id being squatted, and a control that cannot be undone by the administrator who set it is one they
  will avoid using.
- The record of a runner that has served is not erasable through the governance API. Revocation is the only
  way to stop such a runner, so "this runner was authorized and then stopped" always has an audit trail, and
  an operator reading the history of an environment cannot be shown a runner that appears never to have
  existed.
- Every backend implements the removal, since the authorization store is the fence's home and a deployment
  whose store could not remove a record would be one where the typo is still permanent.
