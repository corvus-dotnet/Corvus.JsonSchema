# Arazzo runner — execution-host demo

The **execution-host ("runner")** — the second process in the real Arazzo topology
([execution-host-design.md §2](../../../docs/control-plane/execution-host-design.md)). The control plane creates
runs and owns the catalog; the runner *executes* them. The two never call each other on the hot path — they
cooperate through the **shared durability store**.

It's a **worker process** (its real-life deployment is a container, scaled independently for execution load), so
its long-running work is hosted `BackgroundService`s, not request handlers. The only HTTP surface is the §5.4
health probe (`/health`, `/alive`) — used by the AppHost's health check and container liveness/readiness probes.

## What it does

- **Registers + heartbeats** (`RunnerRegistrationService`, §5.4) in the shared `PostgresRunnerRegistry`, advertising
  the catalog versions it hosts. This is the only thing a runner *pushes* to the control plane — it's what the
  control plane's `GET /runners` reports and what gates triggers on a live host.
- **Dispatches + resumes** (`WorkflowDispatchService`, §7) from the store-as-queue: `WorkflowDispatcher` claims
  `Pending` runs and lease-expired `Running` orphans (a crashed runner's in-flight work); `WorkflowWorker` resumes
  suspended runs whose durable timer is due. Each is leased (CAS), so exactly one runner advances a run.
- **Resolves source credentials, read-only** (`VaultCredentialSelfCheckService`, §13/§13.5). The runner is the
  §13 secret *consumer*: it holds **only a read-only, path-scoped Vault token** (minted by the AppHost's separate
  provisioner — see [the design](../../../docs/control-plane/source-credentials-design.md) §13.5). On startup it
  resolves every seeded credential reference against Vault to prove the wiring, then **asserts a write is refused
  (403)** — demonstrating the separation-of-duties boundary is real. It never writes secrets and never holds a
  write-capable token. (In production this resolution happens at transport-bind time during live execution, not
  on startup.)

## Live execution

The runner **executes catalogued runs for real** (design §11 Phase 2): `WorkflowDispatchService` drives each
claimed run through `HostedWorkflowResumer`, which loads the version's compiled `executor.dll` into a collectible
ALC (on first use, cached thereafter) and re-enters it against the runner's transports — the same live-execution
path the control-plane host runs in-process. Trigger one with
`POST /arazzo/v1/catalog/{id}/versions/{n}/runs?environment=development` and the runner claims it and drives it
through its steps to `Completed` / `Faulted` / `Suspended` against the environment's real source services (the
onboarding, ledger, and KYC services this sample composes).

Before it can dispatch, a runner must be **authorized** for its environment (§5.5): it registers a `Pending`
authorization and an administrator clears it — a runner never self-asserts. The open demo has no interactive
administrator, so the control plane's `RunnerAutoAuthorizationService` stands in for the `development`
environment's administrator and authorizes the runner on registration (production has a human admin do this via
the UI/API). Also visible: the seeded orphaned `Running` run is reclaimed and re-executed shortly after startup —
orphan reclaim in action.

## Run it

The runner is launched as part of the AppHost composition (it shares the store with the control plane and waits
for it to seed) — see the
[AppHost README](../Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost/README.md) for the `aspire start` command
and prerequisites. The dashboard shows both `controlplane` and `runner`; the runner's traces/logs/metrics
(including the `Corvus.Arazzo` workflow source/meter) flow there via the shared ServiceDefaults. It shares the
control plane's Postgres database and its source-service endpoints, so it only runs under the AppHost — launched
standalone it fails fast (`ConnectionStrings:workflowstore` is required), because there is no store to share.
