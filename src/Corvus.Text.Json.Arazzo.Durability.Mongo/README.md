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
// Once, at deploy/migration time — with a user permitted to create indexes:
await MongoWorkflowStateStore.PrepareAsync("mongodb://admin:…@localhost:27017");

// At runtime — with a least-privileged operational user (readWrite); creates no indexes:
await using var store = await MongoWorkflowStateStore.ConnectAsync("mongodb://app:…@localhost:27017");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`PrepareAsync` ensures the indexes (idempotent — needs the `createIndex` privilege); `ConnectAsync` creates no
indexes, so the running app needs only `readWrite` (collections are created lazily on first write). Both also
have `IMongoClient` overloads, so you can hand in a client the app owns — for example one whose
`MongoClientSettings` use an OIDC/managed-identity or AWS-IAM credential.

> The in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend
> runs the same store-conformance suite.
