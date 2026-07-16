// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The ledger/reconciliation domain service, hosted as its own process (design §2 external source). It is a genuine
// backend the Arazzo control plane and runner call over the network — not an inline mock — and it owns its own
// PostgreSQL database (the microservice-owns-its-data pattern: the AppHost stands up a dedicated Postgres for it and
// injects ConnectionStrings:ledgerdb). The service provisions its own schema and seeds its account book on startup,
// then serves the generated ledger API (the nightly-reconcile workflow's six ops, plus reconciliation/account reads).
using Corvus.Text.Json.Arazzo.Samples.Ledger;
using Microsoft.Extensions.FileProviders;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// OpenTelemetry (incl. the Corvus.Arazzo source/meter), health checks, service discovery, HTTP resilience.
builder.AddServiceDefaults();

// The service's own database — provisioned and owned by this service alone (the AppHost injects the connection
// string). Required: the ledger service runs under the AppHost, which stands up its dedicated Postgres.
string connectionString = builder.Configuration.GetConnectionString("ledgerdb")
    ?? throw new InvalidOperationException("ConnectionStrings:ledgerdb (the ledger service's own database) is required — run the ledger service under the AppHost.");

// Provision the schema and seed the account book (the service owns its DDL + data), then open the store.
await LedgerStore.PrepareAsync(connectionString);
NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(dataSource);
LedgerStore store = await LedgerStore.ConnectAsync(dataSource);
var handler = new LedgerService(store);

WebApplication app = builder.Build();

// /health (readiness) and /alive (liveness) — the AppHost's health check polls these.
app.MapDefaultEndpoints();

// The reconciliation console (a separate, build-free web app under web/arazzo-ledger-ui) is served at the host root,
// same origin as the API it calls (GET /reconciliations) — so it needs no cross-origin configuration. In reality this
// product ships independently and merely consumes the workflow engine; here the ledger host serves it for the demo.
string uiRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "..", "web", "arazzo-ledger-ui"));
if (Directory.Exists(uiRoot))
{
    var uiFiles = new PhysicalFileProvider(uiRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = uiFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = uiFiles });
}

// The generated ledger API: GET /ledger, GET /transactions, POST /match|discrepancies|corrections|report, and the
// GET /reconciliations[/{runId}] + GET /accounts reads.
ApiEndpointRegistration.MapApiEndpoints(app, handler);

app.Run();
