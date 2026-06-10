# Corvus.Text.Json.Arazzo.Durability.SqlServer

A [SQL Server](https://www.microsoft.com/sql-server) backend for
[Arazzo](https://github.com/OAI/Arazzo-Specification) workflow durability.

`SqlServerWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under optimistic
concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer / awaiting-message
wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`, over a direct
[Microsoft.Data.SqlClient](https://github.com/dotnet/SqlClient) connection — no ORM, no migrations runtime.
The checkpoint is stored as an opaque `VARBINARY(MAX)` blob alongside a handful of indexed projection
columns; optimistic concurrency maps to a version column and the single-owner lease to a small leases table
(a race-safe `MERGE`).

```csharp
// Once, at deploy/migration time — with a login permitted to create tables:
await SqlServerWorkflowStateStore.PrepareAsync("Server=localhost;Database=workflows;User Id=ddl_admin;Password=…;TrustServerCertificate=true");

// At runtime — with a least-privileged operational login; performs no DDL:
await using var store = await SqlServerWorkflowStateStore.ConnectAsync("Server=localhost;Database=workflows;Authentication=Active Directory Managed Identity");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`PrepareAsync` runs the idempotent schema (`IF OBJECT_ID(...) IS NULL`, provisioning rights); `ConnectAsync`
performs no DDL, so the running app can use a login granted only data access (or an Entra/managed-identity
connection string, as above). The same driver and wire protocol cover **SQL Server, Azure SQL Database and
Azure SQL Managed Instance** — just a connection string.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.
