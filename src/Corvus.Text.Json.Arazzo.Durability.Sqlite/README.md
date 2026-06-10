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
await using var store = await SqliteWorkflowStateStore.ConnectAsync("Data Source=workflows.db");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex (e.g. to build a WorkflowRun or a WorkflowWorker).
```

`ConnectAsync` runs an idempotent `CREATE TABLE IF NOT EXISTS` schema — there is no migrations runtime.
SQLite is the deliberate exception to the prepare/connect split the other backends follow: it is an embedded
engine with no server-side privilege boundary (and an in-memory database lives only for its connection), so
`ConnectAsync` ensures the schema. A `PrepareAsync(connectionString)` is offered for symmetry to pre-create a
file database. Optimistic concurrency maps to a version column; the single-owner lease maps to a small leases
table.

> For tests and the reference behaviour, see `Corvus.Text.Json.Arazzo.Durability`'s in-memory store. Both
> run the same store-conformance suite.

**Encryption at rest:** protect the database file with OS/disk encryption (or a SQLCipher build). For
encryption independent of the file, wrap this store in `ProtectedWorkflowStateStore` (see the
`Corvus.Text.Json.Arazzo.Durability` README).
