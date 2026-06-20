// <copyright file="AccessRequestStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the access-request create seam (<see cref="IAccessRequestStore.CreateAsync"/>) carrying the operator + subject
/// content as a draft <see cref="AccessRequest"/> the store completes (Tier-2 record-seam elimination), end-to-end over
/// the in-memory reference store. The handler/approval pipeline builds the draft with <see cref="AccessRequest.Draft"/> —
/// a pooled, disposable document (no detached <c>ParseValue</c>) the eligibility predicate + approval pipeline read and
/// the store reads bytes-to-bytes (including the requestedScopes array) before stamping id/etag/created + the Pending
/// status. The requested scopes are pre-built so the measured allocation is the seam's (the pooled draft + the persisted
/// document + the pooled result), not the scope-list construction. Regression guard that the draft path stays pooled.
/// </summary>
[MemoryDiagnoser]
public class AccessRequestStoreBenchmarks
{
    private static readonly string[] Scopes = ["runs:write", "runs:read"];

    private InMemoryAccessRequestStore store = null!;

    [GlobalSetup]
    public void Setup() => this.store = new InMemoryAccessRequestStore();

    /// <summary>The create path: build the pooled draft, write the persisted document (scopes carried bytes-to-bytes), stamp id/etag/created + Pending.</summary>
    [Benchmark]
    public void Create_FromDraft()
    {
        using ParsedJsonDocument<AccessRequest> draft = AccessRequest.Draft("nightly-reconcile", Scopes, "sub", "alice", "Alice Smith", "on-call", 3600);
        using ParsedJsonDocument<AccessRequest> created = this.store
            .CreateAsync(draft.RootElement, "alice", default)
            .AsTask().GetAwaiter().GetResult();
    }
}
