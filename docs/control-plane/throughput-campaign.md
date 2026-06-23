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
| **Runner heartbeat** (`IRunnerRegistry.HeartbeatAsync`) | `SELECT doc` → `RunnerRegistration.WriteWithLastSeenAt` (rewrite the **whole** registration) → `UPDATE doc` — **2 round-trips + full-payload rewrite** per heartbeat (the `last_seen_at` column is already updated in the same `UPDATE`; the JSON `doc` mirrors `lastSeenAt`, which is why it is re-serialized) | every runner × heartbeat interval — **highest** | ✅ **DONE** — see closeout |
| **Checkpoint status bump** (`SecuredWorkflowManagement.CancelAsync` → `RewriteStatus` → `SaveAsync`) | `AcquireLease` → `LoadAsync` → `RewriteStatus` (full rewritten doc) → `SaveAsync` (whole checkpoint + index columns + child-table security-tag rewrite) → `ReleaseLease` — a CAS with a terminal-status guard | operator-initiated cancel — **low** | ⏭️ **assessed, not pursued** (see below) |
| **Lease acquire / renew / release** (`AcquireLeaseAsync` / `ReleaseLeaseAsync`) | **already** a single conditional `UPSERT` CAS (acquire/renew) / single `DELETE` (release) — 1 round-trip, no read | every dispatch/worker poll | **NOT a candidate — already native/optimal** |
| Normal `SaveAsync` (checkpoint advances) | full checkpoint write (the run state genuinely changed) | per step | **genuine** — the document changed; not RMW-avoidable |

So the campaign was **narrow**: the runner heartbeat was the clear win (done); the lease is already optimal, the
general checkpoint write is genuine, and the checkpoint status bump (cancel) was assessed and not pursued.

## Per-backend native partial-update mechanism (for the heartbeat row)

`last_seen_at` is already a column on the relational backends; the avoidable work is the `SELECT` + client-side
whole-`doc` rewrite. The single-statement form updates the column **and** the doc's mirrored field together:

> **The mechanisms below were partly wrong at scope time — the column storage matters.** Each backend's
> `doc` column type decides whether an in-place server-side JSON patch is even possible/safe. Grounded reality
> (✅ = done & container-verified):

| Backend | Mechanism (as grounded) | Status |
|---|---|---|
| Postgres | `doc` is **BYTEA** (raw UTF-8) — bare `jsonb_set(doc,…)` does **not** apply; decode/re-encode in one statement: `doc = convert_to(jsonb_set(convert_from(doc,'UTF8')::jsonb,'{lastSeenAt}',to_jsonb(@iso))::text,'UTF8')`. Reorders keys (safe — sole reader parses by name). | ✅ done |
| SqlServer | `doc` was **VARBINARY(UTF-8)** — in-place `JSON_MODIFY` is **unsafe** (CAST varbinary→varchar mis-decodes UTF-8 as CP1252, double-encoding non-ASCII). Column changed to **`VARCHAR(MAX) COLLATE …_UTF8`** + native `JSON_MODIFY(doc,'$.lastSeenAt',@iso)`; write via nvarchar param, read via `CAST(doc AS VARBINARY)` (bytes-native). | ✅ done |
| Sqlite | **Embedded (in-process, no network round-trip)** — not a throughput candidate; the RMW was already alloc-optimized. `json_set` would save only client CPU + one in-process query. **Skipped** (documented non-candidate, like the lease/NATS exceptions). | ⏭️ skipped |
| MySql | `doc` is **LONGBLOB** (UTF-8) — but unlike SqlServer, MySQL's `CONVERT(doc USING utf8mb4)` decodes with an explicit charset, so an in-place patch is safe with **no column change**: `doc = CAST(JSON_SET(CONVERT(doc USING utf8mb4),'$.lastSeenAt',@iso) AS BINARY)`. Normalises (reorders keys + spacing) — safe (sole reader parses by name). Non-ASCII round-trip verified. | ✅ done |
| Cosmos | `doc` is a **nested JSON object** in the item — `PatchItemStreamAsync` replaces `/lastSeenAt` (epoch) + `/doc/lastSeenAt` (ISO mirror) in one call, no read. Non-ASCII safe (SDK JSON-encodes). | ✅ done |
| Mongo | `doc` is a **BSON binary blob** — the `lastSeenAt` mirror is *inside* the opaque blob; `$set` can only replace the whole blob. A native partial needs a BSON-subdocument reshape (invasive, changes the read path). **Not pursued.** | ⏭️ blob — not pursued |
| AzureStorage | `Doc` is a **binary property** (+ top-level `LastSeenAt`) — `MergeEntity` can't reach inside the blob; the mirror would drift unless the read reconstructs `lastSeenAt` from the property (semantics change). **Not pursued.** | ⏭️ blob — not pursued |
| Redis | the registration is a single **String** (whole value), not a hash — no in-place field patch. Whole-value, like NATS. **Not pursued.** | ⏭️ whole-value |
| NATS KV | **blob value, no server-side partial** — KV stores whole values; heartbeat stays RMW. Genuine exception. | ⏭️ exception |
| InMemory | direct field mutation (reference store) — in-process, no round-trips. Non-candidate. | — |

