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
using Corvus.Text.Json.Arazzo.Durability.Runner.Client;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Postgres;
using Npgsql;
using Corvus.Text.Json.Arazzo.Durability.Vault;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Runner;
using Corvus.Text.Json.Arazzo.Runner.Demo;
using Corvus.Text.Json.Arazzo.Samples.Notifications;
using Corvus.Text.Json.Arazzo.SourceCredentials.Http;
using Corvus.Text.Json.AsyncApi.Nats;
using Corvus.Text.Json.OpenApi.HttpTransport;
using VaultSharp;
using VaultSharp.V1.AuthMethods.AppRole;

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
PostgresWorkflowStateStore postgresStateStore = await PostgresWorkflowStateStore.ConnectAsync(dataSource);

// At rest (§14, backlog #861): the SAME checkpoint-protection key the control plane holds — every process
// that touches the shared state store must wrap it identically, or one side writes what the other cannot
// read. The AppHost hands both the identical per-boot key.
IWorkflowStateStore stateStore = builder.Configuration["Runner:CheckpointProtectionKey"] is { Length: > 0 } checkpointKey
    ? new ProtectedWorkflowStateStore(postgresStateStore, new AesGcmCheckpointProtector(Convert.FromBase64String(checkpointKey)))
    : postgresStateStore;
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
// The wait-index cast holds for both branches of the protection wrap above (the protected store delegates its
// index members; index entries pass through in the clear by design, so queries never touch checkpoint bytes).
var catalog = new SecuredWorkflowCatalog(catalogStore, (IWorkflowWaitIndex)stateStore, "runner");

// The single environment this runner serves (design §5.5). Configurable so one host image can be deployed per
// environment; the demo defaults to production. The runner is only dispatchable for runs targeting it.
string runnerEnvironment = builder.Configuration["Runner:Environment"] ?? "production";
// The runner's stable identity. An administrator pre-authorizes a runner id for the principal that will present it
// (ADR 0065 decision 2), so the id has to be known before the process starts rather than derived from the process
// itself. The machine/pid fallback keeps a bare run identifiable, but only a configured id can be pre-authorized.
string runnerId = builder.Configuration["Runner:RunnerId"]
    ?? $"runner-{System.Environment.MachineName}-{System.Environment.ProcessId}";
var options = new RunnerOptions(
    runnerId,
    runnerEnvironment,
    ServesSchedules: builder.Configuration.GetValue("Runner:ServesSchedules", true),
    EnrolmentToken: builder.Configuration["Runner:EnrolmentToken"]);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IWorkflowStateStore>(stateStore);
builder.Services.AddSingleton<IWorkflowCatalogStore>(catalogStore);
builder.Services.AddSingleton<IRunnerRegistry>(registry);
builder.Services.AddSingleton<IEnvironmentStore>(environments);
builder.Services.AddSingleton<IEnvironmentRunnerAuthorizationStore>(runnerAuthorizations);
builder.Services.AddSingleton<ISourceCredentialStore>(credentials);
builder.Services.AddSingleton(catalog);

// The one transport binder EVERY run executes through — catalogued runs AND §18 $draft debug runs. The HTTP sources
// (onboarding/ledger/kyc) are real external services routed at their own endpoints. When Vault is configured (VAULT_ADDR
// + the AppRole RoleID/wrapping-token below — the §13.5 production path), the runner resolves each source's credential
// as its OWN AppRole-authenticated Vault identity at bind time and applies it to the request — the secret never leaves
// Vault and never reaches the control plane, the designer, or the developer. Without Vault (a standalone two-process
// run) it falls back to uncredentialed transports, enough to prove the loop. The message transport below is the real
// NATS JetStream bus (design §8): the async workflow's send step publishes through it (the durable suspend/resume flows
// through the shared store + worker, and a separate consumer resumes on a verdict).
string onboardingBaseUrl = builder.Configuration["Runner:Sources:Onboarding"]
    ?? throw new InvalidOperationException("Runner:Sources:Onboarding (the onboarding service endpoint) is required — the AppHost injects it.");
string ledgerBaseUrl = builder.Configuration["Runner:Sources:Ledger"]
    ?? throw new InvalidOperationException("Runner:Sources:Ledger (the ledger service endpoint) is required — the AppHost injects it.");
string kycBaseUrl = builder.Configuration["Runner:Sources:Kyc"]
    ?? throw new InvalidOperationException("Runner:Sources:Kyc (the KYC service endpoint) is required — the AppHost injects it.");

