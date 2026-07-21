# ADR 0029. Native server-side partial update for the hot heartbeat path

Date: 2026-07-21. Status: **Accepted**. Scope: how a runner's frequent heartbeat is persisted. Builds on
[ADR 0021](0021-state-store-abstraction.md) and [ADR 0027](0027-runner-environment-binding.md). This records
why the runner heartbeat is a single native server-side partial update on backends that store queryable JSON,
rather than a read-modify-write.

## Context

A runner heartbeats frequently to prove it is alive ([ADR 0027](0027-runner-environment-binding.md)). Persisted
naively, a heartbeat is a read-modify-write: read the runner's registry row, set the last-seen time, write it
back. That is two round-trips per heartbeat, per runner, forever, and it is pure overhead: the heartbeat
changes one field and depends on nothing it read. This is a latency and throughput concern, distinct from the
allocation concern that governs the rest of the persistence layer, and it is measured in round-trips rather
than bytes.

### Grounded architectural facts

- **The heartbeat is a single native partial update where the backend supports it.** Backends that store
  queryable JSON update the one field server-side in a single statement: `PostgresRunnerRegistry`
  (jsonb update), `SqlServerRunnerRegistry` (`JSON_MODIFY`), `MySqlRunnerRegistry` (`JSON_SET`), and
  `CosmosRunnerRegistry` (`PatchItem`), under `src/Corvus.Text.Json.Arazzo.Durability.{Postgres,SqlServer,MySql,Cosmos}/`.
- **It collapses two round-trips to one.** The update names the field and the new value, so there is no read
  step, taking the heartbeat from read-then-write to a single write.
- **It is a distinct optimisation axis.** This is the throughput campaign, measured by round-trips and payload
  rather than by `MemoryDiagnoser`, and it applies where the backend can express a server-side partial update.
- **In-place JSON patching needs a charset-safe column.** A server-side JSON patch is only correct where the
  column decodes with an explicit UTF-8 charset. `SqlServerRunnerRegistry` stores the document in a
  `VARCHAR(MAX)` column under a `_UTF8` collation so `JSON_MODIFY` is safe, and reads it back bytes-native with
  `CAST(doc AS VARBINARY(MAX))`; a raw binary column would double-encode non-ASCII. An in-place JSON edit is
  guarded by a non-ASCII probe, because a conformance suite that exercises only ASCII would miss the corruption.
- **The indexed column and the mirrored field update together.** The heartbeat advances both the queryable
  `last_seen_at` column and the document's mirrored `lastSeenAt` field in the one statement, so a later read
  reconstructs a consistent record; letting the two drift would be a data bug.

## Decision

The runner heartbeat is a **single native server-side partial update** on backends that store queryable JSON,
updating just the last-seen field in one statement. Where a backend cannot express a partial update, the
heartbeat falls back to the read-modify-write, but the common relational and document backends
(Postgres, SQL Server, MySQL, Cosmos) take the single-statement path.

## Consequences

- A frequently-heartbeating fleet halves the round-trips its heartbeats cost, from two to one per heartbeat,
  on the backends that support it.
- The optimisation is local to the heartbeat path. The rest of the persistence layer keeps its read-load-write
  shape where a full document is genuinely being replaced; only this hot, single-field update is specialised.
- Because it is measured in round-trips, the definitive metric is the structural round-trip and payload count,
  not a local latency delta. A local-container benchmark systematically under-measures the win, since the
  collapse can show no local delta yet still remove a full round-trip in production, so a correct round-trip
  reduction is kept on the structural count and not reverted for want of a local latency difference.
- The single-statement path is taken only where the backend stores queryable JSON with a charset-safe column
  (Postgres, SQL Server, MySQL, Cosmos). A backend that stores the document as an opaque blob or a whole value
  (Sqlite in-process, Mongo's BSON, Azure Storage's binary property, Redis and NATS KV) keeps the
  read-modify-write, because a partial update there would need an invasive reshape or a read-side reconstruction
  for no net win.
- The lease and authorization paths ([ADR 0027](0027-runner-environment-binding.md)) are not changed by this;
  only the liveness heartbeat is specialised.
