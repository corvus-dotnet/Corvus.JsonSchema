// <copyright file="WorkflowStateStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the allocation floor of the <see cref="IWorkflowWaitIndex.QueryAsync"/> keyset paging path (plan §11),
/// isolated over the in-memory reference store (no driver / no I/O noise): one keyset page of runs ordered by ascending
/// run id, on a corpus large enough that the page emits a continuation token. The seam carries that token; this is the
/// regression guard that the warm operator-list path stays small and CTJ-disciplined (no <c>System.Text.Json</c>).
/// Also measures the <see cref="IWorkflowStateStore.SaveAsync"/> checkpoint <b>bind</b> on the run-state hot path: the
/// opaque checkpoint arrives as a <see cref="ReadOnlyMemory{T}"/>, and the memory/stream backends (SqlServer/Postgres/
/// MySql/Redis) bind it without the per-save GC array that <c>checkpointUtf8.ToArray()</c> used to mint.
/// </summary>
[MemoryDiagnoser]
public class WorkflowStateStoreBenchmarks
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    // A realistic checkpoint payload (~1 KB) — the document the executor serializes and the store persists opaque, saved
    // on every run step (the hot path).
    private static readonly byte[] Checkpoint = CreateCheckpoint();

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

    /// <summary>The checkpoint bind as the memory/stream backends realized it BEFORE this row: one owned GC array per save
    /// (<c>checkpointUtf8.ToArray()</c>), on the run-state hot path.</summary>
    /// <returns>The checkpoint length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Checkpoint_Bind_ToArray()
        => Checkpoint.AsMemory().ToArray().Length;

    /// <summary>The same bind as the memory/stream backends realize it now: a pooled <see cref="ReadOnlyMemoryStream"/> over
    /// the checkpoint memory the SqlClient/etc. driver streams as a VARBINARY parameter — no per-save GC array.</summary>
    /// <returns>The checkpoint length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Checkpoint_Bind_StreamRent()
    {
        using ReadOnlyMemoryStream stream = ReadOnlyMemoryStream.Rent(Checkpoint.AsMemory());
        return (int)stream.Length;
    }

    private static byte[] CreateCheckpoint()
    {
        var bytes = new byte[1024];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)('a' + (i % 26));
        }

        return bytes;
    }
}