// The application-owned message bus (NATS JetStream) — the AppHost injects its URL. This transport is the one the
// executor uses for an AsyncAPI SEND step: when the runner drives the onboard-customer-async workflow, its
// requestKycReview step publishes to kyc.requests through here (each channel is its own JetStream stream, so this
// transport is scoped to kyc-requests). The verdict RECEIVE is durable (the run suspends), so it does not subscribe
// here — a separate consumer (below) subscribes to kyc.verdict and resumes the suspended run. This replaces the old
// in-process InMemoryMessageTransport (design §8): the exchange now flows through the real broker.
string natsUrl = builder.Configuration["Nats:Url"]
    ?? throw new InvalidOperationException("Nats:Url (the KYC message bus) is required — the AppHost injects it.");
NatsMessageTransport messageTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
{
    Url = natsUrl,
    Token = builder.Configuration["Nats:Token"],
    Name = "runner-requests-out",
    UseJetStream = true,
    StreamName = "kyc-requests",
    StorageType = StorageType.File,
});

// Both HTTP sources are now real external services (their own hosts + databases) — there is no /svc mock left. The
// SAME client map drives both binders below: the Vault-credentialed production binder (the runner resolves each
// source's secret as its own read-only Vault identity and applies it — the service ignores the unused header) and the
// standalone fallback. (kyc-notifications is an AsyncAPI channel source, bound through the channel-transport cache, not here.)
var sourceClients = new Dictionary<string, HttpClient>(StringComparer.Ordinal)
{
    ["onboarding"] = new HttpClient { BaseAddress = new Uri(onboardingBaseUrl) },
    ["ledger"] = new HttpClient { BaseAddress = new Uri(ledgerBaseUrl) },
    ["kyc"] = new HttpClient { BaseAddress = new Uri(kycBaseUrl) },
};

// Secure introduction (design §13.5.1): the runner holds NO pre-minted token. It reads the single-use, short-TTL
// wrapping token the provisioner left in the shared handoff file (its stand-in for a runtime host / service principal
// delivering the workload's identity), UNWRAPS it to obtain the AppRole SecretID (which never travelled in plaintext),
// then AUTHENTICATES via AppRole — RoleID (plain config) + SecretID — for a dynamically-issued, short-TTL, renewable
// token scoped to the read-only policy. A VaultTokenLifecycleService (registered below) keeps that token fresh by
// re-authenticating via AppRole on a timer: VaultSharp does NOT renew or re-authenticate on its own, so a runner that
// outlives the role's token_max_ttl would otherwise lose ALL Vault access (every credential read 403s). See the samples
// README: in production this identity comes from the runtime host's service principal / platform attestation.
string? vaultAddress = builder.Configuration["VAULT_ADDR"];
string? vaultRoleId = builder.Configuration["Runner:Vault:RoleId"];
string? vaultWrapTokenFile = builder.Configuration["Runner:Vault:WrapTokenFile"];
WorkflowTransportBinder binder;
if (!string.IsNullOrWhiteSpace(vaultAddress) && !string.IsNullOrWhiteSpace(vaultRoleId) && !string.IsNullOrWhiteSpace(vaultWrapTokenFile))
{
    // Self-unwrap: POST sys/wrapping/unwrap authenticated AS the wrapping token, with NO body token (exactly what
    // `vault unwrap` does). Passing the token in the body as well double-consumes it, so a raw request is clearest.
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
    // Share the AppRole-authenticated client with the startup self-check: the wrapping token was single-use (consumed
    // by the unwrap above), so the self-check cannot re-derive the SecretID — it reuses this client.
    builder.Services.AddSingleton(vaultClient);
    // Keep the AppRole-issued token alive for the life of the runner (it outlives the role's token_max_ttl). Without
    // this, every credential read 403s once the initial token expires — VaultSharp neither renews nor re-authenticates.
    builder.Services.AddHostedService<VaultTokenLifecycleService>();
    ISecretResolver secretResolver = new SecretResolverBuilder().AddHashiCorpVault(vaultClient).Build();
    var providerFactory = new SourceCredentialProviderFactory(secretResolver);
    var credentialCache = new SourceCredentialCache(credentials, providerFactory);

    // Channel sources bind per (source, environment) from their §13 channel credential (ADR 0051): the cache
    // resolves the binding's serverUrl + broker token as this runner's read-only Vault identity and builds one
    // shared, lazily-connected NATS transport per channel source — replacing the host-configured shared transport.
    var channelTransports = new ChannelTransportCache(credentials, secretResolver, runnerEnvironment, [new NatsChannelTransportFactory()]);
    binder = SourceCredentialTransports.CreateBinder(sourceClients, runnerEnvironment, credentialCache, channelTransports);
}
else
{
    binder = DraftRunHost.CreateBinder(sourceClients, messageTransport);
}

