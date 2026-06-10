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
await using var store = await AzureStorageWorkflowStateStore.CreateAsync("<storage connection string>");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`CreateAsync` creates the blob container and tables if they do not exist. Works against Azure Storage and the
Azurite emulator.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.
