# Corvus.Text.Json.Arazzo.Durability.Sqlite

A [SQLite](https://www.sqlite.org/) backend for [Arazzo](https://github.com/OAI/Arazzo-Specification)
workflow durability — a single-file, zero-setup store for local-development and embedded single-node
durable runs.

`SqliteWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under optimistic
concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer / awaiting-message
wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`. The checkpoint is
stored as an opaque blob alongside a handful of indexed projection columns; the store never parses the
checkpoint.

```csharp
await using var store = await SqliteWorkflowStateStore.CreateAsync("Data Source=workflows.db");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex (e.g. to build a WorkflowRun or a WorkflowWorker).
```

`CreateAsync` runs an idempotent `CREATE TABLE IF NOT EXISTS` schema — there is no migrations runtime.
Optimistic concurrency maps to a version column; the single-owner lease maps to a small leases table.

> For tests and the reference behaviour, see `Corvus.Text.Json.Arazzo.Durability`'s in-memory store. Both
> run the same store-conformance suite.
