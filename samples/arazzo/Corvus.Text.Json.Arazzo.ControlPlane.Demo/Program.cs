// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// A self-contained demo host: the REAL Arazzo control-plane server over a fresh-on-startup SQLite store,
// seeded with demo workflows + runs, serving the build-free web UI from the same origin. Run it with
// `dotnet run --project samples/Corvus.Text.Json.Arazzo.ControlPlane.Demo` and open the printed URL.
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.ControlPlane.Demo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sqlite;
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

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry (incl. the Corvus.Arazzo workflow source/meter), health checks,
// service discovery, and HTTP resilience. Under the AppHost this exports traces/logs/metrics to the dashboard;
// run standalone it is a no-op exporter (no OTLP endpoint configured), so `dotnet run` on this project alone
// still works exactly as before.
builder.AddServiceDefaults();

// The shared durability store. The AppHost injects ConnectionStrings:workflowstore so the control plane and the
// runner open the same store (the SQLite file is the local stand-in for the production shared store, e.g.
// Postgres later); the temp-file fallback keeps this host runnable standalone.
string connectionString = builder.Configuration.GetConnectionString("workflowstore")
    ?? $"Data Source={Path.Combine(Path.GetTempPath(), "arazzo-control-plane-demo.db")}";

// The control plane owns reset + seed: delete the store the connection string points at, so the demo always
// starts from the seed. (The runner waits for this host's health before connecting, so it never races the wipe.)
string dbPath = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
foreach (string path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
{
    if (File.Exists(path))
    {
        File.Delete(path);
    }
}

// The catalog store bakes typed-shape + validation metadata at add time via the code-generation provider.
var metadata = new WorkflowSchemaMetadataProvider();
SqliteWorkflowStateStore stateStore = await SqliteWorkflowStateStore.ConnectAsync(connectionString);
// The executor provider compiles a runnable executor into each catalogued version at add time (alongside the typed
// metadata) — so a resumed run can re-enter the real generated Arazzo executor (live execution, §5/§8).
SqliteWorkflowCatalogStore catalogStore = await SqliteWorkflowCatalogStore.ConnectAsync(connectionString, metadataProvider: metadata, executorProvider: new WorkflowExecutorProvider());

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
SqliteRunnerRegistry runners = await SqliteRunnerRegistry.ConnectAsync(connectionString);

// The §13 source-credential store. The control plane manages credential *references* + metadata only — it never
// binds to the secret store (the §13/§13.5 invariant); the runner is the read-only secret consumer. This lights
// up the /credentials surface (and the CLI + web UI) over the shared store.
SqliteSourceCredentialStore sourceCredentials = await SqliteSourceCredentialStore.ConnectAsync(connectionString);

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
}

string specsDir = Path.Combine(builder.Environment.ContentRootPath, "specs");
await DemoData.SeedAsync(catalog, stateStore, specsDir);

// Seed demo source-credential bindings — references only (the §13 invariant: never secret material). Each points
// at the Vault path the AppHost's provisioner seeds (vault://secret/arazzo/<source>#api-key); the runner resolves
// it with its read-only token. This populates /credentials (and the CLI + web UI) out of the box.
foreach (string source in new[] { "onboarding", "ledger" })
{
    // AddAsync returns the persisted binding as a pooled document — dispose it (the seed doesn't read it back).
    using ParsedJsonDocument<SourceCredentialBinding> seeded = await sourceCredentials.AddAsync(
        new SourceCredentialDefinition(
            source,
            "production",
            SourceCredentialKind.ApiKey,
            [new SecretReferenceDefinition("api-key", $"vault://secret/arazzo/{source}#api-key")]),
        "demo",
        default);
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
    selfElevationEligibility: eligibleForSelfElevation);

// The demo backend services the workflows call (generated from the same OpenAPI sources, returning sample data).
OnboardingApi.MapApiEndpoints(app.MapGroup("/svc/onboarding"), new OnboardingService());
LedgerApi.MapApiEndpoints(app.MapGroup("/svc/ledger"), new LedgerService());

// Once the server is listening, resolve its own base URL (for the live resumer's /svc transports) and execute one
// fresh onboarding run live — so the demo shows a genuinely-executed run, not only hand-seeded states.
app.Lifetime.ApplicationStarted.Register(() =>
{
    selfBaseUrl.Value = app.Urls.FirstOrDefault();
    _ = DemoData.RunLiveOnboardingAsync(stateStore, liveResumer, message => app.Logger.LogInformation("{Message}", message));
});

app.Run();
