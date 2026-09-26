// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The public checkpoint listener (ADR 0055/0062): a token-authenticated checkpoint surface — GET/POST
// /runs/{runId}/checkpoint — plus the demo `echo` source, backed by an Azure Storage state store. A deployed serverless
// function (AWS Lambda or Azure Functions) reaches this over the public internet to load and save its run's checkpoint,
// presenting the run-scoped checkpoint token the runner minted; this host validates it against the run in the URL, so the
// surface is reachable by a real cloud function without being an open write endpoint. It is the runner-side host that
// makes a true real-cloud run-to-completion provable, and a reusable way to run a token-authenticated checkpoint surface.
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.AzureStorage;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;

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

// The listener's key ring (ADR 0065 decisions 5, 10 and 11): the listener terminates the function's plaintext
// checkpoint, so it is the host that holds the environment payload key. Each environment named under
// Runner:Environments:N:{Environment,Sealed,KeyId,PayloadKeyRef,SealKeyFingerprint} (as Container App env vars,
// Runner__Environments__0__Environment and so on) has its key read from this container's own secret store,
// an env:// or file:// reference to a Container App secret, never from the control plane. Every checkpoint the
// function posts for it is encrypted and MAC'd here before it reaches the store, and every row the function loads is
// verified and opened here. Empty for an open environment.
// The allowlist (ADR 0065 decision 10) is default deny: this host serves the environments named under
// Runner:Environments:N:{Environment,Sealed,KeyId,PayloadKeyRef,SealKeyFingerprint} and no other, each clear or with
// its key from this host's own secret store (env:// or file://). A checkpoint for an environment with no entry is
// refused, so a function pointed at this host for an environment the tenant did not name gets nothing.
List<RunnerKeyRingEntry> keyRingEntries = [];
foreach (IConfigurationSection entry in builder.Configuration.GetSection("Runner:Environments").GetChildren())
{
    keyRingEntries.Add(new RunnerKeyRingEntry(
        entry["Environment"] ?? throw new InvalidOperationException($"{entry.Path}:Environment is required."),
        entry.GetValue("Sealed", false),
        entry["KeyId"],
        entry["PayloadKeyRef"] is { Length: > 0 } payloadKeyRef ? SecretRef.Parse(payloadKeyRef) : null,
        entry["SealKeyFingerprint"],
        MinimumKeyId: entry["MinimumKeyId"]));
}

RunnerKeyRing keyRing = await RunnerKeyRing.BuildAsync(keyRingEntries, new CompositeSecretResolver(new EnvSecretResolver(), new FileSecretResolver()), CancellationToken.None);
IWorkflowCheckpointStore checkpointSurfaceStore = new SealingCheckpointStore(store, keyRing);

WebApplication app = builder.Build();

// The token-authenticated checkpoint surface (ADR 0062): the run's function presents a run-scoped bearer token, validated
// against the run in the request URL. requireAuthorization is false because the token is the credential, not an OIDC
// principal — a machine callback has no interactive session. For a sealed environment the store behind it is the
// sealing store built above (ADR 0065 decision 11).
app.MapWorkflowCheckpointEndpoints(
    checkpointSurfaceStore,
    requireAuthorization: false,
    authenticateCheckpointToken: (address, token) => CheckpointToken.TryValidate(checkpointSecret, token, address, DateTimeOffset.UtcNow));

// The workflow's `echo` source (the serverless-check workflow's one GET). Unauthenticated: it is a source the function
// calls, not the checkpoint surface, and it carries no token.
app.MapGet("/demo/echo", () => Results.Json(new { status = "ok" }));

// A liveness endpoint for the hosting platform's probe.
app.MapGet("/health", () => Results.Ok());

app.Run();