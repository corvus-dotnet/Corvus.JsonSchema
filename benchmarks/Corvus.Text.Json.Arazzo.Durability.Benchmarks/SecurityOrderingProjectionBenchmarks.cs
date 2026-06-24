// <copyright file="SecurityOrderingProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Allocation profile of the <c>GET /security/orderings</c> projection (design §14.2): the deployment's configured
/// <see cref="SecurityLabelOrderings"/> (a handful of ordered tag dimensions, each with a few ascending labels) is
/// projected into the <c>SecurityOrderingList</c> response body and materialised into the response workspace exactly as
/// the handler does. The source is genuine C# config strings (not a CTJ element), so the labels encode to UTF-8 once via
/// the implicit <c>string -&gt; JsonString.Source</c> conversion; only the projection MECHANISM differs between the
/// arms, isolating the closure-vs-context cost.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="PerItemClosure"/> — the naive shape: the list, each ordering and its label array are built through
/// capturing builder closures (an outer list closure + per-ordering closures + per-label-array closures). One
/// materialisation into the response workspace.</item>
/// <item><see cref="ContextThreaded"/> — the shipped MECHANISM: the whole body is a context-threaded
/// <c>SecurityOrderingList.Source&lt;TContext&gt;</c> (the generated <c>Build&lt;TContext&gt;</c> + static build methods,
/// the orderings/labels threaded in as ref contexts — no closure) materialised once.</item>
/// <item><see cref="EndToEnd_Handler"/> — the real handler path
/// (<see cref="ArazzoControlPlaneSecurityHandler.HandleListSecurityOrderingsAsync"/>) end-to-end: reads the policy's
/// orderings, builds the context-threaded body and materialises it via <c>Ok&lt;TContext&gt;</c> — the realised
/// per-request allocation a caller actually pays.</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
public class SecurityOrderingProjectionBenchmarks
{
    private SecurityLabelOrderings orderings = null!;
    private JsonWorkspace workspace = null!;
    private InMemorySecurityPolicyStore store = null!;
    private PersistentRowSecurityPolicy policy = null!;
    private ArazzoControlPlaneSecurityHandler handler = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A representative deployment taxonomy: four ordered dimensions, each with a few ascending labels.
        this.orderings = new SecurityLabelOrderings(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["classification"] = ["public", "internal", "confidential", "restricted"],
            ["sensitivity"] = ["low", "moderate", "high"],
            ["impact"] = ["minor", "major", "critical"],
            ["tier"] = ["bronze", "silver", "gold", "platinum"],
        });
        this.workspace = JsonWorkspace.Create();
        this.store = new InMemorySecurityPolicyStore();
        this.policy = new PersistentRowSecurityPolicy(this.store, orderings: this.orderings);
        this.handler = new ArazzoControlPlaneSecurityHandler(this.store, this.policy);
    }

    [GlobalCleanup]
    public void Cleanup() => this.workspace.Dispose();

    /// <summary>The naive shape: the list, each ordering and its label array are built through capturing closures; one materialisation.</summary>
    /// <returns>The ordering count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int PerItemClosure()
    {
        SecurityLabelOrderings cfg = this.orderings;
        Models.SecurityOrderingList.Source body = Models.SecurityOrderingList.Build((ref Models.SecurityOrderingList.Builder b) => b.Create(
            orderings: Models.SecurityOrderingList.SecurityOrderingArray.Build((ref Models.SecurityOrderingList.SecurityOrderingArray.Builder ab) =>
            {
                foreach (string dimension in cfg.Dimensions)
                {
                    cfg.TryGetOrdering(dimension, out IReadOnlyList<string> labels);
                    ab.AddItem(ClosureOrdering(dimension, labels));
                }
            })));
        return Models.SecurityOrderingList.CreateBuilder(this.workspace, body, 30).RootElement.Orderings.GetArrayLength();
    }

    /// <summary>The shipped MECHANISM: the whole body is a context-threaded <c>Source&lt;TContext&gt;</c> (no closure), materialised once.</summary>
    /// <returns>The ordering count.</returns>
    [Benchmark]
    public int ContextThreaded()
    {
        SecurityLabelOrderings cfg = this.orderings;
        Models.SecurityOrderingList.Source<SecurityLabelOrderings> body = Models.SecurityOrderingList.Build(
            in cfg,
            orderings: Models.SecurityOrderingList.SecurityOrderingArray.Build(in cfg, BuildOrderings));
        return Models.SecurityOrderingList.CreateBuilder(this.workspace, in body, 30).RootElement.Orderings.GetArrayLength();
    }

    /// <summary>The real handler path end-to-end (the realised per-request allocation).</summary>
    /// <returns>The ordering count.</returns>
    [Benchmark]
    public int EndToEnd_Handler()
    {
        ListSecurityOrderingsResult result = this.handler.HandleListSecurityOrderingsAsync(default, this.workspace).GetAwaiter().GetResult();
        return Models.SecurityOrderingList.From(result.Body).Orderings.GetArrayLength();
    }

    // ── naive closure projection ───────────────────────────────────────────────────────────────────────────────────

    private static Models.SecurityOrdering.Source ClosureOrdering(string dimension, IReadOnlyList<string> labels)
        => Models.SecurityOrdering.Build((ref Models.SecurityOrdering.Builder b) => b.Create(
            dimension: dimension,
            labels: Models.SecurityOrdering.JsonStringArray.Build((ref Models.SecurityOrdering.JsonStringArray.Builder ab) =>
            {
                foreach (string label in labels)
                {
                    ab.AddItem(label);
                }
            })));

    // ── shipped context-threaded projection (mirrors ArazzoControlPlaneSecurityHandler) ────────────────────────────

    private static void BuildOrderings(in SecurityLabelOrderings cfg, ref Models.SecurityOrderingList.SecurityOrderingArray.Builder array)
    {
        foreach (string dimension in cfg.Dimensions)
        {
            cfg.TryGetOrdering(dimension, out IReadOnlyList<string> labels);
            var ctx = new OrderingContext(dimension, labels);
            array.AddItem(Models.SecurityOrdering.Build(in ctx, BuildOrdering));
        }
    }

    private static void BuildOrdering(in OrderingContext ctx, ref Models.SecurityOrdering.Builder b)
        => b.Create(
            in ctx,
            dimension: ctx.Dimension,
            labels: Models.SecurityOrdering.JsonStringArray.Build(in ctx, BuildLabels));

    private static void BuildLabels(in OrderingContext ctx, ref Models.SecurityOrdering.JsonStringArray.Builder array)
    {
        foreach (string label in ctx.Labels)
        {
            array.AddItem(label);
        }
    }

    private readonly ref struct OrderingContext(string dimension, IReadOnlyList<string> labels)
    {
        public string Dimension { get; } = dimension;

        public IReadOnlyList<string> Labels { get; } = labels;
    }
}