**Heterogeneity is expected** (mirrors the alloc campaign): some backends need a column-type change to support a
safe in-place patch (SqlServer); embedded Sqlite is a non-candidate; **NATS KV genuinely cannot** (whole-value
store) and stays RMW — that's the ownership boundary, not a miss.

## Measurement (the per-row gate — NOT MemoryDiagnoser)

**Primary metric: round-trips + bytes-sent. Local latency is a secondary, weak signal.** A container-backed
latency harness exists (`benchmarks/Corvus.Text.Json.Arazzo.Durability.ThroughputBenchmarks`, a Stopwatch harness
over a live container via the pre-tunneled podman socket, [[broker-integration-tests-wsl-podman]]) reporting
mean/p50/p99 latency, but it **systematically under-measures round-trip-reduction wins**: a *local* container's
RTT is tiny, so eliminating a round-trip barely moves wall-clock and can even look slightly worse when a backend
shifts JSON work server-side (SqlServer `JSON_MODIFY`). Latency-over-a-real-network is ∝ round-trips, so the
**definitive metric is the structural round-trip count (2→1) and payload (whole-doc→params)**, which the code
proves directly. Local latency only shows the win cleanly for light backends (Postgres ~900 µs/op, where a saved
~300 µs RTT is visible). Allocation is not the metric here (the alloc campaign already made payloads bytes-native).

> **Why this changed:** the Postgres row showed a clean ~−70% local latency win, but SqlServer (per-call cost
> ~4 ms, of which a round-trip is only ~544 µs ≈ 13%) showed no local delta despite a genuine 2→1 collapse —
> the saved round-trip was swamped by LOB/JSON work + variance. Don't revert correct round-trip reductions for
> want of a *local* latency delta; judge by round-trips + payload (the network-bound win).

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

## Results (measured before→after)

The harness is `benchmarks/Corvus.Text.Json.Arazzo.Durability.ThroughputBenchmarks` — a standalone Stopwatch
latency harness (NOT BenchmarkDotNet/MemoryDiagnoser; BDN's child-process toolchain fights a live container
handle). Run it directly per scenario, e.g.
`DOCKER_HOST=unix:///tmp/podman-arazzo.sock TESTCONTAINERS_RYUK_DISABLED=true dotnet run -c Release --project benchmarks/Corvus.Text.Json.Arazzo.Durability.ThroughputBenchmarks postgres-heartbeat`
(5000 timed heartbeats after 500 warmup, postgres:16-alpine, representative 586-byte runner doc).

### Row 1 — Runner heartbeat, **Postgres** ✅ (Option A: BYTEA preserved, server-side jsonb round-trip)

