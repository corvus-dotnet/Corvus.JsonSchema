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
using Microsoft.Extensions.FileProviders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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

string specsDir = Path.Combine(builder.Environment.ContentRootPath, "specs");
await DemoData.SeedAsync(catalog, stateStore, specsDir);

WebApplication app = builder.Build();

// Serve a demo page (wwwroot/index.html) and the build-free UI source (web/arazzo-control-plane-ui) at /ui.
app.UseDefaultFiles();
app.UseStaticFiles();
string uiRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "web", "arazzo-control-plane-ui"));
if (Directory.Exists(uiRoot))
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(uiRoot), RequestPath = "/ui" });
}
else
{
    app.Logger.LogWarning("Web UI not found at {UiRoot}; the API is still available under /arazzo/v1.", uiRoot);
}

// The real control-plane API, under a conventional base path the UI points at.
app.MapGroup("/arazzo/v1").MapArazzoControlPlane(management, catalog);

app.Run();
