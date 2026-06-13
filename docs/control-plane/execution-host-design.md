# Arazzo workflow execution host — design

> Status: **design / proposal**. Extends the "Future phase" of [`catalog-design.md`](./catalog-design.md)
> (compile-to-assembly + dynamic-load hosting) and the durability execution model of
> [`../ArazzoWorkflowEnginePlan.md`](../ArazzoWorkflowEnginePlan.md) §9 (checkpoint/resume, Tier 1/Tier 2).
> It answers: how a catalogued workflow becomes a **compiled assembly stored with the catalog**, and how a
> hosting service **loads and runs** it — i.e. what "make it available to run" means.

## 1. Goal and scope

Today the catalog stores a version's *package* (workflow + sources + baked schema metadata) and the control
plane governs *runs*, but a run is only ever **seeded** in the demo — nothing compiles or executes a workflow.

This design adds two halves:

1. **Build side (at catalog-add):** generate the workflow's executor + its API clients from the package,
   **compile a release assembly**, and store it *in the version's package* alongside the schema metadata. This
   is deterministic and tied to the content-hashed version.
2. **Run side (the execution host):** a service that **discovers** versions carrying an executor assembly,
   **dynamically loads** each into an isolated, collectible context, and **publishes it to run** — via one or
   more *triggers* (HTTP, message, schedule) — driving each run durably through the existing
   checkpoint/resume machinery.

Non-goals here: the generators themselves (exist), the durability stores (exist), the control-plane REST surface
(exists; we extend it). This doc specifies the *seam shapes*, the *packaging*, the *load/isolation model*, and
the *trigger surface*.

## 2. Topology and where it fits

**Two independently-scaled processes share the durability store** (decision §12):

- **Control-plane service** — catalog (incl. the build-side compile-at-add), the runs/governance REST API, the
  HTTP trigger *create* endpoint, and the **runner registry** (§5.4). Stateless-ish; scale for API load.
- **Execution-host service ("runner")** — loads workflow assemblies, owns the transports/scheduler, and *runs*
  workflows (new runs + resume), checkpointing to the shared store. Scale for execution load; many runners,
  each hosting a configurable set of versions.

They never call each other on the hot path: the control plane creates a `Pending` run in the store; a runner
that hosts that version **claims and executes** it (store-as-dispatch-queue, §7), exactly like the existing
Tier-2 resume loop polls the wait index. The control plane learns what each runner hosts (and whether it's
live) only through the registry + heartbeats — for visibility and to reject triggers no live runner can serve.

```
 CONTROL PLANE (scale: API)                         RUNNER(s) (scale: execution)
 ┌───────────────────────────────┐                 ┌──────────────────────────────────────┐
 │ catalog + compile-at-add       │                 │ load metadata/executor.dll into a     │
 │  IWorkflowExecutorProvider     │                 │  collectible ALC (per version)         │
 │  → metadata/executor.dll       │   shared        │ verify assembly↔version integrity      │
 │ runs/governance REST API       │   durability    │ resolve IHostedWorkflow                │
 │ HTTP trigger → CreateNew(Pending)│  store ◄──────►│ dispatcher: claim Pending runs for     │
 │ runner registry + health view  │                 │  hosted versions → RunAsync (Tier 1)   │
 └───────────────────────────────┘                 │ WorkflowWorker: resume due/awaiting     │
        ▲ register + heartbeat ───────────────────► │ message + schedule triggers (own ports)│
                                                    └──────────────────────────────────────┘
```

- **Tier 1 / Tier 2** (executor checkpointing; the `WorkflowWorker` resume loop) already exist (plan §9.3/§9.4)
  and run **inside the runner**.
- This design adds the **build side** (in the control plane) and the **runner** (Tier 3): *generation→compile→
  store*, then *load→claim→start*, handing off to Tiers 1/2 for the durable lifecycle.

## 3. Build side — compile the executor at catalog-add

### 3.1 Seam: `IWorkflowExecutorProvider`

A sibling to `IWorkflowMetadataProvider`, in the **runtime** project (so the catalog depends only on an
abstraction and treats the output as opaque bytes; the code-generation layer implements it):

```csharp
public interface IWorkflowExecutorProvider
{
    /// Builds the compiled executor artifact for a package, or null when it cannot be produced.
    WorkflowExecutorArtifact? BuildExecutor(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources);
}

public readonly record struct WorkflowExecutorArtifact(
    ReadOnlyMemory<byte> Assembly,   // the compiled .dll
    ReadOnlyMemory<byte> Manifest);  // executor-manifest.json (see §3.3)
```

`CatalogPackage.Project` calls it exactly where it calls `BuildSchemas` today:

