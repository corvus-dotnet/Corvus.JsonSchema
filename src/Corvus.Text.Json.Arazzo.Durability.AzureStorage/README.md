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
