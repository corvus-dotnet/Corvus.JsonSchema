// <copyright file="EnvironmentApiBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the environments control-plane request path (§7.7) end-to-end at the handler boundary — the per-request work
/// between "ASP.NET parsed the request" and "the result body is materialised", over the in-memory reference store (no
/// Kestrel / no sockets / no I/O noise): the store read + the congruent whole-document <c>Models.EnvironmentSummary.From</c>
/// projection + the generated result builder materialising the response into a per-request <see cref="JsonWorkspace"/>.
/// This is the realistic allocation a deployment sees per request minus the HTTP transport, and the regression guard that
/// the handler adds only the response materialisation on top of the (already-swept) store read.
/// <list type="bullet">
/// <item><see cref="List_Page"/> — GET /environments: a 20-item page (the path whose store-level cost the sweep halved).</item>
/// <item><see cref="Create"/> — POST /environments: a fresh store per op so the fixed name does not 409.</item>
/// </list>
/// </summary>
[MemoryDiagnoser]
public class EnvironmentApiBenchmarks
{
    private const string Actor = "bench";
    private const int Seeded = 20;

    // A representative POST /environments request body, already parsed (ASP.NET parsed it before the handler runs).
    private static readonly byte[] CreateBodyJson =
        """{"name":"production","displayName":"Production","description":"The live environment."}"""u8.ToArray();

    private ArazzoControlPlaneEnvironmentsHandler listHandler = null!;
    private ParsedJsonDocument<Models.EnvironmentWrite> createBody = null!;

    [GlobalSetup]
    public void Setup()
    {
        var store = new InMemoryEnvironmentStore();
        SecurityTagSet managementTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]);
        for (int i = 0; i < Seeded; i++)
        {
            using ParsedJsonDocument<Environment> draft = Environment.Draft($"env-{i:D3}", null, null, managementTags);
            using ParsedJsonDocument<Environment> added = store.AddAsync(draft.RootElement, Actor, default).AsTask().GetAwaiter().GetResult();
        }

        this.listHandler = new ArazzoControlPlaneEnvironmentsHandler(store, new SecuredEnvironmentAdministration(new InMemoryEnvironmentAdministratorStore()), Actor);
        this.createBody = ParsedJsonDocument<Models.EnvironmentWrite>.Parse(CreateBodyJson);
    }

    [GlobalCleanup]
    public void Cleanup() => this.createBody.Dispose();

    /// <summary>GET /environments → the handler list of a 20-item page: the keyset store read, the congruent
    /// <c>From</c> projection, and the result body materialised into a per-request workspace (disposed each op).</summary>
    [Benchmark]
    public async Task List_Page()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        ListEnvironmentsResult result = await this.listHandler.HandleListEnvironmentsAsync(default, workspace, default);
        _ = result.StatusCode;
    }

    /// <summary>POST /environments → the handler create: the body's parsed values carried into a draft, persisted, and the
    /// summary materialised. A fresh store per op so the fixed name does not 409 (the store alloc is the InMemory baseline).</summary>
    [Benchmark]
    public async Task Create()
    {
        var store = new InMemoryEnvironmentStore();
        var handler = new ArazzoControlPlaneEnvironmentsHandler(store, new SecuredEnvironmentAdministration(new InMemoryEnvironmentAdministratorStore()), Actor);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var parameters = new CreateEnvironmentParams { Body = this.createBody.RootElement };
        CreateEnvironmentResult result = await handler.HandleCreateEnvironmentAsync(parameters, workspace, default);
        _ = result.StatusCode;
    }
}