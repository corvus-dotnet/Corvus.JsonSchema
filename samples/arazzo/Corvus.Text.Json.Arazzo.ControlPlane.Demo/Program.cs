// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The REAL Arazzo control-plane server over a fresh-on-startup Postgres database, seeded with demo workflows + runs,
// serving the build-free web UI from the same origin. Runs under the AppHost (which stands up its Postgres, Vault,
// Keycloak, and the runner) — see the AppHost README for the `aspire start` command.
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.ControlPlane.Demo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Keycloak;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Postgres;
using Corvus.Text.Json.Arazzo.Durability.Vault;
using Corvus.Text.Json.AsyncApi.Nats;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using Corvus.Text.Json.Internal;
using Npgsql;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.FileProviders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry (incl. the Corvus.Arazzo workflow source/meter), health checks,
// service discovery, and HTTP resilience. Under the AppHost this exports traces/logs/metrics to the dashboard;
// run standalone it is a no-op exporter (no OTLP endpoint configured). This host now requires the AppHost, which
// injects the Postgres connection string — there is no standalone store to open.
builder.AddServiceDefaults();

// The shared durability store — a real Postgres database provided by the AppHost's AddPostgres resource. Both this
// host and the runner open ConnectionStrings:workflowstore (the same database, so they share state). This host now
// runs under the AppHost, which stands up Postgres; there is no standalone SQLite fallback.
string connectionString = builder.Configuration.GetConnectionString("workflowstore")
    ?? throw new InvalidOperationException("ConnectionStrings:workflowstore (the shared Postgres database) is required — run this host under the AppHost.");
NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);

// Provision the Postgres control-plane deployment BEFORE any store opens: the ControlPlane.Deployment.Postgres library
// creates every control-plane store's schema (idempotent CREATE TABLE IF NOT EXISTS) AND runs the deployment-agnostic
// security bootstrap (§14.2 rules + read-all shell binding + §16.2-tier-3 genesis-admin grant — the arazzo-admins group
// gets all capability scopes + unrestricted reach; the first admin logs in via OIDC already holding admin, the identity
// analogue of secret-zero) in one call. That library is coupled to Postgres but identity-provider agnostic — the IdP
// (Keycloak here, but any OIDC provider) is wired separately in the composition root below. A real ZeroFailed
// deployment calls exactly this, binding its DeploymentBootstrapOptions from config; here the config is expressed AS
// JSON (validated against the generated schema) so the demo is self-contained. Postgres adapters do not self-create
// schema on ConnectAsync (unlike SQLite), and the runner never runs DDL — it waits for this host's health, by which
// point the tables exist. The AppHost's Postgres is ephemeral (no volume): every run starts empty — reset, no file to wipe.
// The single runtime switch for demo fiction (§W4 seeding split). The AppHost injects ControlPlane__SeedExampleData
// from its own SeedExampleData flag, so one switch drives all example seeding end to end; default true so the
// standalone single-process demo still seeds. A production host sets it false and gets only the real store + policy.
bool seedExampleData = builder.Configuration.GetValue("ControlPlane:SeedExampleData", true);
string genesisScopesJson = string.Join(", ", ControlPlaneScopes.All.Select(s => $"\"{s}\""));

// identityClaimType is the internal `group` DIMENSION (not the OIDC `groups` claim): reach binding applicability is
// decided by MEMBERSHIP over the caller's canonical sys: identity (§16.5.4), and the resolver below maps the `groups`
// claim to the sys:group tag — so the genesis binding keys on `group`, matching sys:group after the prefix is stripped.
// genesisAdditionalClauses pins the ISSUER too (§16.5.4 tag-set selector, S7): the genesis grant applies only to an
// arazzo-admins group asserted by THIS deployment's Keycloak (sys:iss = DemoData.KeycloakIssuer, stamped by the resolver
// below and the directory adapter), so a same-named group from another identity provider does not inherit admin.
// §16.5.1 — the control plane governs its own access approvals via the bootstrapped access-approval workflow (installed
// by the deployment bootstrap, executed by the system runner). Enabled when Keycloak is configured (the secured AppHost
// deployment): the deployment installs the workflow + provisions the runner's OAuth2 credential at the Vault path the
// AppHost seeds. Absent, the built-in direct-to-administrator approval strategy is used.
string? systemApprovalKeycloakBaseUrl = builder.Configuration["ControlPlane:Keycloak:BaseUrl"];
bool enableSystemApprovalWorkflow = !string.IsNullOrWhiteSpace(systemApprovalKeycloakBaseUrl);
string systemWorkflowsOptionJson = enableSystemApprovalWorkflow
    ? $$""" ,"systemWorkflows": { "tokenUrl": "{{systemApprovalKeycloakBaseUrl!.TrimEnd('/')}}/realms/arazzo/protocol/openid-connect/token", "clientSecretRef": "vault://secret/arazzo/controlplane#client-secret", "brokerUrl": "{{builder.Configuration["Nats:Url"]}}", "brokerTokenRef": "vault://secret/arazzo/access-notifications#token" }"""
    : string.Empty;

using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap.DeploymentBootstrapOptions> bootstrapOptionsDoc =
    ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap.DeploymentBootstrapOptions>.Parse(
        System.Text.Encoding.UTF8.GetBytes($$"""
        {
          "genesisAdminGroup": "arazzo-admins",
          "genesisScopes": [{{genesisScopesJson}}],
          "identityClaimType": "group",
          "genesisAdditionalClauses": [{ "dimension": "iss", "value": "{{DemoData.KeycloakIssuer}}" }],
          "internalTagPrefix": "sys:",
          "selfElevationGroups": ["arazzo-admins"],
          "labelOrderings": { "classification": ["public", "internal", "confidential", "restricted"] },
          "seedExampleData": {{(seedExampleData ? "true" : "false")}}{{systemWorkflowsOptionJson}}
        }
        """));
Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap.DeploymentBootstrapOptions bootstrapOptions = bootstrapOptionsDoc.RootElement;
await Corvus.Text.Json.Arazzo.Durability.ControlPlane.Deployment.Postgres.PostgresControlPlaneDeployment.ProvisionAsync(dataSource, bootstrapOptions);

// The seedExampleData flag (read above, and carried into the bootstrap options so the generated schema records it)
// gates every piece of demo fiction below — the example catalog + credential references + developer sandbox, the
// stand-in runner authorizer, and the live sample run. A production deployment leaves it false and provisions only
// the real store + policy + IdP shell.

// The catalog store bakes typed-shape + validation metadata at add time via the code-generation provider.
var metadata = new WorkflowSchemaMetadataProvider();
PostgresWorkflowStateStore postgresStateStore = await PostgresWorkflowStateStore.ConnectAsync(dataSource);

// At rest (§14, backlog #861): checkpoints — step outputs included — are application-encrypted before the backend
// ever sees them. The key arrives from deployment configuration: the AppHost generates one per composition boot
// (the demo resets its data each run, so an ephemeral key is exactly right; a durable deployment sources it from
// its KMS/secret store instead). Absent the key (bare host runs, tests), the store runs unwrapped.
IWorkflowStateStore stateStore = builder.Configuration["ControlPlane:CheckpointProtectionKey"] is { Length: > 0 } checkpointKey
    ? new ProtectedWorkflowStateStore(postgresStateStore, new AesGcmCheckpointProtector(Convert.FromBase64String(checkpointKey)))
    : postgresStateStore;
