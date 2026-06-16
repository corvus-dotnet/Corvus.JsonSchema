// <copyright file="AppHost.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The Aspire composition root for the Arazzo control-plane demo. This is the *real* host wiring: each piece is
// a first-class resource so the Aspire dashboard becomes the OpenTelemetry viewer (traces/logs/metrics) and the
// single launch point. Today it composes the control-plane host; the runner host and the locally-runnable
// dependency containers (HashiCorp Vault for secrets, Keycloak for OIDC, optionally Postgres) join here next.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// The ASP.NET control-plane host: the real server surface (catalog, runs, credentials, administrators, security)
// plus the build-free web UI and the demo /svc backends. Externally reachable so the dashboard links straight to
// it; its OpenTelemetry flows to the dashboard via the ServiceDefaults it opts into. SQLite is in-process, so no
// connection string is injected — the runner host and Vault/Keycloak/Postgres resources arrive in a later step.
builder.AddProject<Projects.Corvus_Text_Json_Arazzo_ControlPlane_Demo>("controlplane")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
