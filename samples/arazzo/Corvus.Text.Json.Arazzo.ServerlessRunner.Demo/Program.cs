// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The Arazzo serverless execution-host ("serverless runner", ADR 0055/0059) — the deploy-and-invoke runner for an
// Isolated environment, the second serverless-path process alongside the control-plane build worker. It shares the
// durability store with the control plane and, unlike the in-process runner, never loads executor IL. It does two
// things over the shared stores:
//   * Drains the workflow-deployment queue (deploy worker): each Queued deployment the control-plane build worker
//     enqueued on a Ready build is claimed here, its signed native binary verified against the trust anchor, and
//     deployed to a cloud function via the runner's OWN cloud identity — LocalStack here (the AWS analogue, ADR 0060),
//     real AWS in production. The control plane holds no cloud credentials (ADR 0059).
//   * Advances a claimed run by INVOKING its deployed function (ServerlessRunExecutionBackend): the function restores
//     the run, advances it, and checkpoints it back through this host's /runs/{id}/checkpoint surface, which terminates
//     into the shared state store under the lease this runner holds.
// It registers as an Isolated runner (ADR 0058), so the control-plane dispatch matches an Isolated environment's runs to
// it. A worker process (the minimal web surface exists only for the §5.4 health probe, the checkpoint callback, and OTel).
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Postgres;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Runner;
using Corvus.Text.Json.Arazzo.ServerlessRunner.Demo;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, HTTP resilience → the Aspire dashboard.
builder.AddServiceDefaults();

// The shared durability store — the same Postgres database the control plane opens (the AppHost injects
// ConnectionStrings:workflowstore). Read-mostly: the control plane owns the schema + reset + seed; this runner never
// runs DDL (its WaitFor(controlplane) gating means the tables exist when it connects). One NpgsqlDataSource.
string connectionString = builder.Configuration.GetConnectionString("workflowstore")
    ?? throw new InvalidOperationException("ConnectionStrings:workflowstore (the shared Postgres database) is required — run the serverless runner under the AppHost.");
NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);

// The shared state store. At rest (§14): the SAME per-boot checkpoint-protection key the control plane holds — every
// process touching the shared state store must wrap it identically, or one side writes what the other cannot read. The
// serverless function checkpoints back through THIS host's endpoint, so this host owns the wrap on that write path.
PostgresWorkflowStateStore postgresStateStore = await PostgresWorkflowStateStore.ConnectAsync(dataSource);
IWorkflowStateStore stateStore = builder.Configuration["Runner:CheckpointProtectionKey"] is { Length: > 0 } checkpointKey
    ? new ProtectedWorkflowStateStore(postgresStateStore, new AesGcmCheckpointProtector(Convert.FromBase64String(checkpointKey)))
    : postgresStateStore;
PostgresWorkflowCatalogStore catalogStore = await PostgresWorkflowCatalogStore.ConnectAsync(dataSource);
PostgresRunnerRegistry registry = await PostgresRunnerRegistry.ConnectAsync(dataSource);
PostgresEnvironmentStore environments = await PostgresEnvironmentStore.ConnectAsync(dataSource);
PostgresEnvironmentRunnerAuthorizationStore runnerAuthorizations = await PostgresEnvironmentRunnerAuthorizationStore.ConnectAsync(dataSource);

// The workflow-deployment queue the deploy worker drains and the run-execution backend resolves function URLs from — the
// store-as-queue boundary (ADR 0023/0059) the control-plane build worker enqueues into.
PostgresWorkflowDeploymentStore deployments = await PostgresWorkflowDeploymentStore.ConnectAsync(dataSource);

// The wait-index cast holds for both branches of the protection wrap above (index entries pass through in the clear).
var catalog = new SecuredWorkflowCatalog(catalogStore, (IWorkflowWaitIndex)stateStore, "serverless-runner");

// The single Isolated environment this runner serves (design §5.5). The runner advertises the Isolated model (ADR 0058),
// so control-plane dispatch routes only this environment's Isolated runs to it. Configurable so one host image can serve
// any Isolated environment; the demo defaults to "isolated".
string runnerEnvironment = builder.Configuration["Runner:Environment"] ?? "isolated";
var options = new RunnerOptions(
    $"serverless-runner-{System.Environment.MachineName}-{System.Environment.ProcessId}",
    runnerEnvironment,
    IsolationModel: "Isolated");

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IWorkflowStateStore>(stateStore);
builder.Services.AddSingleton<IWorkflowCatalogStore>(catalogStore);
builder.Services.AddSingleton<IRunnerRegistry>(registry);
builder.Services.AddSingleton<IEnvironmentStore>(environments);
builder.Services.AddSingleton<IEnvironmentRunnerAuthorizationStore>(runnerAuthorizations);
builder.Services.AddSingleton<IWorkflowDeploymentStore>(deployments);
builder.Services.AddSingleton(catalog);