```csharp
ReadOnlyMemory<byte> schemas  = metadataProvider?.BuildSchemas(rewritten, sources) ?? default;
WorkflowExecutorArtifact? exe = executorProvider?.BuildExecutor(rewritten, sources);
byte[] package = WorkflowPackage.Pack(rewritten, sources, schemas, exe);   // extended Pack
```

### 3.2 What is generated and compiled

From the package alone (reproducible, tied to the version), the provider runs the existing generators:

- **OpenAPI/AsyncAPI clients** for each source description (`openapi-client`, `asyncapi-generate`) — request /
  response / client types and the message types.
- The **Arazzo executor** (`WorkflowExecutorEmitter.Emit`) using a `WorkflowOperationBinder` built over those
  generated client types.
- A **generated non-generic adapter** implementing the host entry contract (§4) — this is new, small, and the
  key to letting the host run the workflow *without compile-time knowledge of its types*.

All of the above is compiled into **one** assembly (release build) so the load-time dependency closure is just
the **stable Corvus runtime assemblies** the host already references — no per-workflow NuGet restore.

Compilation reuses the validator's `DynamicCompiler` Roslyn path (which is why a *build-side* host needs
`<PreserveCompilationContext>true</PreserveCompilationContext>`; see
[`validator-preservecompilationcontext`](../../samples/Corvus.Text.Json.Arazzo.ControlPlane.Demo/docs/live-execution.md)).

### 3.3 Packaging, hash, and signing

Add to the package zip, following the `metadata/schemas.json` precedent exactly:

| Entry | Constant | Notes |
|---|---|---|
| `metadata/executor.dll` | `ExecutorEntryName` | the compiled assembly bytes (binary entry) |
| `metadata/executor-manifest.json` | `ExecutorManifestEntryName` | descriptor (below) |

Reserved document name `$executor` / `$executorManifest` for `GetDocument`. The **content hash is unchanged by
construction** — it canonicalises only `{ workflow, sources }`, never the zip framing or `metadata/*`.

**Integrity, not code-signing (decision §12).** We only need to verify *the assembly belongs to this
workflow version* — i.e. that a runner doesn't load a stale/mismatched DLL. So the manifest records the
version's `packageHash` plus the assembly's own `assemblyDigest` (SHA-256 of the DLL bytes); a runner verifies
(a) the DLL it read hashes to `assemblyDigest`, and (b) the manifest's `packageHash` equals the content hash of
the version it's loading for. That's an integrity binding, computed entirely by the catalog at add time — no PKI
/ code-signing certificate required. (A real cryptographic signature can be layered on later for untrusted
catalogs; the field is reserved but optional.)

`executor-manifest.json`:

```json
{
  "formatVersion": 1,
  "targetFramework": "net10.0",
  "packageHash": "<sha256 of the version's {workflow,sources}>",
  "assemblyDigest": "<sha256 of metadata/executor.dll>",
  "signature": null,
  "entryType": "Corvus.Generated.<WorkflowId>.HostedWorkflow",
  "runtime": { "corvusTextJson": "<version>", "needsMessageTransport": true },
  "sources": [ { "name": "onboarding", "kind": "openapi" }, { "name": "events", "kind": "asyncapi" } ],
  "triggers": [ /* optional declared triggers — see §6.4 */ ]
}
```

> **Transport note.** A binary entry means the package can no longer always be returned as JSON; the catalog's
> `package` endpoint already needs a raw-stream response for this (flagged in `catalog-design.md`). The
> JSON-only document endpoints (`$workflow`, `$schemas`, `$executorManifest`) are unaffected; `$executor` is
> served as `application/octet-stream`.

### 3.4 Failure / opt-out

`BuildExecutor` returns `null` when generation/compilation fails (e.g. an unsupported Arazzo feature) — the
version is still catalogued (with schema metadata), just not *runnable*. Add-time surfaces a non-fatal warning;
the version record carries `runnable: false`. This keeps "catalogue" and "host" decoupled.

## 4. The hosted-workflow contract

The generated `ExecuteAsync` is `static` and generic over `TInputs`/`TOutputs`; the host must run it without
those types. So the provider also emits a **non-generic adapter** implementing a runtime interface the host
knows:

```csharp
public interface IHostedWorkflow
{
    WorkflowDescriptor Descriptor { get; }   // workflowId, inputs schema id, source names, needsMessageTransport

    /// Durable execution: start or resume `run`, returning the tri-state outcome.
    ValueTask<WorkflowRunResultKind> RunAsync(
        IApiTransport transport,
        IMessageTransport? messageTransport,
        JsonWorkspace workspace,
        IWorkflowRun run,             // CreateNew(...) for a fresh trigger, or a resumed checkpoint
        CancellationToken cancellationToken);
}
```

- The adapter parses `run.Inputs` into the concrete `TInputs`, calls the generated
  `ExecuteAsync(transport, messageTransport, workspace, inputs, run, ct)`, and maps `WorkflowRunResult<T>` →
  `WorkflowRunResultKind`.
