# Corvus.Text.Json.Arazzo.Durability

Durability — checkpoint and resume — for [Arazzo](https://github.com/OAI/Arazzo-Specification)
workflow execution.

A code-generated *durable* executor only ever constructs the genuine products (each step's
`outputs` and the workflow `outputs`), so a checkpoint is almost free: the run serialises JSON
that already exists. This package provides the seam and the reference store that make that work:

- `IWorkflowStateStore` — the pluggable, backend-agnostic checkpoint store (key/value by run id,
  optimistic concurrency via an ETag, and a single-owner lease).
- `WorkflowRun` — the per-run `IWorkflowRun` implementation the generated durable executor touches;
  it owns checkpoint serialization and the store write.
- `WorkflowCheckpointSerializer` — turns the run's products into the checkpoint document and back.
- `InMemoryWorkflowStateStore` — the in-memory reference store, for tests and single-process runs.

> **Tier 1 (crash recovery).** This package ships the checkpoint-after-step mechanism that lets a
> crashed run resume from its last completed step. Long-running suspension (Tier 2 — durable timers
> and correlated-message wakeups, the wait index, and the worker) builds on the same seam. See
> `docs/ArazzoWorkflowEnginePlan.md` §9–§11.

Out-of-process backends (Postgres, SQL Server, Azure Storage, Cosmos, Redis, NATS JetStream, …)
ship as separate `Corvus.Text.Json.Arazzo.Durability.*` packages that implement `IWorkflowStateStore`
against this same conformance contract.

## Related Packages

- `Corvus.Text.Json.Arazzo` — workflow execution runtime (hosts the `IWorkflowRun` seam)
- `Corvus.Text.Json.Arazzo.CodeGeneration` — workflow executor code generator (durable mode)
- `Corvus.Text.Json.Arazzo.Testing` — mock transport and conformance harness