// Executor-package trust (#879, ADR 0055): the deploy verifies each native binary's attestation against the signing
// key's PUBLIC half before it hands the binary to the function platform, so a binary swapped in storage or transit is
// caught. The runner NEVER holds the private key (it cannot sign) — the same trust anchor the in-process runner uses,
// provisioned into this runner's config at deployment time (the AppHost exports it to a file; a real deployment mounts a
// ConfigMap / IaC-dropped secret). Required: a serverless deploy without a trust anchor cannot verify what it deploys.
if (builder.Configuration["Runner:ExecutorTrust:PublicKeyFile"] is not { Length: > 0 } trustKeyFile || !File.Exists(trustKeyFile))
{
    throw new InvalidOperationException(
        "Runner:ExecutorTrust:PublicKeyFile (the executor-signing public key) is required — a serverless deploy verifies the signed native binary's attestation before deploying it (ADR 0055). The AppHost injects it.");
}

string trustKeyId = builder.Configuration["Runner:ExecutorTrust:KeyId"] ?? "arazzo-executor-signing";
IExecutorPackageVerifier verifier = TrustStoreExecutorPackageVerifier.FromPem(
    new Dictionary<string, string>(StringComparer.Ordinal) { [trustKeyId] = await File.ReadAllTextAsync(trustKeyFile) });

// The checkpoint base URL the invoked function calls back to — this host, at a URL reachable from the LocalStack Lambda
// container (the AppHost injects host.containers.internal:<port>). Read early: it drives BOTH the run-execution backend
// (below) AND this host's own listen address — the runner must bind 0.0.0.0 (not loopback), or the Lambda container
// cannot reach the checkpoint callback (nor the demo source endpoint this runner also serves) through the host gateway.
string checkpointBaseUrl = builder.Configuration["Runner:CheckpointBaseUrl"]
    ?? throw new InvalidOperationException("Runner:CheckpointBaseUrl (the /runs/{id}/checkpoint base URL the invoked function calls back to) is required — the AppHost injects a value reachable from the LocalStack Lambda container.");
builder.WebHost.UseUrls($"http://0.0.0.0:{new Uri(checkpointBaseUrl, UriKind.Absolute).Port}");

// The deployed environment's source configuration the baked function reaches its sources through (ADR 0059, the runner
// holds it): each configured source's base URL is set on the Lambda as ARAZZO_SOURCE__<name>, which the baked transport
// binder reads to build one HTTP transport per source. The AppHost supplies Runner:FunctionSources:<name> as URLs the
// Lambda container can reach; in this demo the single 'echo' source is served by THIS runner (its /demo/echo endpoint).
var functionSourceEnv = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (IConfigurationSection source in builder.Configuration.GetSection("Runner:FunctionSources").GetChildren())
{
    if (source.Value is { Length: > 0 } sourceUrl)
    {
        functionSourceEnv[$"ARAZZO_SOURCE__{source.Key}"] = sourceUrl;
    }
}

// The deploy service the deploy worker drives: verify the native artifact, then deploy it via the configured platform's
// deployer — deployer selection is a host-wiring concern (ADR 0061). Runner:Serverless:Platform picks 'lambda' (the
// default; LocalStack here, real AWS in production, the runner's own cloud identity either way — ADR 0059/0060) or
// 'azure-flex' (a real Flex Consumption Function App via One Deploy with the runner's ambient Azure identity — ADR 0061
// amendment; Azure has no local management-plane emulator). Dispatch, the queue, and the worker are platform-blind.
var deployService = new WorkflowDeployService(
    verifier,
    ServerlessDeployerSelection.Create(builder.Configuration, functionSourceEnv));
builder.Services.AddSingleton(deployService);
builder.Services.AddWorkflowDeployWorker(new WorkflowDeployWorkerOptions
{
    WorkerId = $"serverless-deploy-{System.Environment.MachineName}-{System.Environment.ProcessId}",
    PollInterval = TimeSpan.FromSeconds(2),
});

