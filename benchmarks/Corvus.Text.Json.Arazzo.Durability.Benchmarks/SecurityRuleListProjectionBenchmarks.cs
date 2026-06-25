// <copyright file="SecurityRuleListProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// The security-rule LIST projection (<c>GET /security/rules</c>): the handler projects the store's
/// <see cref="SecurityRuleDocument"/>s into the <c>SecurityRuleList</c> response body and materialises it via
/// <c>…Result.Ok(body, workspace)</c>. This benchmark uses the <b>production-faithful</b> lifecycle — a workspace is
/// rented from the thread-local cache and disposed <i>per op</i>, exactly as the request pipeline does — so the pooled
/// arena (<c>JsonWorkspace</c> doc-index array + <c>PooledByteBufferWriter</c> write buffer) is returned to the pool
/// each time and "Allocated" reports steady-state per-request GC, not the held-arena working set.
/// </summary>
/// <remarks>
/// Both arms project allocation-clean and roughly equal: pagination does <b>not</b> reduce steady-state GC (the pool
/// recycles the buffer whatever its size). Paging's win is structural and lives in the <i>size</i> of what is produced,
/// not the GC: the response body bytes (and therefore the transfer and the browser render) are bounded to one page
/// (<see cref="PageSize"/> rows) instead of growing O(total rules), and the live working set per in-flight request is
/// likewise bounded — which is what matters for peak memory under concurrency and for avoiding unbounded/LOH blow-up at
/// scale. <see cref="Unpaged_All"/> vs <see cref="Paged_FirstPage"/> over the rule count confirms there is no per-page
/// GC cost and no allocation regression.
/// </remarks>
[MemoryDiagnoser]
public class SecurityRuleListProjectionBenchmarks
{
    // The handler's default page size (ISecurityPolicyStore's in-memory pager; mirrored here as that constant is internal).
    private const int PageSize = 50;

    [Params(50, 500)]
    public int RuleCount { get; set; }

    private ParsedJsonDocument<SecurityRuleDocument>[] documents = null!;
    private SecurityRuleDocument[] rules = null!;
    private SecurityRuleDocument[] pageRules = null!;
    private ReadOnlyMemory<byte> pageTokenUtf8;

    [GlobalSetup]
    public void Setup()
    {
        this.documents = new ParsedJsonDocument<SecurityRuleDocument>[this.RuleCount];
        this.rules = new SecurityRuleDocument[this.RuleCount];
        for (int i = 0; i < this.RuleCount; i++)
        {
            using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft($"tenant == '{i}'", "A representative reach scope.");
            byte[] created = SecurityPolicySerialization.SerializeNewRule($"scope-{i:D5}", draft.RootElement, "alice", DateTimeOffset.UnixEpoch, new WorkflowEtag($"etag-{i}"));
            this.documents[i] = ParsedJsonDocument<SecurityRuleDocument>.Parse(created.AsMemory());
            this.rules[i] = this.documents[i].RootElement;
        }

        // The first page is a separate array (a SecurityRuleDocument[] projects to IReadOnlyList box-free), so neither arm
        // allocates an enumerable wrapper in the measured op.
        this.pageRules = this.rules[..Math.Min(PageSize, this.RuleCount)];

        // When more than one page exists, the first page carries a continuation token for its last row (names are the
        // zero-padded scope-NNNNN, so ordinal order is numeric order; the boundary row is scope-{PageSize - 1}). The
        // store emits this as pooled UTF-8; the projection just references it, so it is precomputed (not measured here).
        if (this.RuleCount > PageSize)
        {
            string boundaryName = $"scope-{PageSize - 1:D5}";
            byte[] token = new byte[SecurityRuleContinuationToken.GetMaxEncodedLength(boundaryName)];
            int written = SecurityRuleContinuationToken.EncodeToUtf8(boundaryName, token);
            this.pageTokenUtf8 = token.AsMemory(0, written);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (ParsedJsonDocument<SecurityRuleDocument> d in this.documents)
        {
            d.Dispose();
        }
    }

    /// <summary>The unpaged list projection: every stored rule materialised into one response body. The response bytes
    /// grow O(total) even though the per-request pooled arena is returned each op.</summary>
    /// <returns>The projected rule count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Unpaged_All()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<SecurityRuleDocument> list = this.rules;
        Models.SecurityRuleList.Source<IReadOnlyList<SecurityRuleDocument>> body = Models.SecurityRuleList.Build(
            in list,
            rules: Models.SecurityRuleList.SecurityRuleSummaryArray.Build(in list, BuildRuleSummaries));
        return Models.SecurityRuleList.CreateBuilder(workspace, in body, 30).RootElement.Rules.GetArrayLength();
    }

    /// <summary>The paged list projection: only the first keyset page (<see cref="PageSize"/> rows) plus the continuation
    /// token — the response bytes stay bounded as the total grows.</summary>
    /// <returns>The projected rule count (defeats dead-code elimination).</returns>
    [Benchmark]
    public int Paged_FirstPage()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<SecurityRuleDocument> page = this.pageRules;
        Models.SecurityRuleList.Source<IReadOnlyList<SecurityRuleDocument>> body = Models.SecurityRuleList.Build(
            in page,
            rules: Models.SecurityRuleList.SecurityRuleSummaryArray.Build(in page, BuildRuleSummaries),
            nextPageToken: this.pageTokenUtf8.IsEmpty ? default : (Models.JsonString.Source)this.pageTokenUtf8.Span);
        return Models.SecurityRuleList.CreateBuilder(workspace, in body, 30).RootElement.Rules.GetArrayLength();
    }

    // The handler's closure-free list build (the rule list threaded as the context; each item a congruent From-wrap).
    private static void BuildRuleSummaries(in IReadOnlyList<SecurityRuleDocument> rules, ref Models.SecurityRuleList.SecurityRuleSummaryArray.Builder array)
    {
        foreach (SecurityRuleDocument r in rules)
        {
            array.AddItem(Models.SecurityRuleSummary.From(r));
        }
    }
}
