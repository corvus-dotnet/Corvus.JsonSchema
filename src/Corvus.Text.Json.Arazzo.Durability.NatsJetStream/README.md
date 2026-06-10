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
// Once, at deploy/migration time — with an account permitted to manage streams:
await NatsJetStreamWorkflowStateStore.PrepareAsync("nats://admin:…@localhost:4222");

// At runtime — with a least-privileged operational account (get/put/delete on the buckets); creates nothing:
await using var store = await NatsJetStreamWorkflowStateStore.ConnectAsync("nats://app:…@localhost:4222");
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

`PrepareAsync` creates the key/value buckets (a JetStream stream — needs stream-management rights);
`ConnectAsync` binds to the existing buckets, so the running app needs only get/put/delete on their subjects.
Both also have `INatsConnection` overloads, so you can hand in a connection the app owns — for example one
authenticated with a creds file, nkey, or token.

> Wait/visibility queries scan the bucket's keys and filter on the index header — appropriate for the
> co-located-with-NATS use this backend targets. The in-memory store (in
> `Corvus.Text.Json.Arazzo.Durability`) is the reference implementation; this backend runs the same
> store-conformance suite.
