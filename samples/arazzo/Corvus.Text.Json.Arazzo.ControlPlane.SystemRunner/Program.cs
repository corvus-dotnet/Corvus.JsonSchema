// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The control-plane SYSTEM RUNNER (design §16.5.1): the dedicated execution host for the control plane's own internal
// workflows. It shares the durability store with the control plane, claims Pending runs pinned to the "system"
// environment, and drives the bootstrapped access-approval workflow — publishing the approval-required notification,
// suspending durably on the access.decision channel, and, once an approver's decision is delivered, calling
// grantAccessRequest on the control plane's own API as its arazzo-access-approval machine principal (the
// accessRequests:grant capability). It reuses the app runner's infrastructure (registration, dispatch, registrar, Vault
// lifecycle); only this composition root differs — its sources, environment, and consumer.
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Postgres;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Vault;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Runner.Demo;
using Corvus.Text.Json.Arazzo.SourceCredentials.Http;
using Corvus.Text.Json.AsyncApi.Nats;
using Npgsql;
using VaultSharp;
using VaultSharp.V1.AuthMethods.AppRole;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The shared durability store — the same Postgres database the control plane owns. Read-mostly: the control plane owns
// the schema + install (the access-approval workflow is catalogued and made available by the deployment bootstrap), so
// the system runner never runs DDL; it reads the catalog and claims/leases/advances runs against the shared store.
string connectionString = builder.Configuration.GetConnectionString("workflowstore")
    ?? throw new InvalidOperationException("ConnectionStrings:workflowstore (the shared Postgres database) is required — run the system runner under the AppHost.");
NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);

PostgresWorkflowStateStore postgresStateStore = await PostgresWorkflowStateStore.ConnectAsync(dataSource);

// §14 at rest: the SAME per-boot checkpoint-protection key the control plane holds — every process touching the shared
// state store must wrap it identically, or one side writes what the other cannot read.
IWorkflowStateStore stateStore = builder.Configuration["Runner:CheckpointProtectionKey"] is { Length: > 0 } checkpointKey
    ? new ProtectedWorkflowStateStore(postgresStateStore, new AesGcmCheckpointProtector(Convert.FromBase64String(checkpointKey)))
    : postgresStateStore;
PostgresWorkflowCatalogStore catalogStore = await PostgresWorkflowCatalogStore.ConnectAsync(dataSource);
PostgresRunnerRegistry registry = await PostgresRunnerRegistry.ConnectAsync(dataSource);
PostgresSourceCredentialStore credentials = await PostgresSourceCredentialStore.ConnectAsync(dataSource);
PostgresEnvironmentStore environments = await PostgresEnvironmentStore.ConnectAsync(dataSource);
PostgresEnvironmentRunnerAuthorizationStore runnerAuthorizations = await PostgresEnvironmentRunnerAuthorizationStore.ConnectAsync(dataSource);
var catalog = new SecuredWorkflowCatalog(catalogStore, (IWorkflowWaitIndex)stateStore, "system-runner");

// The single environment this runner serves (design §5.5): the control plane's internal "system" environment, where
// the bootstrapped approval workflow is made available. It is only dispatchable for runs pinned to it.
string runnerEnvironment = builder.Configuration["Runner:Environment"] ?? "system";
var options = new RunnerOptions($"system-runner-{System.Environment.MachineName}-{System.Environment.ProcessId}", runnerEnvironment);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IWorkflowStateStore>(stateStore);
builder.Services.AddSingleton<IWorkflowCatalogStore>(catalogStore);
builder.Services.AddSingleton<IRunnerRegistry>(registry);
builder.Services.AddSingleton<IEnvironmentStore>(environments);
builder.Services.AddSingleton<IEnvironmentRunnerAuthorizationStore>(runnerAuthorizations);
builder.Services.AddSingleton<ISourceCredentialStore>(credentials);
builder.Services.AddSingleton(catalog);

// The one HTTP source the approval workflow calls: the control plane's own API (grantAccessRequest). The runner resolves
// the "controlplane" OAuth2 client-credentials credential (its arazzo-access-approval identity, accessRequests:grant
// scope) at bind time and applies the issued token as a bearer to the call.
string controlPlaneBaseUrl = builder.Configuration["Runner:ControlPlane:BaseUrl"]
    ?? throw new InvalidOperationException("Runner:ControlPlane:BaseUrl (the control-plane API the approval workflow calls) is required — the AppHost injects it.");
