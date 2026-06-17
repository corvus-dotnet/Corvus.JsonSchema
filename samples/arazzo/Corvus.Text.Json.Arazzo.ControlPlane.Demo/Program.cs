// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// A self-contained demo host: the REAL Arazzo control-plane server over a fresh-on-startup SQLite store,
// seeded with demo workflows + runs, serving the build-free web UI from the same origin. Run it with
// `dotnet run --project samples/Corvus.Text.Json.Arazzo.ControlPlane.Demo` and open the printed URL.
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.ControlPlane.Demo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
SqliteWorkflowCatalogStore catalogStore = await SqliteWorkflowCatalogStore.ConnectAsync(connectionString, metadataProvider: metadata);

var management = new WorkflowManagementClient(stateStore, "demo", DemoData.CompleteResumer);
var catalog = new WorkflowCatalogClient(catalogStore, stateStore, "demo");

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

// Control-plane authorization is per-deployment (design §14.1). The real strategy is OIDC: bearer tokens from
// Keycloak (humans via the BFF, machines via client-credentials, §16.3), with the dev API-key kept for
// break-glass / scripts (§16.2). Gated behind config so the open demo + its build-free UI still run by default.
// Enable with `ControlPlane__RequireAuthorization=true`, then present a Keycloak bearer token, or an
// `X-Api-Key: demo-admin-key` (all scopes) / `demo-readonly-key` (catalog:read + runs:read) header.
bool requireAuthorization = builder.Configuration.GetValue("ControlPlane:RequireAuthorization", false);
if (requireAuthorization)
{
    // A forwarding policy scheme picks by the presented credential: an X-Api-Key header → the dev API-key scheme,
    // otherwise the Keycloak JWT bearer (validated against the referenced realm via the Aspire Keycloak client).
    builder.Services
        .AddAuthentication("control-plane")
        .AddPolicyScheme("control-plane", "Keycloak bearer or dev API key", options =>
        {
            options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey(DevApiKeyAuthenticationHandler.ApiKeyHeader)
                    ? DevApiKeyAuthenticationHandler.SchemeName
                    : JwtBearerDefaults.AuthenticationScheme;
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
            });

    // The demo's concrete §14.1 mapping: Keycloak `groups` → the capability scopes the policies read (§16.5).
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
    app.UseAuthentication();
    app.UseAuthorization();
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

// The real control-plane API, under a conventional base path the UI points at.
app.MapGroup("/arazzo/v1").MapArazzoControlPlane(management, catalog, runners, requireAuthorization, securityPolicyStore: securityPolicy, sourceCredentialStore: sourceCredentials);

// The demo backend services the workflows call (generated from the same OpenAPI sources, returning sample data).
OnboardingApi.MapApiEndpoints(app.MapGroup("/svc/onboarding"), new OnboardingService());
LedgerApi.MapApiEndpoints(app.MapGroup("/svc/ledger"), new LedgerService());

app.Run();
