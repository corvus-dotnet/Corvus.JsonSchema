# Control-plane throughput campaign — scope

> **Axis:** this is a **throughput / latency** campaign — round-trips, payload bytes, and server-vs-client
> CPU on the high-frequency read-modify-write (RMW) hot paths — **not** an allocation campaign. Do **not**
> reuse the allocation ledger / `MemoryDiagnoser` template; measure with a **container-backed latency
> benchmark** (the durability allocation campaign is complete and closed — see
> [[durability-alloc-campaign-followups]]).

## Goal

Several durable-store operations currently **read the whole document, mutate one field client-side, and write
the whole document back** — two round-trips plus a full-payload rewrite, on paths that run at high frequency.
Where the backend supports it, collapse these to a **single native server-side partial update** (1 round-trip,
tiny payload), preserving optimistic concurrency and the document-as-source-of-truth contract.

## Hot-path triage (grounded against the code)

| Op | Current shape | Frequency | Verdict |
|---|---|---|---|
| **Runner heartbeat** (`IRunnerRegistry.HeartbeatAsync`) | `SELECT doc` → `RunnerRegistration.WriteWithLastSeenAt` (rewrite the **whole** registration) → `UPDATE doc` — **2 round-trips + full-payload rewrite** per heartbeat (the `last_seen_at` column is already updated in the same `UPDATE`; the JSON `doc` mirrors `lastSeenAt`, which is why it is re-serialized) | every runner × heartbeat interval — **highest** | **PRIMARY candidate** |
| **Checkpoint status/etag bump** (`IWorkflowStateStore.SaveAsync` for status-only transitions, e.g. cancel/resume via `WorkflowCheckpointSerializer.RewriteStatus`) | read → `RewriteStatus` (full rewritten doc) → write whole checkpoint (+ child-table security-tag delete/reinsert on column backends) | per state transition — **medium** | **secondary** (the checkpoint genuinely changes on a normal `SaveAsync`; only status-only bumps are RMW-avoidable, and the child-table rewrite is a separate cost) |
| **Lease acquire / renew / release** (`AcquireLeaseAsync` / `ReleaseLeaseAsync`) | **already** a single conditional `UPSERT` CAS (acquire/renew) / single `DELETE` (release) — 1 round-trip, no read | every dispatch/worker poll | **NOT a candidate — already native/optimal** |
| Normal `SaveAsync` (checkpoint advances) | full checkpoint write (the run state genuinely changed) | per step | **genuine** — the document changed; not RMW-avoidable |

So the campaign is **narrow**: the runner heartbeat is the clear win; the checkpoint status-bump is a smaller
secondary; the lease is already optimal and the general checkpoint write is genuine.

## Per-backend native partial-update mechanism (for the heartbeat row)

`last_seen_at` is already a column on the relational backends; the avoidable work is the `SELECT` + client-side
whole-`doc` rewrite. The single-statement form updates the column **and** the doc's mirrored field together:

| Backend | Mechanism |
|---|---|
| Postgres | `UPDATE … SET last_seen_at=@at, doc = jsonb_set(doc,'{lastSeenAt}', to_jsonb(@at)) WHERE runner_id=@id` |
| SqlServer | `UPDATE … SET LastSeenAt=@at, Doc = JSON_MODIFY(Doc,'$.lastSeenAt',@at) …` |
| Sqlite | `UPDATE … SET last_seen_at=@at, doc = json_set(doc,'$.lastSeenAt',@at) …` |
| MySql | `UPDATE … SET last_seen_at=@at, doc = JSON_SET(doc,'$.lastSeenAt',@at) …` |
| Cosmos | `PatchItemAsync` (replace `/lastSeenAt`) — 1 call, no read |
| Mongo | `UpdateOneAsync($set: { lastSeenAt })` |
| AzureStorage | `MergeEntity` (partial table-entity update — already field-granular) |
| Redis | `HSET` the `last_seen_at` field (the registration is a hash) — confirm the doc-mirror story |
| NATS KV | **blob value, no server-side partial** — KV stores whole values; heartbeat stays RMW (or revision-CAS). Document as the genuine exception. |
| InMemory | direct field mutation (reference store) |

**Heterogeneity is expected** (mirrors the alloc campaign): relational + Cosmos + Mongo + Azure get a true
native partial update; Redis is field-granular via the hash; **NATS KV genuinely cannot** (whole-value store)
and stays RMW — that's the ownership boundary, not a miss.

## Measurement (the per-row gate — NOT MemoryDiagnoser)

A **container-backed latency benchmark** per backend (BenchmarkDotNet over a live container via the pre-tunneled
podman socket, [[broker-integration-tests-wsl-podman]]), reporting **mean/p50/p99 latency** and **round-trips**
and **bytes sent** for the op before→after. Allocation is not the metric here (though the alloc work already
made the payload bytes-native). A simple round-trip count + payload-size assertion in a conformance-style test
is an acceptable cheaper proxy where a full latency bench is impractical.

