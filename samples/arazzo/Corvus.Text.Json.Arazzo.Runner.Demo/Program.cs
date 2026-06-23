// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The Arazzo execution-host ("runner") — the second process in the real topology (design §2). It shares the
// durability store with the control plane: the control plane creates Pending runs and owns the catalog; this
// runner registers itself, then claims and drives runs from the store-as-queue (design §5/§7). It is a worker
// process (its real-life deployment is a container, scaled independently) whose long-running loops are hosted
// BackgroundServices; the minimal web surface exists only for the §5.4 health probe + Aspire/OTel.
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sqlite;
using Corvus.Text.Json.Arazzo.Runner.Demo;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, HTTP resilience → the Aspire dashboard.
builder.AddServiceDefaults();

// The shared durability store. The AppHost injects ConnectionStrings:workflowstore so both processes open the
// same store (the SQLite file is the local stand-in for the production shared store, e.g. Postgres later). The
// temp-file fallback lets the runner start standalone; it matches the control-plane host's own fallback.
string connectionString = builder.Configuration.GetConnectionString("workflowstore")
    ?? $"Data Source={Path.Combine(Path.GetTempPath(), "arazzo-control-plane-demo.db")}";

// Connect read-mostly: the control plane owns reset+seed, so the runner never deletes or seeds. It reads the
// catalog (to learn which versions to host) and claims/leases/advances runs against the shared state store.
SqliteWorkflowStateStore stateStore = await SqliteWorkflowStateStore.ConnectAsync(connectionString);
SqliteWorkflowCatalogStore catalogStore = await SqliteWorkflowCatalogStore.ConnectAsync(connectionString);
SqliteRunnerRegistry registry = await SqliteRunnerRegistry.ConnectAsync(connectionString);

// The §13 source-credential store, shared with the control plane: the control plane registers the binding
// (reference + metadata, never the secret), and the runner reads it to learn which Vault references to resolve.
SqliteSourceCredentialStore credentials = await SqliteSourceCredentialStore.ConnectAsync(connectionString);
var catalog = new SecuredWorkflowCatalog(catalogStore, stateStore, "runner");

var options = new RunnerOptions($"runner-{Environment.MachineName}-{Environment.ProcessId}");

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IWorkflowStateStore>(stateStore);
builder.Services.AddSingleton<IWorkflowCatalogStore>(catalogStore);
builder.Services.AddSingleton<IRunnerRegistry>(registry);
builder.Services.AddSingleton<ISourceCredentialStore>(credentials);
builder.Services.AddSingleton(catalog);

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
