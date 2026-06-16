// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// A self-contained demo host: the REAL Arazzo control-plane server over a fresh-on-startup SQLite store,
// seeded with demo workflows + runs, serving the build-free web UI from the same origin. Run it with
// `dotnet run --project samples/Corvus.Text.Json.Arazzo.ControlPlane.Demo` and open the printed URL.
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.ControlPlane.Demo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Sqlite;
using Microsoft.AspNetCore.Authentication;
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

// A fresh SQLite database file each run — deleted on startup, so the demo always starts from the seed.
string dbPath = Path.Combine(Path.GetTempPath(), "arazzo-control-plane-demo.db");
foreach (string path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
{
    if (File.Exists(path))
    {
        File.Delete(path);
    }
}

string connectionString = $"Data Source={dbPath}";

// The catalog store bakes typed-shape + validation metadata at add time via the code-generation provider.
var metadata = new WorkflowSchemaMetadataProvider();
SqliteWorkflowStateStore stateStore = await SqliteWorkflowStateStore.ConnectAsync(connectionString);
SqliteWorkflowCatalogStore catalogStore = await SqliteWorkflowCatalogStore.ConnectAsync(connectionString, metadataProvider: metadata);

var management = new WorkflowManagementClient(stateStore, "demo", DemoData.CompleteResumer);
var catalog = new WorkflowCatalogClient(catalogStore, stateStore, "demo");
var runners = new InMemoryRunnerRegistry();

// The row-security authoring API (§14.2) is served from a security-policy store, seeded with the editable
// bootstrap rules (tenant-scoped / ABAC superset / intersection) so /security/* is populated out of the box.
var securityPolicy = new Corvus.Text.Json.Arazzo.Durability.Security.InMemorySecurityPolicyStore();
await Corvus.Text.Json.Arazzo.Durability.Security.SecurityBootstrap.SeedAsync(securityPolicy);

// Control-plane authorization is per-deployment (design §14.1). The demo ships a concrete strategy — a dev
// API-key scheme mapping a key to capability scopes — gated behind config so the open demo + its build-free
// UI still run by default. Enable enforcement with `ControlPlane__RequireAuthorization=true`, then call the
// API with `X-Api-Key: demo-admin-key` (all scopes) or `demo-readonly-key` (catalog:read + runs:read).
bool requireAuthorization = builder.Configuration.GetValue("ControlPlane:RequireAuthorization", false);
if (requireAuthorization)
{
    builder.Services
        .AddAuthentication(DevApiKeyAuthenticationHandler.SchemeName)
        .AddScheme<DevApiKeyOptions, DevApiKeyAuthenticationHandler>(
            DevApiKeyAuthenticationHandler.SchemeName,
            options =>
            {
                options.Keys["demo-admin-key"] = string.Join(' ', ControlPlaneScopes.All);
                options.Keys["demo-readonly-key"] = $"{ControlPlaneScopes.CatalogRead} {ControlPlaneScopes.RunsRead}";
            });
    builder.Services.AddArazzoControlPlaneAuthorization();
}

string specsDir = Path.Combine(builder.Environment.ContentRootPath, "specs");
await DemoData.SeedAsync(catalog, stateStore, specsDir);

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
app.MapGroup("/arazzo/v1").MapArazzoControlPlane(management, catalog, runners, requireAuthorization, securityPolicyStore: securityPolicy);

// The demo backend services the workflows call (generated from the same OpenAPI sources, returning sample data).
OnboardingApi.MapApiEndpoints(app.MapGroup("/svc/onboarding"), new OnboardingService());
LedgerApi.MapApiEndpoints(app.MapGroup("/svc/ledger"), new LedgerService());

app.Run();