The `doc` column is `BYTEA` (raw CTJ UTF-8), so the scope doc's bare `jsonb_set(doc,…)` does not apply: the
single statement decodes to jsonb, replaces the one field, and re-encodes, all server-side —
`UPDATE … SET last_seen_at=@at, doc=convert_to(jsonb_set(convert_from(doc,'UTF8')::jsonb,'{lastSeenAt}',to_jsonb(@iso))::text,'UTF8') WHERE runner_id=@id`.
The existence `SELECT` is gone (rows-affected = 0 ⇒ unknown runner ⇒ `false`). `@iso` is the caller's round-trip
`"O"` string (the representation the generated model emits/parses; the conformance `Reg` helper proves CTJ
round-trips it), and jsonb stores string values verbatim. **Trade-off accepted:** routing through `::jsonb`
reserializes the doc to jsonb-canonical form (object keys reorder after the first heartbeat) — verified safe: the
sole reader (`ListAsync` → `RunnerRegistration.FromJson`) parses by name, and nothing hashes the doc bytes
(incidental output shape, not a contract).

| metric | before (RMW) | after (native partial) | delta |
|---|---|---|---|
| round-trips | 2 (`SELECT doc` + `UPDATE doc`) | **1** (`UPDATE`) | −1 |
| bytes sent client→server | 586 B (whole doc re-sent) | ~60 B (params only) | ≈ −90% |
| latency p50 | 899–943 µs | **308–314 µs** | ≈ −66% |
| latency mean | 953–1045 µs | **312–314 µs** | ≈ −69% |
| latency p99 | 1772–2141 µs | **509–531 µs** | ≈ −73% |

(Ranges are two runs each. Even against a *local* container the second round-trip dominates; over a real
network the relative win grows.) **Conformance:** 11/11 `PostgresRunnerRegistryConformanceTests` green against a
live container (heartbeat-advances-and-reads-back-the-doc mirror, unknown-runner⇒false, prune-by-column, and the
full register/list/replace/hosted-version suite — jsonb canonicalization broke no reader).

### Row 1 — Runner heartbeat, **Sqlite** ⏭️ skipped (non-candidate)

