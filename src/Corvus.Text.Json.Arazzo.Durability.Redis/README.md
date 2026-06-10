# Corvus.Text.Json.Arazzo.Durability.Redis

A [Redis](https://redis.io/) backend for [Arazzo](https://github.com/OAI/Arazzo-Specification) workflow
durability.

`RedisWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under optimistic
concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer / awaiting-message
wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`, over
[StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/). Each run is a hash (the opaque
checkpoint plus the projected index fields); optimistic concurrency and the single-owner lease are atomic Lua
scripts, and due timers are a **sorted set scored by due-time** so the worker can scan for ready runs
efficiently.

```csharp
await using var store = await RedisWorkflowStateStore.CreateAsync("localhost:6379");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

> Targets a single Redis instance (or a primary); the index-maintenance Lua touches multiple keys. The
> in-memory store (in `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend runs
> the same store-conformance suite.
