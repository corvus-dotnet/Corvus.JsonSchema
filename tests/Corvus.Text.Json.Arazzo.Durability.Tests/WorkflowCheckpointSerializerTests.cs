// <copyright file="WorkflowCheckpointSerializerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the checkpoint row (ADR 0065 decisions 4, 6 and 7): the framing, the split into runner region, payload
/// and control-plane region, the closed schemas, the join that gives a run its effective state, and the one property
/// every later piece rests on: a control-plane write leaves the runner's submitted bytes exactly as they were.
/// </summary>
[TestClass]
public sealed class WorkflowCheckpointSerializerTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [TestMethod]
    public void Round_trips_a_running_checkpoint_through_the_three_regions()
    {
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{ "inputs": { "petId": 7 }, "getPet": { "status": "available" }, "listTags": [ "a", "b" ], "tags": [ "x" ] }"""u8.ToArray());
        JsonElement root = source.RootElement;

        using var retryCounters = PooledUtf8Map<int>.Rent(1);
        retryCounters.Set("getPet", 2);
        var correlationTokens = new Dictionary<string, byte[]> { ["orderRef"] = Encoding.UTF8.GetBytes("abc-123") };
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(2);
        stepOutputs.Set("getPet", root.GetProperty("getPet"u8));
        stepOutputs.Set("listTags", root.GetProperty("listTags"u8));
        var budget = new ExecutionBudget(120, TimeSpan.FromMinutes(5), 3, TimeSpan.FromSeconds(30), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);
        byte[] controlPlane = new ControlPlaneRecord(Budget: budget).ToUtf8();

        byte[] row = WorkflowCheckpointSerializer.Serialize(
            Envelope(WorkflowRunStatus.Running, cursor: 3, sequence: 1, epoch: 9, tags: TagSet.CopyFrom(root.GetProperty("tags"u8))),
            retryCounters,
            correlationTokens,
            root.GetProperty("inputs"u8),
            stepOutputs,
            outputs: default,
            controlPlane);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(row);

        state.RunId.ShouldBe(new WorkflowRunId("run-1"));
        state.Environment.ShouldBe("development");
        state.WorkflowId.ShouldBe("petWorkflow");
        state.Status.ShouldBe(WorkflowRunStatus.Running);
        state.RunnerStatus.ShouldBe(WorkflowRunStatus.Running);
        state.Cursor.ShouldBe(3);
        state.Sequence.ShouldBe(1);
        state.Epoch.ShouldBe(9);
        state.CreatedAt.ShouldBe(CreatedAt);
        state.UpdatedAt.ShouldBe(CreatedAt);
        state.Tags.ToList().ShouldBe(["x"]);
        state.RetryCounters.TryGetValue("getPet", out int getPetRetry).ShouldBeTrue();
        getPetRetry.ShouldBe(2);
        state.CorrelationTokens["orderRef"].ShouldBe(Encoding.UTF8.GetBytes("abc-123"));
        state.Inputs.GetProperty("petId"u8).GetInt32().ShouldBe(7);
        state.StepOutputs.TryGetValue("getPet", out JsonElement getPetOutputs).ShouldBeTrue();
        getPetOutputs.GetProperty("status"u8).GetString().ShouldBe("available");
        state.StepOutputs.TryGetValue("listTags", out JsonElement listTagsOutputs).ShouldBeTrue();
        listTagsOutputs.GetArrayLength().ShouldBe(2);
        state.Outputs.ValueKind.ShouldBe(JsonValueKind.Undefined);
        state.Budget.ShouldBe(budget);
        state.ControlPlane.ShouldBe(new ControlPlaneRecord(Budget: budget));
        state.ControlPlaneRegion.ToArray().ShouldBe(controlPlane, customMessage: "the control-plane region is carried byte for byte");
        state.Row.ToArray().ShouldBe(row);
        state.StepJournal.ShouldBeEmpty();
        state.Wait.ShouldBeNull();
        state.Fault.ShouldBeNull();
        state.ResumeRequestedAt.ShouldBeNull();
        state.Pause.ShouldBeNull();
    }

    [TestMethod]
    public void The_row_is_framed_in_the_pinned_order_and_a_clear_row_carries_empty_crypto_regions()
    {
        byte[] row = Row(WorkflowRunStatus.Running, controlPlane: new ControlPlaneRecord(Budget: ExecutionBudget.Default).ToUtf8());

        CheckpointRow.TryParse(row, out CheckpointRowLayout layout).ShouldBeTrue();
        layout.Algorithm.ShouldBe(CheckpointAlgorithm.Clear);
        row[0].ShouldBe(CheckpointRow.FramingVersion);
        row[layout.KeyId].Length.ShouldBe(0);
        row[layout.Salt].Length.ShouldBe(0);
        row[layout.Nonce].Length.ShouldBe(0);
        row[layout.Tag].Length.ShouldBe(0);
        row[layout.Mac].Length.ShouldBe(0);
        Encoding.UTF8.GetString(row[layout.RunnerRegion]).ShouldStartWith("{\"runId\":\"run-1\",\"environment\":\"development\",\"workflowId\":\"petWorkflow\",\"status\":\"Running\",\"cursor\":");
        Encoding.UTF8.GetString(row[layout.Payload]).ShouldStartWith("{\"correlationTokens\":{}");
        Encoding.UTF8.GetString(row[layout.ControlPlaneRegion]).ShouldStartWith("{\"budget\":");

        // The submitted bytes are everything before the control-plane region's length prefix: header and every
        // runner-written region, which is what the checkpoint digest is taken over (decision 6).
        layout.SubmittedLength.ShouldBe(row.Length - sizeof(uint) - row[layout.ControlPlaneRegion].Length);
        layout.RunnerRegion.Start.Value.ShouldBeLessThan(layout.Payload.Start.Value);
        layout.Payload.End.Value.ShouldBeLessThanOrEqualTo(layout.SubmittedLength);
    }

    [TestMethod]
    public void A_control_plane_write_replaces_the_control_plane_region_and_not_one_submitted_byte()
    {
        // The property every later piece rests on: the MAC and the digest cover the submitted bytes, so a control-plane
        // decision that touched any of them would break every checkpoint it was written on.
        byte[] row = Row(WorkflowRunStatus.Suspended, wait: WorkflowWait.Timer(CreatedAt.AddMinutes(5)));
        CheckpointRowLayout before = CheckpointRow.Parse(row);
        byte[] region = new ControlPlaneRecord(Cancellation: new ControlPlaneCancellation(CreatedAt.AddMinutes(1))).ToUtf8();

        byte[] rewritten = CheckpointRow.WithControlPlaneRegion(row, region);

        CheckpointRowLayout after = CheckpointRow.Parse(rewritten);
        rewritten.AsSpan(0, after.SubmittedLength).SequenceEqual(row.AsSpan(0, before.SubmittedLength)).ShouldBeTrue("the submitted bytes are untouched");
        rewritten[after.ControlPlaneRegion].ShouldBe(region);
        row.AsSpan(before.ControlPlaneRegion).Length.ShouldBe(0, "the source row had no control-plane region");
    }

    [TestMethod]
    public void Bytes_that_are_not_a_row_are_refused_by_the_framing()
    {
        byte[] row = Row(WorkflowRunStatus.Running);

        CheckpointRow.TryParse([], out _).ShouldBeFalse();
        CheckpointRow.TryParse(new byte[] { 1, 2, 3 }, out _).ShouldBeFalse();
        CheckpointRow.TryParse("{\"runId\":\"run-1\"}"u8, out _).ShouldBeFalse("the old flat document is not a row");
        CheckpointRow.TryParse(row.AsSpan(0, row.Length - 1), out _).ShouldBeFalse("truncated");
        CheckpointRow.TryParse([.. row, 0], out _).ShouldBeFalse("trailing bytes");

        byte[] wrongVersion = [.. row];
        wrongVersion[0] = 2;
        CheckpointRow.TryParse(wrongVersion, out _).ShouldBeFalse("another framing version");

        byte[] unknownAlgorithm = [.. row];
        unknownAlgorithm[1] = 7;
        CheckpointRow.TryParse(unknownAlgorithm, out _).ShouldBeFalse("an algorithm this code does not know");

        // A clear row carrying a MAC is not one this code wrote: the crypto regions mean nothing without a key.
        CheckpointRowLayout layout = CheckpointRow.Parse(row);
        byte[] clearWithMac = [.. row.AsSpan(0, layout.Mac.Start.Value - sizeof(uint)), 0, 0, 0, 1, 0xAB, .. row.AsSpan(layout.Mac.End.Value)];
        CheckpointRow.TryParse(clearWithMac, out _).ShouldBeFalse();

        Should.Throw<FormatException>(() => CheckpointRow.Parse(wrongVersion));
        Should.Throw<FormatException>(() => WorkflowCheckpointSerializer.Deserialize(wrongVersion));
        WorkflowCheckpointSerializer.TryProject(wrongVersion, out _).ShouldBeFalse();
        WorkflowCheckpointSerializer.TryReadSequence(wrongVersion, out _).ShouldBeFalse();
        WorkflowCheckpointSerializer.TryReadBudgetFacts(wrongVersion, out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Every_region_is_a_closed_schema()
    {
        // The envelope is what the control plane reads without a key, so an unknown member is a malformed row rather
        // than data: neither region may become a channel (decision 4).
        byte[] extraEnvelopeMember = WithRunnerRegion(Row(WorkflowRunStatus.Running), "{\"runId\":\"run-1\",\"environment\":\"development\",\"workflowId\":\"w\",\"status\":\"Running\",\"cursor\":0,\"sequence\":1,\"createdAt\":\"2026-03-04T05:06:07+00:00\",\"retryCounters\":{},\"note\":\"smuggled\"}");
        Should.Throw<FormatException>(() => WorkflowCheckpointSerializer.Deserialize(extraEnvelopeMember)).Message.ShouldContain("'note'");
        WorkflowCheckpointSerializer.TryProject(extraEnvelopeMember, out _).ShouldBeFalse();

        byte[] missingSequence = WithRunnerRegion(Row(WorkflowRunStatus.Running), "{\"runId\":\"run-1\",\"environment\":\"development\",\"workflowId\":\"w\",\"status\":\"Running\",\"cursor\":0,\"createdAt\":\"2026-03-04T05:06:07+00:00\",\"retryCounters\":{}}");
        Should.Throw<FormatException>(() => WorkflowCheckpointSerializer.Deserialize(missingSequence)).Message.ShouldContain("'sequence'");
        WorkflowCheckpointSerializer.TryProject(missingSequence, out _).ShouldBeFalse();
        WorkflowCheckpointSerializer.TryReadSequence(missingSequence, out _).ShouldBeFalse();

        byte[] missingEnvironment = WithRunnerRegion(Row(WorkflowRunStatus.Running), "{\"runId\":\"run-1\",\"workflowId\":\"w\",\"status\":\"Running\",\"cursor\":0,\"sequence\":1,\"createdAt\":\"2026-03-04T05:06:07+00:00\",\"retryCounters\":{}}");
        Should.Throw<FormatException>(() => WorkflowCheckpointSerializer.Deserialize(missingEnvironment)).Message.ShouldContain("'environment'");

        byte[] extraPayloadMember = WithPayload(Row(WorkflowRunStatus.Running), "{\"correlationTokens\":{},\"stepOutputs\":{},\"secret\":1}");
        Should.Throw<FormatException>(() => WorkflowCheckpointSerializer.Deserialize(extraPayloadMember)).Message.ShouldContain("'secret'");

        byte[] extraControlPlaneMember = CheckpointRow.WithControlPlaneRegion(Row(WorkflowRunStatus.Running), "{\"owner\":\"me\"}"u8);
        Should.Throw<FormatException>(() => WorkflowCheckpointSerializer.Deserialize(extraControlPlaneMember)).Message.ShouldContain("'owner'");
        WorkflowCheckpointSerializer.TryProject(extraControlPlaneMember, out _).ShouldBeFalse();
    }

    [TestMethod]
    public void A_cancellation_in_the_control_plane_region_overrides_the_runner_region_unconditionally()
    {
        byte[] row = Row(WorkflowRunStatus.Suspended, sequence: 4, wait: WorkflowWait.Message("orders", "o-1"), updatedAt: CreatedAt);
        DateTimeOffset cancelledAt = CreatedAt.AddMinutes(2);
        byte[] cancelled = CheckpointRow.WithControlPlaneRegion(row, new ControlPlaneRecord(Cancellation: new ControlPlaneCancellation(cancelledAt)).ToUtf8());

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cancelled);
        state.Status.ShouldBe(WorkflowRunStatus.Cancelled);
        state.RunnerStatus.ShouldBe(WorkflowRunStatus.Suspended);
        state.Wait.ShouldBeNull();

        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(cancelled);
        index.Status.ShouldBe(WorkflowRunStatus.Cancelled);
        index.AwaitingChannel.ShouldBeNull();
        index.UpdatedAt.ShouldBe(cancelledAt, "the index reflects the latest write to the row, the control plane's included");

        // A later runner save carrying the same region does not un-cancel the run.
        byte[] later = WorkflowCheckpointSerializer.Serialize(
            Envelope(WorkflowRunStatus.Running, cursor: 5, sequence: 5),
            PooledUtf8Map<int>.Rent(0),
            new Dictionary<string, byte[]>(),
            inputs: default,
            PooledUtf8Map<JsonElement>.Rent(0),
            outputs: default,
            new ControlPlaneRecord(Cancellation: new ControlPlaneCancellation(cancelledAt)).ToUtf8());
        WorkflowCheckpointSerializer.ProjectIndex(later).Status.ShouldBe(WorkflowRunStatus.Cancelled);
    }

    [TestMethod]
    public void A_control_plane_budget_fault_holds_until_the_runner_advances_past_it()
    {
        var journal = new List<WorkflowStepJournalEntry>
        {
            new("s1", WorkflowStepStatus.Succeeded, 1, CreatedAt, CreatedAt.AddSeconds(1)),
            new("s2", WorkflowStepStatus.Succeeded, 3, CreatedAt.AddSeconds(1), CreatedAt.AddSeconds(2)),
        };
        byte[] row = Row(WorkflowRunStatus.Running, sequence: 4, journal: journal, wait: WorkflowWait.Timer(CreatedAt.AddMinutes(5)), controlPlane: new ControlPlaneRecord(Budget: ExecutionBudget.Default).ToUtf8());
        DateTimeOffset at = CreatedAt.AddMinutes(10);
        var decided = new ControlPlaneRecord(Budget: ExecutionBudget.Default, BudgetFault: new ControlPlaneBudgetFault(ExecutionBudgetFault.Deadline, at, AgainstSequence: 4));
        byte[] faulted = CheckpointRow.WithControlPlaneRegion(row, decided.ToUtf8());

        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(faulted))
        {
            state.Status.ShouldBe(WorkflowRunStatus.Faulted);
            state.Wait.ShouldBeNull();
            state.Fault.ShouldBe(new WorkflowFault("s2", 3, ExecutionBudgetFault.Deadline, at), "recorded at the last journaled step");
            state.RunnerFault.ShouldBeNull();
            state.Sequence.ShouldBe(4, "the runner's sequence is not consumed by a control-plane decision");
        }

        WorkflowCheckpointSerializer.ProjectIndex(faulted).ErrorType.ShouldBe(ExecutionBudgetFault.Deadline);
        WorkflowCheckpointSerializer.TryReadBudgetFacts(faulted, out CheckpointBudgetFacts facts).ShouldBeTrue();
        facts.BudgetFaulted.ShouldBeTrue();
        facts.Budget.ShouldBe(ExecutionBudget.Default);
        facts.JournalCount.ShouldBe(2);

        // The runner's next accepted save, at sequence 5, supersedes the fault: the same region rides along, and the
        // join reads the runner's own status again.
        byte[] advanced = Row(WorkflowRunStatus.Running, sequence: 5, journal: journal, controlPlane: decided.ToUtf8());
        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(advanced))
        {
            state.Status.ShouldBe(WorkflowRunStatus.Running);
            state.Fault.ShouldBeNull();
        }

        WorkflowCheckpointSerializer.TryReadBudgetFacts(advanced, out facts).ShouldBeTrue();
        facts.BudgetFaulted.ShouldBeFalse();

        // A runner-authored budget fault reads as one too, and a run with no journal names no step.
        byte[] selfFaulted = Row(WorkflowRunStatus.Faulted, sequence: 6, fault: new WorkflowFault("s3", 1, ExecutionBudgetFault.Fuel, at));
        WorkflowCheckpointSerializer.TryReadBudgetFacts(selfFaulted, out facts).ShouldBeTrue();
        facts.BudgetFaulted.ShouldBeTrue();
        byte[] noJournal = CheckpointRow.WithControlPlaneRegion(Row(WorkflowRunStatus.Running, sequence: 1), new ControlPlaneRecord(BudgetFault: new ControlPlaneBudgetFault(ExecutionBudgetFault.Fuel, at, 1)).ToUtf8());
        using WorkflowCheckpointState bare = WorkflowCheckpointSerializer.Deserialize(noJournal);
        bare.Fault!.Value.StepId.ShouldBe(string.Empty);
        bare.Fault.Value.Attempt.ShouldBe(0);
    }

    [TestMethod]
    public void A_resume_request_is_outstanding_until_the_runner_saves_past_the_sequence_it_was_made_against()
    {
        byte[] row = Row(WorkflowRunStatus.Faulted, sequence: 3, fault: new WorkflowFault("s1", 1, "boom", CreatedAt));
        DateTimeOffset requestedAt = CreatedAt.AddMinutes(1);
        var pause = new WorkflowPauseConfig(AfterEachStep: true, new HashSet<int> { 1, 3 });
        var decided = new ControlPlaneRecord(Pause: pause, ResumeRequest: new ControlPlaneResumeRequest(requestedAt, AgainstSequence: 3));
        byte[] requested = CheckpointRow.WithControlPlaneRegion(row, decided.ToUtf8());

        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(requested))
        {
            state.ResumeRequestedAt.ShouldBe(requestedAt);
            state.Status.ShouldBe(WorkflowRunStatus.Faulted, "a resume request leaves the lifecycle status alone");
            state.Pause.ShouldNotBeNull();
            state.Pause!.Value.AfterEachStep.ShouldBeTrue();
            state.Pause.Value.BreakpointCursors.ShouldBe(new HashSet<int> { 1, 3 }, ignoreOrder: true);
        }

        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(requested);
        index.ResumeRequestedAt.ShouldBe(requestedAt);
        index.UpdatedAt.ShouldBe(requestedAt);

        // The claiming runner's first save consumes it: the region is carried unchanged, the sequence moves past it.
        byte[] consumed = Row(WorkflowRunStatus.Running, sequence: 4, controlPlane: decided.ToUtf8());
        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(consumed))
        {
            state.ResumeRequestedAt.ShouldBeNull();
            state.Pause.ShouldNotBeNull("the pause stays until the control plane clears it");
        }

        WorkflowCheckpointSerializer.ProjectIndex(consumed).ResumeRequestedAt.ShouldBeNull();
    }

    [TestMethod]
    public void One_projection_yields_the_index_the_environment_the_sequence_the_epoch_the_facts_and_the_region()
    {
        var budget = new ExecutionBudget(3, TimeSpan.FromMinutes(30), 2, TimeSpan.FromSeconds(5), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);
        byte[] region = new ControlPlaneRecord(Budget: budget).ToUtf8();
        var journal = new List<WorkflowStepJournalEntry> { new("s1", WorkflowStepStatus.Succeeded, 1, CreatedAt, CreatedAt.AddSeconds(1)) };
        byte[] row = Row(WorkflowRunStatus.Faulted, sequence: 7, epoch: 12, journal: journal, truncated: true, fault: new WorkflowFault("s1", 2, ExecutionBudgetFault.Fuel, CreatedAt), controlPlane: region);

        WorkflowCheckpointSerializer.TryProject(row, out CheckpointProjection projection).ShouldBeTrue();
        projection.Index.ShouldBe(WorkflowCheckpointSerializer.ProjectIndex(row));
        projection.Index.ErrorType.ShouldBe(ExecutionBudgetFault.Fuel);
        projection.Environment.ShouldBe("development");
        projection.Sequence.ShouldBe(7);
        projection.Epoch.ShouldBe(12);
        projection.ControlPlaneRegion.ToArray().ShouldBe(region);
        WorkflowCheckpointSerializer.TryReadBudgetFacts(row, out CheckpointBudgetFacts scanned).ShouldBeTrue();
        projection.Facts.ShouldBe(scanned);
        projection.Facts.ShouldBe(new CheckpointBudgetFacts(budget, 1, true, true));
        WorkflowCheckpointSerializer.TryReadSequence(row, out long sequence).ShouldBeTrue();
        sequence.ShouldBe(7);

        // An in-process writer holds no grant and writes no epoch.
        WorkflowCheckpointSerializer.Project(Row(WorkflowRunStatus.Running)).Epoch.ShouldBeNull();
    }

    [TestMethod]
    public void A_remediation_rewrites_only_what_it_names()
    {
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{ "inputs": { "petId": 7 }, "patched": { "petId": 8 }, "getPet": { "ok": true } }"""u8.ToArray());
        JsonElement root = source.RootElement;
        using var retryCounters = PooledUtf8Map<int>.Rent(1);
        retryCounters.Set("getPet", 2);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(1);
        stepOutputs.Set("getPet", root.GetProperty("getPet"u8));
        var journal = new List<WorkflowStepJournalEntry> { new("getPet", WorkflowStepStatus.Faulted, 2, CreatedAt, CreatedAt.AddSeconds(1)) };
        byte[] row = WorkflowCheckpointSerializer.Serialize(
            Envelope(WorkflowRunStatus.Faulted, cursor: 4, sequence: 9, journal: journal, fault: new WorkflowFault("getPet", 2, "boom", CreatedAt)),
            retryCounters,
            new Dictionary<string, byte[]> { ["k"] = [1, 2] },
            root.GetProperty("inputs"u8),
            stepOutputs,
            outputs: default,
            new ControlPlaneRecord(Budget: ExecutionBudget.Default).ToUtf8());

        DateTimeOffset at = CreatedAt.AddMinutes(3);
        var rebudget = new ExecutionBudget(500, TimeSpan.FromHours(1), 2, TimeSpan.FromSeconds(5), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);
        using var patchedOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        byte[] rewritten = WorkflowCheckpointSerializer.RewriteForRemediation(row, new CheckpointRemediation(2, at, ReplacesContext: true, root.GetProperty("patched"u8), patchedOutputs, rebudget));

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(rewritten);
        state.Cursor.ShouldBe(2);
        state.UpdatedAt.ShouldBe(at);
        state.Inputs.GetProperty("petId"u8).GetInt32().ShouldBe(8);
        state.StepOutputs.Count.ShouldBe(0);
        state.Budget.ShouldBe(rebudget);

        // Everything the remediation did not name is as it was.
        state.Sequence.ShouldBe(9);
        state.Status.ShouldBe(WorkflowRunStatus.Faulted);
        state.Fault.ShouldBe(new WorkflowFault("getPet", 2, "boom", CreatedAt));
        state.StepJournal.Count.ShouldBe(1);
        state.RetryCounters.TryGetValue("getPet", out int retries).ShouldBeTrue();
        retries.ShouldBe(2);
        state.CorrelationTokens["k"].ShouldBe(new byte[] { 1, 2 });
    }

    [TestMethod]
    public void The_control_plane_record_round_trips_and_an_empty_one_is_an_empty_region()
    {
        default(ControlPlaneRecord).ToUtf8().ShouldBeEmpty();
        ControlPlaneRecord.Parse(ReadOnlyMemory<byte>.Empty).ShouldBe(default(ControlPlaneRecord));

        var full = new ControlPlaneRecord(
            ExecutionBudget.Default,
            new ControlPlaneBudgetFault(ExecutionBudgetFault.Fuel, CreatedAt, 3),
            new ControlPlaneCancellation(CreatedAt.AddSeconds(1)),
            new WorkflowPauseConfig(false, new HashSet<int> { 2 }),
            new ControlPlaneResumeRequest(CreatedAt.AddSeconds(2), 3));
        byte[] region = full.ToUtf8();
        ControlPlaneRecord parsed = ControlPlaneRecord.Parse(region);
        parsed.Budget.ShouldBe(full.Budget);
        parsed.BudgetFault.ShouldBe(full.BudgetFault);
        parsed.Cancellation.ShouldBe(full.Cancellation);
        parsed.ResumeRequest.ShouldBe(full.ResumeRequest);
        parsed.Pause!.Value.AfterEachStep.ShouldBeFalse();
        parsed.Pause.Value.BreakpointCursors.ShouldBe(new HashSet<int> { 2 });

        // Deterministic bytes: the same record renders the same region, in fixed order.
        full.ToUtf8().ShouldBe(region);
        Encoding.UTF8.GetString(region).ShouldStartWith("{\"budget\":");
    }

    private static CheckpointEnvelope Envelope(
        WorkflowRunStatus status,
        int cursor = 0,
        long sequence = 1,
        long? epoch = null,
        DateTimeOffset? updatedAt = null,
        TagSet tags = default,
        IReadOnlyList<WorkflowStepJournalEntry>? journal = null,
        bool truncated = false,
        WorkflowWait? wait = null,
        WorkflowFault? fault = null)
        => new(
            new WorkflowRunId("run-1"),
            "development",
            "petWorkflow",
            status,
            cursor,
            sequence,
            epoch,
            CreatedAt,
            updatedAt ?? CreatedAt,
            CorrelationId: null,
            RerunOf: null,
            tags,
            SecurityTags: default,
            journal ?? [],
            truncated,
            wait,
            fault);

    private static byte[] Row(
        WorkflowRunStatus status,
        long sequence = 1,
        long? epoch = null,
        DateTimeOffset? updatedAt = null,
        IReadOnlyList<WorkflowStepJournalEntry>? journal = null,
        bool truncated = false,
        WorkflowWait? wait = null,
        WorkflowFault? fault = null,
        byte[]? controlPlane = null)
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            Envelope(status, cursor: journal?.Count ?? 0, sequence, epoch, updatedAt, journal: journal, truncated: truncated, wait: wait, fault: fault),
            retryCounters,
            new Dictionary<string, byte[]>(StringComparer.Ordinal),
            inputs: default,
            stepOutputs,
            outputs: default,
            controlPlane ?? []);
    }

    // Re-frames a row around a hand-written runner region, keeping every other region.
    private static byte[] WithRunnerRegion(byte[] row, string runnerRegion)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(row);
        return CheckpointRow.WriteClear(Encoding.UTF8.GetBytes(runnerRegion), row[layout.Payload], row[layout.ControlPlaneRegion]);
    }

    private static byte[] WithPayload(byte[] row, string payload)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(row);
        return CheckpointRow.WriteClear(row[layout.RunnerRegion], Encoding.UTF8.GetBytes(payload), row[layout.ControlPlaneRegion]);
    }
}