var sourceClients = new Dictionary<string, HttpClient>(StringComparer.Ordinal)
{
    ["controlplane"] = new HttpClient { BaseAddress = new Uri(controlPlaneBaseUrl) },
};

// The message bus (NATS JetStream, design §8): the approval workflow's notifyApprovalRequired SEND step publishes to
// access.notify through here (its own stream). The access.decision RECEIVE is durable (the run suspends), so it does not
// subscribe here — the decision consumer below subscribes separately and resumes the suspended run.
string natsUrl = builder.Configuration["Nats:Url"]
    ?? throw new InvalidOperationException("Nats:Url (the message bus) is required — the AppHost injects it.");
NatsMessageTransport messageTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
{
    Url = natsUrl,
    Name = "system-runner-notify-out",
    UseJetStream = true,
    StreamName = "access-notify",
    StorageType = StorageType.File,
});

// Secure introduction (§13.5.1): identical to the app runner — the runner holds no pre-minted Vault token; it unwraps
// the single-use wrapping token the provisioner left, authenticates via AppRole, and resolves the "controlplane"
// credential's client secret as its own read-only Vault identity at bind time. Without Vault it falls back to an
// un-credentialed binder (enough to prove the loop against an unsecured control plane).
string? vaultAddress = builder.Configuration["VAULT_ADDR"];
string? vaultRoleId = builder.Configuration["Runner:Vault:RoleId"];
string? vaultWrapTokenFile = builder.Configuration["Runner:Vault:WrapTokenFile"];
WorkflowTransportBinder binder;
if (!string.IsNullOrWhiteSpace(vaultAddress) && !string.IsNullOrWhiteSpace(vaultRoleId) && !string.IsNullOrWhiteSpace(vaultWrapTokenFile))
{
    string wrappingToken = (await File.ReadAllTextAsync(vaultWrapTokenFile)).Trim();
    string secretId;
    using (var unwrapHttp = new HttpClient { BaseAddress = new Uri(vaultAddress) })
    using (var unwrapRequest = new HttpRequestMessage(HttpMethod.Post, "v1/sys/wrapping/unwrap"))
    {
        unwrapRequest.Headers.Add("X-Vault-Token", wrappingToken);
        using HttpResponseMessage unwrapResponse = await unwrapHttp.SendAsync(unwrapRequest);
        unwrapResponse.EnsureSuccessStatusCode();
        await using Stream unwrapStream = await unwrapResponse.Content.ReadAsStreamAsync();
        using System.Text.Json.JsonDocument unwrapDoc = await System.Text.Json.JsonDocument.ParseAsync(unwrapStream);
        secretId = unwrapDoc.RootElement.GetProperty("data").GetProperty("secret_id").GetString()!;
    }

    IVaultClient vaultClient = new VaultClient(new VaultClientSettings(vaultAddress, new AppRoleAuthMethodInfo(vaultRoleId, secretId)));
    builder.Services.AddSingleton(vaultClient);
    builder.Services.AddHostedService<VaultTokenLifecycleService>();
    ISecretResolver secretResolver = new SecretResolverBuilder().AddHashiCorpVault(vaultClient).Build();
    var providerFactory = new SourceCredentialProviderFactory(secretResolver);
    var credentialCache = new SourceCredentialCache(credentials, providerFactory);
    binder = SourceCredentialTransports.CreateBinder(sourceClients, runnerEnvironment, credentialCache, messageTransport);
}
else
{
    binder = DraftRunHost.CreateBinder(sourceClients, messageTransport);
}

