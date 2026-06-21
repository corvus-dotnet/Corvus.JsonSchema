// <copyright file="CatalogProjectionBreakdownBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Attributes the catalog-add projection's per-operation allocation across its stages, so the dominant cost is measured
/// rather than asserted: <see cref="Project"/> is the whole projection; the rest isolate each public stage it runs —
/// the input ZIP read (<see cref="OpenInputZip"/>), the canonical ZIP write (<see cref="PackCanonicalZip"/>), the
/// content hash (<see cref="ComputeHash"/>), and the version-document build (<see cref="BuildVersionDocument"/>). The two
/// ZIP stages expose the <c>System.IO.Compression</c> framework floor; the hash + version-doc stages expose the genuine
/// stored/transient leaves.
/// </summary>
[MemoryDiagnoser]
public class CatalogProjectionBreakdownBenchmarks
{
    private const string BaseWorkflowId = "nightly-reconcile";

    private static readonly byte[] WorkflowBytes = Encoding.UTF8.GetBytes(
        """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Nightly Reconcile", "description": "Reconciles state nightly." },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.json", "type": "openapi" } ],
          "workflows": [ { "workflowId": "nightly-reconcile", "steps": [] } ]
        }
        """);

    private static readonly byte[] PetstoreBytes = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0.0"}}""");

    private IReadOnlyList<KeyValuePair<string, byte[]>> sources = null!;
    private ReadOnlyMemory<byte> package;
    private CatalogOwner owner;
    private TagSet tags;
    private SourceSet sourceSet;
    private SecurityTagSet securityTags;

    [GlobalSetup]
    public void Setup()
    {
        this.sources = [new KeyValuePair<string, byte[]>("petstore", PetstoreBytes)];
        this.package = WorkflowPackage.Pack(WorkflowBytes, this.sources);
        this.owner = new CatalogOwner("Team A", "team-a@example.com", "payments", "https://runbooks.example.com/team-a");
        this.tags = TagSet.FromTags(["prod", "billing"]);
        this.sourceSet = SourceSet.FromSources([new CatalogSourceRef("petstore", "openapi")]);
        this.securityTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]);
    }

    /// <summary>The whole projection (id rewrite + hash + canonical pack + metadata) — the sum to attribute.</summary>
    /// <returns>The assigned version number (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Project() => CatalogPackage.Project(this.package, BaseWorkflowId, 1).Hash.Length;

    /// <summary>Stage: read the input package's documents (a <c>ZipArchive(Read)</c>).</summary>
    /// <returns>The workflow document length.</returns>
    [Benchmark]
    public int OpenInputZip() => WorkflowPackage.Open(this.package).Workflow.Length;

    /// <summary>Stage: write the canonical package (a <c>ZipArchive(Create)</c> + the output array).</summary>
    /// <returns>The canonical package length.</returns>
    [Benchmark]
    public int PackCanonicalZip() => WorkflowPackage.Pack(WorkflowBytes, this.sources).Length;

    /// <summary>Stage: compute the content hash (canonicalise + SHA-256).</summary>
    /// <returns>The hash string length.</returns>
    [Benchmark]
    public int ComputeHash() => WorkflowPackage.ComputeContentHash(WorkflowBytes, this.sources).Length;

    /// <summary>Stage: build the persisted version document.</summary>
    /// <returns>The version number (prevents dead-code elimination).</returns>
    [Benchmark]
    public int BuildVersionDocument()
        => CatalogVersion.Create(
            baseWorkflowId: BaseWorkflowId,
            versionNumber: 1,
            workflowId: "nightly-reconcile-v1",
            title: "Nightly Reconcile",
            description: "Reconciles state nightly.",
            status: CatalogStatus.Active,
            tags: this.tags,
            owner: this.owner,
            sources: this.sourceSet,
            hash: "0000000000000000000000000000000000000000000000000000000000000000",
            createdBy: "alice",
            createdAt: DateTimeOffset.UnixEpoch,
            runnable: false,
            securityTags: this.securityTags).Ref.VersionNumber;
}