## Per-row protocol (one op × one backend at a time)

1. **Ground** — read the op's current implementation in the backend + its conformance test.
2. **Baseline** — measure round-trips + payload + latency (container) for the current RMW. Paste the numbers.
3. **Ledger** — state the optimistic-concurrency invariant the op must preserve (etag/CAS) and the
   document-mirror consistency (the column and the JSON `doc` must agree after the update).
4. **STOP** for go-ahead.
5. **Change** — the single native partial update; keep the RMW fallback only where the backend can't (NATS).
6. **After** — re-measure (before→after round-trips/payload/latency); **container-verify the affected backend's
   conformance suite** (the round-trip behaviour must not change the persisted state or the CAS semantics).
7. **Document + commit when asked.**

## Risks / constraints

- **Optimistic concurrency** — the heartbeat's current `UPDATE … WHERE runner_id=@id` is unconditional (last
  writer wins, which is fine for a heartbeat); a status/etag bump is CAS — the native update must keep the
  `WHERE … AND etag=@expected` guard and return whether it matched.
- **Document-mirror consistency** — the JSON `doc` mirrors the indexed column; the native update must set both
  (the SQL `json_set`/`jsonb_set` in the same statement) so a subsequent read reconstructs correctly. A
  conformance test must read-back-and-assert the mirrored field.
- **Heterogeneity** — per-backend SQL/dialect; NATS KV is the genuine whole-value exception.
- **Correctness over throughput** — a partial update that drifts the doc from the column is a data bug; verify
  every backend round-trips identically.

## First row (recommended)

**Runner heartbeat — Postgres** (the clearest native-partial-update backend), then fan out to the other
relational + Cosmos/Mongo/Azure backends, then the checkpoint status-bump as a secondary pass.

## Kickoff prompt (paste into a fresh session)

> Resume the control-plane **throughput** campaign in the worktree
> `/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/arazzo-workflow-engine-plan` (branch
> `worktree-arazzo-workflow-engine-plan`; never `cd` to the repo root). **Read first:**
> `docs/control-plane/throughput-campaign.md` (this scope + the per-row protocol) and the memory
> `throughput-campaign-scoped`. Then read the skills `corvus-benchmarks` and `corvus-build-and-test`.
>
> This is a **throughput / latency** axis (round-trips, payload bytes, p50/p99 latency) — **NOT** allocation.
> Measure with a **container-backed latency benchmark** over a live container via the pre-tunneled podman
> socket (`DOCKER_HOST=unix:///tmp/podman-arazzo.sock`, `TESTCONTAINERS_RYUK_DISABLED=true`; memory
> `broker-integration-tests-wsl-podman`) — **do NOT use `MemoryDiagnoser`**. There is no latency-benchmark
> harness yet; standing one up (a BenchmarkDotNet bench, or a round-trip-count + payload-size proxy in a
> conformance-style test) is part of the first row.
>
> **First row: runner heartbeat, Postgres.** Follow the per-row protocol literally:
> 1. **Ground** — read `PostgresRunnerRegistry.HeartbeatAsync` (currently `SELECT doc` →
>    `RunnerRegistration.WriteWithLastSeenAt` whole-doc rewrite → `UPDATE doc`) and its conformance test.
> 2. **Baseline** — measure + paste the current round-trips (2), payload, and latency.
> 3. **Ledger** — state the invariants the change must preserve: the heartbeat's last-writer semantics, and the
>    JSON `doc`'s `lastSeenAt` staying **mirror-consistent** with the `last_seen_at` column after the update.
> 4. **STOP for go-ahead** before changing any code.
> 5. **Change** — the single native partial update
>    (`UPDATE runner_registrations SET last_seen_at=@at, doc=jsonb_set(doc,'{lastSeenAt}',to_jsonb(@at)) WHERE runner_id=@id`).
> 6. **After** — re-measure before→after; **container-verify the Postgres runner conformance** (the persisted
>    state + read-back must be identical). Then fan out to the other relational + Cosmos/Mongo/Azure backends
>    (NATS KV is the genuine whole-value exception — leave it RMW). Commit only when asked; one op × backend at
>    a time; warning-free `dotnet build Corvus.Text.Json.slnx` before every commit.

## Cross-references

- Memory: [[durability-alloc-campaign-followups]] (the alloc campaign that defined this throughput follow-up
  and noted it is a *throughput axis, define fresh*), [[broker-integration-tests-wsl-podman]] (container runner),
  [[is-this-the-right-shape-for-the-seam]] (consumer-tracing discipline), [[frequency-is-not-a-licence]].
- Skills: `corvus-benchmarks`, `corvus-build-and-test`, the per-backend store skills.
