// <copyright file="SecurityBindingStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the binding seam (<see cref="ISecurityPolicyStore.UpdateBindingAsync"/>) carrying the operator content as a
/// draft <see cref="SecurityBindingDocument"/> the store completes (Tier-2 record-seam elimination), end-to-end over the
/// in-memory reference store. Unlike the rule seam there is no warm <c>From</c> path (the HTTP handler normalises the
/// verb grants), so the binding draft is always built with <see cref="SecurityBindingDocument.Draft"/> — a pooled,
/// disposable document built through a pooled scratch buffer (no detached <c>ParseValue</c>). The verb grant is pre-built
/// in setup so the measured allocation is the seam's (the pooled draft + the read/rewritten persisted documents), not the
/// grant construction. The benchmark is the regression guard that the draft path stays pooled and bytes-to-bytes.
/// </summary>
[MemoryDiagnoser]
public class SecurityBindingStoreBenchmarks
{
    // Pre-built so the measured path is the draft+store seam, not the grant's doc construction.
    private static readonly VerbGrant Read = VerbGrant.Rules("tenant-scoped");

    private InMemorySecurityPolicyStore store = null!;
    private string id = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemorySecurityPolicyStore();
        using ParsedJsonDocument<SecurityBindingDocument> seed = SecurityBindingDocument.Draft("role", "operator", Read, VerbGrant.None, VerbGrant.None);
        using ParsedJsonDocument<SecurityBindingDocument> created = this.store.AddBindingAsync(seed.RootElement, "admin", default).AsTask().GetAwaiter().GetResult();
        this.id = created.RootElement.IdValue;
    }

    /// <summary>The binding update path: build the pooled draft, read the existing doc, rewrite + stamp, return the pooled result.</summary>
    [Benchmark]
    public void Update_FromDraft()
    {
        using ParsedJsonDocument<SecurityBindingDocument> draft = SecurityBindingDocument.Draft("role", "operator", Read, VerbGrant.None, VerbGrant.None);
        using ParsedJsonDocument<SecurityBindingDocument>? updated = this.store
            .UpdateBindingAsync(this.id, draft.RootElement, WorkflowEtag.None, "admin", default)
            .AsTask().GetAwaiter().GetResult();
    }
}
