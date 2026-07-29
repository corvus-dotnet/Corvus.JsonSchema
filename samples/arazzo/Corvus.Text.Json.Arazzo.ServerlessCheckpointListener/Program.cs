// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The public checkpoint listener (ADR 0055/0062): a token-authenticated checkpoint surface — GET/POST
// /runs/{runId}/checkpoint — plus the demo `echo` source, backed by an Azure Storage state store. A deployed serverless
// function (AWS Lambda or Azure Functions) reaches this over the public internet to load and save its run's checkpoint,
// presenting the run-scoped checkpoint token the runner minted; this host validates it against the run in the URL, so the
// surface is reachable by a real cloud function without being an open write endpoint. It is the runner-side host that
// makes a true real-cloud run-to-completion provable, and a reusable way to run a token-authenticated checkpoint surface.
using Corvus.Text.Json.Arazzo.Durability.AzureStorage;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

// Configuration (environment variables in the Container App): the shared Azure Storage store and the checkpoint secret.
string storageConnection = builder.Configuration["ARAZZO_CHECKPOINT_STORAGE"]
    ?? throw new InvalidOperationException("ARAZZO_CHECKPOINT_STORAGE (the Azure Storage connection string for the shared run store) is required.");
string secretBase64 = builder.Configuration["ARAZZO_CHECKPOINT_SECRET"]
    ?? throw new InvalidOperationException("ARAZZO_CHECKPOINT_SECRET (the base64 shared checkpoint secret) is required.");
byte[] checkpointSecret = Convert.FromBase64String(secretBase64);

// Provision (idempotent) then open the shared store. The seeding/asserting process connects to the same account.
await AzureStorageWorkflowStateStore.PrepareAsync(storageConnection);
AzureStorageWorkflowStateStore store = await AzureStorageWorkflowStateStore.ConnectAsync(storageConnection);

WebApplication app = builder.Build();

// The token-authenticated checkpoint surface (ADR 0062): the run's function presents a run-scoped bearer token, validated
// against the run in the request URL. requireAuthorization is false because the token is the credential, not an OIDC
// principal — a machine callback has no interactive session.
app.MapWorkflowCheckpointEndpoints(
    store,
    requireAuthorization: false,
    authenticateCheckpointToken: (id, token) => CheckpointToken.TryValidate(checkpointSecret, token, id.Value, DateTimeOffset.UtcNow));

// The workflow's `echo` source (the serverless-check workflow's one GET). Unauthenticated: it is a source the function
// calls, not the checkpoint surface, and it carries no token.
app.MapGet("/demo/echo", () => Results.Json(new { status = "ok" }));

// A liveness endpoint for the hosting platform's probe.
app.MapGet("/health", () => Results.Ok());

app.Run();