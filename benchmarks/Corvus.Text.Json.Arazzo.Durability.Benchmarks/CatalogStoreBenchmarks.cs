// <copyright file="CatalogStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the catalog ADD write seam (POST /catalog → <see cref="IWorkflowCatalogStore.AddAsync"/>), end-to-end over
/// the in-memory reference store (no driver / no I/O noise). The store projects the package (parse + canonicalise + hash
/// + id-rewrite — the unavoidable bulk) and then writes the governance metadata; this benchmark isolates the metadata
/// <em>seam</em>:
/// <list type="bullet">
/// <item><see cref="Add_FromRecord"/> — the old record seam: the governance owner is a hand-rolled
/// <see cref="CatalogOwner"/> record (four managed strings) carried in <see cref="CatalogMetadata"/>, which the store
/// writes field-by-field into the version document.</item>
/// </list>
/// A fresh store per invocation keeps version numbers from accumulating; the package bytes are pre-built so the measured
/// allocation is the projection + metadata write, not the fixture construction.
/// </summary>
[MemoryDiagnoser]
public class CatalogStoreBenchmarks
{
    private const string BaseWorkflowId = "nightly-reconcile";

    private static readonly ReadOnlyMemory<byte> Package = BuildPackage();

    // A representative owner part from the POST /catalog request body, already parsed (the framework parsed it), so the
    // record arm transcodes its UTF-8 to managed strings per op exactly as the handler's ToOwner does — the seam cost.
    private static readonly byte[] OwnerJson =
        """{ "name": "Team A", "email": "team-a@example.com", "team": "payments", "url": "https://runbooks.example.com/team-a" }"""u8.ToArray();

    private ParsedJsonDocument<Models.CatalogOwner> owner = null!;
    private TagSet tags;
    private SecurityTagSet securityTags;
    private InMemoryWorkflowCatalogStore searchStore = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.owner = ParsedJsonDocument<Models.CatalogOwner>.Parse(OwnerJson);

        // Precomputed so the measured region is the owner seam + the package projection, not the tag construction.
        this.tags = TagSet.FromTags(["prod", "billing"]);
        this.securityTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]);

        // A pre-seeded catalog so Search_Page measures the keyset query path. 25 distinct base ids » the limit of 10, so
        // the page emits a continuation token (the carrier-seam allocation the row targets).
        var ownerRecord = new CatalogOwner("Team A", "team-a@example.com", "payments", "https://runbooks.example.com/team-a");
        this.searchStore = new InMemoryWorkflowCatalogStore();
        for (int i = 0; i < 25; i++)
        {
            using ParsedJsonDocument<CatalogVersion> seeded = this.searchStore.AddAsync(
                $"wf-{i:D3}",
                Package,
                new CatalogMetadata(ownerRecord, "alice", this.tags, this.securityTags),
                default).AsTask().GetAwaiter().GetResult();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => this.owner.Dispose();

    /// <summary>The old record seam: read the parsed owner body into a <see cref="CatalogOwner"/> record (four managed
    /// strings transcoded off the body's UTF-8) + <see cref="CatalogMetadata"/>, hand it to the store, which writes the
    /// owner's strings field-by-field into the version document.</summary>
    /// <returns>The assigned version number (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Add_FromRecord()
    {
        Models.CatalogOwner o = this.owner.RootElement;
        var ownerRecord = new CatalogOwner(
            (string)o.Name,
            (string)o.Email,
            o.Team.IsNotUndefined() ? (string)o.Team : null,
            o.Url.IsNotUndefined() ? (string)o.Url : null);

        var store = new InMemoryWorkflowCatalogStore();
        using ParsedJsonDocument<CatalogVersion> version = store.AddAsync(
            BaseWorkflowId,
            Package,
            new CatalogMetadata(ownerRecord, "alice", this.tags, this.securityTags),
            default).AsTask().GetAwaiter().GetResult();
        return version.RootElement.Ref.VersionNumber;
    }

    /// <summary>One keyset page of the catalog search — the warm operator/type-ahead read path. The page emits a
    /// continuation token (25 versions, limit 10); pooled version documents, ordered keyset, no STJ.</summary>
    /// <returns>The page size (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Search_Page()
    {
        using CatalogPage page = this.searchStore.QueryAsync(new CatalogQuery(Limit: 10), default).AsTask().GetAwaiter().GetResult();
        return page.Versions.Count;
    }

    private static ReadOnlyMemory<byte> BuildPackage()
    {
        byte[] workflow = Encoding.UTF8.GetBytes(
            """
            {
              "arazzo": "1.1.0",
              "info": { "title": "Nightly Reconcile", "description": "Reconciles state nightly." },
              "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.json", "type": "openapi" } ],
              "workflows": [ { "workflowId": "nightly-reconcile", "steps": [] } ]
            }
            """);
        byte[] petstore = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0.0"}}""");
        return CatalogPackage.Build(workflow, [new KeyValuePair<string, byte[]>("petstore", petstore)]);
    }
}
