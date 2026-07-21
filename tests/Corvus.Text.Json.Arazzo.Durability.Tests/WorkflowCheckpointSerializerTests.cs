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

        using var retryCounters = PooledUtf8Map<int>.Rent(1);
        retryCounters.Set("getPet", 2);
        var correlationTokens = new Dictionary<string, byte[]> { ["orderRef"] = Encoding.UTF8.GetBytes("abc-123") };
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(2);
        stepOutputs.Set("getPet", root.GetProperty("getPet"u8));
        stepOutputs.Set("listTags", root.GetProperty("listTags"u8));

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
        state.RetryCounters.TryGetValue("getPet", out int getPetRetry).ShouldBeTrue();
        getPetRetry.ShouldBe(2);
        state.CorrelationTokens["orderRef"].ShouldBe(Encoding.UTF8.GetBytes("abc-123"));
        state.Inputs.GetProperty("petId"u8).GetInt32().ShouldBe(7);
        state.StepOutputs.TryGetValue("getPet", out JsonElement getPetOutputs).ShouldBeTrue();
        getPetOutputs.GetProperty("status"u8).GetString().ShouldBe("available");
        state.StepOutputs.TryGetValue("listTags", out JsonElement listTagsOutputs).ShouldBeTrue();
        listTagsOutputs.GetArrayLength().ShouldBe(2);
        state.Outputs.ValueKind.ShouldBe(JsonValueKind.Undefined);

        // A checkpoint written without the updatedAt stamp (this one, and any persisted before the stamp existed)
        // deserializes with no UpdatedAt rather than a fabricated instant.
        state.UpdatedAt.ShouldBeNull();

        // A checkpoint written without a step journal (ADR 0050) deserializes to an empty journal, not null.
        state.StepJournal.ShouldBeEmpty();
        state.JournalTruncated.ShouldBeFalse();
    }

    [TestMethod]
    public void Round_trips_the_step_journal()
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);

        DateTimeOffset started = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var journal = new List<WorkflowStepJournalEntry>
        {
            new("getPet", WorkflowStepStatus.Succeeded, 1, started, started.AddMilliseconds(12)),
            new("adopt", WorkflowStepStatus.Faulted, 3, started.AddSeconds(1), started.AddSeconds(2)),
            new("notify", WorkflowStepStatus.Skipped, 1, started.AddSeconds(3), started.AddSeconds(3)),
        };

        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "petWorkflow",
            WorkflowRunStatus.Faulted,
            cursor: 2,
            CreatedAt,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            stepJournal: journal,
            journalTruncated: true);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);

        state.JournalTruncated.ShouldBeTrue();
        state.StepJournal.Count.ShouldBe(3);
        state.StepJournal[0].ShouldBe(new WorkflowStepJournalEntry("getPet", WorkflowStepStatus.Succeeded, 1, started, started.AddMilliseconds(12)));
        state.StepJournal[1].StepId.ShouldBe("adopt");
        state.StepJournal[1].Status.ShouldBe(WorkflowStepStatus.Faulted);
        state.StepJournal[1].Attempt.ShouldBe(3);
        state.StepJournal[2].Status.ShouldBe(WorkflowStepStatus.Skipped);
    }

    [TestMethod]
    public void Round_trips_the_updated_at_stamp_when_the_writer_provides_one()
    {
        DateTimeOffset updatedAt = CreatedAt.AddMinutes(5);

        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "wf",
            WorkflowRunStatus.Running,
            cursor: 1,
            CreatedAt,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            updatedAt: updatedAt);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);

        state.CreatedAt.ShouldBe(CreatedAt);
        state.UpdatedAt.ShouldBe(updatedAt);
    }

    [TestMethod]
    public void Serializes_the_final_outputs_of_a_completed_run()
    {
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{ "outputs": { "ok": true } }"""u8.ToArray());

        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "wf",
            WorkflowRunStatus.Completed,
            cursor: 9,
            CreatedAt,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            source.RootElement.GetProperty("outputs"u8));

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);

        state.Status.ShouldBe(WorkflowRunStatus.Completed);
        state.Outputs.GetProperty("ok"u8).GetBoolean().ShouldBeTrue();
        // Undefined inputs are omitted (not written as null), so they round-trip as Undefined.
        state.Inputs.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [TestMethod]
    public void Empty_registers_round_trip_as_empty()
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "wf",
            WorkflowRunStatus.Pending,
            cursor: 0,
            CreatedAt,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);

        state.RetryCounters.Count.ShouldBe(0);
        state.CorrelationTokens.ShouldBeEmpty();
        state.StepOutputs.Count.ShouldBe(0);
    }

    [TestMethod]
    public void Deserialize_of_a_minimal_checkpoint_defaults_missing_sections()
    {
        // A checkpoint that omits every optional section (forward/back-compat): each missing section defaults
        // to empty/undefined rather than throwing.
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(
            """{ "runId": "r", "workflowId": "w", "status": "Pending", "cursor": 0 }"""u8.ToArray());

        state.CreatedAt.ShouldBe(default);
        state.RetryCounters.Count.ShouldBe(0);
        state.CorrelationTokens.ShouldBeEmpty();
        state.StepOutputs.Count.ShouldBe(0);
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

    [TestMethod]
    public void Round_trips_a_persisted_pause_configuration()
    {
        // §18: a paused run persists its pause configuration so a claiming runner (a different process) applies
        // the same stops without re-supplying them — both the after-each-step flag and the breakpoint cursors.
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "wf",
            WorkflowRunStatus.Suspended,
            cursor: 2,
            CreatedAt,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            wait: WorkflowWait.Pause(),
            pause: new WorkflowPauseConfig(AfterEachStep: true, new HashSet<int> { 1, 3 }));

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);

        state.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Pause);
        state.Pause.ShouldNotBeNull();
        state.Pause!.Value.AfterEachStep.ShouldBeTrue();
        state.Pause.Value.BreakpointCursors.ShouldBe(new HashSet<int> { 1, 3 }, ignoreOrder: true);
    }

    [TestMethod]
    public void A_run_with_no_pause_configuration_omits_the_pause_property()
    {
        // A no-pause run writes nothing under "pause" and round-trips as an ordinary (unpaused) run, so an
        // ordinary run's checkpoint is byte-for-byte unaffected by the pause seam.
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "wf",
            WorkflowRunStatus.Running,
            cursor: 1,
            CreatedAt,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default);

        Encoding.UTF8.GetString(bytes).ShouldNotContain("pause");

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(bytes);
        state.Pause.ShouldBeNull();
    }
}