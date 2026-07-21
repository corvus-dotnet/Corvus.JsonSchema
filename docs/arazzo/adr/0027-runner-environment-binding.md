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