// The serverless run-execution backend as this runner's resumer (ADR 0055): dispatch and timer-resume advance a claimed
// run by INVOKING its deployed function. The production DeployedFunctionUrlResolver maps a run's (base workflow, version,
// environment) to the Deployed function URL from the shared deployment store; the function checkpoints back to
// checkpointBaseUrl. A resolve failure (no deployed function yet) throws, leaving the run claimable.
var serverlessBackend = new ServerlessRunExecutionBackend(
    new HttpClient(),
    DeployedFunctionUrlResolver.ForStore(deployments, environments),
    new Uri(checkpointBaseUrl, UriKind.Absolute));
builder.Services.AddSingleton<WorkflowResumer>(serverlessBackend.AsResumer());

// Authenticated registration (design §5.5/§16.4): when the control-plane API + this runner's machine-principal
// credentials are configured (the real topology under the AppHost), the runner registers through the control plane's
// authenticated HTTP endpoint as its Keycloak client-credentials client, so the control plane derives the trusted
// principal from the token and binds the runner's authorization to it. Absent any of these (a bare two-process run), the
// registrar stays null and registration falls back to the store-direct path (an administrator then authorizes it).
string? controlPlaneBaseUrl = builder.Configuration["Runner:ControlPlane:BaseUrl"];
string? runnerKeycloakBaseUrl = builder.Configuration["Runner:Keycloak:BaseUrl"];
string? runnerClientId = builder.Configuration["Runner:Keycloak:ClientId"];
string? runnerClientSecret = builder.Configuration["Runner:Keycloak:ClientSecret"];
string runnerRealm = builder.Configuration["Runner:Keycloak:Realm"] ?? "arazzo";
ControlPlaneRunnerRegistrar? runnerRegistrar = null;
if (!string.IsNullOrWhiteSpace(controlPlaneBaseUrl) && !string.IsNullOrWhiteSpace(runnerKeycloakBaseUrl)
    && !string.IsNullOrWhiteSpace(runnerClientId) && !string.IsNullOrWhiteSpace(runnerClientSecret))
{
    runnerRegistrar = new ControlPlaneRunnerRegistrar(
        new HttpClient(),
        controlPlaneBaseUrl,
        runnerEnvironment,
        ControlPlaneRunnerRegistrar.TokenEndpointFor(runnerKeycloakBaseUrl, runnerRealm),
        runnerClientId,
        runnerClientSecret);
}

// The two long-running loops (design §5.4 registration/heartbeat, §7 dispatch + resume). The registration service is
// constructed explicitly so the optional control-plane registrar (or null) flows in deterministically; the dispatch
// service resolves the serverless resumer registered above.
builder.Services.AddHostedService(sp => new RunnerRegistrationService(
    sp.GetRequiredService<IRunnerRegistry>(),
    sp.GetRequiredService<IEnvironmentStore>(),
    sp.GetRequiredService<IEnvironmentRunnerAuthorizationStore>(),
    sp.GetRequiredService<SecuredWorkflowCatalog>(),
    sp.GetRequiredService<RunnerOptions>(),
    sp.GetRequiredService<ILogger<RunnerRegistrationService>>(),
    serverlessBackend.IsolationModel,
    runnerRegistrar));
builder.Services.AddHostedService<WorkflowDispatchService>();

WebApplication app = builder.Build();

// /health (readiness) and /alive (liveness) — the AppHost's WithHttpHealthCheck("/health") polls these.
app.MapDefaultEndpoints();

// The serverless checkpoint callback surface (ADR 0055): the invoked function GETs the run's checkpoint to restore it and
// POSTs the advanced checkpoint back here, terminating into the shared state store under the lease this runner holds. The
// function binds no store SDK — it saves over HTTP, and the dispatching runner terminates that into the real store. Open
// here (the demo's callback is unauthenticated); a dedicated checkpoint scope is deferred (see WorkflowCheckpointEndpoints).
app.MapWorkflowCheckpointEndpoints(stateStore, requireAuthorization: false);

// The demo's 'echo' source: a trivial always-200 endpoint the serverless-check workflow calls, served by this runner so
// a serverless run has a reachable source and can run to completion. The invoked Lambda reaches it at the same
// host.containers.internal:<port> it uses for the checkpoint callback (wired via Runner:FunctionSources:echo).
app.MapGet("/demo/echo", () => Results.Json(new { status = "ok" }));

// A tiny identity endpoint so the dashboard's resource link lands somewhere informative.
app.MapGet("/", () => Results.Text(
    $"Arazzo serverless runner '{options.RunnerId}' serving Isolated environment '{runnerEnvironment}'. Health at /health.",
    "text/plain"));

app.Run();