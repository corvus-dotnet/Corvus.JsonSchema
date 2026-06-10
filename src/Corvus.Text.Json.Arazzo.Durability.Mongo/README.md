# Corvus.Text.Json.Arazzo.Durability.Mongo

A [MongoDB](https://www.mongodb.com/) backend for [Arazzo](https://github.com/OAI/Arazzo-Specification)
workflow durability.

`MongoWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under optimistic
concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer / awaiting-message
wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`, over
[MongoDB.Driver](https://www.mongodb.com/docs/drivers/csharp/). Each run is a document holding the opaque
checkpoint (binary) plus the projected index fields; optimistic concurrency maps to a version field
(conditional replace) and the single-owner lease to a small leases collection (a conditional upsert).

```csharp
await using var store = await MongoWorkflowStateStore.CreateAsync("mongodb://localhost:27017");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`CreateAsync` ensures the indexes (idempotent).

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.
