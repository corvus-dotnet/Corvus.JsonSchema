# Live workflow execution — resume notes

The demo currently **seeds** runs in fixed states (see `DemoData.cs`). The next phase is to make the demo
*execute* the workflows live: the control plane's `WorkflowResumer` re-enters a generated Arazzo executor that
calls the real backend services (the `/svc/onboarding` + `/svc/ledger` APIs already hosted by this sample, plus
an AsyncAPI messaging service), checkpointing/suspending/faulting against the SQLite store.

This was scoped and assessed as **feasible**, then paused at the spike. These notes capture the approach so it
can be resumed without re-discovery.

## What already exists (done)

- Backend services generated + hosted: `Generated/Onboarding`, `Generated/Ledger`, implemented by
  `Services/OnboardingService.cs` / `Services/LedgerService.cs`, mapped under `/svc/...` in `Program.cs`.
- The control-plane host, SQLite stores, catalog/run seeding, and the web UI.
- `PreserveCompilationContext=true` is already set (needed if executors are compiled at runtime).

## The executor pipeline (the thorny part)

`WorkflowExecutorEmitter.Emit(workflow, binder, options)` returns the executor **source string**. It does NOT
take an OpenAPI document — it takes a `WorkflowOperationBinder` built from `OperationDescriptor[]` that name the
**generated client types** per operation. There is no CLI verb for this; it is invoked programmatically.

Reference implementations in the test suite:
- `tests/Corvus.Text.Json.Arazzo.Tests/WorkflowExecutorEndToEndTests.cs` — `EmitGetPetExecutor(...)` and the
  durable run → crash → resume → complete cycle (the pattern to copy for the resumer).
- `tests/Corvus.Text.Json.Arazzo.Tests/Coverage_GeneratorInlinerTests.cs` (~line 100) — concrete
  `OperationDescriptor[]` + `WorkflowOperationBinder` + `OperationResolver.Create(source, operations)` +
  `WorkflowExecutorEmitter.Emit(workflow, binder, new WorkflowExecutorOptions(ns, className, inputsType, outputsType))`.

Generated executor entry point (`WorkflowExecutorEmitter.cs`):

```csharp
public static async ValueTask<WorkflowRunResult<TOutputs>> ExecuteAsync(
    IApiTransport transport,
    IMessageTransport messageTransport,   // only when the workflow has AsyncAPI steps
    JsonWorkspace workspace,
    TInputs inputs,
    IWorkflowRun? run = null,             // durable mode (checkpoint/resume) when non-null
    CancellationToken cancellationToken = default,
    TimeProvider? timeProvider = null)
```

`WorkflowRunResult<T>` is tri-state (Completed / Faulted / Suspended) → map to `WorkflowRunResultKind` in the
`WorkflowResumer`.

## Stages to do

1. **Clients** — `corvusjson openapi-client` for `specs/onboarding.openapi.json` + `specs/ledger.openapi.json`
   (gives `*Request`/`*Response`/`*Client`/`*Async`). `corvusjson asyncapi-generate` for a new
   `specs/notifications.asyncapi.json` (a `kyc.results` channel a step `receive`s).
2. **Binders** — for each workflow, build `OperationDescriptor[]` mapping each Arazzo `operationId` to the
   generated client request/response/client/method type names; build the `WorkflowOperationBinder`; call
   `WorkflowExecutorEmitter.Emit`. Decide build-time (write the `.cs` into the project, commit) vs runtime
   (`DynamicCompiler`, like the validator). Build-time is simpler to debug.
3. **Transports** — an `IApiTransport` over `HttpClient` (see `Corvus.Text.Json.OpenApi` / a HttpTransport)
   with a per-source base URL of `http://localhost:<port>/svc/<source>`; an `IMessageTransport` =
   `Corvus.Text.Json.AsyncApi.Testing.InMemoryMessageTransport`.
4. **Live resumer + seeding** — wire a `WorkflowResumer` that calls `ExecuteAsync(...)`; replace the hand-built
   seed states with actually-run workflows (some complete; one faults on a success-criterion against the
   backend data; one suspends on the awaited message). Use `WorkflowWorker` (`ResumeDueTimersAsync` /
   `DeliverMessageAsync`) for timer/message resume.

## Key types

| Purpose | Type / location |
|---|---|
| Executor emitter | `Corvus.Text.Json.Arazzo.CodeGeneration.WorkflowExecutorEmitter.Emit` |
| Binder | `WorkflowOperationBinder`, `OperationResolver`, `OperationDescriptor`, `WorkflowExecutorOptions` |
| HTTP transport | `Corvus.Text.Json.OpenApi.IApiTransport` |
| Message transport | `Corvus.Text.Json.AsyncApi.IMessageTransport` + `…AsyncApi.Testing.InMemoryMessageTransport` |
| Resumer seam | `Corvus.Text.Json.Arazzo.Durability.WorkflowResumer` / `WorkflowWorker` |
