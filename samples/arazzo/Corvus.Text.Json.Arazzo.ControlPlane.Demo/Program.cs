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
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Postgres;
using Npgsql;
using System.Security.Claims;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.FileProviders;
using OnboardingApi = Corvus.Text.Json.Arazzo.ControlPlane.Demo.Onboarding.ApiEndpointRegistration;
using OnboardingService = Corvus.Text.Json.Arazzo.ControlPlane.Demo.Onboarding.OnboardingService;
using LedgerApi = Corvus.Text.Json.Arazzo.ControlPlane.Demo.Ledger.ApiEndpointRegistration;
using LedgerService = Corvus.Text.Json.Arazzo.ControlPlane.Demo.Ledger.LedgerService;
using CpEnvironment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

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

// Postgres adapters do NOT create their schema on ConnectAsync (unlike SQLite, which self-creates on open). The
// control plane owns the schema: run each store's PrepareAsync (idempotent CREATE TABLE IF NOT EXISTS) once, before
// any store opens. The runner never runs DDL — it waits for this host's health, by which point the tables exist. The
// AppHost's Postgres container is ephemeral (no data volume), so every run starts from an empty database — the
// fresh-each-run reset with no file to wipe.
await PostgresWorkflowStateStore.PrepareAsync(dataSource);
await PostgresWorkflowCatalogStore.PrepareAsync(dataSource);
await PostgresRunnerRegistry.PrepareAsync(dataSource);
await PostgresEnvironmentRunnerAuthorizationStore.PrepareAsync(dataSource);
await PostgresSourceCredentialStore.PrepareAsync(dataSource);
await PostgresDraftRunStore.PrepareAsync(dataSource);
await PostgresDraftRunTraceStore.PrepareAsync(dataSource);
await PostgresEnvironmentStore.PrepareAsync(dataSource);
await PostgresWorkspaceWorkflowStore.PrepareAsync(dataSource);

// The catalog store bakes typed-shape + validation metadata at add time via the code-generation provider.
var metadata = new WorkflowSchemaMetadataProvider();
PostgresWorkflowStateStore stateStore = await PostgresWorkflowStateStore.ConnectAsync(dataSource);
// The executor provider compiles a runnable executor into each catalogued version at add time (alongside the typed
// metadata) — so a resumed run can re-enter the real generated Arazzo executor (live execution, §5/§8).
PostgresWorkflowCatalogStore catalogStore = await PostgresWorkflowCatalogStore.ConnectAsync(dataSource, metadataProvider: metadata, executorProvider: new WorkflowExecutorProvider());

// Live execution (§5/§8): a resumed run re-enters its baked executor, calling this host's own /svc backends. The
// resumer is built now but invoked only after the server is listening, so it reads the host base URL lazily (set in
// the ApplicationStarted callback below) — the same delegate also drives one fresh run at startup to demonstrate it.
var selfBaseUrl = new System.Runtime.CompilerServices.StrongBox<string?>(null);
WorkflowResumer liveResumer = DemoData.CreateLiveResumer(catalogStore, () => selfBaseUrl.Value ?? throw new InvalidOperationException("The host base URL is not available until the server has started."));
var management = new SecuredWorkflowManagement(stateStore, "demo", liveResumer);

// A workflow's §15 administrator set governs who may approve access requests for it (and publish further versions).
// The submitter of version 1 establishes administration (DemoData seeds the workflows as administered by the
// arazzo-admins group); the access-request approval flow routes a request to these administrators.
var administrators = new InMemoryWorkflowAdministratorStore();
var catalog = new SecuredWorkflowCatalog(catalogStore, stateStore, "demo", administrators: administrators);

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
// this host's own /svc backends: a single-process deployment advances debug runs by pumping this runner (started once
// the host is listening, below). The control plane only marks runs claimable — it never executes (§18).
PostgresDraftRunStore draftRunStore = await PostgresDraftRunStore.ConnectAsync(dataSource);
PostgresDraftRunTraceStore draftRunTraceStore = await PostgresDraftRunTraceStore.ConnectAsync(dataSource);
PostgresEnvironmentStore environmentStore = await PostgresEnvironmentStore.ConnectAsync(dataSource);

// The durable working-copy store (workflow-designer design §4.1): a designer's in-progress edits survive a restart and
// are shared across control-plane instances, rather than living only in memory. One of the nine fanned-out backends.
PostgresWorkspaceWorkflowStore workspaceStore = await PostgresWorkspaceWorkflowStore.ConnectAsync(dataSource);
var draftRunner = new InProcessDraftRunner(
    stateStore,
    owner: "arazzo-inprocess-draft-runner",
    // Pinned to the draft-enabled environment: the dispatcher claims only the $draft runs started in THIS environment
    // (a real deployment runs one runner per environment). This must match the environment debug runs are started in.
    runnerEnvironment: "development",
    draftRunStore,
    draftRunTraceStore,
    new WorkflowExecutorProvider(),
    DemoData.CreateSvcBinder(() => selfBaseUrl.Value ?? throw new InvalidOperationException("The host base URL is not available until the server has started.")),
    // Do NOT host timer waits here: the worker's ResumeDueTimersAsync resumes EVERY due-timer run in the shared store,
    // including seeded CATALOG runs this draft-only resumer cannot host. A draft run that suspends on a retry timer is
    // out of scope for the minimum stand-up (the base onboard-customer workflow has none).
    hostTimerWaits: false);

