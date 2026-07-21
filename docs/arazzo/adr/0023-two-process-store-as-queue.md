# ADR 0023. Two processes sharing the store, never calling each other on the hot path

Date: 2026-07-21. Status: **Accepted**. Scope: the topology of the control plane and the runner. Builds on
[ADR 0020](0020-durability-is-opt-in-codegen.md) and [ADR 0021](0021-state-store-abstraction.md). This records
why the control plane and the execution host are separate processes that cooperate only through the shared
durability store, rather than calling each other.

## Context

Managing workflows (cataloguing, governing, querying) and executing them are different jobs with different
scaling profiles. The control plane handles administration and read-heavy queries; the runner does the CPU and
IO of executing runs. Coupling them into one process, or having them call each other synchronously, would tie
their scaling together and put run execution on the control plane's request path. The control plane also holds
elevated authority (it governs access), which is exactly what should not be executing tenant workflow code.

### Grounded architectural facts

- **The control plane never executes production runs.** It enqueues a `Pending` run and returns. Starting a
  run is `HandleStartCatalogWorkflowRunAsync` (`ControlPlane.Server/ArazzoControlPlaneCatalogHandler.cs`),
  which returns `202 Accepted`. The only in-process worker the control plane hosts is the `$draft` debug
  runner, documented as having no production analogue.
- **A separate runner claims and advances runs.** The runner is a `BackgroundService`
  (`WorkflowDispatchService`, `samples/arazzo/Corvus.Text.Json.Arazzo.Runner.Demo/Program.cs`) that claims,
  leases, and advances runs against the shared state store, driving each through `HostedWorkflowResumer` and
  the `WorkflowExecutorLoader`.
- **The store is the dispatch queue.** The two processes share the durability store
  ([ADR 0021](0021-state-store-abstraction.md)) and cooperate only through it: the control plane writes a
  `Pending` run, the runner claims it under the run lease. Neither calls the other on the hot path.

## Decision

The control plane and the runner are **separate processes that share the durability store and never call each
other on the hot path**. The control plane enqueues a run and returns `202`; the runner claims it from the
store under the lease and executes it. The store is the dispatch queue between them.

## Consequences

- The two processes scale independently. Adding runners scales execution without touching the control plane,
  and vice versa.
- The control plane never runs tenant workflow code, so its elevated authority is not exposed to that code.
  Tenant execution happens on a runner.
- Because dispatch is a store write and a lease claim, there is no synchronous call to fail between the two
  processes. A runner that is down does not fail the control plane's enqueue; the run waits in the store.
- Delivering an out-of-band signal to a run (an approver's decision) uses the transport, not a control-plane
  call, precisely because the control plane cannot reach a runner-hosted run
  ([ADR 0012](0012-approval-decision-delivery.md)).