// Executor-package verification (#879): the runner trusts the control plane's executor-signing PUBLIC key, provisioned
// into its own config at deployment time (here the AppHost's signing-vault-init exports it to a file — the stand-in for
// a ConfigMap/mounted secret the platform drops for the workload). The runner loads that key into its trust store and
// verifies every catalogued executor's signature before activating it; it NEVER contacts the signing vault, so it holds
// no material it could sign with. Absent the trust config (bare-host runs), it loads packages without a signature check.
IExecutorPackageVerifier? executorVerifier = null;
if (builder.Configuration["Runner:ExecutorTrust:PublicKeyFile"] is { Length: > 0 } trustKeyFile && File.Exists(trustKeyFile))
{
    string trustKeyId = builder.Configuration["Runner:ExecutorTrust:KeyId"] ?? "arazzo-executor-signing";
    executorVerifier = TrustStoreExecutorPackageVerifier.FromPem(
        new Dictionary<string, string>(StringComparer.Ordinal) { [trustKeyId] = await File.ReadAllTextAsync(trustKeyFile) });
}

// Machine-principal credentials for this runner's authenticated calls to the control plane (design §16.4) — the runner
// API, authenticated registration, and governed schedule firing all go out as this one identity. Required: under ADR
// 0065 the runner reaches the durable store ONLY through the control plane, so a runner that cannot authenticate to it
// has no way to be given work and should say so at startup rather than poll a queue it will never be offered.
string controlPlaneBaseUrl = builder.Configuration["Runner:ControlPlane:BaseUrl"]
    ?? throw new InvalidOperationException("Runner:ControlPlane:BaseUrl (the control plane serving the runner API) is required — run the runner under the AppHost.");
string runnerKeycloakBaseUrl = builder.Configuration["Runner:Keycloak:BaseUrl"]
    ?? throw new InvalidOperationException("Runner:Keycloak:BaseUrl (the identity provider issuing this runner's machine-principal token) is required — run the runner under the AppHost.");
string runnerClientId = builder.Configuration["Runner:Keycloak:ClientId"]
    ?? throw new InvalidOperationException("Runner:Keycloak:ClientId (this runner's machine principal) is required — run the runner under the AppHost.");
string runnerClientSecret = builder.Configuration["Runner:Keycloak:ClientSecret"]
    ?? throw new InvalidOperationException("Runner:Keycloak:ClientSecret (this runner's machine-principal secret) is required — run the runner under the AppHost.");
string runnerRealm = builder.Configuration["Runner:Keycloak:Realm"] ?? "arazzo";

// One authentication provider for every outbound call to the control plane: the runner API below, registration, and
// schedule firing are all the same machine principal, and the control plane binds this runner's authorization to it.
var controlPlaneAuthentication = new OAuth2ClientCredentialsAuthenticationProvider(
    new HttpClient(),
    new OAuth2ClientCredentialsOptions
    {
        TokenEndpoint = new Uri(ControlPlaneRunnerRegistrar.TokenEndpointFor(runnerKeycloakBaseUrl, runnerRealm)),
        ClientId = runnerClientId,
        ClientSecret = runnerClientSecret,
    });

// The runner API client (ADR 0065) — THE change that retires this runner's store credential for execution. Claiming,
// leasing, checkpointing, and pulling executor artifacts are now requests the control plane authorises, not queries the
// runner makes for itself, and it names no environment on any of them: the control plane intersects every candidate set
// with the environments an administrator bound this machine principal to. The store handles this host still opens are
// for the surfaces that have no API counterpart yet (registration/heartbeat, source credentials, environments, draft
// runs, and the verdict consumer below), not for executing catalogued runs.
var runnerApiTransport = new HttpClientTransport(
    new HttpClient { BaseAddress = new Uri($"{controlPlaneBaseUrl.TrimEnd('/')}/arazzo/runner/v1") },
    controlPlaneAuthentication);
var runnerClient = new ArazzoRunnerClient(runnerApiTransport);
builder.Services.AddSingleton(runnerClient);

// Durable schedules (#896): a schedule is a durable run of the built-in scheduler workflow. When this runner serves
// schedules, wire the scheduler into the resumer; on each due occurrence it fires the target through the control plane's
// governed run endpoint (ControlPlaneRunStarter), authenticated as the same machine principal the runner registers under
// and carrying an idempotency key — so a scheduled start is governed exactly like an operator start and a re-fire after
// a recycle starts the target once.
ScheduleHostedWorkflow? scheduleWorkflow = null;
if (options.ServesSchedules)
{
    var runStarter = new ControlPlaneRunStarter(new HttpClient(), controlPlaneAuthentication, controlPlaneBaseUrl, runnerEnvironment);
    scheduleWorkflow = new ScheduleHostedWorkflow(runStarter.AsStartHandler(), TimeProvider.System);
}

