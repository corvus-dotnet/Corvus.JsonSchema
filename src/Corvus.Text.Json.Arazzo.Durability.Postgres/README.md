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
// Once, at deploy/migration time — with a credential permitted to create tables:
await PostgresWorkflowStateStore.PrepareAsync("Host=localhost;Database=workflows;Username=ddl_admin;Password=…");

// At runtime — with a least-privileged operational credential; performs no DDL:
await using var store = await PostgresWorkflowStateStore.ConnectAsync("Host=localhost;Database=workflows;Username=app;Password=…");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex (e.g. to build a WorkflowRun or a WorkflowWorker).
```

`PrepareAsync` runs the idempotent `CREATE TABLE IF NOT EXISTS` schema (DDL — provisioning rights);
`ConnectAsync` performs no DDL, so the running app can use a role granted only `select/insert/update/delete`
on the tables. Both also have `NpgsqlDataSource` overloads, so you can hand in a data source the app owns —
for example one whose periodic password provider issues Entra ID tokens (managed identity).

Because the adapter speaks the PostgreSQL wire protocol directly, it also serves **CockroachDB, YugabyteDB,
AlloyDB, Aurora PostgreSQL, Neon, and Citus**.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.

**Encryption at rest:** managed PostgreSQL (Azure Database, Aurora, AlloyDB, …) encrypts at rest, optionally
under a customer-managed key. For encryption independent of the server, wrap this store in
`ProtectedWorkflowStateStore` (see the `Corvus.Text.Json.Arazzo.Durability` README).

## The tenant anchor store

`PostgresTenantAnchorStore` implements `ITenantAnchorStore` (ADR 0065 decision 6) for the **tenant's own**
database, never the control plane's: one row per run holding the tenant anchor record as its persisted JSON, and
one row per environment holding the tenant-attested store incarnation. A record write is one transaction that reads
the stored row under a row lock, compares it with the record the writer decided against, classifies the proposed
record with `AnchorAcceptance.Classify` against the environment's attested incarnation, and replaces it only if a
clause admits it. That is the whole of what the store enforces; it verifies no signature. The attestation is
strictly monotonic in one statement.

```csharp
// The tenant's own database, provisioned by the runner that owns it.
await PostgresTenantAnchorStore.PrepareAsync(tenantConnectionString);
await using var anchors = await PostgresTenantAnchorStore.ConnectAsync(tenantConnectionString);
await anchors.AttestIncarnationAsync("production", 1, cancellationToken);   // the environment's first attestation
var runner = new ArazzoRunnerClient(transport, keyRing: keyRing, anchors: anchors);
```

> The in-memory anchor store is the reference implementation; this backend runs the same anchor-store
> conformance suite.