// Executor-package verification (#879): same trust config as the app runner — verify each catalogued executor's
// signature with the control plane's signing public key before loading it (the runner never contacts the signing vault).
IExecutorPackageVerifier? executorVerifier = null;
if (builder.Configuration["Runner:ExecutorTrust:PublicKeyFile"] is { Length: > 0 } trustKeyFile && File.Exists(trustKeyFile))
{
    string trustKeyId = builder.Configuration["Runner:ExecutorTrust:KeyId"] ?? "arazzo-executor-signing";
    executorVerifier = TrustStoreExecutorPackageVerifier.FromPem(
        new Dictionary<string, string>(StringComparer.Ordinal) { [trustKeyId] = await File.ReadAllTextAsync(trustKeyFile) });
}

// Catalogued-run execution: claim a Pending access-approval run and re-enter its baked executor through the real
// HostedWorkflowResumer, running it against the binder above. The dispatch/resume loops consume it as their resumer.
WorkflowResumer catalogResumer = new HostedWorkflowResumer(catalogStore, new WorkflowExecutorLoader(verifier: executorVerifier), binder).AsResumer();
builder.Services.AddSingleton(catalogResumer);

// The decision-consumer transport (§8): a dedicated NATS JetStream consumer on access.decision, DeliverPolicy.All so a
// decision published before this consumer subscribed is not lost. Started once the host is listening (below).
NatsMessageTransport decisionsTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
{
    Url = natsUrl,
    Name = "system-runner-decisions-in",
    UseJetStream = true,
    StreamName = "access-decisions",
    ConsumerName = "system-runner-decision-consumer",
    DeliverPolicy = DeliverPolicy.All,
    StorageType = StorageType.File,
});

// Authenticated registration (§5.5/§16.4): the system runner registers through the control plane's authenticated HTTP
// endpoint as its arazzo-access-approval machine principal (client-credentials), so the control plane binds its
// authorization to the trusted principal rather than accepting a self-asserted store row. Absent the config (a bare
// run), the registrar stays null and registration falls back to the store-direct path.
ControlPlaneRunnerRegistrar? runnerRegistrar = null;
string? runnerKeycloakBaseUrl = builder.Configuration["Runner:Keycloak:BaseUrl"];
string? runnerClientId = builder.Configuration["Runner:Keycloak:ClientId"];
string? runnerClientSecret = builder.Configuration["Runner:Keycloak:ClientSecret"];
if (!string.IsNullOrWhiteSpace(runnerKeycloakBaseUrl) && !string.IsNullOrWhiteSpace(runnerClientId) && !string.IsNullOrWhiteSpace(runnerClientSecret))
{
    string runnerRealm = builder.Configuration["Runner:Keycloak:Realm"] ?? "arazzo";
    runnerRegistrar = new ControlPlaneRunnerRegistrar(
        new HttpClient(),
        controlPlaneBaseUrl,
        runnerEnvironment,
        ControlPlaneRunnerRegistrar.TokenEndpointFor(runnerKeycloakBaseUrl, runnerRealm),
        runnerClientId,
        runnerClientSecret);
}

builder.Services.AddHostedService(sp => new RunnerRegistrationService(
    sp.GetRequiredService<IRunnerRegistry>(),
    sp.GetRequiredService<IEnvironmentStore>(),
    sp.GetRequiredService<IEnvironmentRunnerAuthorizationStore>(),
    sp.GetRequiredService<SecuredWorkflowCatalog>(),
    sp.GetRequiredService<RunnerOptions>(),
    sp.GetRequiredService<ILogger<RunnerRegistrationService>>(),
    runnerRegistrar));
builder.Services.AddHostedService<WorkflowDispatchService>();

WebApplication app = builder.Build();

// The consumer of the access-decision exchange, built here so it gets a categorized logger from the running host — each
// decision receipt (and the number of runs it resumed) is logged, so a resume is visible rather than silent.
var decisionConsumer = new ReceiveAccessDecisionConsumer(
    decisionsTransport,
    new AccessDecisionResumeHandler(
        new WorkflowWorker(stateStore, options.RunnerId),
        catalogResumer,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<AccessDecisionResumeHandler>()));

// Start the decision consumer now the process is up: it subscribes to access.decision and resumes the suspended run.
await decisionConsumer.StartAsync();

// /health (readiness) and /alive (liveness) — the AppHost's WithHttpHealthCheck("/health") polls these.
app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Text($"Arazzo control-plane system runner '{options.RunnerId}'. Health at /health.", "text/plain"));

app.Run();