// Executor-package signing (#879): when the AppHost provisions a control-plane signing vault, sign each compiled
// executor's manifest with its HashiCorp Vault Transit key at catalog-add. The private key stays in that vault (the
// sign runs server-side), a vault the RUNNER cannot reach — the runner verifies with only the exported public key it
// carries in its own deployment config, so a compromised runner cannot forge a package. Absent the signing vault (bare
// host / single-process runs), versions are stored unsigned and the runner loads them without a signature check.
IExecutorPackageSigner? executorSigner = null;
if (builder.Configuration["ControlPlane:SigningVault:Address"] is { Length: > 0 } signingVaultAddress
    && builder.Configuration["SIGNING_VAULT_TOKEN"] is { Length: > 0 } signingVaultToken)
{
    string signingKeyName = builder.Configuration["ControlPlane:SigningVault:KeyName"] ?? "arazzo-executor-signing";
    string signingKeyId = builder.Configuration["ControlPlane:SigningVault:KeyId"] ?? signingKeyName;
    string signingMount = builder.Configuration["ControlPlane:SigningVault:MountPoint"] ?? "transit";
    string signingAlgorithm = builder.Configuration["ControlPlane:SigningVault:Algorithm"] ?? ExecutorSignatureAlgorithms.EcdsaP256Sha256;
    var signingVaultClient = new VaultClient(new VaultClientSettings(signingVaultAddress, new TokenAuthMethodInfo(signingVaultToken)));
    executorSigner = new VaultTransitExecutorPackageSigner(signingVaultClient, signingKeyName, signingKeyId, signingAlgorithm, signingMount);
}

// The executor provider compiles a runnable executor into each catalogued version at add time (alongside the typed
// metadata) — so a resumed run can re-enter the real generated Arazzo executor (live execution, §5/§8).
// The provider's build progress is surfaced (not swallowed): a "skipped" line means a catalogued version could not be
// compiled into a runnable executor. For an ordinary user workflow that is a diagnosable state; for a bootstrapped SYSTEM
// workflow it is a deployment error, so the message must be visible rather than lost to a null progress sink.
PostgresWorkflowCatalogStore catalogStore = await PostgresWorkflowCatalogStore.ConnectAsync(
    dataSource,
    metadataProvider: metadata,
    executorProvider: new WorkflowExecutorProvider(progress: msg =>
    {
        if (msg.Contains("skipped", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"[executor-build] {msg}");
        }
    }),
    signer: executorSigner);

// Live execution (§5/§8): a resumed run re-enters its baked executor, calling the real external source services
// (onboarding, ledger, kyc — their own processes + databases). The resumer is built now but invoked only after the
// server is listening, so it reads the host base URL lazily (set in the ApplicationStarted callback below — used only
// as the never-hit /svc fallback root) — the same delegate also drives one fresh run at startup to demonstrate it.
var selfBaseUrl = new System.Runtime.CompilerServices.StrongBox<string?>(null);

// The onboarding source is a real external service (its own process + database — the AppHost stands it up and injects
// its endpoint). Both this host's live-execution transports and the out-of-process runner route the onboarding source
// there; the former inline /svc/onboarding mock is gone. Required: this host runs under the AppHost.
string onboardingBaseUrl = builder.Configuration["ControlPlane:Sources:Onboarding"]
    ?? throw new InvalidOperationException("ControlPlane:Sources:Onboarding (the onboarding service endpoint) is required — the AppHost injects it.");
string ledgerBaseUrl = builder.Configuration["ControlPlane:Sources:Ledger"]
    ?? throw new InvalidOperationException("ControlPlane:Sources:Ledger (the ledger service endpoint) is required — the AppHost injects it.");
string kycBaseUrl = builder.Configuration["ControlPlane:Sources:Kyc"]
    ?? throw new InvalidOperationException("ControlPlane:Sources:Kyc (the KYC service endpoint) is required — the AppHost injects it.");

// The application-owned message bus (NATS JetStream) — the AppHost injects its URL. The control plane's live resumer
// executes the seeded async onboarding run, whose requestKycReview send step publishes to kyc.requests through this
// transport (each channel is its own JetStream stream, so this is scoped to kyc-requests). The verdict receive is
// durable (the run suspends), so this transport only sends here; the runner's consumer resumes the run when a verdict
// arrives. This replaces the former in-process InMemoryMessageTransport (design §8).
string natsUrl = builder.Configuration["Nats:Url"]
    ?? throw new InvalidOperationException("Nats:Url (the KYC message bus) is required — the AppHost injects it.");
NatsMessageTransport messageTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
{
    Url = natsUrl,
    Token = builder.Configuration["Nats:Token"],
    Name = "controlplane-requests-out",
    UseJetStream = true,
    StreamName = "kyc-requests",
    StorageType = StorageType.File,
});

// §16.5.1: when the system approval workflow is enabled, an approver's decision is published on the access.decision
// channel the system runner's consumer subscribes to (its own JetStream stream), so a governed approval advances the
// suspended run rather than granting inline. Its own transport because each channel is a distinct JetStream stream.
Corvus.Text.Json.AsyncApi.IMessageTransport? decisionTransport = null;
if (enableSystemApprovalWorkflow)
{
    decisionTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
    {
        Url = natsUrl,
        Token = builder.Configuration["Nats:Token"],
        Name = "controlplane-decisions-out",
        UseJetStream = true,
        StreamName = "access-decisions",
        StorageType = StorageType.File,
    });
}
WorkflowResumer liveResumer = DemoData.CreateLiveResumer(catalogStore, () => selfBaseUrl.Value ?? throw new InvalidOperationException("The host base URL is not available until the server has started."), onboardingBaseUrl, ledgerBaseUrl, kycBaseUrl, messageTransport);

// The deployment's run-derivation key (ADR 0065 §9): idempotent starts and the schedules surface derive run ids
// under it — the same instance DemoData seeds schedules with, so seeded and API-created schedules share one id space.
var management = new SecuredWorkflowManagement(stateStore, "demo", liveResumer, runDerivation: DemoData.RunDerivation);

// A workflow's §15 administrator set governs who may approve access requests for it (and publish further versions).
// The submitter of version 1 establishes administration (DemoData seeds the workflows as administered by the
// arazzo-admins group); the access-request approval flow routes a request to these administrators.
var administrators = await PostgresWorkflowAdministratorStore.ConnectAsync(dataSource);
// The wait-index cast holds for both branches of the protection wrap above (the protected store delegates its
// index members; index entries pass through in the clear by design, so queries never touch checkpoint bytes).
var catalog = new SecuredWorkflowCatalog(catalogStore, (IWorkflowWaitIndex)stateStore, "demo", administrators: administrators);

// The runner registry is store-backed and shared, so a runner registering in its own process is visible to this
// control plane's GET /runners (§5.4) — not an in-memory table only this process can see.
PostgresRunnerRegistry runners = await PostgresRunnerRegistry.ConnectAsync(dataSource);

// The §5.5 runner-authorization store, shared with the runner process: a runner records a Pending authorization to
// serve its environment when it registers; this control plane reads that inbox and an environment administrator
// authorizes (or revokes) it. It must be the same store both processes open — hence the shared Postgres database.
PostgresEnvironmentRunnerAuthorizationStore runnerAuthorizations = await PostgresEnvironmentRunnerAuthorizationStore.ConnectAsync(dataSource);

// The §13 source-credential store. The control plane manages credential *references* + metadata only — it never
// binds to the secret store (the §13/§13.5 invariant); the runner is the read-only secret consumer. This lights
// up the /credentials surface (and the CLI + web UI) over the shared store.
PostgresSourceCredentialStore sourceCredentials = await PostgresSourceCredentialStore.ConnectAsync(dataSource);

// §18 debug runs: the durable draft-run stores — the captured {document, sources} blob and the SimulationTrace-shaped
// metadata trace, both shared with any out-of-process runner — plus a governed environment store (drafts run only in
// an environment whose administrators allow it). The IN-PROCESS runner executes the enqueued $draft debug runs against
// the real external source services: a single-process deployment advances debug runs by pumping this runner (started
// once the host is listening, below). The control plane only marks runs claimable — it never executes (§18).
PostgresDraftRunStore draftRunStore = await PostgresDraftRunStore.ConnectAsync(dataSource);
PostgresDraftRunTraceStore draftRunTraceStore = await PostgresDraftRunTraceStore.ConnectAsync(dataSource);
PostgresEnvironmentStore environmentStore = await PostgresEnvironmentStore.ConnectAsync(dataSource);

// The durable working-copy store (workflow-designer design §4.1): a designer's in-progress edits survive a restart and
// are shared across control-plane instances, rather than living only in memory. One of the nine fanned-out backends.
PostgresWorkspaceWorkflowStore workspaceStore = await PostgresWorkspaceWorkflowStore.ConnectAsync(dataSource);