- This *is* the `WorkflowResumer` the existing `WorkflowWorker`/`WorkflowManagementClient` already expect — so
  **resume, retry, rewind, skip, cancel all work unchanged** once the host can produce an `IHostedWorkflow`.
- `IHostedWorkflow` + `WorkflowDescriptor` + `IWorkflowRun` + the transport interfaces live in the runtime
  project, so the loaded assembly and the host share one contract.

## 5. Run side — the runner (load, isolation, registration)

A separate service (§2). Each runner:

1. **Discovers** which versions to host from its configuration (an allow-list / tag selector) intersected with
   catalogued versions that carry `$executor`. (Watches the catalog or pulls on demand.)
2. **Loads** `metadata/executor.dll` into a **collectible `AssemblyLoadContext` per (base, version)** (reuse the
   validator's `DynamicAssemblyLoadContext` pattern — `LoadFromStream`, `isCollectible: true`). Resolves
   `manifest.entryType`, instantiates the `IHostedWorkflow`, caches it.
   - Pure load of a prebuilt DLL needs no Roslyn/`DependencyContext`; the runner's ALC must resolve the
     **Corvus runtime** assemblies (it references them) — that's the whole closure, since clients+executor+
     adapter were compiled into the one DLL.
3. **Verifies before load** (integrity, §3.3): the DLL hashes to `assemblyDigest` and the manifest's
   `packageHash` equals the version's content hash; refuse on mismatch, incompatible target framework, or an
   out-of-range runtime version.
4. **Unloads** the collectible ALC when the version is **deleted or obsoleted**, or on idle-eviction (bounded
   LRU like the validator cache). Unload semantics: **stop accepting new runs immediately**, let in-flight runs
   drain (or checkpoint+suspend on shutdown), then dispose the ALC. Delete is the key case the host must honour
   promptly — the runner watches the catalog (or the registry relays a delete) and unloads.

Isolation: per-version collectible ALC gives clean unload and version coexistence; resource governance (CPU /
mem / time per run) is runner policy (§9).

### 5.4 Runner registry and health

So the control plane can show **which runners host which workflows and which are live**, runners register and
heartbeat; the registry is a store-backed table (visible to all control-plane instances), with TTL liveness.

- **On startup**, a runner registers: `{ runnerId, instance/address, startedAt, capabilities (transports it can
  bind, max concurrency), hostedVersions: [ {base, version, hash, loaded|failed} ] }`.
- **Heartbeat** on an interval refreshes a `lastSeenAt` (+ updates `hostedVersions` as it loads/unloads). A
  runner missing `N` intervals is `Stale`; past a TTL it's `Dead` and pruned (its leases expire naturally, so
  its in-flight runs become claimable by others).
- **Health probe**: each runner exposes a `GET /health` (liveness + readiness: ALC load OK, store reachable,
  transports bound). The registry stores the last probe result; the control plane can also probe directly.
- **Control-plane surface** (new, read-only): `GET /arazzo/v1/runners` → runners with status + hosted versions;
  `GET /arazzo/v1/catalog/{base}/versions/{n}/runners` → which live runners host a version (used to gate
  triggers, §6.2). The UI gets a "runners" view (who's live, what they host) for free.
- **Seam:** an `IRunnerRegistry` in the durability layer (`RegisterAsync`, `HeartbeatAsync`,
  `ListAsync`, `PruneAsync(before)`), implemented per backend like the run/catalog stores. The control plane
  reads it; runners write to it.

This pairs with the process split: registration is the only thing a runner *pushes* to the control plane;
everything else flows through the shared store.

## 6. "Make it available to run" — the trigger surface

This is the open question. A run is *started* by a **trigger**; the host owns the
`trigger → CreateNew(run) → RunAsync` path (the gap today — nothing starts a fresh run). We define a small
trigger abstraction and ship **HTTP first**, then message and schedule.

### 6.1 Recommended: a trigger abstraction, HTTP-first

```csharp
public interface IWorkflowTrigger : IAsyncDisposable
{
    // Raises StartRequested(inputs, correlationId?) to the host, which creates + dispatches a run.
    event Func<WorkflowStartRequest, CancellationToken, ValueTask<WorkflowRunId>> StartRequested;
    ValueTask StartListeningAsync(HostedWorkflowBinding binding, CancellationToken ct);
}
```

With the process split, triggers live in two places but converge on one **start path** —
`CreateNew(store, newId, "{base}-v{n}", inputs, …)` as a `Pending` run, which a hosting runner then claims and
executes (§7):

- **HTTP** is owned by the **control plane** (it already fronts catalog + runs): it validates inputs, checks the
  registry that a live runner hosts the version, and creates the `Pending` run.
- **Message / schedule** are owned by **runners** (they hold the transports + scheduler): they create the
  `Pending` run locally and typically claim it immediately.

### 6.2 HTTP trigger (ship first — recommended default)

`POST /arazzo/v1/workflows/{baseWorkflowId}/versions/{versionNumber}/runs`

- Served by the **control plane**. Body: the workflow's **inputs** (validated against the baked inputs schema —
  reuse the `/validate` machinery). It checks the registry (§5.4) that a **live runner hosts this version**;
  if none, `409 Conflict` ("no runner available"). Otherwise it **creates a `Pending` run** and returns
  `202 Accepted` + the run id + a `Location` to the control-plane run resource.
