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

## Provisioning vs operation (least privilege)

Each backend separates the **privileged, one-time provisioning** of its schema from the
**least-privileged operational** open:

- `PrepareAsync(...)` performs the DDL (create tables/indexes/containers/buckets). Run it once at
  deploy/migration time with an elevated credential.
- `ConnectAsync(...)` opens the store for operation and performs **no** DDL, so the running app can
  use a credential granted only data access. Most backends also offer a `ConnectAsync`/`PrepareAsync`
  overload taking the SDK's credential-bearing client (e.g. a managed-identity `BlobServiceClient`,
  `CosmosClient`, `NpgsqlDataSource`) so no secret need live in a connection string.

(SQLite is the exception — an embedded file with no privilege boundary — so its `ConnectAsync` still
ensures the schema.)

## Encryption at rest

**Layer 1 — storage-native (the baseline).** Every backing service encrypts at rest in its managed
form — Azure Storage and Cosmos always; Azure SQL / PostgreSQL / MySQL via TDE; MongoDB Atlas, Redis
Enterprise/Azure Cache, etc. — optionally under a customer-managed key (CMK) in a Key Vault/KMS. This
needs no code: it is a deployment/configuration choice and is the right answer for most workloads.

**Layer 2 — application-level (zero-trust of the store / regulatory / defence-in-depth).** When the
checkpoint must be unreadable even to the backend operator, wrap any store in
`ProtectedWorkflowStateStore`, which encrypts the checkpoint **before** it reaches the backend and
decrypts it on read via an `ICheckpointProtector`. Because every backend stores the checkpoint as an
opaque blob, one decorator works for all of them — the store-conformance suite passes through it
unchanged.

```csharp
// key from a KMS / Key Vault; 16/24/32 bytes
var protector = new AesGcmCheckpointProtector(key);
await using var store = new ProtectedWorkflowStateStore(
    await CosmosWorkflowStateStore.ConnectAsync(client), protector);
```

`AesGcmCheckpointProtector` uses AES-GCM (authenticated) under a single symmetric key and binds the ciphertext
to the run id, so a checkpoint cannot be tampered with or moved between runs (decrypt fails closed). Only the
checkpoint **payload** is encrypted; the projected index fields (status, workflow id, due time, awaiting
channel/correlation, error type) stay in the clear so the backend can still serve the wait/visibility queries —
if a correlation id is itself sensitive, index a deterministic hash of it.

For managed key custody and rotation, use a key-management service via **envelope encryption** — a fresh
per-checkpoint data key wrapped by a KMS/Key Vault key. `EnvelopeCheckpointProtector` is the base that does the
AES-GCM and framing; the satellite packages supply the wrap/unwrap:

- `Corvus.Text.Json.Arazzo.Durability.KeyVault` — `KeyVaultCheckpointProtector` (Azure Key Vault).
- `Corvus.Text.Json.Arazzo.Durability.Kms` — `KmsCheckpointProtector` (AWS KMS).

Implement `EnvelopeCheckpointProtector` (or `ICheckpointProtector`) directly for any other KMS.

## Control plane (run management)

`WorkflowManagementClient` (`IWorkflowManagementClient`) is the operator surface over a store (plan §11) — the
workflow-level analogue of dead-letter inspection and redelivery:

```csharp
var management = new WorkflowManagementClient(store, owner: "ops", resumer);

await management.ListAsync(new WorkflowQuery(WorkflowRunStatus.Faulted), ct);   // visibility
await management.GetAsync(runId, ct);                                           // status + fault detail
await management.ResumeAsync(runId, ResumeOptions.RetryFaultedStep, ct);        // retry a faulted run
await management.CancelAsync(runId, "operator abandoned", ct);                  // mark cancelled
await management.PurgeAsync(new WorkflowPurgeQuery(cutoff), ct);                // reap old terminal runs
```

`ListAsync` uses the same `IWorkflowWaitIndex` Tier 2 uses for wakeups. Every control action takes a
single-owner lease and writes under optimistic concurrency, so operators can't conflict with each other or with
a worker. `ResumeAsync` re-enters the run through the host-supplied `WorkflowResumer` (the same adapter a
`WorkflowWorker` uses) at its last checkpoint — the faulted step — and the generated executor clears the fault
on its next checkpoint. (Rewind/skip/state-patch resume and a CLI surface are planned; see plan §11.)

## Related Packages

- `Corvus.Text.Json.Arazzo` — workflow execution runtime (hosts the `IWorkflowRun` seam)
- `Corvus.Text.Json.Arazzo.CodeGeneration` — workflow executor code generator (durable mode)
- `Corvus.Text.Json.Arazzo.Testing` — mock transport and conformance harness