// Catalogued-run execution (design §5/§8, §11 Phase 2): the runner claims a Pending run and re-enters the version's
// baked executor.dll through the real HostedWorkflowResumer — loading it into a collectible ALC on first use and
// running it against the binder above. This is the same live-execution path the control-plane host runs in-process;
// wiring it here is what makes the separate runner genuinely EXECUTE catalogued runs (it previously only leased them
// and marked them complete via a stub). The dispatch/timer-resume loops consume it as their WorkflowResumer, and the
// scheduler (when wired) rides the same loops — a schedule run is dispatched, resumed on its due timer, and re-fired.
// The executor comes from the runner API too (ADR 0065), so the runner holds no catalog credential either; what it is
// served is still verified against the manifest signature and the content hash before it is activated.
var catalogResumerBackend = new HostedWorkflowResumer(new RunnerApiArtifactSource(runnerApiTransport), new WorkflowExecutorLoader(verifier: executorVerifier), binder, scheduleWorkflow);
WorkflowResumer catalogResumer = catalogResumerBackend.AsResumer();
builder.Services.AddSingleton(catalogResumer);

// The consumer side of the async KYC verdict exchange (design §8): subscribe to kyc.verdict and, per verdict, resume
// the run suspended awaiting it (matched by account-id correlation) over the shared store. A dedicated transport
// because each channel is its own JetStream stream; DeliverPolicy.All so a verdict published before this consumer
// subscribed is not lost. Started once the host is listening (below).
NatsMessageTransport verdictsTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
{
    Url = natsUrl,
    Token = builder.Configuration["Nats:Token"],
    Name = "runner-verdicts-in",
    UseJetStream = true,
    StreamName = "kyc-verdicts",
    ConsumerName = "runner-verdict-consumer",
    DeliverPolicy = DeliverPolicy.All,
    StorageType = StorageType.File,
});

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
        new WorkflowExecutorProvider(progress: msg => Console.Error.WriteLine($"[draft-executor-build] {msg}")),
        binder,
        hostTimerWaits: false);
    builder.Services.AddSingleton(draftRunner);
    builder.Services.AddHostedService<DraftRunPumpService>();
}

// Authenticated registration (design §5.5/§16.4): the runner registers through the control plane's authenticated HTTP
// endpoint as a Keycloak client-credentials client, so the control plane derives the trusted principal from the token and
// binds the runner's authorization to it — instead of the runner self-asserting a Pending row into the shared store. It
// is the same machine principal the runner API resolves this runner's bindings from, which is what makes an
// administrator's decision on this registration govern what the runner is subsequently offered.
var runnerRegistrar = new ControlPlaneRunnerRegistrar(
    new HttpClient(),
    controlPlaneBaseUrl,
    runnerEnvironment,
    ControlPlaneRunnerRegistrar.TokenEndpointFor(runnerKeycloakBaseUrl, runnerRealm),
    runnerClientId,
    runnerClientSecret);

// The two long-running loops (design §5.4 registration/heartbeat, §7 dispatch + resume). The registration service is
// constructed explicitly so the optional control-plane registrar (or null) flows in deterministically.
builder.Services.AddHostedService(sp => new RunnerRegistrationService(
    sp.GetRequiredService<IRunnerRegistry>(),
    sp.GetRequiredService<IEnvironmentStore>(),
    sp.GetRequiredService<IEnvironmentRunnerAuthorizationStore>(),
    sp.GetRequiredService<SecuredWorkflowCatalog>(),
    sp.GetRequiredService<RunnerOptions>(),
    sp.GetRequiredService<ILogger<RunnerRegistrationService>>(),
    catalogResumerBackend.IsolationModel,
    runnerRegistrar,
    heartbeatInterval: null,
    runnerApi: runnerClient));
builder.Services.AddHostedService<WorkflowDispatchService>();

// A startup self-check (design §13.5): resolve the seeded credential references against Vault using only the
// runner's read-only token, and assert a write is refused — proving the secret-consumer boundary end to end.
builder.Services.AddHostedService<VaultCredentialSelfCheckService>();

WebApplication app = builder.Build();

// The consumer of the async KYC verdict exchange, built here so it gets a categorized logger from the running host —
// each verdict receipt (and the number of runs it resumed) is logged, so a resume is visible rather than silent.
var verdictConsumer = new ReceiveKycVerdictConsumer(
    verdictsTransport,
    new KycVerdictResumeHandler(
        new WorkflowWorker(stateStore, options.RunnerId),
        catalogResumer,
        runnerEnvironment,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<KycVerdictResumeHandler>()));

// Start the KYC verdict consumer now the process is up: it subscribes to kyc.verdict and resumes suspended async runs.
await verdictConsumer.StartAsync();

// /health (readiness) and /alive (liveness) — the AppHost's WithHttpHealthCheck("/health") polls these.
app.MapDefaultEndpoints();

// A tiny identity endpoint so the dashboard's resource link lands somewhere informative.
app.MapGet("/", () => Results.Text($"Arazzo execution-host runner '{options.RunnerId}'. Health at /health.", "text/plain"));

app.Run();