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
using Corvus.Text.Json.Arazzo.Durability.Runner.Client;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Runner;
using Corvus.Text.Json.Arazzo.ServerlessRunner.Demo;
using Corvus.Text.Json.Arazzo.SourceCredentials.Http;
using Corvus.Text.Json.OpenApi.HttpTransport;
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
// Stable identity, for the same reason as the app runner: the id is pre-authorized before this process exists.
string runnerId = builder.Configuration["Runner:RunnerId"]
    ?? $"serverless-runner-{System.Environment.MachineName}-{System.Environment.ProcessId}";
var options = new RunnerOptions(
    runnerId,
    runnerEnvironment,
    IsolationModel: "Isolated",
    EnrolmentToken: builder.Configuration["Runner:EnrolmentToken"]);

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

// The ADR 0062 callback secret: this runner mints a run-scoped token per dispatch and validates it on the callback, so
// the invoked function proves which run it is entitled to read and overwrite. Mint and validate are the same process
// here, so the secret never travels. It is required rather than optional because a checkpoint surface without it has no
// way to bound a caller that holds no principal, and this surface is reachable from the function's container.
byte[] checkpointSecret = Convert.FromBase64String(
    builder.Configuration["Runner:CheckpointSecret"]
    ?? throw new InvalidOperationException("Runner:CheckpointSecret (base64, at least 32 bytes — the ADR 0062 secret this runner mints and validates checkpoint-callback tokens with) is required. The AppHost injects a per-boot value."));

// The serverless run-execution backend as this runner's resumer (ADR 0055): dispatch and timer-resume advance a claimed
// run by INVOKING its deployed function. The production DeployedFunctionUrlResolver maps a run's (base workflow, version,
// environment) to the Deployed function URL from the shared deployment store; the function checkpoints back to
// checkpointBaseUrl, carrying the token minted above. A resolve failure (no deployed function yet) throws, leaving the
// run claimable.
var serverlessBackend = new ServerlessRunExecutionBackend(
    new HttpClient(),
    DeployedFunctionUrlResolver.ForStore(deployments, environments),
    new Uri(checkpointBaseUrl, UriKind.Absolute),
    checkpointTokenIssuer: runId => CheckpointToken.Issue(checkpointSecret, runId.Value, DateTimeOffset.UtcNow.AddHours(1)));
builder.Services.AddSingleton<WorkflowResumer>(serverlessBackend.AsResumer());

// This runner's machine-principal credentials (design §16.4), required for the same reason as on every other runner:
// under ADR 0065 it is given work through the runner API, so being unable to authenticate to the control plane means
// being unable to be given work at all.
string controlPlaneBaseUrl = builder.Configuration["Runner:ControlPlane:BaseUrl"]
    ?? throw new InvalidOperationException("Runner:ControlPlane:BaseUrl (the control plane serving the runner API) is required — the AppHost injects it.");
string runnerKeycloakBaseUrl = builder.Configuration["Runner:Keycloak:BaseUrl"]
    ?? throw new InvalidOperationException("Runner:Keycloak:BaseUrl (the identity provider issuing this runner's machine-principal token) is required — the AppHost injects it.");
string runnerClientId = builder.Configuration["Runner:Keycloak:ClientId"]
    ?? throw new InvalidOperationException("Runner:Keycloak:ClientId (this runner's machine principal) is required — the AppHost injects it.");
string runnerClientSecret = builder.Configuration["Runner:Keycloak:ClientSecret"]
    ?? throw new InvalidOperationException("Runner:Keycloak:ClientSecret (this runner's machine-principal secret) is required — the AppHost injects it.");
string runnerRealm = builder.Configuration["Runner:Keycloak:Realm"] ?? "arazzo";

// One authentication provider for every outbound call to the control plane: the runner API and registration are the
// same machine principal, and the control plane binds this runner's authorization to it.
var controlPlaneAuthentication = new OAuth2ClientCredentialsAuthenticationProvider(
    new HttpClient(),
    new OAuth2ClientCredentialsOptions
    {
        TokenEndpoint = new Uri(ControlPlaneRunnerRegistrar.TokenEndpointFor(runnerKeycloakBaseUrl, runnerRealm)),
        ClientId = runnerClientId,
        ClientSecret = runnerClientSecret,
    });

// The runner API client (ADR 0065): claims, leases, and checkpoints go through the control plane rather than the store.
// This runner never loads an executor itself — it invokes the version's deployed function — so it pulls no artifacts;
// what it needs from the API is the queue, and the hosted-version listing that tells it which versions it may claim.
var runnerClient = new ArazzoRunnerClient(new HttpClientTransport(
    new HttpClient { BaseAddress = new Uri($"{controlPlaneBaseUrl.TrimEnd('/')}/arazzo/runner/v1") },
    controlPlaneAuthentication));
builder.Services.AddSingleton(runnerClient);

// Authenticated registration (design §5.5/§16.4): the runner registers through the control plane's authenticated HTTP
// endpoint as its Keycloak client-credentials client, so the control plane derives the trusted principal from the token
// and binds the runner's authorization to it. It is the same principal the runner API resolves its bindings from.
var runnerRegistrar = new ControlPlaneRunnerRegistrar(
    new HttpClient(),
    controlPlaneBaseUrl,
    runnerEnvironment,
    ControlPlaneRunnerRegistrar.TokenEndpointFor(runnerKeycloakBaseUrl, runnerRealm),
    runnerClientId,
    runnerClientSecret);

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
    runnerRegistrar,
    heartbeatInterval: null,
    runnerApi: runnerClient));
// The runner's single answer to what it has baked, shared by dispatch, the due-timer sweep and every message
// listener. One instance, so a delivery and a dispatch can never disagree about which versions this runner can run.
builder.Services.AddSingleton(sp => new RunnerHostedVersions(
    runnerClient,
    sp.GetRequiredService<RunnerOptions>().ServesSchedules));
builder.Services.AddHostedService<WorkflowDispatchService>();

WebApplication app = builder.Build();

// /health (readiness) and /alive (liveness) — the AppHost's WithHttpHealthCheck("/health") polls these.
app.MapDefaultEndpoints();

// The serverless checkpoint callback surface (ADR 0055): the invoked function GETs the run's checkpoint to restore it and
// POSTs the advanced checkpoint back here, terminating into the shared state store under the lease this runner holds. The
// function binds no store SDK — it saves over HTTP, and the dispatching runner terminates that into the real store.
//
// Every request must carry the run-scoped token this runner minted for that dispatch (ADR 0062). There is no ambient
// principal to require: the caller is a function in a container, and the token is what binds it to one run. The surface
// is reachable from that container, so an unauthenticated one would let anything on that network read and overwrite
// every run this runner holds.
app.MapWorkflowCheckpointEndpoints(
    stateStore,
    requireAuthorization: false,
    authenticateCheckpointToken: (id, token) => CheckpointToken.TryValidate(checkpointSecret, token, id.Value, DateTimeOffset.UtcNow));

// The demo's 'echo' source: a trivial always-200 endpoint the serverless-check workflow calls, served by this runner so
// a serverless run has a reachable source and can run to completion. The invoked Lambda reaches it at the same
// host.containers.internal:<port> it uses for the checkpoint callback (wired via Runner:FunctionSources:echo).
app.MapGet("/demo/echo", () => Results.Json(new { status = "ok" }));

// A tiny identity endpoint so the dashboard's resource link lands somewhere informative.
app.MapGet("/", () => Results.Text(
    $"Arazzo serverless runner '{options.RunnerId}' serving Isolated environment '{runnerEnvironment}'. Health at /health.",
    "text/plain"));

app.Run();