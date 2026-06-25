// <copyright file="AccessRequestListPagingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// The access-request LIST projection (<c>GET /accessRequests</c> — the unbounded approval queue), paged vs unpaged, with
/// the production-faithful lifecycle (a workspace rented and disposed per op, as the request pipeline does). The projection
/// is a congruent whole-document <c>AccessRequestView.From</c> wrap, so it is allocation-clean either way — the pooled
/// arena returns each request — and paging bounds CPU and response size O(total) → O(page).
/// </summary>
[MemoryDiagnoser]
public class AccessRequestListPagingBenchmarks
{
    private const int PageSize = 50;

    [Params(50, 500)]
    public int RequestCount { get; set; }

    private ParsedJsonDocument<AccessRequest>[] documents = null!;
    private AccessRequest[] requests = null!;
    private AccessRequest[] pageRequests = null!;
    private ReadOnlyMemory<byte> pageTokenUtf8;

    [GlobalSetup]
    public void Setup()
    {
        this.documents = new ParsedJsonDocument<AccessRequest>[this.RequestCount];
        this.requests = new AccessRequest[this.RequestCount];
        for (int i = 0; i < this.RequestCount; i++)
        {
            using ParsedJsonDocument<AccessRequest> draft = AccessRequest.Draft("flow", ["runs:write"], "sub", $"user-{i:D5}");
            byte[] created = AccessRequestSerialization.SerializeNew($"req-{i:D5}", draft.RootElement, "system", DateTimeOffset.UnixEpoch.AddSeconds(i), new WorkflowEtag($"etag-{i}"));
            this.documents[i] = ParsedJsonDocument<AccessRequest>.Parse(created.AsMemory());
            this.requests[i] = this.documents[i].RootElement;
        }

        // The first page is a separate array (an AccessRequest[] projects to IReadOnlyList box-free).
        this.pageRequests = this.requests[..Math.Min(PageSize, this.RequestCount)];

        // When more than one page exists, the first page carries a (createdAt, id) continuation token for its last row.
        if (this.RequestCount > PageSize)
        {
            long boundaryTicks = DateTimeOffset.UnixEpoch.AddSeconds(PageSize - 1).UtcTicks;
            byte[] boundaryId = System.Text.Encoding.UTF8.GetBytes($"req-{PageSize - 1:D5}");
            byte[] token = new byte[AccessRequestContinuationToken.GetMaxEncodedLength(boundaryId.Length)];
            int written = AccessRequestContinuationToken.EncodeToUtf8(boundaryTicks, boundaryId, token);
            this.pageTokenUtf8 = token.AsMemory(0, written);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (ParsedJsonDocument<AccessRequest> d in this.documents)
        {
            d.Dispose();
        }
    }

    /// <summary>The unpaged list projection: every queued request materialised into one response body — grows O(total).</summary>
    /// <returns>The projected request count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Unpaged_All()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<AccessRequest> list = this.requests;
        Models.AccessRequestList.Source<IReadOnlyList<AccessRequest>> body = Models.AccessRequestList.Build(
            in list,
            accessRequests: Models.AccessRequestList.AccessRequestViewArray.Build(in list, BuildViews));
        return Models.AccessRequestList.CreateBuilder(workspace, in body, 30).RootElement.AccessRequests.GetArrayLength();
    }

    /// <summary>The paged list projection: only the first keyset page (<see cref="PageSize"/> rows) plus the continuation
    /// token — the response bytes stay bounded as the queue grows.</summary>
    /// <returns>The projected request count (defeats dead-code elimination).</returns>
    [Benchmark]
    public int Paged_FirstPage()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<AccessRequest> page = this.pageRequests;
        Models.AccessRequestList.Source<IReadOnlyList<AccessRequest>> body = Models.AccessRequestList.Build(
            in page,
            accessRequests: Models.AccessRequestList.AccessRequestViewArray.Build(in page, BuildViews),
            nextPageToken: this.pageTokenUtf8.IsEmpty ? default : (Models.JsonString.Source)this.pageTokenUtf8.Span);
        return Models.AccessRequestList.CreateBuilder(workspace, in body, 30).RootElement.AccessRequests.GetArrayLength();
    }

    // The handler's congruent whole-document projection (each view an AccessRequestView.From wrap of the request).
    private static void BuildViews(in IReadOnlyList<AccessRequest> requests, ref Models.AccessRequestList.AccessRequestViewArray.Builder array)
    {
        foreach (AccessRequest request in requests)
        {
            array.AddItem(Models.AccessRequestView.From(request));
        }
    }
}