// The row-security authoring API (§14.2) is served from a security-policy store, seeded with the editable
// bootstrap rules (tenant-scoped / ABAC superset / intersection) so /security/* is populated out of the box.
var securityPolicy = new Corvus.Text.Json.Arazzo.Durability.Security.InMemorySecurityPolicyStore();
await Corvus.Text.Json.Arazzo.Durability.Security.SecurityBootstrap.SeedAsync(securityPolicy);

// Every authenticated principal may READ the whole control plane; WRITE/run reach is deny-by-default and conferred
// per-principal only through the access-request → approval flow (§16.5). So a stored grant is what lets a principal
// *act*, scoped to exactly the workflow it names — the worked example's "alice may trigger that workflow, and only it".
using (ParsedJsonDocument<SecurityBindingDocument> readAllBinding = SecurityBindingDocument.Draft("*", null, read: VerbGrant.Full, write: VerbGrant.None, purge: VerbGrant.None, description: "Authenticated principals may read the whole control plane."))
{
    (await securityPolicy.AddBindingAsync(readAllBinding.RootElement, "bootstrap", default)).Dispose();
}

// §16.2 tier 3 — the genesis administrator. The realm import (tier 2) seeds the arazzo-admins group + the arazzo-admin
// seed user; this deployment-policy grant maps that group claim to the "service operator": all capability scopes +
// unrestricted reach (read/write/purge Full). So the first admin logs in via OIDC and ALREADY HOLDS admin —
// bootstrapped by config, not an in-system grant (the identity analogue of secret-zero, exactly as vault-init
// bootstraps the secret store, §13.5). It is a STORED grant keyed on the group claim, consistent with the
// no-ambient-elevation rule: no group confers capability by mere membership; this binding IS the deliberate, auditable
// deployment policy §14.1 leaves open. Everyone else earns reach through the §16.5 access-request -> approval flow,
// approved by these admins. Break-glass for recovery remains the dev API-key scheme.
using (ParsedJsonDocument<SecurityBindingDocument> adminBootstrap = SecurityBindingDocument.Draft("groups", "arazzo-admins", read: VerbGrant.Full, write: VerbGrant.Full, purge: VerbGrant.Full, scopes: ControlPlaneScopes.All, description: "§16.2 tier 3: the arazzo-admins group is the deployment's genesis administrator (service operator) — all capability scopes + unrestricted reach."))
{
    (await securityPolicy.AddBindingAsync(adminBootstrap.RootElement, "bootstrap", default)).Dispose();
}

// The entitlement resolver (§16.5.2 Decision-A): ONE PersistentRowSecurityPolicy over the security-policy store
// backs both layers — the claims transformer unions its ResolveGrantedScopes into the scope claim (capability), and
// it is passed to MapArazzoControlPlane as the row-reach policy. A grant the approval service writes is refreshed
// into this same instance in-process, taking effect immediately. The principal's Keycloak groups become its sys:
// identity — the §15-administrator identity and the label stamped on rows it creates.
// The deployment's ordered tag dimensions (§14.2): a classification taxonomy the ordered rule templates (classification
// <= 'confidential', …) rank against. Baked into every compiled rule and surfaced read-only via GET /security/orderings
// so the authoring UI offers those templates with exactly these labels.
var labelOrderings = new SecurityLabelOrderings(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
{
    ["classification"] = ["public", "internal", "confidential", "restricted"],
});
var entitlements = new PersistentRowSecurityPolicy(
    securityPolicy,
    internalTagResolver: static principal => principal?.FindAll("groups")
        .Select(c => new SecurityTag(SecurityShell.DefaultInternalPrefix + "group", c.Value)).ToArray() ?? [],
    orderings: labelOrderings);
await entitlements.RefreshAsync();

// The access-request store (§16.5). In-memory like the security policy; the demo reseeds on every start.
var accessRequests = new Corvus.Text.Json.Arazzo.Durability.Security.InMemoryAccessRequestStore();

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

string specsDir = Path.Combine(builder.Environment.ContentRootPath, "specs");
await DemoData.SeedAsync(catalog, stateStore, specsDir);

