# ADR 0021. The state-store abstraction: core state store plus optional wait index

Date: 2026-07-21. Status: **Accepted**. Scope: what a durability backend has to implement, and what is
optional. Builds on [ADR 0019](0019-products-are-the-checkpoint.md). This records why durability is split into
a required core state store and an optional wait index, so a backend can offer crash-durability without
implementing suspension.

## Context

Durability has two levels of ambition. The first is surviving a crash: a run that was mid-flight when the
process died can be picked up and continued. The second is suspension: a run can pause waiting for a timer to
fire or a correlated message to arrive, and be woken when it does. The second needs more from a backend than
the first (an index of what each suspended run is waiting for), and not every backend can or should provide
it. Requiring both from every backend would exclude simple stores from offering the first level.

### Grounded architectural facts

- **The core store persists the checkpoint.** `IWorkflowStateStore`
  (`src/Corvus.Text.Json.Arazzo.Durability/IWorkflowStateStore.cs`) is the required seam: persist and load a
  run's checkpoint document ([ADR 0019](0019-products-are-the-checkpoint.md)) under optimistic concurrency,
  with the lease that lets a runner claim a run.
- **The wait index is a separate, optional seam.** `IWorkflowWaitIndex`
  (`src/Corvus.Text.Json.Arazzo.Durability/IWorkflowWaitIndex.cs`) is the seam for suspension: it indexes what
  each suspended run is waiting for (a timer, a correlated message) so a wake can find it. A backend that does
  not implement it offers crash-durability but not suspension.
- **The plan states the tier split.** The engine plan (`docs/ArazzoWorkflowEnginePlan.md` §10) frames the two
  tiers: Tier 1 is crash-durability from the core store; Tier 2 is suspension, which adds the wait index.

## Decision

Durability is **two seams, one required and one optional**. `IWorkflowStateStore` is required: it persists and
loads the checkpoint under optimistic concurrency and carries the run lease, which is enough for
crash-durability (Tier 1). `IWorkflowWaitIndex` is optional: it indexes what suspended runs await, which adds
suspension (Tier 2). A backend implements the core store to be durable, and adds the wait index to support
suspending runs.

## Consequences

- A simple backend can offer crash-durability by implementing only the core store, without the machinery
  suspension needs.
- Suspension is available only where the wait index is implemented, so a workflow that suspends (a durable
  timer, a correlated receive) requires a Tier-2 backend, and the requirement is explicit in which seams a
  backend provides.
- Both seams operate on the small per-run checkpoint document ([ADR 0019](0019-products-are-the-checkpoint.md))
  under optimistic concurrency, so a backend implements them as ordinary conditional writes on one document,
  not a bespoke durable-execution engine.
- The lease on the core store is what lets a separate runner claim and advance a run, which the runner and
  execution-host domain builds on.
