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

    [TestMethod]
    public void Deserialize_of_a_minimal_checkpoint_defaults_missing_sections()
    {
        // A checkpoint that omits every optional section (forward/back-compat): each missing section defaults
        // to empty/undefined rather than throwing.
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(
            """{ "runId": "r", "workflowId": "w", "status": "Pending", "cursor": 0 }"""u8.ToArray());

        state.CreatedAt.ShouldBe(default);
        state.RetryCounters.ShouldBeEmpty();
        state.CorrelationTokens.ShouldBeEmpty();
        state.StepOutputs.ShouldBeEmpty();
        state.Inputs.ValueKind.ShouldBe(JsonValueKind.Undefined);
        state.Outputs.ValueKind.ShouldBe(JsonValueKind.Undefined);
        state.Wait.ShouldBeNull();
        state.Fault.ShouldBeNull();
    }

    [TestMethod]
    public void Deserialize_tolerates_explicit_null_string_fields()
    {
        // Defensive: null string fields fall back to their defaults rather than producing null state, and a
        // wait with a null kind falls back to a timer.
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(
            """{ "runId": null, "workflowId": null, "status": null, "cursor": 0, "wait": { "kind": null, "dueAt": "2026-01-01T00:00:00+00:00" } }"""u8.ToArray());

        state.RunId.ShouldBe(new WorkflowRunId(string.Empty));
        state.WorkflowId.ShouldBe(string.Empty);
        state.Status.ShouldBe(WorkflowRunStatus.Pending);
        state.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Timer);
    }

    [TestMethod]
    public void Deserialize_tolerates_null_message_wait_and_fault_fields()
    {
        // Defensive: a message wait with a null channel and a fault with null stepId/error fall back to empty
        // strings rather than nulls.
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(
            """{ "runId": "r", "workflowId": "w", "status": "Suspended", "cursor": 0, "wait": { "kind": "Message", "channel": null }, "fault": { "stepId": null, "attempt": 0, "error": null, "at": "2026-01-01T00:00:00+00:00" } }"""u8.ToArray());

        state.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Message);
        state.Wait.Value.Channel.ShouldBe(string.Empty);
        state.Fault!.Value.StepId.ShouldBe(string.Empty);
        state.Fault.Value.Error.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void Deserialize_of_a_non_checkpoint_document_throws_and_disposes()
    {
        // Valid JSON, but not a checkpoint (no runId): the parse succeeds, a property access fails, and the
        // owned parsed document is disposed before the exception propagates.
        Should.Throw<Exception>(() => WorkflowCheckpointSerializer.Deserialize("{}"u8.ToArray()));
    }
}