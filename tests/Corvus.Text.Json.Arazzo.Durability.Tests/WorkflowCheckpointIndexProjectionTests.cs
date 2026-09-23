// <copyright file="WorkflowCheckpointIndexProjectionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the lean index re-projection the runner's checkpoint surface uses: a serverless function checks a
/// checkpoint in as opaque bytes, and the runner re-indexes it with <see cref="WorkflowCheckpointSerializer.ProjectIndex"/>
/// rather than a full <see cref="WorkflowCheckpointSerializer.Deserialize"/>. The load-bearing guarantee is that the
/// re-projected entry is identical to the one <see cref="WorkflowRun"/> stamps from its live fields — both go through
/// <see cref="WorkflowRunIndexEntry.Project"/> — so the two producers cannot drift.
/// </summary>
[TestClass]
public sealed class WorkflowCheckpointIndexProjectionTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAt = CreatedAt.AddMinutes(5);

    [TestMethod]
    public void Re_projection_from_bytes_equals_the_entry_the_run_would_stamp()
    {
        // The drift guard: serialize a checkpoint from a set of run fields, then assert re-projecting it from the
        // bytes yields the same index entry the run projects from those live fields. Tags are left empty here so
        // the whole-record comparison is exact (the tag sets compare by reference range, not content, and get
        // their own byte-level coverage below).
        WorkflowWait wait = WorkflowWait.Timer(UpdatedAt.AddHours(1));
        byte[] bytes = SerializeCheckpoint(
            WorkflowRunStatus.Suspended,
            wait: wait,
            correlationId: "trace-abc",
            environment: "prod",
            updatedAt: UpdatedAt);

        WorkflowRunIndexEntry projected = WorkflowCheckpointSerializer.ProjectIndex(bytes, out string? claimedEnvironment);

        WorkflowRunIndexEntry expected = WorkflowRunIndexEntry.Project(
            "petWorkflow",
            WorkflowRunStatus.Suspended,
            CreatedAt,
            UpdatedAt,
            wait,
            fault: null,
            correlationId: "trace-abc",
            tags: default,
            securityTags: default,
            resumeRequestedAt: null);

        projected.ShouldBe(expected);

        // The environment is not an entry field (it is half the run's address, ADR 0065 decision 9); the projection
        // reports the body's claim separately, for the coordinator's every-save structural check.
        claimedEnvironment.ShouldBe("prod");
    }

    [TestMethod]
    public void A_timer_wait_projects_to_due_at()
    {
        DateTimeOffset dueAt = UpdatedAt.AddMinutes(30);
        byte[] bytes = SerializeCheckpoint(WorkflowRunStatus.Suspended, wait: WorkflowWait.Timer(dueAt), updatedAt: UpdatedAt);

        WorkflowRunIndexEntry projected = WorkflowCheckpointSerializer.ProjectIndex(bytes);

        projected.DueAt.ShouldBe(dueAt);
        projected.AwaitingChannel.ShouldBeNull();
        projected.AwaitingCorrelationId.ShouldBeNull();
    }

    [TestMethod]
    public void A_message_wait_projects_to_the_awaiting_channel_and_correlation_id()
    {
        byte[] bytes = SerializeCheckpoint(
            WorkflowRunStatus.Suspended,
            wait: WorkflowWait.Message("orders", "corr-7"),
            updatedAt: UpdatedAt);

        WorkflowRunIndexEntry projected = WorkflowCheckpointSerializer.ProjectIndex(bytes);

        projected.AwaitingChannel.ShouldBe("orders");
        projected.AwaitingCorrelationId.ShouldBe("corr-7");
        projected.DueAt.ShouldBeNull();
    }

    [TestMethod]
    public void A_fault_projects_to_the_error_type()
    {
        byte[] bytes = SerializeCheckpoint(
            WorkflowRunStatus.Faulted,
            fault: new WorkflowFault("adopt", 3, "adoptionFailed", UpdatedAt),
            updatedAt: UpdatedAt);

        WorkflowRunIndexEntry projected = WorkflowCheckpointSerializer.ProjectIndex(bytes);

        projected.ErrorType.ShouldBe("adoptionFailed");
    }

    [TestMethod]
    public void Tags_and_security_tags_are_copied_into_the_entry_and_outlive_the_parsed_document()
    {
        // ProjectIndex disposes the parsed document before returning, so a tag set that still reads correctly here
        // proves the bytes were copied into the entry (a view into the disposed document would be corrupt).
        TagSet tags = TagSet.CopyFromJsonArray("""["nightly","eu"]"""u8);
        SecurityTagSet securityTags = SecurityTagSet.CopyFromJsonArray("""[{"key":"tenant","value":"acme"}]"""u8);

        byte[] bytes = SerializeCheckpoint(WorkflowRunStatus.Running, tags: tags, securityTags: securityTags, updatedAt: UpdatedAt);

        WorkflowRunIndexEntry projected = WorkflowCheckpointSerializer.ProjectIndex(bytes);

        projected.Tags.IsEmpty.ShouldBeFalse();
        projected.Tags.Count.ShouldBe(2);
        projected.Tags.RawJson.SequenceEqual(tags.RawJson).ShouldBeTrue();
        projected.SecurityTags.IsEmpty.ShouldBeFalse();
        projected.SecurityTags.Count.ShouldBe(1);
        projected.SecurityTags.RawJson.SequenceEqual(securityTags.RawJson).ShouldBeTrue();
    }

    [TestMethod]
    public void The_resume_requested_marker_projects_through()
    {
        byte[] bytes = SerializeCheckpoint(WorkflowRunStatus.Suspended, resumeRequestedAt: UpdatedAt, updatedAt: UpdatedAt);

        WorkflowRunIndexEntry projected = WorkflowCheckpointSerializer.ProjectIndex(bytes);

        projected.ResumeRequestedAt.ShouldBe(UpdatedAt);
    }

    [TestMethod]
    public void A_minimal_checkpoint_projects_with_no_optional_fields()
    {
        byte[] bytes = SerializeCheckpoint(WorkflowRunStatus.Pending);

        WorkflowRunIndexEntry projected = WorkflowCheckpointSerializer.ProjectIndex(bytes);

        projected.WorkflowId.ShouldBe("petWorkflow");
        projected.Status.ShouldBe(WorkflowRunStatus.Pending);
        projected.DueAt.ShouldBeNull();
        projected.AwaitingChannel.ShouldBeNull();
        projected.AwaitingCorrelationId.ShouldBeNull();
        projected.ErrorType.ShouldBeNull();
        projected.CorrelationId.ShouldBeNull();
        projected.ResumeRequestedAt.ShouldBeNull();
        projected.Tags.IsEmpty.ShouldBeTrue();
        projected.SecurityTags.IsEmpty.ShouldBeTrue();
    }

    [TestMethod]
    public void Try_project_index_returns_the_entry_for_a_valid_checkpoint()
    {
        byte[] bytes = SerializeCheckpoint(WorkflowRunStatus.Running, updatedAt: UpdatedAt);

        WorkflowCheckpointSerializer.TryProjectIndex(bytes, out WorkflowRunIndexEntry index).ShouldBeTrue();
        index.WorkflowId.ShouldBe("petWorkflow");
    }

    [TestMethod]
    public void Try_project_index_rejects_malformed_bytes()
    {
        // Not a row at all: the runner's checkpoint surface uses this to turn a bad body into a clean rejection rather
        // than an unhandled fault.
        WorkflowCheckpointSerializer.TryProjectIndex(new byte[] { 1, 2, 3 }, out WorkflowRunIndexEntry index).ShouldBeFalse();
        index.ShouldBe(default);
    }

    [TestMethod]
    public void Try_project_index_rejects_a_non_checkpoint_object()
    {
        // Not a framed row at all, whatever it may be as JSON.
        WorkflowCheckpointSerializer.TryProjectIndex("{}"u8.ToArray(), out WorkflowRunIndexEntry index).ShouldBeFalse();
        index.ShouldBe(default);
    }

    private static byte[] SerializeCheckpoint(
        WorkflowRunStatus status,
        WorkflowWait? wait = null,
        WorkflowFault? fault = null,
        string? correlationId = null,
        string environment = "development",
        TagSet tags = default,
        SecurityTagSet securityTags = default,
        DateTimeOffset? resumeRequestedAt = null,
        DateTimeOffset? updatedAt = null)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            new CheckpointEnvelope(
                "run-1",
                environment,
                "petWorkflow",
                status,
                1,
                1,
                Epoch: null,
                CreatedAt,
                updatedAt,
                correlationId,
                null,
                tags,
                securityTags,
                [],
                false,
                wait,
                fault),
            retryCounters,
            new Dictionary<string, byte[]>(),
            default,
            stepOutputs,
            default,
            new ControlPlaneRecord(ResumeRequest: resumeRequestedAt is { } requestedAt ? new ControlPlaneResumeRequest(requestedAt, 1) : null).ToUtf8());
    }
}