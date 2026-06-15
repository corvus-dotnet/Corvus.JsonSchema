// <copyright file="CancelRewriteBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures producing a cancelled run's checkpoint bytes (status -> Cancelled, wait cleared, everything else
/// preserved): the old way — full <see cref="WorkflowCheckpointSerializer.Deserialize"/> (which materializes the
/// retry/correlation/step-output dictionaries) then <see cref="WorkflowCheckpointSerializer.Serialize"/> from them —
/// versus the new <see cref="WorkflowCheckpointSerializer.RewriteStatus"/>, which copies every preserved property's
/// raw bytes verbatim and never builds a dictionary. The win scales with the run's working-state size.
/// </summary>
public class CancelRewriteBenchmark
{
    private byte[] full = null!;    // 3 retry + 2 correlation + 3 stepOutputs
    private byte[] large = null!;   // 40 retry + 40 stepOutputs

    [GlobalSetup]
    public void Setup()
    {
        using ParsedJsonDocument<JsonElement> outputsDoc = ParsedJsonDocument<JsonElement>.Parse(
            """{"step-a":{"id":1},"step-b":{"id":2},"step-c":{"id":3}}"""u8.ToArray());
        var stepOutputs = new Dictionary<string, JsonElement>();
        foreach (JsonProperty<JsonElement> p in outputsDoc.RootElement.EnumerateObject())
        {
            stepOutputs[p.Name] = p.Value;
        }

        using ParsedJsonDocument<JsonElement> inputsDoc = ParsedJsonDocument<JsonElement>.Parse("""{"petId":42}"""u8.ToArray());
        this.full = Build(
            new() { ["step-a"] = 0, ["step-b"] = 2, ["step-c"] = 1 },
            new() { ["orders"] = [1, 2, 3, 4], ["shipping"] = [5, 6, 7, 8] },
            stepOutputs,
            inputsDoc.RootElement);

        var manyRetries = new Dictionary<string, int>();
        var manyBuilder = new System.Text.StringBuilder("{");
        for (int i = 0; i < 40; i++)
        {
            manyRetries[$"step-{i}"] = i % 3;
            manyBuilder.Append(i == 0 ? "" : ",").Append($"\"step-{i}\":{{\"id\":{i}}}");
        }

        manyBuilder.Append('}');
        using ParsedJsonDocument<JsonElement> manyDoc = ParsedJsonDocument<JsonElement>.Parse(manyBuilder.ToString().AsMemory());
        var manyOutputs = new Dictionary<string, JsonElement>();
        foreach (JsonProperty<JsonElement> p in manyDoc.RootElement.EnumerateObject())
        {
            manyOutputs[p.Name] = p.Value;
        }

        this.large = Build(manyRetries, [], manyOutputs, inputsDoc.RootElement);
    }

    /// <summary>Old: deserialize (materializing the three dictionaries) then re-serialize as cancelled.</summary>
    /// <returns>The cancelled checkpoint length.</returns>
    [Benchmark(Baseline = true)]
    public int Old_Full_DeserializeReserialize() => DeserializeReserialize(this.full);

    /// <summary>New: rewrite the status verbatim, no dictionaries.</summary>
    /// <returns>The cancelled checkpoint length.</returns>
    [Benchmark]
    public int New_Full_Rewrite() => WorkflowCheckpointSerializer.RewriteStatus(this.full, WorkflowRunStatus.Cancelled, dropWait: true).Length;

    /// <summary>Old, long workflow (40 steps).</summary>
    /// <returns>The cancelled checkpoint length.</returns>
    [Benchmark]
    public int Old_Large_DeserializeReserialize() => DeserializeReserialize(this.large);

    /// <summary>New, long workflow (40 steps).</summary>
    /// <returns>The cancelled checkpoint length.</returns>
    [Benchmark]
    public int New_Large_Rewrite() => WorkflowCheckpointSerializer.RewriteStatus(this.large, WorkflowRunStatus.Cancelled, dropWait: true).Length;

    private static int DeserializeReserialize(byte[] checkpoint)
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(checkpoint);
        return WorkflowCheckpointSerializer.Serialize(
            state.RunId,
            state.WorkflowId,
            WorkflowRunStatus.Cancelled,
            state.Cursor,
            state.CreatedAt,
            state.RetryCounters,
            state.CorrelationTokens,
            state.Inputs,
            state.StepOutputs,
            state.Outputs,
            wait: null,
            fault: state.Fault,
            correlationId: state.CorrelationId,
            tags: state.Tags,
            securityTags: state.SecurityTags).Length;
    }

    private static byte[] Build(
        Dictionary<string, int> retryCounters,
        Dictionary<string, byte[]> correlationTokens,
        Dictionary<string, JsonElement> stepOutputs,
        JsonElement inputs)
        => WorkflowCheckpointSerializer.Serialize(
            new WorkflowRunId("run-0001"),
            "wf-orders",
            WorkflowRunStatus.Suspended,
            cursor: 3,
            createdAt: DateTimeOffset.UnixEpoch,
            retryCounters: retryCounters,
            correlationTokens: correlationTokens,
            inputs: inputs,
            stepOutputs: stepOutputs,
            outputs: default,
            wait: WorkflowWait.Timer(DateTimeOffset.UnixEpoch),
            fault: null,
            correlationId: "00-trace-01",
            tags: TagSet.FromTags(["nightly", "eu"]),
            securityTags: [new SecurityTag("tenant", "acme")]);
}