// The governance stores (§7.6-§7.8, §16.5.4). Passing these to MapArazzoControlPlane makes the availability matrix,
// promotion requests, the source registry, per-environment administration, and grantee typeahead durable + shared —
// previously each fell back to a fresh in-memory instance (ephemeral, empty, invisible to the runner). Wiring
// availabilityStore also restores run-creation availability gating (the catalog handler was getting null).
PostgresAvailabilityStore availabilityStore = await PostgresAvailabilityStore.ConnectAsync(dataSource);
PostgresAvailabilityRequestStore availabilityRequestStore = await PostgresAvailabilityRequestStore.ConnectAsync(dataSource);
PostgresSourceStore sourceStore = await PostgresSourceStore.ConnectAsync(dataSource);
PostgresEnvironmentAdministratorStore environmentAdministratorStore = await PostgresEnvironmentAdministratorStore.ConnectAsync(dataSource);

// §16.5.1: install the bootstrapped access-approval system workflow through the HOST's catalog store — which, unlike the
// deployment bootstrap's plain store, compiles and signs the executor at catalog-add time (executorProvider + signer,
// wired at line ~131), so the catalogued version is runnable and its executor verifies against the system runner's trust
// key. Idempotent; establishes the internal environment, the runner's OAuth2 credential, the catalogued+signed version,
// and its availability. Enabled when systemWorkflows is present in the bootstrap options (secured AppHost deployment).
if (enableSystemApprovalWorkflow)
{
    await new Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap.DefaultDeploymentBootstrap().BootstrapSystemWorkflowsAsync(
        catalogStore,
        (Corvus.Text.Json.Arazzo.Durability.IWorkflowWaitIndex)stateStore,
        administrators,
        sourceCredentials,
        availabilityStore,
        environmentStore,
        environmentAdministratorStore,
        bootstrapOptions,
        // The bake probe (§16.5.1 hard-fail): an un-bakeable SYSTEM workflow refuses the deployment
        // instead of cataloguing a non-runnable version whose runner then crash-loops causelessly.
        new WorkflowExecutorProvider(progress: msg => Console.Error.WriteLine($"[system-workflow-bake] {msg}")),
        // The sources registry: the install registers controlplane + access-notifications so the credentials
        // surface classifies their bindings (ADR 0051) and operators see the system sources.
        sourceStore);
}
PostgresObservedIdentityStore observedIdentityStore = await PostgresObservedIdentityStore.ConnectAsync(dataSource);

// Serverless execution backend (#876): the two store-as-queue boundaries between this control plane and the runner.
// The build-job queue — this host's build worker (Phase 3b) drains it, compiling each Ready version into a native-AOT
// executor package; and the workflow-deployment queue — the RUNNER's deploy worker drains it, deploying each package to
// its cloud function. This host reads both back: the catalog handler's dispatch gate blocks a run until its deployment
// is Deployed, and (Phase 3b) the build worker enqueues a deployment once a build reports Ready. Their schema is created
// by PostgresControlPlaneDeployment.ProvisionAsync above (the control plane owns DDL); ConnectAsync never runs DDL.
PostgresNativeBuildJobStore nativeBuildJobStore = await PostgresNativeBuildJobStore.ConnectAsync(dataSource);
PostgresWorkflowDeploymentStore workflowDeploymentStore = await PostgresWorkflowDeploymentStore.ConnectAsync(dataSource);

