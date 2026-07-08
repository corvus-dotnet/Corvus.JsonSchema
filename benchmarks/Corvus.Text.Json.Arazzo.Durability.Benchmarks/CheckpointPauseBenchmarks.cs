// <copyright file="CheckpointPauseBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the §18 R1 debugger pause-config's checkpoint cost, proving two claims:
/// <list type="number">
/// <item>A NON-DEBUG run's checkpoint carries no <c>pause</c> object at all — the guarded serialize writes nothing —
/// so its deserialize allocates exactly as it did before R1: debug support has ~0 impact on the hot run-state path
/// (<see cref="Deserialize_NonDebug"/> is the baseline; <see cref="Deserialize_Debug_AfterEachStep"/> allocates the
/// same, no per-load set).</item>
/// <item>The common debug shape (single-step: <c>afterEachStep</c> with no breakpoints) shares the cached
/// <see cref="FrozenSet{T}.Empty"/> rather than minting a per-resume-load <see cref="HashSet{T}"/>, so
/// <see cref="Deserialize_Debug_AfterEachStep"/> allocates the same as the non-debug baseline — only a genuine
/// breakpoint list (<see cref="Deserialize_Debug_Breakpoints"/>) costs a set.</item>
/// </list>
/// </summary>
[MemoryDiagnoser]
public class CheckpointPauseBenchmarks
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private ParsedJsonDocument<JsonElement> inputsDoc = null!;
    private byte[] nonDebug = null!;
    private byte[] afterEachStep = null!;
    private byte[] withBreakpoints = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.inputsDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"email":"ada@example.com"}""").AsMemory());
        JsonElement inputs = this.inputsDoc.RootElement;
        this.nonDebug = SerializeCheckpoint(inputs, pause: null);
        this.afterEachStep = SerializeCheckpoint(inputs, new WorkflowPauseConfig(AfterEachStep: true, FrozenSet<int>.Empty));
        this.withBreakpoints = SerializeCheckpoint(inputs, new WorkflowPauseConfig(AfterEachStep: false, new HashSet<int> { 1, 3, 5 }));
    }

    [GlobalCleanup]
    public void Cleanup() => this.inputsDoc.Dispose();

    /// <summary>The hot path: a non-debug run's checkpoint has no pause object, so R1 adds nothing to its deserialize
    /// (no pause property, no set). The baseline every debug arm is compared against.</summary>
    /// <returns>The cursor (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Deserialize_NonDebug()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.nonDebug);
        return state.Cursor;
    }

    /// <summary>A single-step debug checkpoint (afterEachStep, no breakpoints): the fix shares the cached empty set,
    /// so its deserialize allocates the same as the non-debug baseline — no per-load HashSet.</summary>
    /// <returns>1/0/-1 (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Deserialize_Debug_AfterEachStep()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.afterEachStep);
        return state.Pause is { } p ? (p.AfterEachStep ? 1 : 0) : -1;
    }

    /// <summary>A checkpoint with real breakpoints: a HashSet is genuinely required to carry them — the only debug
    /// shape that allocates a set on deserialize (the delta over the baseline is that unavoidable set).</summary>
    /// <returns>The breakpoint count (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Deserialize_Debug_Breakpoints()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.withBreakpoints);
        return state.Pause is { } p ? p.BreakpointCursors.Count : -1;
    }

    private static byte[] SerializeCheckpoint(JsonElement inputs, WorkflowPauseConfig? pause)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "wf",
            WorkflowRunStatus.Running,
            cursor: 2,
            T0,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs,
            stepOutputs,
            outputs: default,
            pause: pause);
    }
}
