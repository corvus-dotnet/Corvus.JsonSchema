# Corvus.Text.Json.Arazzo.Durability.NatsJetStream

A [NATS JetStream](https://docs.nats.io/nats-concepts/jetstream) backend for
[Arazzo](https://github.com/OAI/Arazzo-Specification) workflow durability — a natural fit when NATS is
already the message bus.

`NatsJetStreamWorkflowStateStore` implements both `IWorkflowStateStore` (checkpoint save/load under
optimistic concurrency, plus an advisory single-owner lease) and `IWorkflowWaitIndex` (due-timer /
awaiting-message wakeups and the operator visibility query) from `Corvus.Text.Json.Arazzo.Durability`, over a
JetStream **key/value bucket**. Each run's value is a small envelope (the projected index header plus the
opaque checkpoint); the KV entry's **native revision is the optimistic-concurrency token**, and the
single-owner lease is a second KV bucket guarded by the same compare-and-set on revision.

```csharp
await using var store = await NatsJetStreamWorkflowStateStore.CreateAsync("nats://localhost:4222");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

> Wait/visibility queries scan the bucket's keys and filter on the index header — appropriate for the
> co-located-with-NATS use this backend targets. The in-memory store (in
> `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend runs the same
> store-conformance suite.