- The run then executes **asynchronously and durably** on a hosting runner (§7); the caller polls/streams via
  the existing control-plane run endpoints. Optional `?wait=...` makes the control plane poll the store for a
  bounded time and return the terminal result (convenience for short workflows / tests).
- Why first: universal, no broker dependency, integrates cleanly with the existing control plane, trivially
  demoable, and it's the "publish the workflow at a configured, secured endpoint" from `catalog-design.md`.
  Versioned in the path so callers pin a version (a `…/workflows/{base}/runs` alias can resolve latest-active).
- Note the control plane never executes the workflow — it only *creates* the run; the data path is the store.

### 6.3 Message trigger (second)

A workflow may be *initiated* by an inbound event (distinct from Tier-2 resume, which wakes an
already-suspended run). The host subscribes (via `IMessageTransport`) to a configured **start channel**; each
inbound message → `CreateNew` with the message payload mapped to inputs → dispatch. Natural for event-driven
workflows; needs the broker binding (§8) and idempotency keying (correlationId) to avoid duplicate runs. The
**channel + payload→inputs mapping live in the runner's trigger binding (host config)** — decision §12.

### 6.4 Schedule trigger (third)

Cron-like initiation (e.g. `nightly-reconcile`). The host's scheduler fires → `CreateNew` with templated inputs
(e.g. `{ "date": "<today>" }`). Reuses the same start path. The **schedule + input template live in the
runner's trigger binding (host config)** — decision §12.

### 6.5 Where triggers are declared — decision

Two options:

- **(A) Host configuration** (recommended first): triggers are bound out-of-band in the host's config, keyed by
  `(base, version)`. Keeps the Arazzo document pure/portable and lets ops bind the same workflow differently
  per environment. HTTP needs *no* declaration (the endpoint exists for every runnable version).
- **(B) Declared in the package** via an `x-arazzo-triggers` extension, baked into `executor-manifest.json`
  `triggers[]`. Self-describing and portable, but couples deployment intent into the versioned artifact.

**Decision (A, HTTP always-on):** **HTTP is always available** for any runnable version (no declaration);
message/schedule triggers are **host-configured** initially, with an optional declared-trigger manifest
(`x-arazzo-triggers` → manifest `triggers[]`) as a later convenience. The manifest already reserves the
`triggers` array for that.

## 7. Execution model and concurrency (in the runner)

### 7.1 Dispatch via the store (store-as-queue)

A trigger only *creates* a `Pending` run; runners pick it up. The run record **is** the durable work item, so
the store is the queue — no second system, no dual-write/outbox, and one concurrency mechanism (CAS + leases)
serves both dispatch and resume.

- **Dispatch index (new store capability).** `IWorkflowDispatchIndex` (sibling to `IWorkflowWaitIndex`):
  `QueryClaimableAsync(hostedVersions, now, ct)` returns runs the runner may take for versions it hosts —
  namely **`Pending` runs** *and* **`Running` runs whose lease has expired** (orphans left by a crashed
  runner; see §7.3). Returning orphans here is essential — otherwise a run interrupted mid-step would never be
  reclaimed.
- **The runner's dispatcher** mirrors `WorkflowWorker`: poll `QueryClaimableAsync` → take a **per-run lease**
  (CAS; skip if held and unexpired) → resolve `IHostedWorkflow` → build `IApiTransport`/`IMessageTransport`
  (§8) → `RunAsync(..., run)` → checkpoint/suspend/fault. For **runner-owned triggers** (message/schedule) the
  same runner usually claims its own `Pending` run immediately (no poll latency).
- **Optional doorbell.** To cut poll latency, a lightweight "work available for version V" notification (e.g.
  Postgres `LISTEN/NOTIFY`, or the message transport) can *wake* runners to query sooner. It is only a hint —
  the store stays authoritative, so a missed notification costs latency, never correctness.

### 7.2 Resume shares the path

Resume also lives in the runner: the existing `WorkflowWorker` polls the **wait** index for due timers /
delivered messages and calls the same `IHostedWorkflow.RunAsync` (it *is* the `WorkflowResumer`). One
`workflowId → IHostedWorkflow` resolver serves new-run dispatch, orphan reclaim, and wait-resume — they are all
"load checkpoint → lease → re-call `ExecuteAsync(run)` → tri-state."

