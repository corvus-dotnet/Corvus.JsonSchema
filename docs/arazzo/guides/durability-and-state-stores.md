# Durability and state stores

How a workflow run survives a crash, resumes, and is persisted, and how to write a state-store backend for a new
datastore. The decisions are the durability ADRs: the products are the checkpoint
([ADR 0019](../adr/0019-products-are-the-checkpoint.md)), durability is opt-in code generation
([ADR 0020](../adr/0020-durability-is-opt-in-codegen.md)), the state-store abstraction
([ADR 0021](../adr/0021-state-store-abstraction.md)), and the resume-mode taxonomy
([ADR 0022](../adr/0022-resume-mode-taxonomy.md)).

## The checkpoint model

A durable run is a sequence of checkpoints. The engine does not snapshot a call stack; the checkpoint is the
run's **products** so far (its inputs and the outputs each completed step produced) plus a cursor
([ADR 0019](../adr/0019-products-are-the-checkpoint.md)). To advance a run, the engine loads the checkpoint,
re-enters at the cursor, runs the next step, and persists the new products. A completed step is never re-run from
its products; only the in-flight step is executed, which is why step execution is at-least-once and request
bindings should carry idempotency keys.

Durability is opt-in at code-generation time ([ADR 0020](../adr/0020-durability-is-opt-in-codegen.md)): the
generator emits a durable executor (one that checkpoints between steps) only when asked, so a workflow that does
not need durability pays nothing for it.

## The state-store abstraction

Persistence is behind two seams ([ADR 0021](../adr/0021-state-store-abstraction.md)):

- **`IWorkflowStateStore`** is the core store: it saves and loads a run's checkpoint and administers the per-run
  lease that guarantees a single executor advances a run at a time.

  ```csharp
  public interface IWorkflowStateStore
  {
      ValueTask<WorkflowEtag> SaveAsync(/* run id, checkpoint, expected etag */ CancellationToken ct);
      ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken ct);
      ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken ct);
      ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken ct);
      ValueTask DeleteAsync(WorkflowRunId id, CancellationToken ct);
  }
  ```

- **`IWorkflowWaitIndex`** is the optional visibility and wait index: the queryable projection the control plane
  lists over and the resume loop polls for runs whose durable timer is due or whose awaited message arrived. The
  runner's claim query is `IWorkflowDispatchIndex` over the same data. A store that needs only execution (no
  control-plane visibility, no timers) can implement the core store alone.

Every checkpoint save is an optimistic-concurrency write (an etag compare), so a slow or zombie executor whose
lease was taken over fails its next save and aborts, with no split-brain.

## Resume

A faulted or suspended run is advanced again through one of four resume modes
([ADR 0022](../adr/0022-resume-mode-taxonomy.md)): `RetryFaultedStep` (re-run the faulted step), `Rewind` (reset
the cursor and re-run forward), `Skip` (advance past the faulted step, optionally recording its outputs), and
`StatePatch` (apply an RFC 6902 patch to the run's products, then retry). There is deliberately no cancel resume
mode; cancellation is a separate operation. Because resume is load-checkpoint-then-re-enter, the same path serves
a control-plane resume, an orphan reclaim after a crash, and a timer or message wake-up.

## Writing a state-store backend

To back a new datastore, implement `IWorkflowStateStore` (and, for control-plane visibility, `IWorkflowWaitIndex`
and its dispatch query) over that datastore, following the platform conventions
([platform-conventions guide](platform-conventions.md)): the checkpoint persists bytes-native (no
record-to-document string round-trip), lists are keyset-paged, and a bounded count answers the total. Persist the
checkpoint as its generated CTJ document with an in-document etag for the optimistic-concurrency compare, and
implement the lease as a compare-and-swap on an owner and expiry so exactly one executor holds it. A shared
conformance suite runs against every backend, so a new backend is correct when it passes the same suite the
in-memory reference and the shipped backends (Postgres, SQL Server, MySQL, Sqlite, Redis, Mongo, Cosmos, NATS
JetStream, Azure Storage) pass.

## See also

- The [runner guide](running-a-runner.md) for how a runner claims and advances a durable run.
- The [platform-conventions guide](platform-conventions.md) for the bytes-native, keyset-paged store discipline.
