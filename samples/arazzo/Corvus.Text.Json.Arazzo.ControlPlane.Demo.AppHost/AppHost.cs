// <copyright file="AppHost.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The Aspire composition root for the Arazzo control-plane demo. This is the *real* host wiring: each piece is
// a first-class resource so the Aspire dashboard becomes the OpenTelemetry viewer (traces/logs/metrics) and the
// single launch point. Today it composes the two-process topology (control plane + runner); the locally-runnable
// dependency containers (HashiCorp Vault for secrets, Keycloak for OIDC, optionally Postgres) join here next.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// The shared durability store. SQLite is the local stand-in for the production shared store (becomes an
// AddPostgres resource later); injecting one connection string into both processes is how they share it — the
// exact shape that generalizes to a real database. The control plane owns reset + seed; the runner reads + claims.
string sharedStore = $"Data Source={Path.Combine(Path.GetTempPath(), "arazzo-demo-shared.db")}";

// The ASP.NET control-plane host: the real server surface (catalog, runs, credentials, administrators, security)
// plus the build-free web UI and the demo /svc backends. Externally reachable so the dashboard links straight to
// it; its OpenTelemetry flows to the dashboard via the ServiceDefaults it opts into.
var controlplane = builder.AddProject<Projects.Corvus_Text_Json_Arazzo_ControlPlane_Demo>("controlplane")
    .WithEnvironment("ConnectionStrings__workflowstore", sharedStore)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// The runner ("execution-host") — the second process in the topology. It shares the store, registers in the
// runner registry, and claims/resumes runs (design §5/§7). It waits for the control plane to seed the store, and
// holds a reference to it so the executor can call the /svc backends once live execution is unpaused.
builder.AddProject<Projects.Corvus_Text_Json_Arazzo_Runner_Demo>("runner")
    .WithEnvironment("ConnectionStrings__workflowstore", sharedStore)
    .WithReference(controlplane)
    .WaitFor(controlplane)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
