// <copyright file="WorkflowStateStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the allocation floor of the <see cref="IWorkflowWaitIndex.QueryAsync"/> keyset paging path (plan §11),
/// isolated over the in-memory reference store (no driver / no I/O noise): one keyset page of runs ordered by ascending
/// run id, on a corpus large enough that the page emits a continuation token. The seam carries that token; this is the
/// regression guard that the warm operator-list path stays small and CTJ-disciplined (no <c>System.Text.Json</c>).
/// </summary>
public class WorkflowStateStoreBenchmarks
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private InMemoryWorkflowStateStore store = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemoryWorkflowStateStore();

        // A realistic corpus so the keyset page emits a continuation token (limit 10 « 100 rows).
        for (int i = 0; i < 100; i++)
        {
            this.store.SaveAsync(
                $"run-{i:D3}",
                new byte[] { 1 },
                new WorkflowRunIndexEntry("wf", WorkflowRunStatus.Running, T0, T0),
                WorkflowEtag.None,
                default).AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>One keyset page of the run list — the warm operator-facing path the runs API hits. The page emits a
    /// continuation token (100 rows, limit 10); ordered keyset, no STJ.</summary>
    /// <returns>The page size (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Query_Page()
    {
        IWorkflowWaitIndex index = this.store;
        using WorkflowRunPage page = index.QueryAsync(new WorkflowQuery(Limit: 10), default).AsTask().GetAwaiter().GetResult();
        return page.Runs.Count;
    }
}