// <copyright file="CheckpointDeserializeBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Decomposes the per-resume cost of <see cref="WorkflowCheckpointSerializer.Deserialize"/> by checkpoint shape, to
/// attribute the allocation across the three working dictionaries (retryCounters/correlationTokens/stepOutputs), the
/// per-token base64 byte arrays, and the security-tag list — relative to the unavoidable parse-only floor. The model
/// is deterministic replay: on resume the executor re-enters from the top and reads every completed step's output and
/// retry count back out of these dictionaries, so they are mutable working state, not a removable record seam. The
/// shapes isolate what each component contributes so optimization options can be judged on data.
/// </summary>
public class CheckpointDeserializeBenchmarks
{
    private byte[] full = null!;        // 3 retry + 2 correlation + 3 stepOutputs + 2 securityTags
    private byte[] typical = null!;     // 0 retry + 0 correlation + 3 stepOutputs + 0 securityTags (a common resume)
    private byte[] empty = null!;       // 0 / 0 / 0 / 0 — only the scalars
    private byte[] large = null!;       // 40 retry + 40 stepOutputs — a long workflow (dict-growth re-alloc case)
    private byte[] xlarge = null!;      // 200 retry + 200 stepOutputs — scaling baseline for the working-state spike

    [GlobalSetup]
    public void Setup()
    {
        using ParsedJsonDocument<JsonElement> outputsDoc = ParsedJsonDocument<JsonElement>.Parse(
            """{"step-a":{"id":1,"ok":true},"step-b":{"id":2,"items":["x","y"]},"step-c":{"id":3}}"""u8.ToArray());
        var stepOutputs = new Dictionary<string, JsonElement>();
        foreach (JsonProperty<JsonElement> property in outputsDoc.RootElement.EnumerateObject())
        {
            stepOutputs[property.Name] = property.Value;
        }

        var noOutputs = new Dictionary<string, JsonElement>();
        using ParsedJsonDocument<JsonElement> inputsDoc = ParsedJsonDocument<JsonElement>.Parse("""{"petId":42}"""u8.ToArray());

        this.full = Build(
            new() { ["step-a"] = 0, ["step-b"] = 2, ["step-c"] = 1 },
            new() { ["orders"] = [1, 2, 3, 4], ["shipping"] = [5, 6, 7, 8] },
            stepOutputs,
            inputsDoc.RootElement,
            SecurityTagSet.FromTags([new SecurityTag("tenant", "acme"), new SecurityTag("team", "payments")]));

        this.typical = Build([], [], stepOutputs, inputsDoc.RootElement, default);
        this.empty = Build([], [], noOutputs, inputsDoc.RootElement, default);

        var manyRetries = new Dictionary<string, int>();
        var manyOutputBuilder = new System.Text.StringBuilder("{");
        for (int i = 0; i < 40; i++)
        {
            manyRetries[$"step-{i}"] = i % 3;
            manyOutputBuilder.Append(i == 0 ? "" : ",").Append($"\"step-{i}\":{{\"id\":{i}}}");
        }

        manyOutputBuilder.Append('}');
        using ParsedJsonDocument<JsonElement> manyOutputsDoc = ParsedJsonDocument<JsonElement>.Parse(manyOutputBuilder.ToString().AsMemory());
        var manyOutputs = new Dictionary<string, JsonElement>();
        foreach (JsonProperty<JsonElement> property in manyOutputsDoc.RootElement.EnumerateObject())
        {
            manyOutputs[property.Name] = property.Value;
        }

        this.large = Build(manyRetries, [], manyOutputs, inputsDoc.RootElement, default);

        var xlRetries = new Dictionary<string, int>();
        var xlOutputBuilder = new System.Text.StringBuilder("{");
        for (int i = 0; i < 200; i++)
        {
            xlRetries[$"step-{i}"] = i % 3;
            xlOutputBuilder.Append(i == 0 ? "" : ",").Append($"\"step-{i}\":{{\"id\":{i}}}");
        }

        xlOutputBuilder.Append('}');
        using ParsedJsonDocument<JsonElement> xlOutputsDoc = ParsedJsonDocument<JsonElement>.Parse(xlOutputBuilder.ToString().AsMemory());
        var xlOutputs = new Dictionary<string, JsonElement>();
        foreach (JsonProperty<JsonElement> property in xlOutputsDoc.RootElement.EnumerateObject())
        {
            xlOutputs[property.Name] = property.Value;
        }

        this.xlarge = Build(xlRetries, [], xlOutputs, inputsDoc.RootElement, default);
    }

    /// <summary>The unavoidable floor: parse the full checkpoint document (what any read must do).</summary>
    /// <returns>1 (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int ParseOnly_Floor()
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(this.full);
        return document.RootElement.ValueKind == JsonValueKind.Object ? 1 : 0;
    }

    /// <summary>Production deserialize of the full checkpoint (all three dicts populated + security tags).</summary>
    /// <returns>The cursor.</returns>
    [Benchmark]
    public int Deserialize_Full()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.full);
        return state.Cursor;
    }

    /// <summary>A common resume: only stepOutputs populated (no retries, no correlation, no security tags).</summary>
    /// <returns>The cursor.</returns>
    [Benchmark]
    public int Deserialize_Typical()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.typical);
        return state.Cursor;
    }

    /// <summary>The empty-state lower bound: parse + three empty dictionaries.</summary>
    /// <returns>The cursor.</returns>
    [Benchmark]
    public int Deserialize_Empty()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.empty);
        return state.Cursor;
    }

    /// <summary>A long workflow (40 retries + 40 stepOutputs): exercises dictionary growth re-allocations.</summary>
    /// <returns>The cursor.</returns>
    [Benchmark]
    public int Deserialize_Large()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.large);
        return state.Cursor;
    }

    /// <summary>A very long workflow (200 retries + 200 stepOutputs): the scaling baseline for the working-state materialization spike.</summary>
    /// <returns>The cursor.</returns>
    [Benchmark]
    public int Deserialize_XLarge()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.xlarge);
        return state.Cursor;
    }

    private static byte[] Build(
        Dictionary<string, int> retryCounters,
        Dictionary<string, byte[]> correlationTokens,
        Dictionary<string, JsonElement> stepOutputs,
        JsonElement inputs,
        SecurityTagSet securityTags)
    {
        // Setup-time fixture construction (not the measured path): mirror the production working-state maps.
        using var retryMap = PooledUtf8Map<int>.Rent(retryCounters.Count);
        foreach (KeyValuePair<string, int> counter in retryCounters)
        {
            retryMap.Set(counter.Key, counter.Value);
        }

        using var stepMap = PooledUtf8Map<JsonElement>.Rent(stepOutputs.Count);
        foreach (KeyValuePair<string, JsonElement> step in stepOutputs)
        {
            stepMap.Set(step.Key, step.Value);
        }

        return WorkflowCheckpointSerializer.Serialize(
            new WorkflowRunId("run-0001"),
            "wf-orders",
            WorkflowRunStatus.Suspended,
            cursor: 3,
            createdAt: DateTimeOffset.UnixEpoch,
            retryCounters: retryMap,
            correlationTokens: correlationTokens,
            inputs: inputs,
            stepOutputs: stepMap,
            outputs: default,
            wait: WorkflowWait.Timer(DateTimeOffset.UnixEpoch),
            fault: null,
            correlationId: "00-trace-01",
            tags: TagSet.FromTags(["nightly", "eu"]),
            securityTags: securityTags);
    }
}