# Corvus.Text.Json.Arazzo.Durability.AzureStorage

An [Azure Storage](https://learn.microsoft.com/azure/storage/) backend for
[Arazzo](https://github.com/OAI/Arazzo-Specification) workflow durability — the Azure-native option.

`AzureStorageWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under optimistic
concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer / awaiting-message
wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`:

- the opaque checkpoint is a **block blob** per run, and the blob's **ETag is the optimistic-concurrency
  token** (conditional `If-None-Match` create / `If-Match` update);
- the projected index and the single-owner lease are **Table storage** entities (the lease guarded by the
  entity ETag).

```csharp
// Once, at deploy/migration time — with a credential permitted to create the container and tables:
await AzureStorageWorkflowStateStore.PrepareAsync("<admin storage connection string>");

// At runtime — with a least-privileged data-plane managed identity; creates nothing:
var blob = new BlobServiceClient(new Uri("https://acct.blob.core.windows.net"), new DefaultAzureCredential());
var table = new TableServiceClient(new Uri("https://acct.table.core.windows.net"), new DefaultAzureCredential());
await using var store = await AzureStorageWorkflowStateStore.ConnectAsync(blob, table);
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`PrepareAsync` creates the blob container and tables (a broader right than runtime data access); `ConnectAsync`
creates nothing, so the running app can use a managed identity granted only the blob and table **data** roles —
no account key in a connection string. Connection-string overloads of both are provided for local/dev and the
Azurite emulator.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.

**Encryption at rest:** Azure Storage encrypts blobs and tables at rest by default (customer-managed key
optional). For encryption the storage operator cannot read, wrap this store in `ProtectedWorkflowStateStore`
(see the `Corvus.Text.Json.Arazzo.Durability` README).

## The audit sink

`AzureBlobAuditSink` is the deployment's audit evidence store ([ADR 0069](../../docs/arazzo/adr/0069-audit-as-evidence-append-only-chained-signed-sink.md)): each audit chain is one append blob, `{writerId}/{chainId}.jsonl`, and each record is one appended block. A control plane instance that starts finds its own last chain by listing its prefix, and continues it. Give it a container in a storage account **other than** the one the operational stores use, so that whoever holds the operational data does not hold its audit.

```csharp
BlobContainerClient auditContainer = new BlobServiceClient(auditAccountUri, credential).GetBlobContainerClient("arazzo-audit");
AzureBlobAuditSink sink = await AzureBlobAuditSink.ConnectAsync(auditContainer);
var auditor = new GovernanceAuditor(auditLogger, sink, headSigner: auditHeadSigner, writerId: instanceName);
await auditor.StartAsync();
```

**The container must be immutable, and the sink checks.** `ConnectAsync` refuses a container that has neither an immutability policy nor a legal hold. Create the container with a time-based retention policy and **allow protected append writes** on it: without that setting the policy refuses the appends themselves. The retention period is the audit's retention. The platform adds none of its own.

For development and for the Azurite emulator, which has no immutability policies, pass `allowMutableContainer: true`. What such a container keeps can be rewritten, so it is not evidence.

A chain is never reopened: the blob is created with `If-None-Match: *`. The chain writer opens a new chain at 40,000 records, so a chain stays under an append blob's limit of 50,000 blocks. To verify a chain, download its blob and give it to `arazzo-runs audit verify`, which reads the bytes directly and not through the control plane.