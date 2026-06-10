# Corvus.Text.Json.Arazzo.Durability.Postgres

A [PostgreSQL](https://www.postgresql.org/) backend for [Arazzo](https://github.com/OAI/Arazzo-Specification)
workflow durability — the relational default.

`PostgresWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under optimistic
concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer / awaiting-message
wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`, over a direct
[Npgsql](https://www.npgsql.org/) connection — no ORM, no migrations runtime. The checkpoint is stored as an
opaque `bytea` blob alongside a handful of indexed projection columns; optimistic concurrency maps to a
version column and the single-owner lease to a small leases table.

```csharp
await using var store = await PostgresWorkflowStateStore.CreateAsync("Host=localhost;Database=workflows;Username=postgres;Password=…");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex (e.g. to build a WorkflowRun or a WorkflowWorker).
```

`CreateAsync` runs an idempotent `CREATE TABLE IF NOT EXISTS` schema.

Because the adapter speaks the PostgreSQL wire protocol directly, it also serves **CockroachDB, YugabyteDB,
AlloyDB, Aurora PostgreSQL, Neon, and Citus**.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.