// Serverless execution backend (#876, ADR 0055/0059): the control-plane build worker. It drains the native-AOT
// build-job queue, compiling each Ready version's signed executor IL into a native binary INSIDE the arazzo-aot-builder
// container, signs the binary, and (deploy-on-build) enqueues a Queued deployment the runner's deploy worker claims. It
// runs HERE, not on the runner, because the build reads the catalog and signs against the control-plane signing vault —
// secrets the runner never holds (ADR 0059). It is wired only when the fully-signed chain is present (ADR 0055: a version
// reaches a serverless environment only through a signed chain, else its deployment stays in-process): the signing vault
// (executorSigner, above), the exported PUBLIC half of the signing key the build re-verifies the executor IL against, and
// the local package feed the container restores the pinned runtime graph from. Absent any of these the serverless build
// path stays dark and Isolated environments hold their runs at the build-ready gate — surfaced below, never silently.
string? aotFeedPath = builder.Configuration["ControlPlane:Aot:FeedPath"];
string? aotRuntimeVersion = builder.Configuration["ControlPlane:Aot:RuntimeVersion"];
string? aotTrustPublicKeyFile = builder.Configuration["ControlPlane:ExecutorTrust:PublicKeyFile"];
bool serverlessBuildWorkerWired = false;
if (executorSigner is { } buildSigner
    && !string.IsNullOrWhiteSpace(aotFeedPath) && Directory.Exists(aotFeedPath)
    && !string.IsNullOrWhiteSpace(aotRuntimeVersion)
    && !string.IsNullOrWhiteSpace(aotTrustPublicKeyFile) && File.Exists(aotTrustPublicKeyFile))
{
    // Build only from a verified, signed executor: re-verify the SAME executor-IL signature the catalog store stamped at
    // add time, against the exported public half of the signing key (the private half never leaves the vault).
    string aotTrustKeyId = builder.Configuration["ControlPlane:ExecutorTrust:KeyId"]
        ?? builder.Configuration["ControlPlane:SigningVault:KeyId"]
        ?? builder.Configuration["ControlPlane:SigningVault:KeyName"] ?? "arazzo-executor-signing";
    IExecutorPackageVerifier buildVerifier = TrustStoreExecutorPackageVerifier.FromPem(
        new Dictionary<string, string>(StringComparer.Ordinal) { [aotTrustKeyId] = await File.ReadAllTextAsync(aotTrustPublicKeyFile) });

    // The container builder mounts the local package feed read-only at the path the feed config names, and runs the pared
    // arazzo-aot-builder image (Amazon Linux 2023, matching the Lambda provided.al2023 glibc LocalStack itself runs).
    var containerAotBuilder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions
    {
        ContainerImage = builder.Configuration["ControlPlane:Aot:BuilderImage"] ?? "arazzo-aot-builder:net10",
        ContainerCommand = builder.Configuration["ControlPlane:Aot:ContainerCli"] ?? "podman",
        ReadOnlyMounts = [(aotFeedPath, "/work/local-packages")],
    });

    // The host-app pins the runtime graph at the feed's package version (the serverless-aot guide's invariant 2: the feed
    // retains it) and restores from the local feed plus the .NET package sources ILCompiler resolves from.
    var aotBuildService = new WorkflowAotBuildService(
        buildVerifier,
        buildSigner,
        containerAotBuilder,
        new AotHostAppOptions
        {
            RuntimePackageVersion = aotRuntimeVersion,
            FeedSources =
            [
                ("local", "/work/local-packages"),
                ("nuget.org", "https://api.nuget.org/v3/index.json"),
                ("dotnet-eng", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                ("dotnet-libraries", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json"),
            ],
        });

    // AddNativeAotBuildWorker resolves these four collaborators from DI: the build-job queue it drains, the catalog it
    // loads each version's package from and attaches the native binary back into, the build service, and (deploy-on-build)
    // the deployment queue it enqueues a Queued deployment into on a Ready build.
    builder.Services.AddSingleton<INativeBuildJobStore>(nativeBuildJobStore);
    builder.Services.AddSingleton<IWorkflowCatalogStore>(catalogStore);
    builder.Services.AddSingleton(aotBuildService);
    builder.Services.AddSingleton<IWorkflowDeploymentStore>(workflowDeploymentStore);
    builder.Services.AddNativeAotBuildWorker(new NativeBuildWorkerOptions
    {
        WorkerId = $"cp-build-{System.Environment.MachineName}-{System.Environment.ProcessId}",
        PollInterval = TimeSpan.FromSeconds(2),
    });
    serverlessBuildWorkerWired = true;
}
else if (executorSigner is not null)
{
    // Signing is configured (so the serverless path is intended) but the build worker could not wire: name the missing
    // input rather than leaving an operator to wonder why Isolated runs never leave the build-ready gate.
    Console.Error.WriteLine(
        "[serverless-build] Executor signing is configured but the control-plane build worker is NOT wired. Need " +
        $"ControlPlane:Aot:FeedPath (a directory; got '{aotFeedPath}'), ControlPlane:Aot:RuntimeVersion (got '{aotRuntimeVersion}'), and " +
        $"ControlPlane:ExecutorTrust:PublicKeyFile (a file; got '{aotTrustPublicKeyFile}'). Isolated environments will hold runs at the build-ready gate.");
}

var draftRunner = new InProcessDraftRunner(
    stateStore,
    owner: "arazzo-inprocess-draft-runner",
    // Pinned to the draft-enabled environment: the dispatcher claims only the $draft runs started in THIS environment
    // (a real deployment runs one runner per environment). This must match the environment debug runs are started in.
    runnerEnvironment: "development",
    draftRunStore,
    draftRunTraceStore,
    new WorkflowExecutorProvider(progress: msg => Console.Error.WriteLine($"[draft-executor-build] {msg}")),
    DemoData.CreateLiveBinder(() => selfBaseUrl.Value ?? throw new InvalidOperationException("The host base URL is not available until the server has started."), onboardingBaseUrl, ledgerBaseUrl, kycBaseUrl, messageTransport),
    // Do NOT host timer waits here: the worker's ResumeDueTimersAsync resumes EVERY due-timer run in the shared store,
    // including seeded CATALOG runs this draft-only resumer cannot host. A draft run that suspends on a retry timer is
    // out of scope for the minimum stand-up (the base onboard-customer workflow has none).
    hostTimerWaits: false);

// The row-security authoring API (§14.2) is served from a security-policy store.
var securityPolicy = await PostgresSecurityPolicyStore.ConnectAsync(dataSource);

// The security policy the runtime reads was seeded by PostgresControlPlaneDeployment.ProvisionAsync above (the
// genesis-admin grant, the read-all shell binding, and the §14.2 rules). securityPolicy (connected above) now reads
// those rows; labelOrderings comes from the same bootstrapOptions.

// The entitlement resolver (§16.5.2 Decision-A): ONE PersistentRowSecurityPolicy over the security-policy store backs
// both layers — the claims transformer unions its ResolveGrantedScopes into the scope claim (capability), and it is
// passed to MapArazzoControlPlane as the row-reach policy. The principal's Keycloak groups become its sys: identity.
// The ordered tag dimensions (§14.2) come from the same config — surfaced read-only via GET /security/orderings.
var labelOrderings = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap.DefaultDeploymentBootstrap.BuildLabelOrderings(bootstrapOptions);
var entitlements = new PersistentRowSecurityPolicy(
    securityPolicy,
    // A Keycloak principal's row identity is its group tags PLUS its per-person subject (sys:sub, §16.5.4) PLUS the
    // deployment issuer (sys:iss, §16.5.5) — the same DemoData.KeycloakIssuer the seeded admin grants and the
    // grantee-directory adapter stamp. Stamping sys:sub makes a live member's identity a STRICT SUPERSET of a group-only
    // grant (e.g. the seeded {sys:group=arazzo-admins, sys:iss} founder), so the member administers / reaches / may-use it
    // by MEMBERSHIP (§16.5.4 — caller contains founder), not set-equality — exercising the membership model live rather
    // than keeping every identity set-equal. A principal with no groups (e.g. a DevApiKey) carries no identity here and
    // resolves through the unscoped / System path (unchanged).
    internalTagResolver: DemoData.ResolveInternalTags,
    orderings: labelOrderings);
await entitlements.RefreshAsync();

// The access-request store (§16.5) — Postgres, shared with the runner like every other control-plane store.
var accessRequests = await PostgresAccessRequestStore.ConnectAsync(dataSource);

// arazzo-admins members are eligible to self-elevate (JIT activation, no human approver, §16.5.3); everyone else
// must submit a request and be approved by a §15 administrator of the target workflow.
Func<ClaimsPrincipal, AccessRequest, bool> eligibleForSelfElevation =
    static (principal, _) => principal.FindAll("groups").Any(c => c.Value == "arazzo-admins");

// Control-plane authorization is per-deployment (design §14.1). The real strategy is OIDC: bearer tokens from
// Keycloak (humans via the BFF, machines via client-credentials, §16.3), with the dev API-key kept for
// break-glass / scripts (§16.2). Gated behind config so the open demo + its build-free UI still run by default.
// Enable with `ControlPlane__RequireAuthorization=true`, then present a Keycloak bearer token, or an
// `X-Api-Key: demo-admin-key` (all scopes) / `demo-readonly-key` (catalog:read + runs:read) header.
// The BFF session cookie name, shared by the cookie config and the library anti-forgery check (§16.3).
const string SessionCookieName = "arazzo.session";

// Stated, never defaulted. ADR 0016 exists to remove insecure-by-omission, and a posture that falls back to "off" when
// the setting is missing is the same defect one layer out: a typo in the key name, or a compose file that lost the
// variable, silently serves the whole control plane unauthenticated. Absent configuration is a startup failure.
bool requireAuthorization = builder.Configuration.GetValue<bool?>("ControlPlane:RequireAuthorization")
    ?? throw new InvalidOperationException(
        "ControlPlane:RequireAuthorization must be set explicitly to true or false. There is no default: it decides whether this host authenticates anyone at all (ADR 0016).");
if (requireAuthorization)
{
    // Three ways in (§16.3): browser users via the BFF (interactive OIDC → an HttpOnly cookie session); API
    // callers with a Keycloak bearer token (CLI/machines); and the dev API-key (break-glass/scripts, §16.2). A
    // forwarding policy scheme routes each request to the right scheme by what it presents.
    builder.Services
        .AddAuthentication("control-plane")
        .AddPolicyScheme("control-plane", "BFF cookie, Keycloak bearer, or dev API key", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.ContainsKey(DevApiKeyAuthenticationHandler.ApiKeyHeader))
                {
                    return DevApiKeyAuthenticationHandler.SchemeName;
                }

                // A bearer token → validate it; otherwise it's a browser, served from the BFF cookie session.
                return context.Request.Headers.Authorization.Any(
                    h => h is not null && h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    ? JwtBearerDefaults.AuthenticationScheme
                    : CookieAuthenticationDefaults.AuthenticationScheme;
            };
        })
        .AddKeycloakJwtBearer("keycloak", realm: "arazzo", options =>
        {
            // The demo runs Keycloak on http and does not pin an audience; the realm + signature are validated.
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters.ValidateAudience = false;
        })
        .AddScheme<DevApiKeyOptions, DevApiKeyAuthenticationHandler>(
            DevApiKeyAuthenticationHandler.SchemeName,
            options =>
            {
                options.Keys["demo-admin-key"] = string.Join(' ', ControlPlaneScopes.All);
                options.Keys["demo-readonly-key"] = $"{ControlPlaneScopes.CatalogRead} {ControlPlaneScopes.RunsRead}";
                // The admin key is a member of the arazzo-admins group, so it inherits the §16.2 genesis grant's full
                // row reach — making it a true full administrator (all scopes AND reach over every workflow), which is
                // what lets it read the catalog and trigger runs of the prod/kyc-tagged workflows.
                options.Groups["demo-admin-key"] = "arazzo-admins";
            })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            // The BFF holds the tokens; the SPA never sees them (it calls same-origin with this HttpOnly cookie).
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Name = SessionCookieName;

            // API calls must get 401/403 (the SPA redirects to /login) — never a server-side HTML login redirect.
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        })
        .AddKeycloakOpenIdConnect("keycloak", realm: "arazzo", OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            // The BFF: Authorization Code + PKCE against the arazzo-ui client; tokens are kept server-side in the
            // cookie session. The `groups` claim flows from the id token into the principal, where the §14.1
            // transformer maps it to capability scopes — the same mapping the bearer path uses.
            options.ClientId = "arazzo-ui";
            options.ClientSecret = "arazzo-ui-dev-secret";
            options.ResponseType = "code";
            options.UsePkce = true;

            // Use the standard authorize redirect (Keycloak 26 advertises PAR, which net10's handler would
            // otherwise use); the demo keeps the simpler flow.
            options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
            options.RequireHttpsMetadata = false;
            options.SaveTokens = true;
            options.Scope.Add("openid");
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.TokenValidationParameters.NameClaimType = "preferred_username";
        });

    // The demo's concrete §14.1 mapping: Keycloak `groups` → the capability scopes the policies read (§16.5). The
    // transformer also unions the principal's stored grants (claims ∪ entitlements), so it shares the one resolver.
    builder.Services.AddSingleton(entitlements);
    builder.Services.AddSingleton<IClaimsTransformation, KeycloakClaimsTransformer>();
    builder.Services.AddArazzoControlPlaneAuthorization();

    // The runner API's own scope (ADR 0065). It is what a runner presents INSTEAD of a store credential, so it is
    // deliberately not one of the control-plane capability scopes: holding it lets a runner ask for work, and grants
    // reach over nothing. Which runs it is offered comes from the environments an administrator bound its machine
    // principal to.
    builder.Services.AddArazzoRunnerAuthorization();

    // Scoped-mode row security reads the caller's principal through IHttpContextAccessor; the library requires the host
    // to register it to switch enforcement on (ControlPlaneRowSecurity: "the host must register it ... to switch
    // enforcement on"). Without this, MapArazzoControlPlane throws at startup in Scoped mode — the gap that stayed
    // hidden while the demo ran Open.
    builder.Services.AddHttpContextAccessor();
}

