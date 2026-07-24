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
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
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

// Aspire's orchestrator hands each child an SSL_CERT_DIR holding ONLY the dev certificate (so services can
// trust each other's https endpoints). On Linux, OpenSSL treats SSL_CERT_DIR as a REPLACEMENT for the system
// store, which silently breaks every outbound public TLS call (the GitHub broker's token exchange fails
// PartialChain). OpenSSL accepts a colon-separated list, so append the system store back — public roots are
// restored and the dev-cert trust is kept. Must run before the first outbound TLS handshake.
if (Environment.GetEnvironmentVariable("SSL_CERT_DIR") is { Length: > 0 } injectedCertDir
    && !injectedCertDir.Contains("/etc/ssl/certs", StringComparison.Ordinal)
    && Directory.Exists("/etc/ssl/certs"))
{
    Environment.SetEnvironmentVariable("SSL_CERT_DIR", $"{injectedCertDir}:/etc/ssl/certs");
}

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
var management = new SecuredWorkflowManagement(stateStore, "demo", liveResumer);

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

var draftRunner = new InProcessDraftRunner(
    stateStore,
    owner: "arazzo-inprocess-draft-runner",
    // Pinned to the draft-enabled environment: the dispatcher claims only the $draft runs started in THIS environment
    // (a real deployment runs one runner per environment). This must match the environment debug runs are started in.
    runnerEnvironment: "development",
    draftRunStore,
    draftRunTraceStore,
    new WorkflowExecutorProvider(),
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
    internalTagResolver: static principal =>
    {
        SecurityTag[] groups = principal?.FindAll("groups")
            .Select(c => new SecurityTag(SecurityShell.DefaultInternalPrefix + "group", c.Value)).ToArray() ?? [];
        if (groups.Length == 0)
        {
            return groups;
        }

        string? sub = principal?.FindFirst("sub")?.Value;
        bool hasSub = !string.IsNullOrEmpty(sub);
        var tags = new SecurityTag[groups.Length + (hasSub ? 2 : 1)];
        Array.Copy(groups, tags, groups.Length);
        int next = groups.Length;
        if (hasSub)
        {
            tags[next++] = new SecurityTag(SecurityShell.DefaultInternalPrefix + "sub", sub!);
        }

        tags[next] = DemoData.IssuerTag;
        return tags;
    },
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

bool requireAuthorization = builder.Configuration.GetValue("ControlPlane:RequireAuthorization", false);
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

// DEMO: the open demo has no interactive administrator, so stand in for the development environment's administrator
// (the "demo" identity that created it, §7.7) and authorize each runner that registers a Pending §5.5 authorization to
// serve it — otherwise the out-of-process runner stays dispatch-paused and never claims catalogued runs. This keeps the
// §5.5 semantic intact (an administrator, never the runner, grants authorization); production does it via the UI/API — so
// it is part of the example fiction and only wired when the deployment opts in.
if (seedExampleData)
{
    builder.Services.AddHostedService(sp => new RunnerAutoAuthorizationService(
        runnerAuthorizations,
        sp.GetRequiredService<ILogger<RunnerAutoAuthorizationService>>()));
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

// The GitHub App broker (workflow-designer §4.7): the per-user user-to-server OAuth flow that binds a working copy to
// a branch and commits AS the signed-in user (their token, never in the browser). Enabled only when the deployment
// supplies a GitHub App — the client id (public, so plain config) plus the secret resolved from
// env://GITHUB_OAUTH_CLIENT_SECRET (the AppHost injects it from the uncommitted github-oauth.local.json). Absent →
// gitHubBroker stays null and the Git panel reports "brokers no OAuth App". The callback is the pinned control-plane URL.
GitHubBroker? gitHubBroker = null;
string? gitHubClientId = builder.Configuration["GitHubOAuth:ClientId"];
if (!string.IsNullOrWhiteSpace(gitHubClientId))
{
    ISecretResolver gitHubSecrets = new SecretResolverBuilder().AddEnvironment().Build();

    // A console logger so an exchange refusal names GitHub's error code in the composition logs —
    // the difference between "incorrect_client_credentials" and an unreachable github.com matters.
    Microsoft.Extensions.Logging.ILoggerFactory gitHubLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(logging => logging.AddConsole());
    gitHubBroker = new GitHubBroker(
        new HttpClient(),
        new GitHubBrokerOptions
        {
            ClientId = gitHubClientId,
            ClientSecretRef = "env://GITHUB_OAUTH_CLIENT_SECRET",
            CallbackUrl = "http://localhost:8090/arazzo/v1/github/auth/callback",
        },
        gitHubSecrets,
        logger: Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<GitHubBroker>(gitHubLoggerFactory));
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

// Captured from the endpoint mapping so the demo can seed its pending access request through the SAME submission path a
// real caller uses (starting the approval run), rather than writing it straight to the store with no run to enact it.
IAccessRequestApprovalService? seedApprovalService = null;
app.MapGroup("/arazzo/v1").MapArazzoControlPlane(
    management,
    catalog,
    runners,
    requireAuthorization ? ControlPlaneSecurityMode.Scoped : ControlPlaneSecurityMode.Open,
    rowSecurity: requireAuthorization ? entitlements : null,
    securityPolicyStore: securityPolicy,
    sourceCredentialStore: sourceCredentials,
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
    onApprovalServiceBuilt: svc => seedApprovalService = svc);

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
