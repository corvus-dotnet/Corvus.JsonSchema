# Arazzo workflow execution host — design

> Status: **design / proposal**. Extends the "Future phase" of [`catalog-design.md`](../catalog/catalog-design.md)
> (compile-to-assembly + dynamic-load hosting) and the durability execution model of
> [`../ArazzoWorkflowEnginePlan.md`](../../../ArazzoWorkflowEnginePlan.md) §9 (checkpoint/resume, Tier 1/Tier 2).
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
[`validator-preservecompilationcontext`](../../../../samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo/docs/live-execution.md)).

### 3.3 Packaging, hash, and signing

Add to the package, following the `metadata/schemas.json` precedent exactly:

| Entry | Constant | Notes |
|---|---|---|
| `metadata/executor.dll` | `ExecutorEntryName` | the compiled assembly bytes (binary entry) |
| `metadata/executor-manifest.json` | `ExecutorManifestEntryName` | descriptor (below) |

Reserved document name `$executor` / `$executorManifest` for `GetDocument`. The **content hash is unchanged by
construction** — it canonicalises only `{ workflow, sources }`, never the container framing or `metadata/*`.

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
  "sources": [ { "name": "onboarding", "kind": "openapi" }, { "name": "events", "kind": "asyncapi" } ]
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
- This *is* the `WorkflowResumer` the existing `WorkflowWorker`/`SecuredWorkflowManagement` already expect — so
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

### 5.5 Runner ↔ environment binding, authorization, and reach scoping (IMPLEMENTED)

> Status: the decisions below shipped end-to-end — `RunnerRegistration` carries `environment`/`reachTags`, the
> env-admin authorization inbox is live (`/runnerAuthorizations` + `/environments/{name}/runners/{runnerId}/authorization`),
> runs are environment-pinned at creation and runners claim only their exact environment. §5.6's in-process
> collectible-ALC backend ships; the out-of-process backends (micro-guest / serverless / container-per-run) and
> the runner machine-principal identity remain design intent.

The §5.4 registry above is environment-agnostic and unscoped: a flat global list of processes with no notion of
*which environment* a runner serves or *whose* it is. That is the gap the runner-to-environment binding closes
([ADR 0027](../../adr/0027-runner-environment-binding.md)): a runner executes in and for an environment and
must have that environment's credential set. It is
also an inconsistency: environments are first-class, governed, **reach-scoped** resources
(`Environment.managementTags`, e.g. `tenant=acme`, §14.2); availability is per `(workflow, version, environment)`
(§7.8); source credentials and base URLs resolve **per environment** (the transport binder is literally
`CreateBinder(sources, environment, cache)`, §8/§13.1). The runner is the one link in that chain that is neither
environment-associated nor reach-scoped — so the control plane cannot say *which runners serve production*, cannot
route a run to a runner that can actually reach its target environment, and gives no multi-tenant isolation of
execution capacity (every caller sees every runner).

**Decisions (resolved).**

- **One environment per runner; many runners per environment.** A runner process is provisioned for exactly one
  environment's network reach + credential set, and registers as serving that one environment. An environment may
  be served by any number of runners (capacity/HA). A host that must serve several environments runs several
  runner processes (one per environment) — this keeps the credential/network blast-radius of a process to a
  single environment.
- **A runner's reach is its environment's reach.** At registration the runner inherits (is stamped with) the
  target environment's `managementTags`. Everything in §14.2/§14.4 then composes for free: the registry list is
  reach-filtered by the caller's `AccessContext` over those tags, so a tenant sees only the runners serving its
  environments.
- **The environment's administrators must authorize which runners may serve it.** A runner may *not* self-assert
  into an environment — receiving an environment's runs means receiving its credentials, so it is a security
  boundary. A runner registering for environment *E* enters a **`Pending`** authorization state and is *not*
  dispatchable until an administrator of *E* (§15.1, the `EnvironmentAdministrators` set) authorizes it. This
  reuses the §15.4 approver-inbox pattern (the same surface that approves availability/access requests).