Sqlite is **embedded** (one in-process connection held for the registry's lifetime, no network). The throughput
axis (round-trips/network latency) does not apply; the RMW was already alloc-optimized, and a native `json_set`
(which would need `CAST(doc AS TEXT)` since SQLite reads raw BLOBs as its JSONB binary) saves only client CPU + one
in-process query. Skipped by decision, documented alongside the lease (already native) and NATS (whole-value) as
a genuine non-candidate rather than a miss.

### Row 1 — Runner heartbeat, **SqlServer** ✅ (Option B2′: column → UTF-8 `VARCHAR`, native `JSON_MODIFY`)

The `doc` column was `VARBINARY(MAX)` of UTF-8, and an in-place `JSON_MODIFY` is **unsafe** there: every variant
double-encodes non-ASCII (`rünner-café` → `rÃ¼nner-cafÃ©`) because `CAST(varbinary AS VARCHAR)` commits to the DB
code page (CP1252) before any `COLLATE` — proven against a live container with a non-ASCII probe (the ASCII-only
conformance suite cannot catch it). So there is **no safe column-preserving path**; the column was changed to
`VARCHAR(MAX) COLLATE Latin1_General_100_CI_AS_SC_UTF8` (UTF-8, same byte size), enabling native surgical
`UPDATE … SET last_seen_at=@at, doc = JSON_MODIFY(doc,'$.lastSeenAt',@iso) WHERE runner_id=@id` (rows-affected = 0
⇒ unknown ⇒ `false`; key order preserved). Writes pass the doc as an **nvarchar** param (lossless into the UTF-8
column; a register-time cold-path string), reads stay **bytes-native** (`CAST(doc AS VARBINARY)`). No prior
production releases, so the schema change has no migration cost.

| metric | before (RMW) | after (native partial) | delta |
|---|---|---|---|
| round-trips | 2 (`SELECT doc` + `UPDATE doc`) | **1** (`UPDATE`) | −1 |
| bytes sent client→server | 586 B (whole doc re-sent) | ~60 B (params only) | ≈ −90% |
| latency p50 (local) | 3.6–4.6 ms | 4.3–4.6 ms | none (within noise) |

**Local latency shows no win — and that is expected, not a regression.** A bare round-trip to the local container
is ~544 µs (measured via a `SELECT 1` probe; per-call connection-open adds ~185 µs), but the heartbeat is ~4 ms
dominated by LOB/JSON work + variance, so a saved round-trip (~13%) is swamped. The win is the structural 2→1
round-trips + 586→60 B payload, which is **network-bound** (materializes against a remote DB — the realistic
production case). **Conformance:** 11/11 `SqlServerRunnerRegistryConformanceTests` green (mirror read-back,
unknown-runner⇒false, prune, register/list/replace/hosted-version; the UTF-8 column round-trips correctly).

### Row 1 — Runner heartbeat, **MySql** ✅ (Option A: LONGBLOB preserved, in-place `JSON_SET`)

The `doc` column is `LONGBLOB` (UTF-8), but unlike SqlServer there is a **safe column-preserving** path: MySQL's
`CONVERT(doc USING utf8mb4)` decodes the bytes with an explicit charset (correct, where SqlServer's code-page CAST
was not), so the heartbeat is a single in-place statement —
`UPDATE … SET last_seen_at=@at, doc=CAST(JSON_SET(CONVERT(doc USING utf8mb4),'$.lastSeenAt',@iso) AS BINARY) WHERE runner_id=@id`
(rows-affected = 0 ⇒ unknown ⇒ `false`). Verified against a live container with a non-ASCII probe
(`rünner-café/路径` survives, valid UTF-8). `JSON_SET` normalises the doc (reorders keys + inter-token spacing) —
safe (sole reader parses by name). No column change, reads stay bytes-native.

| metric | before (RMW) | after (native partial) | delta |
|---|---|---|---|
| round-trips | 2 (`SELECT doc` + `UPDATE doc`) | **1** (`UPDATE`) | −1 |
| bytes sent client→server | 586 B (whole doc re-sent) | ~60 B (params only) | ≈ −90% |
| latency p50 (local) | 2.85–3.06 ms | 2.6–3.8 ms | none (within noise) |

Local latency neutral/noisy (same as SqlServer — round-trips are a small fraction of the ~3 ms local per-call
cost); the win is the structural round-trip/payload reduction (network-bound). **Conformance:** 11/11
`MySqlRunnerRegistryConformanceTests` green.

### Row 1 — Runner heartbeat, **Cosmos** ✅ (PatchItem, no read, no reshape)

Cosmos stores `doc` as a **nested JSON object** in the item (`{ id, lastSeenAt, doc, loadedVersions }`), so the
heartbeat needs no read and no rewrite — a single `PatchItemStreamAsync` replaces both mirrored timestamps:
`[ Replace("/lastSeenAt", epochMs), Replace("/doc/lastSeenAt", isoString) ]` (a `NotFound` ⇒ unknown ⇒ `false`).
The loaded-version projection is untouched (a heartbeat never changes hosted versions). Non-ASCII is inherently
safe (the SDK JSON-encodes the patch values). The previous read→parse→whole-envelope-upsert path
(`ReadItemStreamAsync` + `UpsertItemStreamAsync`) is gone, and the now-dead heartbeat envelope overload was
removed (the stale "base64" doc comments — the registration is embedded verbatim as nested JSON — were corrected).

| metric | before (RMW) | after (PatchItem) | delta |
|---|---|---|---|
| round-trips | 2 (`ReadItem` + `UpsertItem`) | **1** (`PatchItem`) | −1 |
| payload | whole envelope re-sent | 2 small patch ops | ≈ −90% |

Latency not separately benched (the Cosmos emulator is heavy/variable under emulation, and the agreed primary
metric is round-trips + payload, which is structural and definitive here). **Conformance:** 11/11
`CosmosRunnerRegistryConformanceTests` green against the live emulator (mirror read-back, unknown-runner⇒false,
prune, register/list/replace/hosted-version).

## Heartbeat row — closeout

| Backend | Outcome |
|---|---|
| Postgres | ✅ in-place `jsonb_set` (BYTEA round-trip) |
| SqlServer | ✅ column → UTF-8 `VARCHAR` + native `JSON_MODIFY` |
| MySql | ✅ in-place `JSON_SET` (`CONVERT … USING utf8mb4`) |
| Cosmos | ✅ `PatchItem` (nested JSON, no read) |
| Sqlite | ⏭️ embedded (no network round-trip) — non-candidate |
| Mongo | ⏭️ BSON binary blob — native partial needs an invasive subdocument reshape; not pursued |
| AzureStorage | ⏭️ binary property — would need read-side mirror reconstruction; not pursued |
| Redis | ⏭️ whole-value String — no in-place field patch |
| NATS KV | ⏭️ whole-value store — genuine exception |
| InMemory | — in-process reference store (no round-trips) |

**The heartbeat row is complete:** the four backends that can do a safe server-side partial update (Postgres,
SqlServer, MySql, Cosmos) now do 1 round-trip instead of 2 with a whole-doc payload; the rest are documented
non-candidates (blob/whole-value storage or embedded/in-process). Net per-backend win is **2→1 round-trips +
whole-doc→params payload** — network-bound, so it materializes against remote production stores (local-container
latency only showed it cleanly for the lightest backend, Postgres).

## Checkpoint status bump (cancel) — assessed, not pursued

The grounded reality narrowed the secondary candidate: **only `SecuredWorkflowManagement.CancelAsync` is a
status-only bump.** "Resume" is *not* — it re-enters the executor (`WorkflowRun.ResumeAsync`) or serializes a whole
mutated `Faulted` checkpoint (working state changes), so it is a genuine full save. The other `SaveAsync` callers
are normal execution saves.

A native cancel would have to collapse `LoadAsync` + `SaveAsync` into a **new `IWorkflowStateStore` method** — a
conditional `UPDATE` setting the `status` column + `updated_at`, patching the doc's `status` and dropping `wait`,
`WHERE id=@id AND etag=@etag AND status NOT IN (terminal)`, skipping the unchanged child security-tags table, and
discriminating not-found / conflict / already-terminal / success from the result — implemented across **9
backends**, and (same feasibility limit as the heartbeat) only on the 4 queryable-JSON backends.

**Decision: not pursued.** Ranked by fix-shape/blast-radius/risk (not frequency): a large, higher-risk new
9-backend CAS seam for a *partial* win (saves 1 of ~4 round-trips — the lease round-trips remain) on a
**low-frequency, latency-insensitive operator action**. Poor cost/benefit on the throughput axis. (If it is ever
revisited, the lease around cancel could likely be dropped in favour of pure CAS — a separate locking-model change.)

## Campaign status — WRAPPED

The throughput campaign is **complete**. The one genuine high-frequency win — the runner heartbeat — was delivered
across the four backends that can do a safe server-side partial update (Postgres, SqlServer, MySql, Cosmos: 2→1
round-trips, whole-doc→params payload), with the rest documented as non-candidates. The remaining triaged ops are
either already optimal (lease), genuine full writes (normal checkpoint save), or a poor-cost/benefit secondary
(cancel, above). No further rows.

## Per-row protocol (retained for reference)

If a future throughput row is opened (e.g. revisiting the cancel locking model, or a new high-frequency RMW op),
follow the protocol that worked here: **ground** the op + its conformance test → **baseline** the round-trips +
payload (latency secondary; the local harness under-measures round-trip wins) → **ledger** the invariants
(optimistic-concurrency CAS + doc/column mirror consistency) → **STOP for go-ahead** → **change** (single native
partial update, only where the backend stores the doc as queryable JSON) → **verify** the backend's container
conformance (and an explicit **non-ASCII probe** for any in-place JSON edit — the conformance suites are ASCII-only)
→ **document + commit**. One op × one backend at a time.

## Cross-references

- Memory: [[durability-alloc-campaign-followups]] (the alloc campaign that defined this throughput follow-up
  and noted it is a *throughput axis, define fresh*), [[broker-integration-tests-wsl-podman]] (container runner),
  [[is-this-the-right-shape-for-the-seam]] (consumer-tracing discipline), [[frequency-is-not-a-licence]].
- Skills: `corvus-benchmarks`, `corvus-build-and-test`, the per-backend store skills.
