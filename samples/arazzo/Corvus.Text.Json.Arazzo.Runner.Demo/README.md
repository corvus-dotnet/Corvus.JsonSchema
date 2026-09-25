# Arazzo runner — execution-host demo

The **execution-host ("runner")** — the second process in the real Arazzo topology
([execution-host-design.md §2](../../../docs/arazzo/guides/execution-host.md)). The control plane creates
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
  provisioner — see [the design](../../../docs/arazzo/guides/source-credentials.md) §13.5). On startup it
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

## Sealed checkpoints

When `Runner:Sealing:Environments` names an environment, the runner builds a key ring at start (ADR 0065
decisions 5 and 10): for each entry it resolves the environment's payload key through its own Vault identity
(`PayloadKeyRef`, the base64 of a 32-byte key), derives the `envelope-mac` subkey once, and from then on every
checkpoint row it saves for that environment has its payload encrypted under a data key derived for that one save
and carries a MAC under the named generation (`KeyId`), and every row it loads is verified and opened before the run
sees it. The control plane, the store and a backup hold the payload as ciphertext; the run detail and step journal
they serve are the envelope, with the journal saying the payload is sealed. An entry marked `Sealed` refuses a clear
row on load, except a run's genesis row, which the control plane writes before any runner has claimed. A runner
configured for sealing that has no Vault refuses to start rather than serve the environment clear. The AppHost runs
a second instance of this project, `runner-production`, sealed for `production`.

## The tenant anchor

A sealed environment is also anchored (ADR 0065 decision 6). When the AppHost injects `ConnectionStrings:tenantanchors`,
the runner opens the tenant's own PostgreSQL database (a `tenant-postgres` instance the control plane never opens),
provisions its two tables itself, and passes the store to the runner client; a sealed key-ring entry with no anchor
store refuses to start. Every load of a production run then evaluates the anchor decision table over the row the
control plane holds before the run trusts it, and every save is staged with the tenant before it is dispatched, so a
control plane that rolls a production run back, substitutes a checkpoint or re-presents a finished run has the run
refused at the runner's next claim rather than advanced: the lease goes back, the refusal is counted, and the sweep
carries on. `Runner:Anchor:InitialIncarnation` (the AppHost sets `1`) records the tenant's first attestation of the
store incarnation when none exists, standing in for the operator's attestation at environment creation; a later
restore is attested by raising it, and no run in an environment with no attestation can be opened.

Production's message waits are blinded as well (ADR 0065 decision 4): `onboard-customer` suspends awaiting the KYC
verdict correlated by account id, and what the store and the control plane hold for that wait is the blind index
under production's key, not `kyc.verdict` and not the account id. The `KycVerdictResumeHandler` delivers the verdict
exactly as before; the runner client computes the index and claims by it.

## Run it

The runner is launched as part of the AppHost composition (it shares the store with the control plane and waits
for it to seed) — see the
[AppHost README](../Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost/README.md) for the `aspire start` command
and prerequisites. The dashboard shows both `controlplane` and `runner`; the runner's traces/logs/metrics
(including the `Corvus.Arazzo` workflow source/meter) flow there via the shared ServiceDefaults. It shares the
control plane's Postgres database and its source-service endpoints, so it only runs under the AppHost — launched
standalone it fails fast (`ConnectionStrings:workflowstore` is required), because there is no store to share.
