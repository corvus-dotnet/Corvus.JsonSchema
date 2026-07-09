// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The KYC domain service, hosted as its own process (design §2 external source). It owns all identity verification —
// the synchronous verifyIdentity the onboard-customer workflow calls (and, in a later slice, the asynchronous,
// manual-recovery verdict). It is a genuine backend the Arazzo control plane and runner call over the network, and it
// owns its own PostgreSQL database (the AppHost injects ConnectionStrings:kycdb).
using Corvus.Text.Json.Arazzo.Samples.Kyc;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// OpenTelemetry (incl. the Corvus.Arazzo source/meter), health checks, service discovery, HTTP resilience.
builder.AddServiceDefaults();

// The service's own database — provisioned and owned by this service alone (the AppHost injects the connection string).
string connectionString = builder.Configuration.GetConnectionString("kycdb")
    ?? throw new InvalidOperationException("ConnectionStrings:kycdb (the KYC service's own database) is required — run the KYC service under the AppHost.");

// Provision the schema (the service owns its DDL), then open the store.
await KycStore.PrepareAsync(connectionString);
NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(dataSource);
KycStore store = await KycStore.ConnectAsync(dataSource);
var handler = new KycService(store, new IdentityVerificationPolicy());

WebApplication app = builder.Build();

// /health (readiness) and /alive (liveness) — the AppHost's health check polls these.
app.MapDefaultEndpoints();

// The generated KYC API: POST /accounts/{id}/identity (verifyIdentity), GET /verifications, GET /accounts/{id}/verification.
ApiEndpointRegistration.MapApiEndpoints(app, handler);

// A tiny identity endpoint so the dashboard's resource link lands somewhere informative.
app.MapGet("/", () => Results.Text("Arazzo KYC service — verifications under /verifications, health at /health.", "text/plain"));

app.Run();
