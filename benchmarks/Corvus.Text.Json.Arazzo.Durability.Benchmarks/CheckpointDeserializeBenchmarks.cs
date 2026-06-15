// <copyright file="CheckpointDeserializeBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the per-resume cost of <see cref="WorkflowCheckpointSerializer.Deserialize"/> — the production read of a
/// checkpoint into the run's resumable state — against the unavoidable parse-only floor. The delta is what the three
/// working dictionaries (retryCounters, correlationTokens, stepOutputs) plus the security-tag list cost to
/// materialize. They are the run's MUTABLE working state (the executor writes step outputs and retry counts back into
/// them as it advances, and they are re-serialized at the next checkpoint), so this delta is the irreducible floor of
/// a resume, not a removable record-document seam: a lazy/read-only view over the parsed document cannot be mutated,
/// and a copy-on-write scheme would re-allocate the same entries on first write.
/// </summary>
public class CheckpointDeserializeBenchmarks
{
    private byte[] checkpoint = null!;

    [GlobalSetup]
    public void Setup()
    {
        var retryCounters = new Dictionary<string, int> { ["step-a"] = 0, ["step-b"] = 2, ["step-c"] = 1 };
        var correlationTokens = new Dictionary<string, byte[]> { ["orders"] = [1, 2, 3, 4], ["shipping"] = [5, 6, 7, 8] };

        using ParsedJsonDocument<JsonElement> outputsDoc = ParsedJsonDocument<JsonElement>.Parse(
            """{"step-a":{"id":1,"ok":true},"step-b":{"id":2,"items":["x","y"]},"step-c":{"id":3}}"""u8.ToArray());
        var stepOutputs = new Dictionary<string, JsonElement>();
        foreach (JsonProperty<JsonElement> property in outputsDoc.RootElement.EnumerateObject())
        {
            stepOutputs[property.Name] = property.Value;
        }

        using ParsedJsonDocument<JsonElement> inputsDoc = ParsedJsonDocument<JsonElement>.Parse("""{"petId":42}"""u8.ToArray());

        this.checkpoint = WorkflowCheckpointSerializer.Serialize(
            new WorkflowRunId("run-0001"),
            "wf-orders",
            WorkflowRunStatus.Suspended,
            cursor: 3,
            createdAt: DateTimeOffset.UnixEpoch,
            retryCounters: retryCounters,
            correlationTokens: correlationTokens,
            inputs: inputsDoc.RootElement,
            stepOutputs: stepOutputs,
            outputs: default,
            wait: WorkflowWait.Timer(DateTimeOffset.UnixEpoch),
            fault: null,
            correlationId: "00-trace-01",
            tags: TagSet.FromTags(["nightly", "eu"]),
            securityTags: [new SecurityTag("tenant", "acme"), new SecurityTag("team", "payments")]);
    }

    /// <summary>The unavoidable floor: parse the checkpoint document (what any read must do).</summary>
    /// <returns>1 (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int ParseOnly_Floor()
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(this.checkpoint);
        return document.RootElement.ValueKind == JsonValueKind.Object ? 1 : 0;
    }

    /// <summary>Production: parse + materialize the run's three working dictionaries + security-tag list.</summary>
    /// <returns>The cursor (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Deserialize_Full()
    {
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(this.checkpoint);
        return state.Cursor;
    }
}