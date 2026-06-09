// <copyright file="WorkflowCheckpointSerializerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the checkpoint serializer: a full round-trip of the run's products and scalars, the
/// undefined-inputs and completed-run (final outputs) edge cases, and the base64 correlation register.
/// </summary>
[TestClass]
public sealed class WorkflowCheckpointSerializerTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [TestMethod]
    public void Round_trips_a_running_checkpoint()
    {
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{ "inputs": { "petId": 7 }, "getPet": { "status": "available" }, "listTags": [ "a", "b" ] }"""u8.ToArray());
        JsonElement root = source.RootElement;

        var retryCounters = new Dictionary<string, int> { ["getPet"] = 2 };
        var correlationTokens = new Dictionary<string, byte[]> { ["orderRef"] = Encoding.UTF8.GetBytes("abc-123") };
        var stepOutputs = new Dictionary<string, JsonElement>
        {
            ["getPet"] = root.GetProperty("getPet"u8),
            ["listTags"] = root.GetProperty("listTags"u8),
        };

        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "petWorkflow",
            WorkflowRunStatus.Running,
            cursor: 3,
            CreatedAt,
            retryCounters,
            correlationTokens,
            root.GetProperty("inputs"u8),
            stepOutputs,
            outputs: default);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);

        state.RunId.ShouldBe(new WorkflowRunId("run-1"));
        state.WorkflowId.ShouldBe("petWorkflow");
        state.Status.ShouldBe(WorkflowRunStatus.Running);
        state.Cursor.ShouldBe(3);
        state.CreatedAt.ShouldBe(CreatedAt);
        state.RetryCounters["getPet"].ShouldBe(2);
        state.CorrelationTokens["orderRef"].ShouldBe(Encoding.UTF8.GetBytes("abc-123"));
        state.Inputs.GetProperty("petId"u8).GetInt32().ShouldBe(7);
        state.StepOutputs["getPet"].GetProperty("status"u8).GetString().ShouldBe("available");
        state.StepOutputs["listTags"].GetArrayLength().ShouldBe(2);
        state.Outputs.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [TestMethod]
    public void Serializes_the_final_outputs_of_a_completed_run()
    {
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{ "outputs": { "ok": true } }"""u8.ToArray());

        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "wf",
            WorkflowRunStatus.Completed,
            cursor: 9,
            CreatedAt,
            new Dictionary<string, int>(),
            new Dictionary<string, byte[]>(),
            inputs: default,
            new Dictionary<string, JsonElement>(),
            source.RootElement.GetProperty("outputs"u8));

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);

        state.Status.ShouldBe(WorkflowRunStatus.Completed);
        state.Outputs.GetProperty("ok"u8).GetBoolean().ShouldBeTrue();
        // Undefined inputs serialise as a JSON null, so they round-trip as a Null element, not Undefined.
        state.Inputs.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [TestMethod]
    public void Empty_registers_round_trip_as_empty()
    {
        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "wf",
            WorkflowRunStatus.Pending,
            cursor: 0,
            CreatedAt,
            new Dictionary<string, int>(),
            new Dictionary<string, byte[]>(),
            inputs: default,
            new Dictionary<string, JsonElement>(),
            outputs: default);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);

        state.RetryCounters.ShouldBeEmpty();
        state.CorrelationTokens.ShouldBeEmpty();
        state.StepOutputs.ShouldBeEmpty();
    }
}