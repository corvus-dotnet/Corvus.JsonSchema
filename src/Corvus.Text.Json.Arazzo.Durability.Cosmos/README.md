# Corvus.Text.Json.Arazzo.Durability.Cosmos

An [Azure Cosmos DB](https://learn.microsoft.com/azure/cosmos-db/) backend for
[Arazzo](https://github.com/OAI/Arazzo-Specification) workflow durability.

`CosmosWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under optimistic
concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer / awaiting-message
wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`:

- each run is a **document** in the `workflow_runs` container holding the opaque checkpoint (a base64 byte
  array) and the projected index fields, with the run id as both the document id and the partition key, so
  every checkpoint operation is a single-partition point operation;
- optimistic concurrency uses the document's native **`_etag`** (a conditional `If-Match` replace);
- the single-owner lease is a document in a `workflow_leases` container, guarded by the same ETag mechanism.

```csharp
await using var store = await CosmosWorkflowStateStore.CreateAsync("<cosmos connection string>");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`CreateAsync` creates the database and containers if they do not exist. There is also an overload taking a
caller-configured `CosmosClient` (the caller retains ownership), which is what the emulator-based tests use.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.