### 7.3 Leases, single-execution, and shutdown

- **Single-execution** across runners is guaranteed by the store's CAS + leases. A claimer holds a per-run
  lease (`owner=runnerId`, TTL) and **renews it (keep-alive) while executing** so long steps don't let the
  lease lapse mid-run; every checkpoint is a CAS write, so a slow/zombie runner whose lease expired and was
  taken over **fails its next CAS and aborts** — no split-brain, no double commit.
- **Crash → orphan reclaim:** a dead runner stops renewing; after the TTL its `Running` run is surfaced by
  `QueryClaimableAsync` and another runner loads the last checkpoint and re-enters at the restored cursor.
  Recovery latency on a hard crash ≈ the lease TTL.
- **Graceful shutdown** (scale-down, version delete/unload): the runner should **release leases** (and/or
  checkpoint-and-suspend in-flight runs) so peers pick them up *immediately* rather than waiting out the TTL.
- **Backpressure / fairness:** bounded dispatcher concurrency per runner (advertised as `maxConcurrency` in the
  registry); per-tenant queues; both indexes are pull-based, so no busy-loop and runners self-balance by
  claiming what they can.

## 8. Transport binding

The executor calls source operations through `IApiTransport`; the host must map each version's **source
descriptions** to real endpoints + credentials:

- **Config:** per `(source name)` → base URL + auth (token provider / mTLS). Resolution can be per-environment
  and per-version. An `HttpClient`-backed `IApiTransport` with the source's base URL; one transport per run (or
  pooled) carrying the run's auth context.
- **Messaging:** the AsyncAPI source binds to a broker via `IMessageTransport` (in-memory for the demo; a real
  broker adapter in production). The same transport serves receive-steps and message triggers.
- The manifest's `sources[]` lists what bindings a version needs, so the host can **fail fast** at load if a
  required binding is missing.

## 9. Versioning, isolation, security

- **Version pinning:** runs are pinned to the `{base}-vN` they were triggered on (already how runs reference
  versions). Obsoleting a version stops *new* triggers but lets in-flight runs finish/resume.
- **Isolation:** collectible ALC per version; optional per-run timeouts and memory ceilings; consider an
  out-of-process executor pool for untrusted workflows (future).
- **Signature verification** before load (over the package hash) — the trust boundary between "catalogued" and
  "executed".
- **AuthZ:** triggering a workflow is a new capability (e.g. `workflows:run`) alongside the existing
  `runs:*` / `catalog:*` scopes; the HTTP trigger enforces it via the existing security-convention seam.
- **Multi-tenancy:** the store already carries `tags`/`correlationId`; the host scopes triggers + transports per
  tenant.

## 10. Failure modes and observability

- Build-side compile failure → `runnable: false`, non-fatal (version still catalogued).
- Load failure (integrity mismatch, TFM mismatch, missing binding) → version marked unloadable; HTTP trigger
  returns `409`/`422`; telemetry event.
- Run failure → the existing `Faulted` path + control-plane resume modes (retry/rewind/skip/state-patch).
- **Runner death** → its leases expire (TTL); orphaned `Running` runs are reclaimed by another runner hosting
  the version via `QueryClaimableAsync` and resumed from the last checkpoint (§7.3). Correctness comes from
  lease+CAS, **not** the registry — the registry only affects *visibility* and trigger gating, so a slow
  registry never strands work. If **no** live runner hosts the version, its runs wait (visible via the
  registry; new triggers `409`) until one is brought up.
- **At-least-once step execution.** Reclaim/resume re-runs the *interrupted* step, so request bindings should
  carry idempotency keys (or use replay-tolerant success criteria). Completed steps are not re-run — only their
  products are persisted, and the in-flight step is redone. This is inherent to durable execution.
- Telemetry: reuse plan §9.7 (per-step spans, checkpoint counts); add load/unload, trigger, dispatch, claim,
  and orphan-reclaim spans. The control plane already gives run visibility.

## 11. Phased delivery

1. **Executor provider + packaging** — `IWorkflowExecutorProvider`, `WorkflowExecutorEmitter` + clients →
   one assembly; `metadata/executor.dll` + `executor-manifest.json` in the package; `$executor` raw-stream
   endpoint; `runnable` flag. (Build side only; no host yet.)
2. **Loader + `IHostedWorkflow`** — generated adapter; collectible-ALC loader with signature verify + cache;
   a `workflowId → IHostedWorkflow` resolver that doubles as the `WorkflowResumer`. Wire it into the existing
   worker/management client so **resume** works on real loaded assemblies.
3. **HTTP trigger + dispatcher** — the start path (`CreateNew` + dispatch), the `…/runs` POST, inputs validated
   via `/validate`. End-to-end: trigger → durable run → resume.
