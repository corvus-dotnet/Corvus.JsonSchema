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
// Once, at deploy/migration time — with the account key (a management-plane credential):
await CosmosWorkflowStateStore.PrepareAsync("<cosmos account connection string>");

// At runtime — with a least-privileged data-plane managed identity; creates nothing:
var client = new CosmosClient("https://acct.documents.azure.com", new DefaultAzureCredential(), CosmosWorkflowStateStore.CreateClientOptions());
await using var store = await CosmosWorkflowStateStore.ConnectAsync(client);
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

Creating a database/container is a Cosmos **management-plane** operation — the data-plane RBAC roles (e.g.
*Cosmos DB Built-in Data Contributor*) cannot do it — so `PrepareAsync` needs the account key or a
control-plane role, while `ConnectAsync` runs entirely on the data plane. Both have connection-string and
`CosmosClient` overloads (the latter for managed identity and for the emulator-based tests).

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.
