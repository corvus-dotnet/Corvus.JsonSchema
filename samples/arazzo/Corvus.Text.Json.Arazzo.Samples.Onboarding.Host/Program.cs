// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The onboarding domain service, hosted as its own process (design §2 external source). It is a genuine backend the
// Arazzo control plane and runner call over the network — not an inline mock — and it owns its own PostgreSQL
// database (the microservice-owns-its-data pattern: the AppHost stands up a dedicated Postgres for it and injects
// ConnectionStrings:onboardingdb). The service provisions its own schema on startup, then serves the generated
// onboarding API (create/verify/provision/welcome for the workflow; list/get for the onboarding console).
using Corvus.Text.Json.Arazzo.Samples.Onboarding;
using Microsoft.Extensions.FileProviders;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// OpenTelemetry (incl. the Corvus.Arazzo source/meter), health checks, service discovery, HTTP resilience.
builder.AddServiceDefaults();

// The service's own database — provisioned and owned by this service alone (the AppHost injects the connection
// string). Required: the onboarding service runs under the AppHost, which stands up its dedicated Postgres.
string connectionString = builder.Configuration.GetConnectionString("onboardingdb")
    ?? throw new InvalidOperationException("ConnectionStrings:onboardingdb (the onboarding service's own database) is required — run the onboarding service under the AppHost.");

// Provision the schema (the service owns its DDL), then open the store over one shared data source.
await OnboardingAccountStore.PrepareAsync(connectionString);
NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(dataSource);
OnboardingAccountStore store = await OnboardingAccountStore.ConnectAsync(dataSource);
var handler = new OnboardingService(store, new ResourceAllocator());

WebApplication app = builder.Build();

// /health (readiness) and /alive (liveness) — the AppHost's health check polls these.
app.MapDefaultEndpoints();

// The onboarding console (a separate, build-free web app under web/arazzo-onboarding-ui) is served at the host root,
// same origin as the API it calls (GET /accounts) — so it needs no cross-origin configuration. In reality this
// product ships independently and merely consumes the workflow engine; here the onboarding host serves it for the demo.
string uiRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "..", "web", "arazzo-onboarding-ui"));
if (Directory.Exists(uiRoot))
{
    var uiFiles = new PhysicalFileProvider(uiRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = uiFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = uiFiles });
}

// The generated onboarding API: POST /accounts, POST /accounts/{id}/identity|resources|welcome, GET /accounts[/{id}].
ApiEndpointRegistration.MapApiEndpoints(app, handler);

app.Run();