// Seed demo source-credential bindings — references only (the §13 invariant: never secret material). Each points
// at the Vault path the AppHost's provisioner seeds (vault://secret/arazzo/<source>#api-key); the runner resolves
// it with its read-only token. This populates /credentials (and the CLI + web UI) out of the box.
foreach (string environment in new[] { "production", "development" })
{
    foreach (string source in new[] { "onboarding", "ledger" })
    {
        // AddAsync returns the persisted binding as a pooled document — dispose it (the seed doesn't read it back).
        using ParsedJsonDocument<SourceCredentialBinding> seeded = await sourceCredentials.AddAsync(
            new SourceCredentialDefinition(
                source,
                environment,
                SourceCredentialKind.ApiKey,
                // The ApiKey provider resolves the key from the secret reference named "value" (default header
                // X-API-Key); the reference target is the Vault path's #api-key field the provisioner seeds.
                [new SecretReferenceDefinition("value", $"vault://secret/arazzo/{source}#api-key")]),
            "demo",
            default);
    }
}

// §18: seed a development-class environment that ALLOWS working-copy drafts to run as debug runs (default off
// everywhere else — crossing that line is an explicit per-environment decision by its administrators). Its two sources
// are credentialed above, so the run dialog's readiness gate passes and a debug run executes against this host's own
// /svc backends. The environment store is the governed, reach-scoped one the debug-run seam reads.
using (ParsedJsonDocument<CpEnvironment> developmentEnvironment = ParsedJsonDocument<CpEnvironment>.Parse(
    """{"name":"development","displayName":"Development","description":"Developer sandbox — working-copy drafts may run here as debug runs (§18).","allowsDraftRuns":true}"""u8.ToArray()))
{
    (await environmentStore.AddAsync(developmentEnvironment.RootElement, "demo", default)).Dispose();
}

// DEMO: the open demo has no interactive administrator, so stand in for the development environment's administrator
// (the "demo" identity that created it, §7.7) and authorize each runner that registers a Pending §5.5 authorization to
// serve it — otherwise the out-of-process runner stays dispatch-paused and never claims catalogued runs. This keeps the
// §5.5 semantic intact (an administrator, never the runner, grants authorization); production does it via the UI/API.
builder.Services.AddHostedService(sp => new RunnerAutoAuthorizationService(
    runnerAuthorizations,
    sp.GetRequiredService<ILogger<RunnerAutoAuthorizationService>>()));

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
            new AuthenticationProperties { RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/ui/" : returnUrl },
            [OpenIdConnectDefaults.AuthenticationScheme]));

    app.MapPost("/logout", () =>
        Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/ui/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

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

// The real control-plane API, under a conventional base path the UI points at. Row security (reach scoping) is
// applied only when authorization is on — the open, unauthenticated demo stays fully visible. The access-request
// surface keys a grant on the requester's `preferred_username`, the same claim the resolver matches.
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
    workspaceWorkflowStore: workspaceStore,
    workflowStateStore: stateStore,
    draftRunStore: draftRunStore,
    draftRunner: draftRunner,
    draftRunTraceStore: draftRunTraceStore);

// The demo backend services the workflows call (generated from the same OpenAPI sources, returning sample data).
OnboardingApi.MapApiEndpoints(app.MapGroup("/svc/onboarding"), new OnboardingService());
LedgerApi.MapApiEndpoints(app.MapGroup("/svc/ledger"), new LedgerService());

// Once the server is listening, resolve its own base URL (for the live resumer's /svc transports) and execute one
// fresh onboarding run live — so the demo shows a genuinely-executed run, not only hand-seeded states.
app.Lifetime.ApplicationStarted.Register(() =>
{
    selfBaseUrl.Value = app.Urls.FirstOrDefault();

    // §18: start the in-process draft runner's pump now the host is listening (its /svc binder needs the base URL). It
    // claims the Pending and resume-claimable $draft debug runs the control plane marks, advances each one step (or to
    // its next pause), records the metadata trace, and persists it — a short poll keeps the designer's dock responsive.
    // UNLESS a SEPARATE runner process hosts $draft (the multi-process topology): set
    // ControlPlane__HostDraftRunnerInProcess=false so the two runners never both claim the same runs. The runner
    // instance is still constructed above (the debug-run endpoints require it to be wired); it is simply not pumped here.
    if (builder.Configuration.GetValue("ControlPlane:HostDraftRunnerInProcess", true))
    {
        draftRunner.Start(TimeSpan.FromMilliseconds(200), onError: ex => app.Logger.LogError(ex, "Draft runner pump failed."));
    }

    _ = DemoData.RunLiveOnboardingAsync(stateStore, liveResumer, message => app.Logger.LogInformation("{Message}", message));
});

// Stop the draft runner's pump cleanly on shutdown (best-effort; the process exit would end it regardless).
app.Lifetime.ApplicationStopping.Register(() => draftRunner.StopAsync().AsTask().GetAwaiter().GetResult());

app.Run();
