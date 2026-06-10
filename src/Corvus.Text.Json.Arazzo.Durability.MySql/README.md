# Corvus.Text.Json.Arazzo.Durability.MySql

A [MySQL](https://www.mysql.com/) backend for [Arazzo](https://github.com/OAI/Arazzo-Specification)
workflow durability.

`MySqlWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under optimistic
concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer / awaiting-message
wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`, over a direct
[MySqlConnector](https://mysqlconnector.net/) connection — no ORM, no migrations runtime. The checkpoint is
stored as an opaque `LONGBLOB` alongside a handful of indexed projection columns; optimistic concurrency
maps to a version column and the single-owner lease to a small leases table.

```csharp
await using var store = await MySqlWorkflowStateStore.CreateAsync("Server=localhost;Database=workflows;User ID=root;Password=…");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`CreateAsync` runs an idempotent `CREATE TABLE IF NOT EXISTS` schema. Speaking the MySQL wire protocol
directly, it also serves **MariaDB and Aurora MySQL**.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.
