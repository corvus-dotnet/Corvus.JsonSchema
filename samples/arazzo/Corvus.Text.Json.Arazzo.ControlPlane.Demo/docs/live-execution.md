# Live workflow execution — current state

The demo **executes workflows live** against the real external services this sample composes (the onboarding,
ledger, and KYC services — each its own process + database — plus a real NATS JetStream message bus). It runs
both catalogued workflows and §18 working-copy debug runs, checkpointing, suspending, faulting, and resuming
against the shared Postgres store. These notes describe how it is wired (an earlier version of this file was a
plan for a paused spike; that work is done).

## Catalogued runs

A resumed run re-enters the executor baked into its catalogued version at add time.

- `PostgresWorkflowCatalogStore.ConnectAsync(..., executorProvider: new WorkflowExecutorProvider())` compiles a
  runnable executor into each version when it is added (alongside the typed metadata). No CLI verb, no
  hand-built binder. The code generator does the emit through the provider.
- `DemoData.CreateLiveResumer` builds a `HostedWorkflowResumer` over the catalog and a `WorkflowExecutorLoader`,
  bound to transports rooted at the sample's real services (`DemoData.CreateLiveBinder`). It returns a
  `WorkflowResumer` the run machinery drives.
- `DemoData.RunLiveOnboardingAsync` executes fresh runs at startup so the browsable demo shows genuinely-executed
  runs, not hand-seeded states: a completing run, an intentionally-faulting run (a success criterion evaluated
  against the real backend response), the resilient v2 that handles the same failure, an async run that suspends
  on an AsyncAPI message wait and resumes when the verdict is delivered, and a retry run that suspends on a
  durable timer between attempts and resumes when the backoff elapses (`WorkflowWorker.ResumeDueTimersAsync` /
  `DeliverMessageAsync`).

`PreserveCompilationContext=true` is set on both the control-plane host and the runner: the runtime compile of a
captured draft (below) needs the reference assemblies preserved.

## §18 debug runs

A debug run is a durable run of a working copy's captured document, not a published version (workflow-designer
design §18). The control plane never executes it. It writes the capture to the draft-run store, marks the run
claimable, and a runner advances it. See the R1–R5 commits and the debug-run UI for the full loop.

- `InProcessDraftRunner` claims the `$draft` runs pinned to its environment, resolves each captured draft,
  compiles it in durable mode (`WorkflowExecutorProvider`, content-hash cached), runs it forward-only, records
  the metadata-only exchanges (`RecordingApiTransport`), and persists a `SimulationTrace`-shaped trace to the
  trace store. A failing or uncompilable run is faulted, never allowed to abort the pump's batch.
- **Single-process** (the default standalone host): the control plane pumps the runner in-process
  (`ControlPlane:HostDraftRunnerInProcess`, default on). The runner is architecturally a runner, just co-located.
- **Multi-process** (the AppHost composition): a separate `Runner.Demo` process hosts `$draft`
  (`Runner:HostDraftRuns`) and the control plane's in-process pump is off, so the two planes are physically
  split. Both share the Postgres draft/state/trace stores.

## Transports and credentials

- The demo routes each source's generated client directly at its real service (onboarding, ledger, kyc — each
  its own host); there is no control-plane `/svc` mock left (`DemoData.CreateLiveBinder` on the control-plane
  path, `DraftRunHost.CreateBinder` on the runner path). In production these are the environment's real endpoints
  and the credentialed `SourceCredentialTransports.CreateBinder` applies each source's Vault-resolved secret.
- **§13.5 credentials.** When Vault is configured (`VAULT_ADDR`/`VAULT_TOKEN`, which the AppHost injects as the
  runner's read-only token), the runner resolves each source's secret **as its own read-only Vault identity** at
  bind time and applies it to the request (`SourceCredentialTransports.CreateBinder` over a
  `SourceCredentialCache` and the Vault secret resolver). The secret never reaches the control plane, the
  designer, or the developer. The control plane holds only the reference (`vault://secret/arazzo/<source>#…`).

## Key types

| Purpose | Type / location |
|---|---|
| Executor at add time | `Corvus.Text.Json.Arazzo.Generation.WorkflowExecutorProvider` |
| Catalog resumer | `HostedWorkflowResumer` + `WorkflowExecutorLoader` (`DemoData.CreateLiveResumer`) |
| §18 draft runner | `Corvus.Text.Json.Arazzo.Durability.InProcessDraftRunner` |
| HTTP transport | `Corvus.Text.Json.OpenApi.IApiTransport` + `HttpClientApiTransportFactory` |
| Message transport | `Corvus.Text.Json.AsyncApi.Nats.NatsMessageTransport` (JetStream) |
| Credentialed binder | `Corvus.Text.Json.Arazzo.SourceCredentials.Http.SourceCredentialTransports.CreateBinder` |
| Resumer seam | `Corvus.Text.Json.Arazzo.Durability.WorkflowResumer` / `WorkflowWorker` |