// The example seed layers the demo fiction on top of the real bootstrap above: catalogued workflow versions, the
// source-credential references, and the developer sandbox environment (§18). It is the counterpart to the config-driven
// bootstrap — a production deployment omits it entirely; the demo opts in via seedExampleData. The instance is created
// unconditionally because the live-sample run below (post-startup) also goes through it.
IExampleSeed exampleSeed = new ArazzoExampleSeed();
if (seedExampleData)
{
    string specsDir = Path.Combine(builder.Environment.ContentRootPath, "specs");
    await exampleSeed.SeedAsync(new ExampleSeedContext(
        catalog, sourceCredentials, environmentStore, environmentAdministratorStore, sourceStore,
        availabilityStore, accessRequests, availabilityRequestStore, securityPolicy, specsDir, natsUrl));

    // The persona rules/bindings the seed just wrote must take effect for THIS process's resolver (capability scopes
    // + row reach) without waiting for a write-triggered refresh.
    await entitlements.RefreshAsync(default);

    // Seed the observed-identity ("seen") typeahead so the grant pickers are non-empty on a fresh boot: the realm groups
    // as Team grantees, each stamped the {sys:group=<name>, sys:iss} identity (DemoData) — a SUBSET of a live member's now
    // richer {sys:group, sys:sub, sys:iss}, so a grant on an observed group pick confers reach to every member of that
    // group by MEMBERSHIP (§16.5.4), exactly like a directory pick. Provenance "seed" marks the origin.
    static Corvus.Text.Json.Arazzo.Durability.JsonString Observed(string v)
    {
        // The generated scalar Create() replaces the interpolate + GetBytes + Parse round trip (and escapes correctly);
        // the Clone stays — the value outlives the pooled document.
        using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> doc =
            Corvus.Text.Json.Arazzo.Durability.JsonString.Create(v);
        return doc.RootElement.Clone();
    }
    foreach ((string group, string label) in new[]
    {
        ("arazzo-admins", "Arazzo administrators"),
        ("payments", "Payments team"),
        ("onboarding", "Onboarding team"),
        ("observers", "Observers"),
        ("env-admins", "Environment administrators"),
        ("reconcile-owners", "Reconcile owners"),
    })
    {
        await observedIdentityStore.SeenAsync(
            GranteeKind.Team.ToObservedKind(), Observed(group), Observed(label), DemoData.GroupIdentity(group), complete: true, "seed", default);
    }
}

// Serverless (Isolated) demo (#876, ADR 0055/0058): stand up the Isolated environment the ServerlessRunner serves and
// publish a version into it, so the whole serverless loop runs live. Only when the control-plane build worker is wired
// (the runtime feed is present) — otherwise there is nothing to build, deploy, or invoke. Reusing an existing catalogued
// workflow (nightly-reconcile v2) avoids authoring a new spec: a source-less workflow is impossible (Arazzo requires
// minItems:1 steps and every step references a source), and the point of the demo is the serverless PATH — build the
// native binary in the pared arazzo-aot-builder container, deploy it to LocalStack as a Lambda, invoke it — not the
// workflow's content. Making the version available in an Isolated environment enqueues its native build
// (deploy-on-publish); the direct-store MakeAvailableAsync bypasses that enqueue, so this mirrors the availability
// handler and enqueues the NativeBuildJob explicitly.
if (seedExampleData && serverlessBuildWorkerWired)
{
    const string isolatedEnvironment = "isolated";
    const string isolatedRuntimeIdentifier = "linux-x64";
    const string serverlessWorkflowId = "serverless-check";
    const int serverlessVersion = 1;

    // The Isolated environment (ADR 0058): its runs execute in a deployed native-AOT function, never in-process. Parsed
    // as JSON (like the run-start gate's own environment fixtures) so requiredIsolation + runtimeIdentifier are set
    // directly; the store stamps createdAt/etc. Governed by arazzo-admins like every other seeded environment (§7.7).
    using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.Environments.Environment> isolatedEnv =
        ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.Environments.Environment>.Parse(
            System.Text.Encoding.UTF8.GetBytes($$"""
            {
              "name": "{{isolatedEnvironment}}",
              "displayName": "Isolated (serverless)",
              "description": "The serverless Isolated environment (ADR 0058): runs execute in a deployed native-AOT AWS Lambda function (LocalStack here), not in-process.",
              "allowsDraftRuns": false,
              "requiredIsolation": "Isolated",
              "runtimeIdentifier": "{{isolatedRuntimeIdentifier}}"
            }
            """)))
    {
        (await environmentStore.AddAsync(isolatedEnv.RootElement, "demo", default)).Dispose();
    }

    await new SecuredEnvironmentAdministration(environmentAdministratorStore, "demo")
        .EstablishAsync(isolatedEnvironment, DemoData.GroupIdentity("arazzo-admins"), default, false, default, false, default);

    // Catalogue the source-simple serverless-check workflow (its executor is compiled + signed at add-time like every
    // version). It has a single GET on the 'echo' source the ServerlessRunner serves, so a run of it executes entirely
    // inside the deployed Lambda and completes — the cleanest demonstration of the serverless path end to end.
    string serverlessSpecsDir = Path.Combine(builder.Environment.ContentRootPath, "specs");
    ReadOnlyMemory<byte> serverlessPackage = WorkflowPackage.Pack(
        await File.ReadAllBytesAsync(Path.Combine(serverlessSpecsDir, "serverless-check.arazzo.json")),
        [new KeyValuePair<string, byte[]>("echo", await File.ReadAllBytesAsync(Path.Combine(serverlessSpecsDir, "echo.openapi.json")))]);
    (await catalog.AddAsync(serverlessPackage, new CatalogOwner("Serverless Demo", "serverless@example.com", "Platform", null), TagSet.FromTags(["serverless"]), DemoData.GroupIdentity("arazzo-admins"), default)).Dispose();

    // Publish serverless-check v1 into the Isolated environment: make it available, then enqueue the native build for the
    // environment's runtime target. The control-plane build worker compiles + signs the native binary in the container,
    // deploy-on-build enqueues a deployment, and the ServerlessRunner deploys it to LocalStack. A run started in this
    // environment then dispatches to the ServerlessRunner, which invokes the deployed function.
    (await availabilityStore.MakeAvailableAsync(serverlessWorkflowId, serverlessVersion, isolatedEnvironment, "demo", default)).Entry.Dispose();
    using (ParsedJsonDocument<NativeBuildJob> buildDraft =
        NativeBuildJob.Draft(serverlessWorkflowId, serverlessVersion, isolatedEnvironment, isolatedRuntimeIdentifier, null))
    {
        (await nativeBuildJobStore.EnqueueAsync(buildDraft.RootElement, "demo", default)).Dispose();
    }
}

