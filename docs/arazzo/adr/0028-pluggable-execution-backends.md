# ADR 0028. Pluggable execution backends and isolation models

Date: 2026-07-21. Status: **Proposed**. Scope: how a runner isolates the execution of a run. Builds on
[ADR 0024](0024-collectible-assembly-per-version.md). This records the intended seam for dispatching a run to a
pluggable execution backend with a chosen isolation model, and is explicit that only the in-process backend
ships today.

## Context

Today a runner executes a run in-process, loading the version's assembly into a collectible load context
([ADR 0024](0024-collectible-assembly-per-version.md)). In-process is efficient and enough when the runner
trusts the workflow code it hosts. Some deployments will want stronger isolation: a per-run micro-guest, a
serverless function, or a container, so a run cannot affect the runner or its neighbours. That isolation should
be a choice a deployment makes, not a rewrite of the runner, which means dispatch has to go through a seam that
an isolation model plugs into.

### Grounded architectural facts

- **The in-process collectible-ALC backend ships.** A run executes in-process through
  `WorkflowExecutorLoader` and a collectible `AssemblyLoadContext` ([ADR 0024](0024-collectible-assembly-per-version.md)).
  It now implements the seam below (`HostedWorkflowResumer` is `IRunExecutionBackend`).
- **The pluggable-backend seam now exists.** `IRunExecutionBackend` (advance a run, pre-warm a version, and
  advertise an isolation model) is the seam execution-host §5.6 specifies. `AdvanceAsync` is delegate-shaped, so a
  backend slots in behind the existing `WorkflowResumer` the dispatcher, worker, and management client consume,
  without touching dispatch, leases, timers, or message delivery. The in-process backend implements it; the
  out-of-process backends are not yet built.
- **The tracking issue is in progress.** The alternative execution backends are the subject of #876. The seam has
  landed (behind the in-process backend), and the out-of-process backends follow.

## Decision (proposed)

Execution is to be a **pluggable backend seam**. A runner dispatches a run to an execution backend that
applies a chosen isolation model, and a resume goes through the same seam. The in-process collectible-load-
context backend is the shipping default. Out-of-process backends (per-run micro-guest, serverless, container)
are a declared future the seam is shaped to admit, not yet implemented.

This ADR is **Proposed**. The seam and the in-process backend are now the current reality; the out-of-process
backends are the pending work. It moves to Accepted once at least one out-of-process backend lands.

### Decided direction (2026-07-21)

The seam and its shape are settled, and the first out-of-process backend is chosen:

- **Isolation is a coarse, comparable axis:** `RunIsolationModel` is `InProcess` or `Isolated`, not one value per
  runtime. A serverless function, a container, or a micro-guest all provide `Isolated`, differing in weight, not in
  the guarantee. The backend (the mechanism) is a separate axis a runner advertises.
- **Serverless is the first out-of-process backend** (AWS Lambda and Azure Functions), ahead of container-per-run
  and micro-guest. A run is a durable checkpoint in the shared store, so an invocation carries only the run id and
  environment; the function is a thin runner-runtime host that advances the run against the store.
- **Per-(environment, version), executor baked in.** A serverless function serves one known version, so publishing
  a version to a serverless environment deploys or updates its function with the content-hashed executor packaged
  in. Warmth is deterministic: the executor is loaded at init, kept warm by the platform (Lambda SnapStart or Azure
  Functions always-ready instances).
- **AOT-native.** Because the executor is a build-time input, the host and that one executor are compiled to a
  single Native AOT binary per version. This removes the executor fetch, verify, and load cost rather than warming
  around it, and Native AOT cannot load runtime IL anyway, so baking is what makes it possible. AOT-cleanliness of
  the executor, runtime, and transports is work to be done, verified by an early spike.
- **Signing shifts from verify-at-load to attest-at-deploy** ([executor signing](../guides/execution-host.md)):
  the source executor's signature is verified before AOT compilation, and the deploy pipeline attests the native
  artifact. The in-process backend keeps verify-at-load, since it loads IL dynamically.
- **The AOT artifact is produced out-of-process, on publish, not in the running host.** Native AOT is an
  ahead-of-time whole-app compile with a native link, so there is no runtime compiler to invoke in-process (unlike
  today's Roslyn-to-IL path). A workflow AOT builder service captures the generator's C# source, assembles a
  host-app project (the cloud entry point plus that one executor plus the runtime references), and compiles it. For
  serverless the builder wraps the vendor tooling (`dotnet lambda` for Lambda, the Azure Functions isolated-worker
  AOT publish) inside a build container; the later micro-guest backend may drive `dotnet publish` directly. The AOT
  unit is the whole host-app, one native binary per version and architecture, which is what per-(environment,
  version) baking means.
- **Publishing to a serverless environment is asynchronous.** The AOT build takes minutes, so a publish queues a
  build job and the version moves through queued, building, then ready or failed, with progress observable through
  the control plane. A version becomes dispatchable on that environment only once its build and deploy complete,
  which the advertise-and-match step already gates on.

## Consequences

- The runner's execution path is expected to move behind a backend seam, so a deployment can select an
  isolation model without changing the runner.
- Until the seam ships, execution is in-process only, and a deployment that needs stronger isolation runs
  runners in separately-isolated hosts at the process boundary rather than per-run.
- Resume is to use the same backend seam as a fresh run, so an isolation model applies uniformly to starting
  and resuming.
- Because this is Proposed, an implementation should revisit this ADR and move it to Accepted (or supersede
  it) once the seam and at least one out-of-process backend land.
