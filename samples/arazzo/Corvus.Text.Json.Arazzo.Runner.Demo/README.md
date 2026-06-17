# Arazzo runner — execution-host demo

The **execution-host ("runner")** — the second process in the real Arazzo topology
([execution-host-design.md §2](../../../docs/control-plane/execution-host-design.md)). The control plane creates
runs and owns the catalog; the runner *executes* them. The two never call each other on the hot path — they
cooperate through the **shared durability store**.

It's a **worker process** (its real-life deployment is a container, scaled independently for execution load), so
its long-running work is hosted `BackgroundService`s, not request handlers. The only HTTP surface is the §5.4
health probe (`/health`, `/alive`) — used by the AppHost's health check and container liveness/readiness probes.

## What it does

- **Registers + heartbeats** (`RunnerRegistrationService`, §5.4) in the shared `SqliteRunnerRegistry`, advertising
  the catalog versions it hosts. This is the only thing a runner *pushes* to the control plane — it's what the
  control plane's `GET /runners` reports and what gates triggers on a live host.
- **Dispatches + resumes** (`WorkflowDispatchService`, §7) from the store-as-queue: `WorkflowDispatcher` claims
  `Pending` runs and lease-expired `Running` orphans (a crashed runner's in-flight work); `WorkflowWorker` resumes
  suspended runs whose durable timer is due. Each is leased (CAS), so exactly one runner advances a run.
- **Resolves source credentials, read-only** (`VaultCredentialSelfCheckService`, §13/§13.5). The runner is the
  §13 secret *consumer*: it holds **only a read-only, path-scoped Vault token** (minted by the AppHost's separate
  provisioner — see [the design](../../../docs/control-plane/execution-host-design.md) §13.5). On startup it
  resolves every seeded credential reference against Vault to prove the wiring, then **asserts a write is refused
  (403)** — demonstrating the separation-of-duties boundary is real. It never writes secrets and never holds a
  write-capable token. (In production this resolution happens at transport-bind time during live execution, not
  on startup.)

## Live execution is the paused phase

Loading a version's compiled `executor.dll` into a collectible ALC and re-entering it (the
`HostedWorkflowResumer` path, design Phases 1–3) is **not wired yet** — it's the deliberately-banked
[live-execution work](../Corvus.Text.Json.Arazzo.ControlPlane.Demo/docs/live-execution.md). Until then the runner
uses a **stub resumer** that drives each claimed run to completion, so the dispatch / lease / orphan-reclaim /
resume plumbing is exercised end-to-end against the shared store. One visible consequence: the seeded orphaned
`Running` run is reclaimed and completed shortly after startup — orphan reclaim in action.

## Run it

Under the AppHost (the runner shares the store with the control plane and waits for it to seed):

```bash
cd samples/arazzo
aspire run
```

The dashboard shows both `controlplane` and `runner`; the runner's traces/logs/metrics (including the
`Corvus.Arazzo` workflow source/meter) flow there via the shared ServiceDefaults. Standalone
(`dotnet run --project samples/arazzo/Corvus.Text.Json.Arazzo.Runner.Demo`) it connects to the temp-file fallback
store and idles if nothing has seeded it.