4. **Message + schedule triggers**, transport binding config (incl. multi-source per-source binding),
   signing, isolation hardening.
5. **Control-plane operation authorization (§14.1)** — capability scopes as ASP.NET Core policies on the
   endpoints; the auth scheme + claim→policy mapping is per-deployment, with a concrete strategy implemented
   in the sample.
6. **Source credentials (§13)** — `ISourceCredentialStore` (encrypted/referenced, per backend); the transport
   binding resolves auth providers from it per run; per-version `credentialStatus` + expiry telemetry +
   trigger gating; typed `credentials-expired` fault refreshable from the catalog and resumable.
7. **Row security — security tags + rule engine (§14.2)** — security KVP labels on runs/catalog versions
   (separate from user tags; runs inherit the version's); tag rules in the `simple`-criterion grammar that
   claims resolve to; rules compiled to an in-memory evaluator **and** an indexed per-backend store predicate
   (reference-then-fan-out across ~18 stores); a separate security API in the control plane to manage rules,
   seeded with bootstrap rules (tenant-scoped / ABAC label-superset / intersection); plus the deployment
   access-control shell (§14.3) — reserved-prefix immutable, client-invisible internal tags + a mandated
   wrapper rule ANDed into every decision for inescapable multi-tenant isolation.

The paused demo work (`samples/.../docs/live-execution.md`) becomes the *manual* prototype of Phase 1–3 (it
hand-builds the binder + compiles in-process); this design productionises it behind the catalog.

## 12. Decisions

**Resolved:**

- **Topology — separate processes (§2).** Control plane and runner are distinct services sharing the store,
  scaled independently. Established from the start.
- **Assembly packaging — single fat assembly per version (§3.2).** Minimal load-time closure (only the stable
  Corvus runtime).
- **Integrity, not code-signing (§3.3).** The manifest binds the assembly to the version via `assemblyDigest`
  + `packageHash`; the runner verifies the binding. A cryptographic signature is reserved but optional/later.
- **Unload on delete (§5).** Deleting (or obsoleting) a version unloads its collectible ALC promptly — stop new
  runs, drain in-flight, dispose.
- **Runner registry + health (§5.4).** Runners register + heartbeat; the control plane surfaces which runners
  host which versions and which are live (`GET /runners`), and gates triggers on a live host.
- **Store-as-queue dispatch (§7).** The control plane creates a `Pending` run; runners claim it (and
  lease-expired orphans) from a dispatch index — no separate queue. Correctness is lease+CAS; an optional
  doorbell may cut poll latency without becoming the source of truth.
- **Trigger declaration — HTTP always-on (§6.5).** HTTP needs no declaration (available for every runnable
  version); message/schedule triggers are **host-configured** initially, with `x-arazzo-triggers` in the
  package as a later convenience.
- **First execution — async by default (§6.2).** A trigger creates a `Pending` run and returns `202` + run id;
  the run executes durably and is observed via the control plane. `?wait` offers a bounded synchronous result
  for short workflows / tests.
- **Non-HTTP trigger inputs — in the runner's trigger binding (host config) (§6.3/§6.4).** The start channel +
  payload→inputs mapping (message) and the schedule + input template (schedule) live in host config, moving
  into the optional declared-trigger manifest later.

All design decisions are resolved; remaining detail (transport-binding config schema, the declared-trigger
manifest shape) is deferred to implementation phasing (§11).

## 13. Source credentials — storage, lifecycle, refresh

Sources need credentials (bearer tokens, OAuth client-credentials, API keys, mTLS certs — §8). The run
requester must **never** supply them: a run carries only inputs. Credentials are **host/operator-managed
state**, bound to the catalog version's sources and resolved per run by the transport binding (§8), so
rotation is transparent — the next run/resume picks up the current secret without changing the workflow or
the request.

### 13.1 Credential store and binding

- **`ISourceCredentialStore`** in the durability layer (per-backend, like the run/catalog/registry stores),
  holding a `SourceCredential` per `(sourceName, environment/tenant)`:
  `{ sourceName, kind (bearer | oauth-client-credentials | api-key | mtls), secretRef, expiresAt?, rotatedAt }`.
  Secret material is **encrypted at rest** via the existing protected-store mechanism (KeyVault/KMS
  backends already exist), or `secretRef` points at an external secret (a KeyVault URI) the host dereferences
  — secrets need not live in the Arazzo store at all.
- **Binding.** `WorkflowTransportRegistry` (§8) resolves each source's `IApiTransportFactory` from the
  credential store: it builds the `IHttpAuthenticationProvider` (`BearerTokenAuthenticationProvider`,
  `ApiKeyAuthenticationProvider`, … already exist) from the stored credential. For
  **oauth-client-credentials** the provider holds the long-lived client id/secret and fetches + caches a
  short-lived access token at runtime, so *access-token* expiry is handled automatically by re-fetching; only
  the **long-lived** secret (client secret, refresh token, API key, cert) is what §13.2 tracks for operator
  rotation.
- **No per-run credentials, no per-requester secrets.** The seam already established (`IApiTransportFactory`
  per source) means credential resolution is entirely host-side; the trigger surface (§6) is unchanged.

### 13.2 Expiry tracking, states, and telemetry

- Each `SourceCredential` carries `expiresAt` when knowable (cert `NotAfter`, API-key/refresh-token lifetime).
- A catalog version derives a **`credentialStatus`** — `Valid` | `ExpiringSoon(at)` | `Expired` — as the worst
  status across the sources it binds (min `expiresAt`). It surfaces on the version's control-plane GET
  endpoints and the UI. The catalog list endpoint accepts a `credentialStatus` filter (indexed), so the
  **catalog UI can filter to active workflows with expiring/expired credentials** — the operator's primary
  rotation worklist.
- A control-plane **credential monitor** (a periodic sweep, like the runner-registry prune §5.4) evaluates
  credentials and:
  - **emits telemetry** so operators build their own alerting/rotation rules (we expose the signal, not a
    built-in scheduler): an `arazzo.credential.expires_at` gauge and `arazzo.credential.expired` counter,
    tagged by `sourceName` / `baseWorkflowId` / `versionNumber`. This is the "auto-reminder" — surfaced in
    OpenTelemetry and the UI's version view.
  - when a credential is **expired**, marks the version's binding **`Credentials Expired`** — a degraded,
    non-runnable state that gates *new* triggers (`409`, like the no-live-runner gate §6.2) while leaving
    catalogued/in-flight state intact.

### 13.3 Faulted run → refresh → resume

- A run that fails because a source rejected its credential (`401`/`403`, or the binding throws a
  credential-expired error) records a **typed fault**: `Faulted` with `errorType = "credentials-expired"` and
  the offending source — distinguishable from ordinary faults and **filterable** in the control plane.
- An operator **refreshes the credential in the catalog** (uploads a new secret / re-runs OAuth consent /
  rotates the cert) via a control-plane credential endpoint. The refresh lives with the catalog version's
  source binding, not the run.
- **Resume** uses the existing machinery (retry/rewind §7.2): because the transport binding resolves
  credentials from the store **at bind time**, the resumed run picks up the refreshed credential
  automatically — the original requester is not involved. At-least-once step re-execution (§10) re-runs the
  interrupted step against the now-valid credential and continues from the last checkpoint.

**Decision (§13):** credentials are operator-managed catalog state, encrypted/referenced, resolved per run by
the transport binding; expiry is surfaced as version status + telemetry (operators own the rotation policy);
a `credentials-expired` fault is refreshable from the catalog and resumable with no requester involvement.

## 14. Authorization — control plane and tag-based row security

Two layers: **operation** authorization (can this principal call this endpoint at all) and **row**
authorization (which workflows/runs can it see or act on). The control plane is ASP.NET Core; the mechanism
is standard and **per-deployment configurable**, with a concrete strategy shipped in the sample.

### 14.1 Operation authorization (capability scopes)

- The control plane ships **capability scopes as authorization policy names** — `catalog:read`,
  `catalog:write`, `runs:read`, `runs:write`, `workflows:run`, `credentials:write` (§9, §13) — and each
  endpoint declares its requirement (`.RequireAuthorization("workflows:run")`).
- The **deployment** supplies authentication (any ASP.NET Core scheme — JWT bearer / OIDC / mTLS) and the
  claim→policy mapping (`AddAuthentication().Add…` + `AddAuthorization`). The control plane does **not**
  hard-code an identity provider; it depends only on `ClaimsPrincipal` + the named policies. This is the
  "configurable per deployment" seam.
- The **sample** implements one concrete strategy (JWT bearer with a `scope` claim mapped to the policies,
  plus a dev API-key scheme) to demonstrate end to end.

### 14.2 Row-level security — security tags + rule engine

Row authorization decides **which** workflows (catalog versions) and runs a principal may see or act on. It is
**not** the free-form user `tags` (those stay as user-facing, AND-filtered metadata). It is a separate concept:

- **Security tags** are **key/value pairs** (labels) on a row — e.g. `tenant=acme`, `team=payments`,
  `classification=restricted`. They are set when the row is created (a run **inherits** its workflow version's
  security tags; a catalog version is labelled when added) and are distinct from user tags.
- **Tag rules** are boolean expressions over those labels, written in (a reuse of) the **Arazzo `simple`
  criterion grammar** — the same `==`/`!=`/`<`/`<=`/`>`/`>=`, `&&`/`||`/`!`/grouping engine already inlined for
  step criteria (`SimpleConditionEvaluator` runtime + `SimpleCriterionInliner` codegen, over `Comparand`).
  Example: `tenant == 'acme' && (team == 'payments' || team == 'billing')`. Real-world access is richer than
  "this tag AND that tag", which is exactly why a small expression language — not a fixed KVP match — is used.
- **A principal's claims resolve to a well-defined rule** — their effective access predicate. Rules reference
  both **literals** and **claim values** (e.g. `tenant == $claim.tenant`), so one parameterised rule serves
  many principals; the principal's claims supply the parameter values at evaluation time.
- **Rules compile to emitted evaluators.** Because the grammar is the `simple` one, a rule is compiled the same
  way step criteria are — into (a) an efficient in-memory evaluator, and (b) a per-backend **store predicate**
  (the grammar maps cleanly to SQL/NoSQL boolean `WHERE`s over the security-tag storage). So row filtering is
  **pushed into the store as an indexed query** (never scan-then-filter, per §5.4), and a single-row access
  check (get-by-id, write/trigger) runs the in-memory evaluator → `403` when the rule is unsatisfied.
- **A separate security-focused API in the control plane** authors and manages the rules and the claim→rule
  mapping (its own capability scopes, e.g. `security:read`/`security:write`), kept apart from the run/catalog
  operational surface. Rules are versioned state; changing a rule re-emits its evaluator/predicate.
- **Bootstrap rules.** The system seeds a set of common, ready-to-use rules at initialization so the model is
  usable from the start — **tenant-scoped** (one designated key must match the principal's value, e.g.
  `tenant == $claim.tenant`), **ABAC label-superset** (the principal must satisfy every label the row carries),
  and **intersection** (the principal shares at least one label with the row). These are ordinary rules, not
  hard-coded behaviour: a deployment uses them as-is, edits them, or removes them via the security API.
- The layers compose: scopes (§14.1) gate the **operation**; the resolved tag rule gates the **rows**. A
  `runs:read` principal lists runs, but only those whose security tags satisfy its rule.

**Open/assumed for implementation** (revise as the security API design firms up): unlabelled rows are visible
only to a rule that admits them (default-deny is the safer posture once a principal has a non-trivial rule);
the rule grammar may need an `in (...)` set operator and null/absent-label handling beyond the step-criterion
subset; and the store-predicate translation is per backend (~18 stores) so it follows the established
reference-then-fan-out pattern.

### 14.3 Deployment access-control shell — mandated filters + internal tags

A deployment can **wrap** the row-security model so its own constraints are inescapable — e.g. mandate that
every principal is filtered to its own tenant/customer/organization. Users author their tags and rules
*within* that shell; they cannot reach outside it. This is what keeps one shared-hosting tenant from leaking
into another even if a user rule is misconfigured.

- **Internal (deployment) security tags** are marked by a **reserved key prefix** (deployment-configurable,
  e.g. `sys:`). They are:
  - **immutable** — set by the deployment at row creation (e.g. the tenant resolved from the principal /
    hosting context), never editable through the user-facing API;
  - **invisible to clients** — stripped from catalog/run read responses so the isolation labels are not
    disclosed;
  - **reserved on input** — the API rejects any user attempt to create or edit a security tag (or reference a
    rule operand) whose key carries the internal prefix. End-users own the unprefixed keyspace only.
- **Mandated wrapper rule (defense in depth).** The effective access decision is the deployment's mandated
  wrapper rule **AND** the principal's resolved user rule — both must hold. A user rule can therefore only
  *narrow* within the shell, never widen past it. The wrapper references internal tags
  (e.g. `sys:tenant == $claim.tenant`) and is **ANDed into the store predicate** alongside the user rule, so
  tenant isolation is enforced on every query and single-row check, pushed down to the store.
- **Hooks:** security-tag key validation (reject the reserved prefix from user input); a deployment-configured
  access-control wrapper (the mandated rule + an internal-tag injector at row creation + a response stripper);
  the compiled predicate becomes `wrapperPredicate AND userPredicate`. The wrapper is per-deployment
  configuration, like the auth scheme (§14.1) — the sample demonstrates a tenant shell.

**Decision (§14):** operation authz = ASP.NET Core policies named after capability scopes, with the scheme +
claim mapping supplied per deployment (sample-implemented). Row authz = **security tags (KVP labels) + tag
rules in the `simple`-criterion grammar**; claims resolve to a rule, rules compile to an in-memory evaluator
**and** an indexed per-backend store predicate, and a separate security API in the control plane manages the
rules. A deployment may **wrap** the model (§14.3) with a mandated filter + reserved-prefix internal tags
(immutable, client-invisible) that AND into every decision, so multi-tenant isolation is inescapable. Applied
uniformly to workflows and runs.
