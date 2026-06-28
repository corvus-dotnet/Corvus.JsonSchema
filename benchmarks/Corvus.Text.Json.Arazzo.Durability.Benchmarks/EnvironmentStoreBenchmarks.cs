// <copyright file="EnvironmentStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the environment store seams (design §7.7) end-to-end over the in-memory reference store (no driver / no I/O
/// noise) — the per-request work a handler does between "the framework parsed the body" and "the store holds the bytes".
/// There is no record/POCO seam to compare against (the design carries the body's JSON values straight into a draft
/// <see cref="Environment"/>, reifying only inside the store), so these record the bytes-to-bytes seam's allocation as
/// the baseline-to-beat for the backend fan-out.
/// <list type="bullet">
/// <item><see cref="Create_FromDraft"/> — POST /environments → <see cref="IEnvironmentStore.AddAsync"/>: the body's
/// already-parsed JSON values are carried bytes-to-bytes into a draft (no per-field strings) which the store completes
/// with the server-stamped createdBy/createdAt/etag; each iteration uses a fresh store so the unique name does not 409.</item>
/// <item><see cref="List_Page"/> — GET /environments → <see cref="IEnvironmentStore.ListAsync"/>: one reach-filtered
/// keyset page over a pre-seeded store.</item>
/// </list>
/// The management <see cref="SecurityTagSet"/> the handler resolves from the caller's <c>AccessContext</c> is
/// precomputed in setup so the measured region is the persistence seam, not the access-tag resolution.
/// </summary>
[MemoryDiagnoser]
public class EnvironmentStoreBenchmarks
{
    private const string Actor = "bench";
    private const int SeededCount = 50;
    private const int PageSize = 20;

    // A representative POST /environments request body, already parsed (the HTTP framework parsed it before the handler
    // runs), so the benchmark measures the seam, not the parse. Held as a pooled document, disposed in cleanup.
    private static readonly byte[] BodyJson =
        """
        {
          "name": "production",
          "displayName": "Production",
          "description": "The live environment."
        }
        """u8.ToArray();

    private ParsedJsonDocument<Environment> body = null!;
    private SecurityTagSet managementTags;
    private InMemoryEnvironmentStore listStore = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.body = ParsedJsonDocument<Environment>.Parse(BodyJson);

        // Resolved by the handler from the caller's AccessContext (the internal tenant tag); precomputed here so the
        // measured region is the persistence seam, not the access-tag resolution.
        this.managementTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]);

        // A pre-seeded store for the list benchmark (created once; ListAsync does not mutate).
        this.listStore = new InMemoryEnvironmentStore();
        for (int i = 0; i < SeededCount; i++)
        {
            using ParsedJsonDocument<Environment> draft = Environment.Draft($"env-{i:D3}", null, null, this.managementTags);
            using ParsedJsonDocument<Environment> added = this.listStore.AddAsync(draft.RootElement, Actor, default).AsTask().GetAwaiter().GetResult();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => this.body.Dispose();

    /// <summary>The create seam: carry the body's already-parsed JSON values bytes-to-bytes into a draft environment (no
    /// per-field strings), then persist it (the store completes it with the server-stamped fields).</summary>
    [Benchmark]
    public void Create_FromDraft()
    {
        var store = new InMemoryEnvironmentStore();
        Environment b = this.body.RootElement;

        using ParsedJsonDocument<Environment> draft = Environment.Draft(
            name: (JsonElement)b.Name,
            displayName: (JsonElement)b.DisplayName,
            description: (JsonElement)b.Description,
            managementTags: this.managementTags);

        // Dispose the returned pooled document exactly as a HandleCreateEnvironmentAsync would (its `using`).
        using ParsedJsonDocument<Environment> created =
            store.AddAsync(draft.RootElement, Actor, default).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The list seam: one reach-filtered keyset page over the pre-seeded store (dispose the page as a handler does).</summary>
    [Benchmark]
    public void List_Page()
    {
        using EnvironmentPage page = this.listStore.ListAsync(AccessContext.System, PageSize, default, default).AsTask().GetAwaiter().GetResult();
    }
}