// DEMO: the open demo has no interactive administrator, so stand in for the administrators of the environments this
// composition starts runners for and make their §5.5 authorization decisions up front. Since ADR 0065 decision 2 a
// runner cannot announce itself — registration requires a decision that already names it and its machine principal — so
// without this the out-of-process runners are refused registration and never claim catalogued runs. It keeps the §5.5
// semantic intact (an administrator, never the runner, decides); production does it via the UI/API, so this is part of
// the example fiction and only wired when the deployment opts in.
if (seedExampleData)
{
    builder.Services.AddHostedService(sp => new RunnerPreAuthorizationService(
        runnerAuthorizations,
        sp.GetRequiredService<ILogger<RunnerPreAuthorizationService>>()));
}

WebApplication app = builder.Build();

// /health (readiness) and /alive (liveness) — the AppHost's WithHttpHealthCheck("/health") polls these.
app.MapDefaultEndpoints();

if (requireAuthorization)
{
    // BFF anti-forgery (§16.3) — before authn/authz so a forged request is rejected up front (defence in depth).
    // Provided by the control-plane server library so any deployment adds it with one call; the SPA sends the
    // X-CSRF header on every API request, which (combined with the cookie) forces a same-origin CORS preflight.
    app.UseArazzoControlPlaneAntiForgery(SessionCookieName);

    app.UseAuthentication();
    app.UseAuthorization();

    // BFF endpoints (§16.3). The SPA is same-origin and carries the HttpOnly cookie automatically; on a 401 it
    // sends the browser to /login (the OIDC challenge → Keycloak), and reads /me to show who is signed in.
    app.MapGet("/login", (string? returnUrl) =>
        Results.Challenge(
            new AuthenticationProperties { RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl },
            [OpenIdConnectDefaults.AuthenticationScheme]));

    app.MapPost("/logout", async (HttpContext http) =>
    {
        // RP-initiated logout, robust to a stale session. Read the saved tokens, then ALWAYS clear the local cookie first
        // so the user is signed out of the app no matter what Keycloak does next.
        AuthenticateResult auth = await http.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        string? idToken = auth.Properties?.GetTokenValue("id_token");
        string? refreshToken = auth.Properties?.GetTokenValue("refresh_token");
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Keycloak's end-session endpoint needs a VALID, unexpired id_token_hint. We only saved the login-time id_token,
        // which has a short lifespan (~5 min) and is orphaned if Keycloak was restarted (fresh signing keys) — Keycloak then
        // rejects a stale/expired hint with "Invalid parameter: id_token_hint". So mint a fresh id_token from the saved
        // refresh_token: a success proves the session is live, and we do the full sign-out (ending the Keycloak SSO session
        // too, so the next sign-in re-authenticates). If the refresh fails the session is genuinely stale, so we skip the
        // Keycloak round-trip and land the (already locally signed-out) user back on /. Carrying ONLY the id_token into the
        // OIDC properties keeps it out of the `state` param (the whole token set there would bloat the URL to a 431).
        string? hint = !string.IsNullOrEmpty(refreshToken)
            ? await RefreshIdTokenAsync(http.RequestServices, refreshToken, http.RequestAborted)
            : idToken;

        if (!string.IsNullOrEmpty(hint))
        {
            AuthenticationProperties props = new() { RedirectUri = "/" };
            props.StoreTokens([new AuthenticationToken { Name = "id_token", Value = hint }]);
            await http.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, props);
            return;
        }

        http.Response.Redirect("/");
    });

    app.MapGet("/me", (ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        ? Results.Json(new
        {
            name = user.Identity!.Name,
            groups = user.FindAll("groups").Select(static c => c.Value).ToArray(),
        })
        : Results.Unauthorized());
}

// Serve a demo page (wwwroot/index.html) and the build-free UI source (web/arazzo-control-plane-ui) at /ui.
app.UseDefaultFiles();
app.UseStaticFiles();
string uiRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "web", "arazzo-control-plane-ui"));
if (Directory.Exists(uiRoot))
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(uiRoot), RequestPath = "/ui" });
}
else
{
    app.Logger.LogWarning("Web UI not found at {UiRoot}; the API is still available under /arazzo/v1.", uiRoot);
}

// The workflow designer's production entry: a clean, app-linked route that serves the design surface in LIVE mode
// (the page detects the /designer path → its data calls go through the real /arazzo/v1 with the BFF auth fetch, so
// it authenticates like the rest of the app). The page's own assets load absolutely from /ui.
string designerPage = Path.Combine(uiRoot, "demo", "designer.html");
if (File.Exists(designerPage))
{
    app.MapGet("/designer", () => Results.File(designerPage, "text/html"));
}

// The connected-provider MECHANISM demo's spec endpoint (ADR 0052 tier 4): an OpenAPI document whose endpoint
// accepts the realm's bearer token — 401 anonymous, readable with a signed-in realm user's token. "Fetch URL"
// against this host exercises the whole interactive path live: the pane offers Connect (the Demo Keycloak
// provider covers localhost), the popup signs the user in, and the fetch runs AS them with a token this endpoint
// accepts. This demonstrates the tier-4 mechanism (an endpoint that accepts the brokered token); it is NOT an
// independent third-party portal (the endpoint validates this deployment's own realm), and a portal you merely
// log into in a browser would instead use browser-mediated acquisition (paste/upload), not this. Secured
// composition only (there is no auth stack to sit behind otherwise).
if (requireAuthorization)
{
    const string portalSampleSpec = /*lang=json,strict*/ """
        {
          "openapi": "3.1.0",
          "info": { "title": "Portal Petstore", "version": "1.0.0", "description": "A sample spec whose endpoint accepts the demo realm's bearer token, so the fetch pane's connected-provider (tier 4) mechanism is live-testable." },
          "paths": {
            "/pets": {
              "get": {
                "operationId": "listPets",
                "summary": "List pets",
                "responses": { "200": { "description": "A page of pets." } }
              },
              "post": {
                "operationId": "createPet",
                "summary": "Create a pet",
                "responses": { "201": { "description": "The pet was created." } }
              }
            }
          }
        }
        """;
    app.MapGet("/portal/specs/petstore.json", () => Results.Text(portalSampleSpec, "application/json"))
        .RequireAuthorization();
}

// The connected-provider registry (ADR 0052) and the GitHub App broker (workflow-designer §4.7). GitHub folds in as
// provider #1: its OAuth registration becomes the registry's 'github' entry, the shared ProviderBroker owns the
// sign-in/custody machinery, and the GitHubBroker keeps the repos/browse surface over that shared custody — so one
// GitHub sign-in serves the Git panel and the fetch pane alike. Enabled only when the deployment supplies a GitHub
// App — the client id (public, so plain config) plus the secret resolved from env://GITHUB_OAUTH_CLIENT_SECRET (the
// AppHost injects it from the uncommitted github-oauth.local.json). Absent → both stay null and the Git panel reports
// "brokers no OAuth App". The callback is the pinned control-plane URL.
GitHubBroker? gitHubBroker = null;
ProviderBroker? providerBroker = null;
var providerEntries = new List<ConnectedProviderOptions>();
GitHubBrokerOptions? gitHubOptions = null;
string? gitHubClientId = builder.Configuration["GitHubOAuth:ClientId"];
if (!string.IsNullOrWhiteSpace(gitHubClientId))
{
    gitHubOptions = new GitHubBrokerOptions
    {
        ClientId = gitHubClientId,
        ClientSecretRef = "env://GITHUB_OAUTH_CLIENT_SECRET",
        CallbackUrl = "http://localhost:8090/arazzo/v1/github/auth/callback",
    };
    providerEntries.Add(gitHubOptions.ToProviderEntry());
}

