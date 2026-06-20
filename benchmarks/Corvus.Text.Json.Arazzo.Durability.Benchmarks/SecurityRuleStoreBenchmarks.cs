// <copyright file="SecurityRuleStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the two ways the rule seam (<see cref="ISecurityPolicyStore.UpdateRuleAsync"/>) receives the operator-supplied
/// content as a draft <see cref="SecurityRuleDocument"/> the store completes (Tier-2 record-seam elimination), end-to-end
/// over the in-memory reference store (no driver / no I/O noise):
/// <list type="bullet">
/// <item><see cref="Update_FromRequest"/> — the warm HTTP path: the request body is already a parsed Corvus.Text.Json
/// value, carried straight to the store with <c>From</c> (a free re-wrap, no per-field string realisation). The only
/// allocations are the irreducible ones: the rewritten persisted document and the pooled documents read/returned.</item>
/// <item><see cref="Update_FromDraft"/> — the cold programmatic path: a caller that holds only the content
/// <em>strings</em> (e.g. <see cref="SecurityBootstrap"/>) builds a draft with <see cref="SecurityRuleDocument.Draft"/>.
/// The delta over the warm arm is exactly that pooled draft document (built through a pooled scratch buffer and disposed,
/// never a detached <c>ParseValue</c>) — the price of carrying a JSON value when you only had strings.</item>
/// </list>
/// Both stamp the server fields (etag/last-updated) and write the content bytes-to-bytes; the benchmark is the regression
/// guard that the warm path stays free of per-field strings and the cold path stays pooled.
/// </summary>
[MemoryDiagnoser]
public class SecurityRuleStoreBenchmarks
{
    private const string Expression = "team == $claim.team";
    private const string Description = "Team scope.";

    private InMemorySecurityPolicyStore store = null!;

    // The request body the warm path carries via From() — pre-built once (the HTTP framework already parsed it), so the
    // benchmark measures the store path, not the parse. Held as a pooled document, disposed in cleanup.
    private ParsedJsonDocument<SecurityRuleDocument> requestBody = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemorySecurityPolicyStore();
        using (ParsedJsonDocument<SecurityRuleDocument> seed = SecurityRuleDocument.Draft(Expression, Description))
        {
            this.store.AddRuleAsync("r", seed.RootElement, "admin", default).AsTask().GetAwaiter().GetResult().Dispose();
        }

        this.requestBody = SecurityRuleDocument.Draft(Expression, Description);
    }

    [GlobalCleanup]
    public void Cleanup() => this.requestBody.Dispose();

    /// <summary>The warm HTTP path: carry the already-parsed request body to the store with a free <c>From</c> re-wrap.</summary>
    [Benchmark(Baseline = true)]
    public void Update_FromRequest()
    {
        using ParsedJsonDocument<SecurityRuleDocument>? updated = this.store
            .UpdateRuleAsync("r", SecurityRuleDocument.From(this.requestBody.RootElement), WorkflowEtag.None, "admin", default)
            .AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The cold programmatic path: a caller holding only content strings builds a pooled draft, then updates.</summary>
    [Benchmark]
    public void Update_FromDraft()
    {
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft(Expression, Description);
        using ParsedJsonDocument<SecurityRuleDocument>? updated = this.store
            .UpdateRuleAsync("r", draft.RootElement, WorkflowEtag.None, "admin", default)
            .AsTask().GetAwaiter().GetResult();
    }
}
