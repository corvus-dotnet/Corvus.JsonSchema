// <copyright file="CatalogVersionWriteReachBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the redundant pre-fetch on the catalog version write path (PATCH/DELETE /catalog/{id}/versions/{n}). The
/// handler fetches+parses the whole version once (a reach check, to split 403 "readable but not writable" from 404
/// "not readable"), then the reach-aware client fetches it <em>again</em> for the actual reach-gated mutation. FIX #6
/// moves the 403/404 split into the client (which already has the fetched version), letting the handler drop its
/// pre-fetch — so the version is fetched+parsed once instead of twice.
/// <list type="bullet">
/// <item><see cref="Update_WithPrefetch"/> — today: the handler pre-fetch (<c>GetAsync</c> + parse + <c>Admits</c>) plus
/// the metadata update.</item>
/// <item><see cref="Update_NoPrefetch"/> — after: the metadata update alone (the client's own reach check, common to both
/// arms, is what remains).</item>
/// </list>
/// The delta is the eliminated pre-fetch. (Measured with <see cref="AccessContext.System"/>, so <c>Admits</c> is a
/// trivial allow — the dominant eliminated cost is the fetch+parse, the same regardless of reach; a restricted context
/// would add only the rule evaluation. The store's read array is the §13.4.1 driver leaf and the parse is pooled, so the
/// GC delta is modest by design — the win is the removed fetch+parse round-trip.)
/// </summary>
[MemoryDiagnoser]
public class CatalogVersionWriteReachBenchmarks
{
    private const string BaseWorkflowId = "nightly-reconcile";

    private static readonly ReadOnlyMemory<byte> Package = BuildPackage();

    private InMemoryWorkflowCatalogStore store = null!;
    private int versionNumber;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemoryWorkflowCatalogStore();
        var owner = new CatalogOwner("Team A", "team-a@example.com", "payments", "https://runbooks.example.com/team-a");
        TagSet tags = TagSet.FromTags(["prod", "billing"]);
        SecurityTagSet securityTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]);
        using ParsedJsonDocument<CatalogVersion> seeded = this.store.AddAsync(
            BaseWorkflowId,
            Package,
            new CatalogMetadata(owner, "alice", tags, securityTags),
            default).AsTask().GetAwaiter().GetResult();
        this.versionNumber = seeded.RootElement.Ref.VersionNumber;
    }

    /// <summary>Today: the handler pre-fetches the version for the reach check, then the metadata update runs.</summary>
    /// <returns>The version number (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Update_WithPrefetch()
    {
        AccessContext ctx = AccessContext.System;

        // The handler's reach pre-fetch: fetch + parse the version, read its tags, and check read+write reach.
        using (ParsedJsonDocument<CatalogVersion>? existing = this.store.GetAsync(BaseWorkflowId, this.versionNumber, default).AsTask().GetAwaiter().GetResult())
        {
            if (existing is { } ev)
            {
                _ = ctx.Admits(AccessVerb.Read, ev.RootElement.SecurityTagsValue)
                    && ctx.Admits(AccessVerb.Write, ev.RootElement.SecurityTagsValue);
            }
        }

        using ParsedJsonDocument<CatalogVersion>? updated = this.store
            .UpdateMetadataAsync(BaseWorkflowId, this.versionNumber, new CatalogMetadataPatch("alice", null, null, null), default)
            .AsTask().GetAwaiter().GetResult();
        return updated is { } v ? v.RootElement.Ref.VersionNumber : -1;
    }

    /// <summary>After the fix: only the metadata update runs (the reach split is folded into the client's own fetch).</summary>
    /// <returns>The version number.</returns>
    [Benchmark]
    public int Update_NoPrefetch()
    {
        using ParsedJsonDocument<CatalogVersion>? updated = this.store
            .UpdateMetadataAsync(BaseWorkflowId, this.versionNumber, new CatalogMetadataPatch("alice", null, null, null), default)
            .AsTask().GetAwaiter().GetResult();
        return updated is { } v ? v.RootElement.Ref.VersionNumber : -1;
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
