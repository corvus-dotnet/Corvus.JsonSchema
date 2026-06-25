// <copyright file="SecurityBindingListPagingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// The security-binding LIST projection (<c>GET /security/bindings</c>), paged vs unpaged, with the production-faithful
/// lifecycle (a workspace rented from <c>JsonWorkspaceCache</c> and disposed per op, as the request pipeline does). Like
/// the rule list, the projection is allocation-clean either way — the pooled arena returns each request — so paging does
/// not change steady-state GC; its win is bounding the produced response bytes and the projection CPU, O(total) → O(page).
/// </summary>
[MemoryDiagnoser]
public class SecurityBindingListPagingBenchmarks
{
    private const int PageSize = 50;

    private static readonly byte[] StoredJson =
        """
        {
          "id": "binding-00000",
          "claimType": "role",
          "claimValue": "tenant-admin",
          "read": { "unrestricted": false, "ruleNames": [ "tenant-scoped" ] },
          "write": { "unrestricted": false, "ruleNames": [ "tenant-scoped" ] },
          "purge": { "unrestricted": false },
          "order": 10,
          "createdBy": "alice",
          "createdAt": "1970-01-01T00:00:00+00:00",
          "description": "A representative grant.",
          "etag": "etag-1"
        }
        """u8.ToArray();

    [Params(50, 500)]
    public int BindingCount { get; set; }

    private ParsedJsonDocument<SecurityBindingDocument> document = null!;
    private SecurityBindingDocument[] bindings = null!;
    private SecurityBindingDocument[] pageBindings = null!;
    private ReadOnlyMemory<byte> pageTokenUtf8;

    [GlobalSetup]
    public void Setup()
    {
        this.document = ParsedJsonDocument<SecurityBindingDocument>.Parse(StoredJson);
        this.bindings = new SecurityBindingDocument[this.BindingCount];
        for (int i = 0; i < this.BindingCount; i++)
        {
            this.bindings[i] = this.document.RootElement;
        }

        // The first page is a separate array (a SecurityBindingDocument[] projects to IReadOnlyList box-free).
        this.pageBindings = this.bindings[..Math.Min(PageSize, this.BindingCount)];

        // When more than one page exists, the first page carries a representative (order, id) continuation token. The
        // store emits this as pooled UTF-8; the projection just references it, so it is precomputed (not measured here).
        if (this.BindingCount > PageSize)
        {
            byte[] boundaryId = System.Text.Encoding.UTF8.GetBytes("binding-00049");
            byte[] token = new byte[SecurityBindingContinuationToken.GetMaxEncodedLength(boundaryId.Length)];
            int written = SecurityBindingContinuationToken.EncodeToUtf8(10, boundaryId, token);
            this.pageTokenUtf8 = token.AsMemory(0, written);
        }
    }

    [GlobalCleanup]
    public void Cleanup() => this.document.Dispose();

    /// <summary>The unpaged list projection: every binding materialised into one response body. The response bytes grow
    /// O(total) even though the per-request pooled arena is returned each op.</summary>
    /// <returns>The projected binding count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Unpaged_All()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<SecurityBindingDocument> list = this.bindings;
        Models.SecurityBindingList.Source<IReadOnlyList<SecurityBindingDocument>> body = Models.SecurityBindingList.Build(
            in list,
            bindings: Models.SecurityBindingList.SecurityBindingSummaryArray.Build(in list, BuildBindingSummaries));
        return Models.SecurityBindingList.CreateBuilder(workspace, in body, 30).RootElement.Bindings.GetArrayLength();
    }

    /// <summary>The paged list projection: only the first keyset page (<see cref="PageSize"/> rows) plus the continuation
    /// token — the response bytes stay bounded as the total grows.</summary>
    /// <returns>The projected binding count (defeats dead-code elimination).</returns>
    [Benchmark]
    public int Paged_FirstPage()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<SecurityBindingDocument> page = this.pageBindings;
        Models.SecurityBindingList.Source<IReadOnlyList<SecurityBindingDocument>> body = Models.SecurityBindingList.Build(
            in page,
            bindings: Models.SecurityBindingList.SecurityBindingSummaryArray.Build(in page, BuildBindingSummaries),
            nextPageToken: this.pageTokenUtf8.IsEmpty ? default : (Models.JsonString.Source)this.pageTokenUtf8.Span);
        return Models.SecurityBindingList.CreateBuilder(workspace, in body, 30).RootElement.Bindings.GetArrayLength();
    }

    // The handler's closure-free per-field projection (the binding threaded as the context; each leaf carried bytes-native).
    private static void BuildBindingSummaries(in IReadOnlyList<SecurityBindingDocument> bindings, ref Models.SecurityBindingList.SecurityBindingSummaryArray.Builder array)
    {
        foreach (SecurityBindingDocument binding in bindings)
        {
            array.AddItem(Models.SecurityBindingSummary.Build(in binding, BuildBindingSummary));
        }
    }

    private static void BuildBindingSummary(in SecurityBindingDocument binding, ref Models.SecurityBindingSummary.Builder b)
        => b.Create(
            claimType: Models.JsonString.From(binding.ClaimType),
            createdAt: binding.CreatedAtValue,
            createdBy: Models.JsonString.From(binding.CreatedBy),
            etag: Models.JsonString.From(binding.Etag),
            id: Models.JsonString.From(binding.Id),
            order: binding.OrderValue,
            purge: Models.VerbGrant.From(binding.Purge),
            read: Models.VerbGrant.From(binding.Read),
            write: Models.VerbGrant.From(binding.Write),
            claimValue: Models.JsonString.From(binding.ClaimValue),
            description: Models.JsonString.From(binding.Description),
            lastUpdatedAt: Models.JsonDateTime.From(binding.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(binding.LastUpdatedBy));
}