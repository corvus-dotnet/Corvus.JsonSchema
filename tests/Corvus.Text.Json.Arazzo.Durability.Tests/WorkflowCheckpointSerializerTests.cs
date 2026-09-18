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
    public void The_budget_facts_are_read_from_the_bytes_without_parsing_the_document()
    {
        // ADR 0068: the coordinator reads these on every save, so they come from a forward scan of the same bytes the
        // caller projected, not from a materialized run.
        var budget = new ExecutionBudget(3, TimeSpan.FromMinutes(30), 2, TimeSpan.FromSeconds(5));
        byte[] bytes = BudgetCheckpoint(journalEntries: 2, budget: budget, truncated: true, fault: new WorkflowFault("s2", 2, ExecutionBudgetFault.Fuel, CreatedAt));

        WorkflowCheckpointSerializer.TryReadBudgetFacts(bytes, out CheckpointBudgetFacts facts).ShouldBeTrue();
        facts.Budget.ShouldBe(budget);
        facts.JournalCount.ShouldBe(2);
        facts.JournalTruncated.ShouldBeTrue();
        facts.BudgetFaulted.ShouldBeTrue();

        // A run faulted on a step error is not budget-faulted, and a checkpoint from before budgets carries none.
        WorkflowCheckpointSerializer.TryReadBudgetFacts(BudgetCheckpoint(journalEntries: 1, budget: budget, fault: new WorkflowFault("s1", 1, "boom", CreatedAt)), out facts).ShouldBeTrue();
        facts.BudgetFaulted.ShouldBeFalse();
        WorkflowCheckpointSerializer.TryReadBudgetFacts(BudgetCheckpoint(journalEntries: 0, budget: null), out facts).ShouldBeTrue();
        facts.ShouldBe(new CheckpointBudgetFacts(null, 0, false, false));

        WorkflowCheckpointSerializer.TryReadBudgetFacts(new byte[] { 1, 2, 3 }, out _).ShouldBeFalse();
        WorkflowCheckpointSerializer.TryReadBudgetFacts("[]"u8.ToArray(), out _).ShouldBeFalse();
    }

    [TestMethod]
    public void One_projection_yields_the_index_the_environment_the_sequence_and_the_budget_facts()
    {
        // The checkpoint surfaces read a posted body exactly once; everything the save needs comes from that parse.
        var budget = new ExecutionBudget(3, TimeSpan.FromMinutes(30), 2, TimeSpan.FromSeconds(5));
        byte[] bytes = BudgetCheckpoint(journalEntries: 2, budget: budget, sequence: 7, truncated: true, fault: new WorkflowFault("s2", 2, ExecutionBudgetFault.Fuel, CreatedAt));

        WorkflowCheckpointSerializer.TryProject(bytes, out CheckpointProjection projection).ShouldBeTrue();
        projection.Index.ShouldBe(WorkflowCheckpointSerializer.ProjectIndex(bytes));
        projection.Index.ErrorType.ShouldBe(ExecutionBudgetFault.Fuel);
        projection.Environment.ShouldBe("development");
        projection.Sequence.ShouldBe(7);
        WorkflowCheckpointSerializer.TryReadBudgetFacts(bytes, out CheckpointBudgetFacts scanned).ShouldBeTrue();
        projection.Facts.ShouldBe(scanned);

        // Absence of a sequence is reported, not folded into zero; malformed bytes do not project.
        WorkflowCheckpointSerializer.TryProject("{\"runId\":\"run-1\",\"workflowId\":\"wf\",\"status\":\"Running\",\"cursor\":0}"u8.ToArray(), out projection).ShouldBeTrue();
        projection.Sequence.ShouldBeNull();
        projection.Facts.ShouldBe(default(CheckpointBudgetFacts));
        WorkflowCheckpointSerializer.TryProject(new byte[] { 1, 2, 3 }, out _).ShouldBeFalse();
    }

    [TestMethod]
    public void A_budget_fault_is_recorded_at_the_last_journaled_step_and_the_run_is_neither_waiting_nor_resumable()
    {
        var budget = new ExecutionBudget(2, TimeSpan.FromMinutes(30), 2, TimeSpan.FromSeconds(5));
        byte[] source = BudgetCheckpoint(journalEntries: 3, budget: budget, sequence: 4, wait: WorkflowWait.Timer(CreatedAt.AddMinutes(5)), resumeRequestedAt: CreatedAt.AddMinutes(1));
        DateTimeOffset at = CreatedAt.AddMinutes(10);

        byte[] faulted = WorkflowCheckpointSerializer.RewriteFaulted(source, sequence: 5, ExecutionBudgetFault.Deadline, at);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(faulted);
        state.Status.ShouldBe(WorkflowRunStatus.Faulted);
        state.Sequence.ShouldBe(5);
        state.Wait.ShouldBeNull();
        state.ResumeRequestedAt.ShouldBeNull();
        state.UpdatedAt.ShouldBe(at);
        state.Fault.ShouldNotBeNull();
        state.Fault!.Value.StepId.ShouldBe("s3");
        state.Fault.Value.Attempt.ShouldBe(3);
        state.Fault.Value.Error.ShouldBe(ExecutionBudgetFault.Deadline);
        state.Fault.Value.At.ShouldBe(at);

        // The working state is the last durable checkpoint's, so the run stays inspectable.
        state.Budget.ShouldBe(budget);
        state.StepJournal.Count.ShouldBe(3);
        state.CreatedAt.ShouldBe(CreatedAt);
        state.Environment.ShouldBe("development");

        WorkflowCheckpointSerializer.ProjectIndex(faulted).ErrorType.ShouldBe(ExecutionBudgetFault.Deadline);
        WorkflowCheckpointSerializer.TryReadSequence(faulted, out long sequence).ShouldBeTrue();
        sequence.ShouldBe(5);
        WorkflowCheckpointSerializer.TryReadBudgetFacts(faulted, out CheckpointBudgetFacts facts).ShouldBeTrue();
        facts.BudgetFaulted.ShouldBeTrue();
    }

    [TestMethod]
    public void A_budget_fault_on_a_run_with_no_journal_names_no_step_and_replaces_an_earlier_fault()
    {
        byte[] source = BudgetCheckpoint(journalEntries: 0, budget: ExecutionBudget.Default, fault: new WorkflowFault("s1", 1, "boom", CreatedAt));

        byte[] faulted = WorkflowCheckpointSerializer.RewriteFaulted(source, sequence: 2, ExecutionBudgetFault.Fuel, CreatedAt.AddMinutes(1));

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(faulted);
        state.Fault!.Value.StepId.ShouldBe(string.Empty);
        state.Fault.Value.Attempt.ShouldBe(0);
        state.Fault.Value.Error.ShouldBe(ExecutionBudgetFault.Fuel);
    }

    private static byte[] BudgetCheckpoint(
        int journalEntries,
        ExecutionBudget? budget,
        long sequence = 1,
        bool truncated = false,
        WorkflowFault? fault = null,
        WorkflowWait? wait = null,
        DateTimeOffset? resumeRequestedAt = null)
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(1);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(1);
        var journal = new List<WorkflowStepJournalEntry>(journalEntries);
        for (int i = 1; i <= journalEntries; i++)
        {
            journal.Add(new WorkflowStepJournalEntry($"s{i}", WorkflowStepStatus.Succeeded, i, CreatedAt.AddSeconds(i), CreatedAt.AddSeconds(i + 1)));
        }

        return WorkflowCheckpointSerializer.Serialize(
            new WorkflowRunId("run-1"),
            "wf",
            fault is null ? WorkflowRunStatus.Running : WorkflowRunStatus.Faulted,
            cursor: journalEntries,
            sequence,
            CreatedAt,
            retryCounters,
            new Dictionary<string, byte[]>(StringComparer.Ordinal),
            inputs: default,
            stepOutputs,
            outputs: default,
            wait: wait,
            fault: fault,
            environment: "development",
            resumeRequestedAt: resumeRequestedAt,
            updatedAt: CreatedAt,
            stepJournal: journal,
            journalTruncated: truncated,
            budget: budget);
    }

    [TestMethod]
    public void The_write_sequence_round_trips_through_the_document()
    {
        // ADR 0065 decision 6: the server accepts only the persisted sequence plus one, so the persisted sequence has
        // to be recoverable from the row. Holding it only in the writer's memory loses it across a restart and hides it
        // from a second instance entirely, which turns the freshness check into a per-process opinion.
        using var retryCounters = PooledUtf8Map<int>.Rent(1);
        var correlationTokens = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(1);

        byte[] bytes = WorkflowCheckpointSerializer.Serialize(
            "run-1",
            "petWorkflow",
            WorkflowRunStatus.Running,
            cursor: 3,
            sequence: 42,
            CreatedAt,
            retryCounters,
            correlationTokens,
            default,
            stepOutputs,
            outputs: default);

        Assert.IsTrue(WorkflowCheckpointSerializer.TryReadSequence(bytes, out long sequence));
        Assert.AreEqual(42L, sequence);
    }

    [TestMethod]
    public void Bytes_that_are_not_a_checkpoint_read_as_no_sequence_rather_than_throwing()
    {
        // A Try method that threw on malformed input would turn a corrupt or foreign row into an exception on the load
        // path, where the caller is asking a question it is entitled to be told "no" to.
        Assert.IsFalse(WorkflowCheckpointSerializer.TryReadSequence(new byte[] { 1, 2, 3 }, out long sequence));
        Assert.AreEqual(0L, sequence);
    }

    [TestMethod]
    public void A_document_without_a_sequence_reads_as_absent_rather_than_zero()
    {
        // Zero is a legitimate sequence for a genesis row, so "no sequence here" must be distinguishable from "sequence
        // zero". Conflating them would let a malformed row present itself as the start of a run.
        Assert.IsFalse(WorkflowCheckpointSerializer.TryReadSequence("""{"runId":"run-1"}"""u8.ToArray(), out long sequence));
        Assert.AreEqual(0L, sequence);
    }

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
            sequence: 1,
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
            sequence: 1,
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
    public void Round_trips_the_execution_budget_and_reads_none_from_a_checkpoint_written_without_one()
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        var budget = new ExecutionBudget(120, TimeSpan.FromMinutes(5), 3, TimeSpan.FromSeconds(30));

        byte[] withBudget = WorkflowCheckpointSerializer.Serialize(
            "run-1", "petWorkflow", WorkflowRunStatus.Running, cursor: 1, sequence: 1, CreatedAt, retryCounters, new Dictionary<string, byte[]>(),
            inputs: default, stepOutputs, outputs: default, budget: budget);
        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(withBudget))
        {
            state.Budget.ShouldBe(budget);
        }

        // ADR 0068: a checkpoint written before budgets existed carries none, and reads back as none rather than as a default.
        byte[] without = WorkflowCheckpointSerializer.Serialize(
            "run-1", "petWorkflow", WorkflowRunStatus.Running, cursor: 1, sequence: 1, CreatedAt, retryCounters, new Dictionary<string, byte[]>(),
            inputs: default, stepOutputs, outputs: default);
        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(without))
        {
            state.Budget.ShouldBeNull();
        }
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
            sequence: 1,
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
            sequence: 1,
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
            sequence: 1,
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
            sequence: 1,
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
            sequence: 1,
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