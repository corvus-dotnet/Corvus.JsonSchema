// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The Arazzo execution-host ("runner") — the second process in the real topology (design §2). It shares the
// durability store with the control plane: the control plane creates Pending runs and owns the catalog; this
// runner registers itself, then claims and drives runs from the store-as-queue (design §5/§7). It is a worker
// process (its real-life deployment is a container, scaled independently) whose long-running loops are hosted
// BackgroundServices; the minimal web surface exists only for the §5.4 health probe + Aspire/OTel.
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Postgres;
using Npgsql;
using Corvus.Text.Json.Arazzo.Durability.Vault;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Runner.Demo;
using Corvus.Text.Json.Arazzo.SourceCredentials.Http;
using Corvus.Text.Json.AsyncApi.Testing;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, HTTP resilience → the Aspire dashboard.
builder.AddServiceDefaults();

// The shared durability store — the same Postgres database the control plane opens (the AppHost injects
// ConnectionStrings:workflowstore). Required: the runner runs under the AppHost, which stands up Postgres. Read-mostly:
// the control plane owns the schema (each store's PrepareAsync) + reset + seed, so the runner never runs DDL — its
// WaitFor(controlplane) gating means the tables already exist when it connects. One NpgsqlDataSource, shared across
// the stores it opens.
string connectionString = builder.Configuration.GetConnectionString("workflowstore")
    ?? throw new InvalidOperationException("ConnectionStrings:workflowstore (the shared Postgres database) is required — run the runner under the AppHost.");
NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);

// Connect read-mostly: the control plane owns reset+seed, so the runner never deletes or seeds. It reads the
// catalog (to learn which versions to host) and claims/leases/advances runs against the shared state store.
PostgresWorkflowStateStore stateStore = await PostgresWorkflowStateStore.ConnectAsync(dataSource);
PostgresWorkflowCatalogStore catalogStore = await PostgresWorkflowCatalogStore.ConnectAsync(dataSource);
PostgresRunnerRegistry registry = await PostgresRunnerRegistry.ConnectAsync(dataSource);

// The §13 source-credential store, shared with the control plane: the control plane registers the binding
// (reference + metadata, never the secret), and the runner reads it to learn which Vault references to resolve.
PostgresSourceCredentialStore credentials = await PostgresSourceCredentialStore.ConnectAsync(dataSource);

// The §7.7 environments registry, shared with the control plane: the runner reads the environment it serves to
// inherit its reach (managementTags → the registration's reachTags, design §5.5). The control plane owns writes.
PostgresEnvironmentStore environments = await PostgresEnvironmentStore.ConnectAsync(dataSource);

// The §5.5 runner-authorization store, shared with the control plane: registering only records this runner's intent
// to serve its environment (an idempotent Pending authorization); an administrator of that environment authorizes it
// before it is dispatchable. The runner writes Pending here; the control plane reads/decides over the same store.
PostgresEnvironmentRunnerAuthorizationStore runnerAuthorizations = await PostgresEnvironmentRunnerAuthorizationStore.ConnectAsync(dataSource);
var catalog = new SecuredWorkflowCatalog(catalogStore, stateStore, "runner");

// The single environment this runner serves (design §5.5). Configurable so one host image can be deployed per
// environment; the demo defaults to production. The runner is only dispatchable for runs targeting it.
string runnerEnvironment = builder.Configuration["Runner:Environment"] ?? "production";
var options = new RunnerOptions($"runner-{System.Environment.MachineName}-{System.Environment.ProcessId}", runnerEnvironment);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IWorkflowStateStore>(stateStore);
builder.Services.AddSingleton<IWorkflowCatalogStore>(catalogStore);
builder.Services.AddSingleton<IRunnerRegistry>(registry);
builder.Services.AddSingleton<IEnvironmentStore>(environments);
builder.Services.AddSingleton<IEnvironmentRunnerAuthorizationStore>(runnerAuthorizations);
builder.Services.AddSingleton<ISourceCredentialStore>(credentials);
builder.Services.AddSingleton(catalog);