- **Two ways to take a runner out of the pool, for two different situations.** A **faulted** runner is
  *quarantined*: it stops receiving new and orphaned work, but its in-flight runs are left to drain, and it is
  *reinstated* to `Authorized` by authorizing it again, with no re-registration. A **compromised** runner is
  *revoked*: this is permanent, and the runner returns only by a deliberate re-authorization. Revoke also *fences*
  its in-flight work. The dispatch gate stops new claims within a poll, but a run the runner already leased would
  otherwise run to completion, so revoke additionally expires every lease the runner holds through the store's
  lease-administration capability. An authorized peer then reclaims those runs at once, and the revoked runner's
  own next checkpoint write conflicts under optimistic concurrency. The fence is enforced in the store, not on the
  runner (a cooperative self-check is worthless against a compromised, attacker-controlled runner). The lifecycle
  is therefore `Pending -> Authorized`, `Authorized <-> Quarantined` (quarantine/reinstate), and
  `any -> Revoked -> Authorized` (revoke/re-authorize). Only `Authorized` is dispatchable.
- **Run→environment pinning — folded in now.** A run is pinned to an environment at start (`§7.7` already says "a
  run targets an environment and uses that environment's credential set"). `startCatalogWorkflowRun` takes a
  required `environment`; the surface validates it against the version's availability (§7.8) and the caller's
  reach; the run record (and dispatch index) carry it; the runner resolves *that* environment's credentials.

**How dispatch composes (§7.1).** The store-as-queue dispatch already filters claimable `Pending` runs by reach +
hosted version. Add the environment predicate: a `Pending` run pinned to environment *E* is claimable only by a
runner that is (a) `Authorized` + live for *E*, (b) hosting the version, (c) within the caller/run reach — which,
because the runner inherited *E*'s management tags, is automatically true for runners of *E*. The credential binder the
winning runner builds is already per-environment, so the loop closes: a production run can only land on a runner
provisioned for, and authorized in, production.

**Schema sketch (draft).** Additions to `RunnerRegistration.json`, a new governed `EnvironmentRunnerAuthorization`,
and `environment` on the run:

```jsonc
// RunnerRegistration (added properties)
"environment":   { "type": "string", "description": "The single deployment environment this runner serves; its sources/credentials/network reach." },
"reachTags":     { "type": "array", "items": { "$ref": "#/$defs/SecurityTagInfo" },
                   "description": "Stamped from the environment's managementTags at registration; the runner's row-security reach (§14.2)." },
"authorization": { "type": "string", "enum": ["pending", "authorized", "revoked"],
                   "description": "Whether an administrator of the environment has authorized this runner to serve it; only 'authorized' runners are dispatchable." }
// required adds: "environment"

// EnvironmentRunnerAuthorization (new, governed by the environment's administrators — like Availability)
{ "title": "EnvironmentRunnerAuthorization",
  "required": ["environment", "runnerId", "status", "decidedBy", "decidedAt", "etag"],
  "properties": {
    "environment": { "type": "string" },
    "runnerId":    { "type": "string" },
    "status":      { "type": "string", "enum": ["pending", "authorized", "quarantined", "revoked"] },
    "decidedBy":   { "type": "string" },
    "decidedAt":   { "type": "string", "format": "date-time" },
    "etag":        { "type": "string" } } }

// WorkflowRun checkpoint/index (added)
"environment": { "type": "string", "description": "The environment the run targets; selects the credential set and constrains dispatch to runners serving it (§7.7)." }
```

**API surface sketch (draft).**

- **Runner registration (runner-side seam, `IRunnerRegistry.RegisterAsync`)** now requires `environment`. If the
  runner is not yet authorized for it, registration succeeds in `Pending` and the runner is listed but not
  dispatchable.
- `GET /arazzo/v1/runners` — **reach-scoped** (was global); optional `?environment=` filter; each row carries
  `environment` + `authorization`.
- `GET /arazzo/v1/environments/{name}/runners` — runners serving an environment with their authorization status
  (governed by the environment's administrators).
- `POST /arazzo/v1/environments/{name}/runners/{runnerId}/authorization` (+ `DELETE` to revoke) — an environment
  administrator authorizes/revokes a runner; appears in the approver inbox (§15.4). The `POST` also reinstates a
  quarantined runner and re-authorizes a revoked one (any non-authorized state to `Authorized`); the `DELETE`
  revoke fences the runner's in-flight leases.
- `POST /arazzo/v1/environments/{name}/runners/{runnerId}/quarantine` — temporarily excludes a faulted runner
  (drains in-flight); only an authorized runner may be quarantined (`409` otherwise).
- `startCatalogWorkflowRun` request body gains a required `environment`, validated against availability (§7.8) +
  reach; surfaced on the run (the runs UI already wants this, §7.7).

**Resolved (§16.4 machine-principal registration).** The runner's own identity is now a machine principal: it
registers through the control plane's authenticated `POST /environments/{name}/runners` endpoint as a machine
principal (client-credentials, private-key-JWT, or mTLS), and the control plane derives the trusted principal from
the presented token and binds the authorization to it, rather than to a self-asserted `runnerId`. A registration
presenting a principal that differs from the one already bound to a `runnerId` is refused (`409`) — the
anti-impersonation hardening. Both admission orders live on the same record: **pre-authorization** (an administrator
allow-lists a `runnerId` before any runner registers, creating an `Authorized` row with no bound principal) and
**register-then-approve** (the runner registers into `Pending`, then an administrator authorizes). A pre-authorized
row is name-based trust by the administrator's deliberate choice, so it is never bound to a principal — its trust
stays the administrator's allow-listing, gated by the runner still having to present a valid `runners:register`
machine token to register at all. **Migration is abandon / fresh-deploys-only:** every runner authorization is
`(environment, runnerId)`-keyed from introduction, so no flat environment-less rows exist to backfill; a deployment
that predates the environment-keyed record redeploys its runners rather than migrating rows.

### 5.6 Execution backends and isolation models (DRAFT — for review)

§5.4/§5.5 describe a runner as a process that loads the executor into a collectible ALC and runs it **in
process**. That is one execution model, not the only one. The runner is better understood as an *execution host*
that **dispatches a run to a pluggable execution backend** with a chosen isolation model — in-process today, but
equally a per-run micro-guest, a serverless function, or a container — and **wake-ups (resume) use the same
seam**.

**Why this is cheap to add — two properties already designed for it.** (1) The executor is a **portable,
self-contained, content-hashed compiled assembly** baked at catalog-add (§3) and dynamically loadable from the
package — it isn't tied to the control-plane process. (2) Execution is **resumable from a durable checkpoint**
(§7.2: "resume shares the path"); a run is a serializable state in the store, so *any* host that can load the
package and read/write the checkpoint can advance it one step (or to the next suspend) and persist. Together these
make *where* a run executes a late-bound, policy-driven choice rather than an architectural assumption. So this
section adds no new requirement to the core; it names a seam the existing shape already permits.

**The seam.** Generalise the in-process `HostedWorkflowResumer.RunAsync` into an execution-backend strategy —
`IRunExecutionBackend.AdvanceAsync(packageRef, checkpoint, environmentTransports, ct) → WorkflowRunResult`. The
`WorkflowResumer`/`WorkflowWorker` already invoke "advance this run" through a delegate, so the backend slots in
behind that delegate without touching dispatch, leases, timers, or message delivery. A runner advertises its
backend + isolation model in its registration capabilities (§5.4), and dispatch (§7.1) can match a run's required
isolation against it.

**Candidate models (a spectrum of isolation ↔ latency ↔ cost ↔ ops).**

- **In-process collectible ALC (ships first).** Lowest latency, version coexistence, clean unload; isolation
  boundary = the process. Best for trusted/first-party workflows and high throughput.
- **Per-run micro-guest (e.g. Hyperlight).** Spin a micro-VM/guest per run for hardware-grade isolation at
  ~sub-millisecond startup; the executor (native or a wasm build) runs in the guest with inputs + checkpoint
  marshalled in and the next checkpoint/result marshalled out. Best for untrusted or strongly multi-tenant
  workflows where per-run blast-radius containment matters.
- **Serverless function (AWS Lambda / Azure Functions).** A run — or a single step-to-next-suspend — executes as
  a function invocation: per-invocation isolation, elastic scale, no idle cost. The function loads the package +
  checkpoint by run id, advances, persists. The function's identity (IAM role / managed identity) *is* the
  environment's credential principal — which is exactly why §5.5 pins a runner/backend to **one** environment.
- **Container/pod-per-run (e.g. a k8s Job).** Another point on the spectrum — coarser startup, strong isolation,
  familiar ops.

**Warm capacity (cold-start avoidance).** A per-run backend need not pay a cold start every time — the runner is a
*capacity manager*, not just a dispatcher: it can keep a **warm pool** of instances already running with the
appropriate executor loaded, and assign an incoming run to a warm one. Two levels of warmth: *runtime-warm* (the
host/runtime + runner shell are up; a run still loads the version's ALC on assignment) and *version-warm* (the
version's executor is already loaded — instant). The §5.4 registry already models the version-warm signal:
`RunnerHostedVersion.loaded` ("the runner has the version loaded and ready to execute"), so **dispatch (§7.1) can
prefer a runner/instance that already has the run's version loaded** (`loaded: true`) — warm-pool routing falls
out of the existing readiness gate; for pooled backends each warm instance is effectively a sub-runner with its
own loaded-version set the runner aggregates. Some backends provide this natively (Lambda *provisioned
concurrency* / SnapStart, Azure Functions *always-ready* instances); for container/guest pools the runner manages
it explicitly. Keep-warm vs scale-to-zero is the cost ↔ latency dial and is **policy** (per environment/version):
hot paths hold warm capacity for the appropriate versions; cold paths scale to zero and accept the cold start.

*Isolation caveat for reuse.* Reusing a warm instance **across** runs is only safe where execution is stateless
per run — which the executor already is: `RunAsync` scopes a fresh `JsonWorkspace` per run, loads inputs +
checkpoint per run, and shares no mutable state between runs, so a warm in-process/container worker can serve many
runs back-to-back. Where the *point* is per-run VM isolation (Hyperlight), a warm pool means **pre-spawned,
single-use** guests (take one run, then recycle), or a **snapshot/restore** start (resume from a warm snapshot)
to get fast start *and* fresh isolation per run — i.e. warmth must respect the chosen isolation model, not defeat
it. Warm capacity is per-environment (a runner serves one, §5.5), so a pool is scoped to its environment's
credentials/network by construction.

**Wake-ups (resume) ride the same seam.** A due timer (§5.5/the timer model) or a delivered message (the message
model) is just a *trigger* that calls the backend to resume from the checkpoint. In-process: the worker poll loop
re-enters. Serverless: the trigger is an event source — EventBridge Scheduler / Azure Timer for due timers, a
queue/topic for messages — that invokes the function with the run id. Micro-guest: the runner spins a guest on the
trigger. Because resume == execute-from-checkpoint, wake-ups are backend-agnostic; only the *trigger plumbing*
differs per backend.

**Why §5.5 is the security anchor for out-of-process backends.** A Lambda or a guest executes with the
environment's credentials and network reach. The one-environment-per-runner + admin-authorized model (§5.5) is
therefore not just for the in-process case: it is the boundary that says *this function/guest is provisioned for,
and authorized in, exactly this environment* — the function's role holds that environment's secrets; an
unauthorized backend can never receive that environment's runs.

**Policy.** Required isolation is a routing input, not hard-wired: an environment policy ("require VM isolation")
or a per-version label ("untrusted") routes to a micro-guest/serverless backend, while a trusted high-throughput
version stays in-process. Dispatch matches the run's required isolation against runners' advertised backends.

**What an adapter needs (deferred to phasing, §11).** The checkpoint is already serialisable; the transport
binding (§8) must be constructible *inside* the backend (credentials resolved in-function/in-guest, per
environment — §13.1); the result is the tri-state `WorkflowRunResult` written back. In-process ships first;
each additional backend is a new `IRunExecutionBackend` + its wake-up trigger plumbing + a dispatch policy entry —
additive, not a re-architecture.

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
- **Message / schedule** are owned by **runners** (they host the trigger machinery): a message trigger is a
  dispatcher workflow (§6.5 / #880) and a schedule is a durable scheduler run (§6.4 / #896). Rather than creating
  the run locally, each fires the target through the **same governed control-plane start endpoint** HTTP uses, so
  all three triggers converge on one admission-controlled, reach-checked start path.

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

### 6.4 Schedule trigger (third) — durable cron (#896)

Cron-like initiation (e.g. `nightly-reconcile`). A **schedule is a durable _run_** of a built-in scheduler
workflow (`ScheduleHostedWorkflow`, the reserved `$schedule` workflow id), pinned to an environment. Its inputs
(`WorkflowScheduleInput`) carry the cadence: a cron expression, a time zone, the target versioned workflow id,
and an input template. On each entry the run evaluates the cadence, fires every occurrence now due, advances and
checkpoints a watermark, then suspends on a durable timer until the next occurrence. Because it is an ordinary
durable run it rides the existing wait index, CAS/lease dispatch, and environment pinning, so a million schedules
is a million suspended runs at the engine's existing scale, with no new per-backend store. A runner that was down
while an occurrence came due fires it late on resume rather than missing it.

Each occurrence fires the target through the **governed control-plane run endpoint**, not a direct local
`CreateNew`. The runner authenticates as its machine principal and carries an `Idempotency-Key` (`scheduleId`
plus occurrence), so a re-fire after a runner recycle starts the target at most once (`StartIdempotentAsync`
derives a deterministic run id from `(workflowId, key)`). A scheduled start is therefore subject to the same
admission control, environment pinning, availability (§7.8), reach (§14.2), and audit as an operator start. In
particular the scheduling principal needs read reach to the target version AND to the environment it fires into
(§5.5), exactly as a human trigger does, so a scheduling runner is granted that reach as a deliberate, bounded
governance grant (read reach to what it schedules, plus its environment). It does not inherit blanket authority.

A runner opts in to serving schedules with `servesSchedules`, advertised in its registration, so the control
plane can report which environments have a scheduling-capable runner and refuse to create a schedule in one that
none serves. The cadence lives in the schedule run's own inputs, so a schedule is **data**, created and managed
through the API like any run, not a package field or an out-of-band host-config binding.

### 6.5 Where triggers are declared — decision

Two options:

- **(A) Host configuration** (recommended first): triggers are bound out-of-band in the host's config, keyed by
  `(base, version)`. Keeps the Arazzo document pure/portable and lets ops bind the same workflow differently
  per environment. HTTP needs *no* declaration (the endpoint exists for every runnable version).
- **(B) Declared in the package** via an `x-arazzo-triggers` extension, baked into `executor-manifest.json`
  `triggers[]`. Self-describing and portable, but couples deployment intent into the versioned artifact.

**Decision (A, HTTP always-on):** **HTTP is always available** for any runnable version (no declaration);
message/schedule triggers are **host-configured**, keyed by `(base, version)`.

**Option B (`x-arazzo-triggers`) is declined.** It has no consumer and earns its keep nowhere: HTTP
initiation is the always-on `.../runs` endpoint (no declaration read); message/schedule *hosting* is the
dispatcher-workflow pattern (§6 / #880) — a durable `receive` → cross-workflow `goto` → loop expressed in
**standard Arazzo**, not a manifest field; and durable cron (#896) binds from the **schedule run's own inputs**
(`WorkflowScheduleInput`, §6.4), not the package. Beyond having nothing that reads it, the extension is a
proprietary `x-` addition that makes the Arazzo document non-portable (other tools ignore it), and it
conflates a **deployment concern** (what initiates a workflow in a given environment) with the workflow's
**definition** — the very separation host config preserves, letting one catalogued version bind to different
triggers per environment. The in-band dispatcher-workflow pattern already expresses "declarative triggers"
portably, so the extension would only duplicate it non-standardly. The `executor-manifest.json` `triggers[]`
array is therefore **not emitted** (a workflow that wants declarative triggering models it as a dispatcher
workflow instead).

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
  runner hosts the trigger machinery (the dispatcher workflow, or the durable `$schedule` run) and fires the
  target through the governed start endpoint, so the target run is then dispatched like any other (the doorbell
  below can cut its poll latency).
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
- **AuthZ:** triggering a run is gated by **`runs:write`** (the route is `POST /catalog/{baseWorkflowId}/versions/
  {versionNumber}/runs`, op `StartCatalogWorkflowRun`); there is no separate `workflows:run` scope. The HTTP trigger
  enforces it via the existing security-convention seam.
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
6. **Source credentials (§13)** — secret-store-first: `ISourceCredentialStore` persists a **secret reference +
   non-sensitive metadata** (never secret material), per backend; an `ISecretResolver` dereferences the reference
   to a real secret store at bind time (Key Vault / AWS Secrets Manager / HashiCorp Vault / env+file; an
   encrypted-in-DB fallback is discouraged). The transport binding builds auth providers from the resolved
   secret per run; per-version `credentialStatus` + expiry telemetry + trigger gating; typed
   `credentials-expired` fault refreshable from the catalog and resumable. **✅ Implemented** — binding store +
   resolvers + usage-security across all nine backends, the `/credentials` REST surface, lifecycle metadata with a
   derived `credentialStatus`, and a resumable `credentials-expired` fault (see §13.2 for what shipped vs. this
   sketch), plus the §15 `/administrators` API and an `arazzo-runs credentials`/`administrators` CLI, and the web UIs
   for both surfaces (the Connections credentials table + the catalog-detail administrators panel).
7. **Row security — security tags + rule engine (§14.2)** — security KVP labels on runs/catalog versions
   (separate from user tags; runs inherit the version's); tag rules in the `simple`-criterion grammar that
   claims resolve to; rules compiled to an in-memory evaluator **and** an indexed per-backend store predicate
   (reference-then-fan-out across ~18 stores); a separate security API in the control plane to manage rules,
   seeded with bootstrap rules (tenant-scoped / ABAC label-superset / intersection); plus the deployment
   access-control shell (§14.3) — reserved-prefix immutable, client-invisible internal tags + a mandated
   wrapper rule ANDed into every decision for inescapable multi-tenant isolation. Engine + InMemory store +
   shell + **control-plane HTTP enforcement (§14.4)** + the **per-backend predicate pushdown** (all ~18 stores,
   container-verified; fail-loud `ISupportsRowSecurityFilter` guard until a backend honors the filter) +
   **deny-by-default** (empty criteria / unclassified rows admit nothing) + the **security/bootstrap-rule API**
   (the `$claims.superset`/`$claims.intersects` grammar predicates, the persistent `ISecurityPolicyStore` of
   named rules + per-verb claim→rule bindings, the `PersistentRowSecurityPolicy` resolver, idempotent bootstrap
   seeding, the `/security/*` control-plane endpoints under `security:read`/`security:write` scopes, and the CLI
   `security rule`/`binding` commands) are done, and the `ISecurityPolicyStore` per-backend fan-out is complete —
   all eight production backends (Postgres, SqlServer, MySql, Redis, MongoDB, NATS JetStream, Azure Table Storage,
   Cosmos DB) implement it alongside the InMemory reference and SQLite, each persisting records as their
   Corvus.Text.Json schema documents with in-document-etag optimistic concurrency and a monotonic generation, and
   each passing the shared `SecurityPolicyStoreConformance` suite container-verified.

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
  version); message/schedule triggers are **host-configured**, keyed by `(base, version)`. The
  `x-arazzo-triggers` package extension is **declined** (§6.5): no consumer, non-portable, and it conflates a
  deployment concern with the workflow definition.
- **First execution — async by default (§6.2).** A trigger creates a `Pending` run and returns `202` + run id;
  the run executes durably and is observed via the control plane. `?wait` offers a bounded synchronous result
  for short workflows / tests.
- **Non-HTTP trigger inputs — in the runner's trigger binding (host config) (§6.3/§6.4).** The start channel +
  payload→inputs mapping (message) and the schedule + input template (schedule) live in host config. A workflow
  that wants declarative triggering models it as a dispatcher workflow (§6 / #880), in standard Arazzo.

All design decisions are resolved. The transport-binding config schema is implemented (§13); the
`x-arazzo-triggers` declared-trigger manifest shape is declined (§6.5).


## See also

This design was split so each subsystem stands on its own. The sections beyond the execution host now live in:

- [Source credentials: storage, lifecycle, and refresh](../credentials/source-credentials-design.md) (the former §13).
- [Access, identity, and entitlement design detail](../access/identity-and-authorization-design.md) (the former §14 to
  §17: authorization and row security, administration, the identity and entitlement lifecycle, and the
  security-review remediation).