// The demo Keycloak folds in as a connected provider (ADR 0052 tier 4) covering the demo's own host, purely to
// demonstrate the mechanism: the sample spec endpoint below accepts the realm's bearer token, so the fetch
// pane's interactive sign-in is live-testable with the seeded realm users and no external account. It is a
// mechanism demo, not a foreign portal (the endpoint validates this same realm). The arazzo-portal client is
// registered by the realm import; its dev secret is injected by the AppHost as ARAZZO_PORTAL_CLIENT_SECRET.
// Secured composition only: with authorization off there is no token-checking host to authenticate against.
string? providerKeycloakBaseUrl = builder.Configuration["ControlPlane:Keycloak:BaseUrl"];
if (requireAuthorization && !string.IsNullOrWhiteSpace(providerKeycloakBaseUrl))
{
    providerEntries.Add(new ConnectedProviderOptions
    {
        Name = "keycloak",
        DisplayName = "Demo Keycloak",
        Issuer = $"{providerKeycloakBaseUrl.TrimEnd('/')}/realms/arazzo",
        ClientId = "arazzo-portal",
        ClientSecretRef = "env://ARAZZO_PORTAL_CLIENT_SECRET",
        Scopes = "openid profile",
        CallbackUrl = "http://localhost:8090/arazzo/v1/providers/keycloak/auth/callback",
        Hosts = ["localhost", "127.0.0.1"],
    });
}

if (providerEntries.Count > 0)
{
    ISecretResolver providerSecrets = new SecretResolverBuilder().AddEnvironment().Build();

    // A console logger so an exchange refusal names the provider's error code in the composition logs —
    // the difference between "incorrect_client_credentials" and an unreachable endpoint matters.
    Microsoft.Extensions.Logging.ILoggerFactory providerLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(logging => logging.AddConsole());
    var providerHttpClient = new HttpClient();
    providerBroker = new ProviderBroker(
        providerHttpClient,
        providerEntries,
        providerSecrets,
        logger: Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<ProviderBroker>(providerLoggerFactory));
    if (gitHubOptions is not null)
    {
        gitHubBroker = new GitHubBroker(providerHttpClient, gitHubOptions, providerBroker);
    }
}

// The grantee directory (§16.5.4): resolve REAL Keycloak users/groups/roles for the view/operate/administer grant
// pickers, via the arazzo-directory service-account client (client-credentials; the realm import grants it
// realm-management view-users/query-groups). Enabled only when the deployment injects the Keycloak base URL (the
// AppHost does). Groups map to Team grantees stamped {sys:group=<group>, sys:iss=KeycloakIssuer} — the SAME identity
// the runtime stamper and the seeded admins carry (DemoData), so a directory pick set-equals a live caller. The adapter
// stamps sys:iss from Options.Issuer; the mapper only emits the group/sub/role tag (DirectoryIssuer adds the issuer).
// A Person is resolved to its FULL membership-expanded identity (§16.5.4): the adapter fetches the user's real Keycloak
// groups and unions a sys:group per group through this same mapper, so a directory-resolved person carries
// {sys:sub, sys:group per membership, sys:iss} — the exact identity the login resolver stamps for that person, which is
// what lets the effective-access lookup surface the grants a person inherits through its groups.
KeycloakPrincipalDirectory? granteeDirectory = null;
string? keycloakBaseUrl = builder.Configuration["ControlPlane:Keycloak:BaseUrl"];
if (!string.IsNullOrWhiteSpace(keycloakBaseUrl))
{
    ISecretResolver directorySecrets = new SecretResolverBuilder().AddEnvironment().Build();
    var directoryMapper = DirectorySpanIdentityMapper.FromIdentity(
        [],
        static (DirectoryRecordView record, ref IdentityBuilder identity) =>
        {
            switch (record.Kind)
            {
                case GranteeKind.Team:
                    identity.Add("sys:group"u8, record.ValueUtf8);
                    return true;
                case GranteeKind.Person:
                    identity.Add("sys:sub"u8, record.ValueUtf8);
                    return true;
                case GranteeKind.Role:
                    identity.Add("sys:role"u8, record.ValueUtf8);
                    return true;
                default:
                    return false;
            }
        });
    granteeDirectory = new KeycloakPrincipalDirectory(
        new KeycloakDirectoryOptions
        {
            Issuer = DemoData.KeycloakIssuer,
            BaseUrl = new Uri(keycloakBaseUrl),
            Realm = "arazzo",
            TokenRealm = "arazzo",
            Authentication = new KeycloakClientCredentials(
                builder.Configuration["ControlPlane:Directory:ClientId"] ?? "arazzo-directory",
                DirectoryCredential.Parse("env://ARAZZO_DIRECTORY_CLIENT_SECRET")),
            Kinds = new Dictionary<GranteeKind, KeycloakResource>
            {
                [GranteeKind.Team] = KeycloakResource.Groups,
                [GranteeKind.Person] = KeycloakResource.Users,
                [GranteeKind.Role] = KeycloakResource.Roles,
            },
        },
        directorySecrets,
        directoryMapper);
}

// The real control-plane API, under a conventional base path the UI points at. Row security (reach scoping) is
// applied only when authorization is on — the open, unauthenticated demo stays fully visible. The access-request
// surface keys a grant on the requester's `preferred_username`, the same claim the resolver matches.
// The deterministic simulator (design §8) powers the designer's Mock runs: a working copy replayed against
// auto-scripted mocks, forward to completion (or a breakpoint) with no live environment or credentials. An output
// whose pointer misses an absent field is omitted, not fatal (OutputExtractionEmitter / AppendWorkflowOutputs guard).
var workflowSimulator = new Corvus.Text.Json.Arazzo.Testing.WorkflowSimulator(new WorkflowExecutorProvider(durable: true));

// The server-side spec-document fetcher (§4.4/ADR 0052) behind the fetch pane: no browser CORS, one fetch
// implementation, authenticated as a provider connection, a one-shot secret, or a §13 binding (resolved through
// the env secret scheme, the same reference pattern the composition's other secrets use). The demo composition
// serves http on localhost, so the insecure opt-in is on. The fetch client is configured to NOT auto-follow
// redirects (the fetcher follows them itself so a credential never crosses an origin — ADR 0052 hardening); a
// production deployment MUST do the same, and should fence outbound destinations at the network layer (SSRF).
var sourceFetcher = new SourceDocumentFetcher(
    new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }),
    sourceCredentials,
    new Corvus.Text.Json.Arazzo.SourceCredentials.Http.SourceCredentialProviderFactory(new SecretResolverBuilder().AddEnvironment().Build()),
    allowInsecureHttp: true);

// Captured from the endpoint mapping so the demo can seed its pending access request through the SAME submission path a
// real caller uses (starting the approval run), rather than writing it straight to the store with no run to enact it.
IAccessRequestApprovalService? seedApprovalService = null;

