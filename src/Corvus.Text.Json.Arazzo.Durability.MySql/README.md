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
// Once, at deploy/migration time — with a user permitted to create tables:
await MySqlWorkflowStateStore.PrepareAsync("Server=localhost;Database=workflows;User ID=ddl_admin;Password=…");

// At runtime — with a least-privileged operational user; performs no DDL:
await using var store = await MySqlWorkflowStateStore.ConnectAsync("Server=localhost;Database=workflows;User ID=app;Password=…");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`PrepareAsync` runs the idempotent `CREATE TABLE IF NOT EXISTS` schema (provisioning rights); `ConnectAsync`
performs no DDL, so the running app can use a user granted only data access on the tables. Both also have
`MySqlDataSource` overloads, so you can hand in a data source the app owns (it must set
`UseAffectedRows=true`, which the connection-string overload does for you). Speaking the MySQL wire protocol
directly, it also serves **MariaDB and Aurora MySQL**.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.

**Encryption at rest:** managed MySQL/MariaDB (Azure, Aurora, …) encrypts at rest, optionally under a
customer-managed key. For encryption independent of the server, wrap this store in
`ProtectedWorkflowStateStore` (see the `Corvus.Text.Json.Arazzo.Durability` README).