// The one transport binder EVERY run executes through — catalogued runs AND §18 $draft debug runs. In the demo the
// sources are the control plane's own /svc backends (Runner:SourcesBaseUrl); a real deployment points them at the
// environment's real endpoints. When Vault is configured (VAULT_ADDR/VAULT_TOKEN — the AppHost injects the runner's
// read-only token), this is the §13.5 production path: the runner resolves each source's credential as its OWN
// read-only Vault identity at bind time and applies it to the request — the secret never leaves Vault and never
// reaches the control plane, the designer, or the developer. Without Vault (a standalone two-process run) it falls
// back to plain /svc transports, enough to prove the loop. The shared in-memory message transport satisfies an
// AsyncAPI receive step's need for an IMessageTransport (the durable suspend/resume itself flows through the shared
// store + worker); a real broker adapter replaces it in production (design §8).
string sourcesBaseUrl = builder.Configuration["Runner:SourcesBaseUrl"]
    ?? throw new InvalidOperationException("Runner:SourcesBaseUrl must be set (the host serving the sources — the control plane's /svc backends in the demo).");
var messageTransport = new InMemoryMessageTransport();
string? vaultAddress = builder.Configuration["VAULT_ADDR"];
string? vaultToken = builder.Configuration["VAULT_TOKEN"];
WorkflowTransportBinder binder;
if (!string.IsNullOrWhiteSpace(vaultAddress) && !string.IsNullOrWhiteSpace(vaultToken))
{
    IVaultClient vaultClient = new VaultClient(new VaultClientSettings(vaultAddress, new TokenAuthMethodInfo(vaultToken)));
    ISecretResolver secretResolver = new SecretResolverBuilder().AddHashiCorpVault(vaultClient).Build();
    var providerFactory = new SourceCredentialProviderFactory(secretResolver);
    var credentialCache = new SourceCredentialCache(credentials, providerFactory);
    Dictionary<string, HttpClient> sourceClients = DraftRunHost.CreateSvcClients(sourcesBaseUrl, "onboarding", "ledger");
    binder = SourceCredentialTransports.CreateBinder(sourceClients, runnerEnvironment, credentialCache, messageTransport);
}
else
{
    binder = DraftRunHost.CreateSvcBinder(sourcesBaseUrl, messageTransport);
}

// Catalogued-run execution (design §5/§8, §11 Phase 2): the runner claims a Pending run and re-enters the version's
// baked executor.dll through the real HostedWorkflowResumer — loading it into a collectible ALC on first use and
// running it against the binder above. This is the same live-execution path the control-plane host runs in-process;
// wiring it here is what makes the separate runner genuinely EXECUTE catalogued runs (it previously only leased them
// and marked them complete via a stub). The dispatch/timer-resume loops consume it as their WorkflowResumer.
WorkflowResumer catalogResumer = new HostedWorkflowResumer(catalogStore, new WorkflowExecutorLoader(), binder).AsResumer();
builder.Services.AddSingleton(catalogResumer);

// §18 out-of-process draft hosting (the multi-process debug-run topology): the control plane only MARKS $draft debug
// runs claimable; THIS runner claims and executes them, reusing the same binder. Disabled with Runner:HostDraftRuns=false
// (e.g. a catalog-only runner — catalogued-run execution above stays on regardless).
if (builder.Configuration.GetValue("Runner:HostDraftRuns", true))
{
    PostgresDraftRunStore draftRunStore = await PostgresDraftRunStore.ConnectAsync(dataSource);
    PostgresDraftRunTraceStore draftRunTraceStore = await PostgresDraftRunTraceStore.ConnectAsync(dataSource);
    var draftRunner = new InProcessDraftRunner(
        stateStore,
        options.RunnerId,
        runnerEnvironment,
        draftRunStore,
        draftRunTraceStore,
        new WorkflowExecutorProvider(),
        binder,
        hostTimerWaits: false);
    builder.Services.AddSingleton(draftRunner);
    builder.Services.AddHostedService<DraftRunPumpService>();
}

// The two long-running loops (design §5.4 registration/heartbeat, §7 dispatch + resume).
builder.Services.AddHostedService<RunnerRegistrationService>();
builder.Services.AddHostedService<WorkflowDispatchService>();

// A startup self-check (design §13.5): resolve the seeded credential references against Vault using only the
// runner's read-only token, and assert a write is refused — proving the secret-consumer boundary end to end.
builder.Services.AddHostedService<VaultCredentialSelfCheckService>();

WebApplication app = builder.Build();

// /health (readiness) and /alive (liveness) — the AppHost's WithHttpHealthCheck("/health") polls these.
app.MapDefaultEndpoints();

// A tiny identity endpoint so the dashboard's resource link lands somewhere informative.
app.MapGet("/", () => Results.Text($"Arazzo execution-host runner '{options.RunnerId}'. Health at /health.", "text/plain"));

app.Run();