// One checkpoint coordinator for this host, shared by every surface that authors a checkpoint. ADR 0065 decision 6
// requires the per-run single-flight interlock to be per run rather than per component, and the coordinator holds that
// interlock in memory — two instances over one store would hold one gate each, which is exactly the shape that lets two
// honest components author the same run at once. This host maps the runner API below, and would map the serverless
// checkpoint surface too if it were given a checkpoint secret, so the instance is built here and passed to both.
var checkpointCoordinator = new WorkflowCheckpointCoordinator(stateStore);
app.MapGroup("/arazzo/v1").MapArazzoControlPlane(
    management,
    catalog,
    runners,
    requireAuthorization ? ControlPlaneSecurityMode.Scoped : ControlPlaneSecurityMode.Open,
    rowSecurity: requireAuthorization ? entitlements : null,
    securityPolicyStore: securityPolicy,
    sourceCredentialStore: sourceCredentials,
    sourceFetcher: sourceFetcher,
    accessRequestStore: accessRequests,
    accessRequestSubjectClaimType: "preferred_username",
    selfElevationEligibility: eligibleForSelfElevation,
    environmentRunnerAuthorizationStore: runnerAuthorizations,
    // §18 debug-run seam: the governed environment store, the run state store, the captured-draft store, the
    // in-process runner that advances the marked runs, and the durable trace store the dock reads back.
    environmentStore: environmentStore,
    // Governance stores (§7.6-§7.8, §16.5.4) — durable, no in-memory fallback.
    environmentAdministratorStore: environmentAdministratorStore,
    sourceStore: sourceStore,
    availabilityStore: availabilityStore,
    availabilityRequestStore: availabilityRequestStore,
    observedIdentityStore: observedIdentityStore,
    principalDirectory: granteeDirectory,
    workspaceWorkflowStore: workspaceStore,
    workflowStateStore: stateStore,
    draftRunStore: draftRunStore,
    draftRunner: draftRunner,
    draftRunTraceStore: draftRunTraceStore,
    gitHubBroker: gitHubBroker,
    providerBroker: providerBroker,
    workflowSimulator: workflowSimulator,
    // §16.5.1: route access-request approvals through the bootstrapped access-approval workflow when it is enabled —
    // approve/reject/withdraw publish the decision on access.decision (the system runner resumes the run and grants),
    // instead of the built-in direct-to-administrator grant.
    workflowApproval: enableSystemApprovalWorkflow
        ? new WorkflowApprovalOptions
        {
            DecisionTransport = decisionTransport!,
            ApprovalWorkflowId = "access-approval-v1",
            Environment = "system",
        }
        : null,
    onApprovalServiceBuilt: svc => seedApprovalService = svc,
    // Serverless execution backend (#876): the shared build-job + workflow-deployment queues. Passing the deployment
    // store lights up the catalog handler's dispatch gate (a run of an Isolated-backend version is refused until its
    // deployment reaches Deployed); passing the build-job store lets the build worker (Phase 3b) drain it here.
    nativeBuildJobStore: nativeBuildJobStore,
    workflowDeploymentStore: workflowDeploymentStore,
    // No checkpoint secret, so this host serves no serverless checkpoint surface (ADR 0062). It does not need one: the
    // serverless runner dispatches functions at its own surface and is the process that mints their tokens. Absent
    // rather than open is the ADR 0016 posture — a surface mapped without a secret would admit any caller this host
    // authenticates to every run in the deployment, and everyone at all in Open.
    checkpoints: checkpointCoordinator);

// The runner API (ADR 0065) — the surface every runner store interaction goes through, so that a runner needs no store
// credential to execute. It shares this host's stores because the demo is one process; the point of the split is that
// the runner is a DIFFERENT process, reaching them only through these operations and only for the environments its
// machine principal is bound to. RunnerAuthorizationBindings is what resolves that, per request and from the
// authorization records, so revoking a runner stops it being offered work within the cache window rather than depending
// on the runner standing itself down.
//
// Mapped only when authentication is enforced. Every operation derives its lease ownership and its reach from the
// authenticated principal, so with authentication off there is no principal, nothing to own a lease, and nothing the
// API could safely answer. The runner processes only run in the secured (AppHost) topology in any case.
//
// Quotas (ADR 0065 decision 3) are metered with the deployment defaults, because no guard is passed. That is the
// production posture rather than a demo shortcut: the defaults sit well clear of what the sample runners generate, so
// the demo exercises the metered path without ever reaching a refusal.
if (requireAuthorization)
{
    app.MapGroup("/arazzo/runner/v1").MapArazzoRunnerApi(
        stateStore,
        catalogStore,
        availabilityStore,
        new RunnerAuthorizationBindings(runnerAuthorizations, environmentStore),
        checkpoints: checkpointCoordinator);
}

// oscar's PENDING access request (the approver-inbox content): seeded THROUGH the approval service, exactly as a real
// caller submits — so with the system approval workflow enabled it starts the bootstrapped approval run and can be
// enacted by an approver's decision (§16.5.1). Writing it straight to the store (as the seed did before) left a pending
// request with no suspended run, so approving it resumed nothing and it never settled. The subject claim type matches
// the API's (preferred_username), so the seeded request is indistinguishable from one oscar submits himself.
if (seedExampleData && seedApprovalService is { } approvalForSeed)
{
    using ParsedJsonDocument<AccessRequest> pending = AccessRequest.Draft(
        "onboard-customer", ["runs:write"], "preferred_username", "oscar", "Oscar (Observer)", "Investigating a stuck onboarding run.", 4 * 3600);
    (await approvalForSeed.SubmitAsync(pending.RootElement, "oscar", principal: null, cancellationToken: default)).Dispose();
}

// The source backends the workflows call — onboarding, ledger, and kyc — are all real external services (their own
// processes + databases); no inline /svc mock remains (kyc-notifications is an AsyncAPI message source, not HTTP). This
// host serves ONLY the control-plane API (/arazzo/v1), its auth BFF, and the console — never any example-service API.

// Once the server is listening, resolve its own base URL (the live resumer's never-hit /svc fallback root) and execute
// one fresh onboarding run live — so the demo shows a genuinely-executed run, not only hand-seeded states.
app.Lifetime.ApplicationStarted.Register(() =>
{
    selfBaseUrl.Value = app.Urls.FirstOrDefault();

    // §18: start the in-process draft runner's pump now the host is listening (its transport binder needs the base URL
    // for the never-hit /svc fallback root; real runs route to the external source services). It
    // claims the Pending and resume-claimable $draft debug runs the control plane marks, advances each one step (or to
    // its next pause), records the metadata trace, and persists it — a short poll keeps the designer's dock responsive.
    // UNLESS a SEPARATE runner process hosts $draft (the multi-process topology): set
    // ControlPlane__HostDraftRunnerInProcess=false so the two runners never both claim the same runs. The runner
    // instance is still constructed above (the debug-run endpoints require it to be wired); it is simply not pumped here.
    if (builder.Configuration.GetValue("ControlPlane:HostDraftRunnerInProcess", true))
    {
        draftRunner.Start(TimeSpan.FromMilliseconds(200), onError: ex => app.Logger.LogError(ex, "Draft runner pump failed."));
    }

    // Example fiction: one genuinely-executed onboarding run so the demo shows a real run, not only seeded states.
    if (seedExampleData)
    {
        _ = exampleSeed.RunLiveSampleAsync(stateStore, liveResumer, message => app.Logger.LogInformation("{Message}", message));
    }
});

// Stop the draft runner's pump cleanly on shutdown (best-effort; the process exit would end it regardless).
app.Lifetime.ApplicationStopping.Register(() => draftRunner.StopAsync().AsTask().GetAwaiter().GetResult());

app.Run();

// Exchanges the saved refresh_token for a fresh token set (the id_token is returned via the openid scope) at the Keycloak
// token endpoint — the same realm + confidential client the BFF authenticates with, over the plain client the directory
// adapter already uses against this Keycloak. Best-effort: any failure (a stale refresh_token, network, misconfiguration)
// returns null so the logout falls back to a local-only sign-out.
static async Task<string?> RefreshIdTokenAsync(IServiceProvider services, string refreshToken, CancellationToken cancellationToken)
{
    string? baseUrl = services.GetRequiredService<IConfiguration>()["ControlPlane:Keycloak:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        return null;
    }

    try
    {
        using var client = new HttpClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = "arazzo-ui",
            ["client_secret"] = "arazzo-ui-dev-secret",
            ["scope"] = "openid",
        });
        var tokenEndpoint = new Uri(new Uri(baseUrl), "/realms/arazzo/protocol/openid-connect/token");
        using HttpResponseMessage response = await client.PostAsync(tokenEndpoint, form, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return ReadIdToken(body);
    }
    catch (HttpRequestException)
    {
        return null;
    }
}

// Reads the `id_token` string out of a Keycloak token response in place (the Corvus reader, no STJ DOM), or null if absent.
static string? ReadIdToken(ReadOnlySpan<byte> body)
{
    var reader = new Utf8JsonReader(body);
    if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
    {
        return null;
    }

    while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
    {
        if (reader.ValueTextEquals("id_token"u8))
        {
            reader.Read();
            return reader.GetString();
        }

        reader.Read();
        reader.Skip();
    }

    return null;
}