// <copyright file="NativeBuildJobInMemoryStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Behavioural tests for the reference <see cref="InMemoryNativeBuildJobStore"/> (ADR 0055): enqueue is idempotent per
/// target tuple, the worker claim picks the oldest queued job, completion is state- and etag-gated, target-readiness tracks
/// the terminal Ready state, and the list pages oldest-first.
/// </summary>
[TestClass]
public sealed class NativeBuildJobInMemoryStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Enqueue_creates_a_queued_job_stamped_from_the_target_tuple()
    {
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(new TestTimeProvider(Start));

        using ParsedJsonDocument<NativeBuildJob> job = await EnqueueAsync(store, "wf", 3, "production", "linux-x64", "nightly");

        NativeBuildJob value = job.RootElement;
        value.HasStatus(NativeBuildJobStatus.Queued).ShouldBeTrue();
        value.IdValue.ShouldBe(NativeBuildJob.DeriveId("wf", 3, "production", "linux-x64"));
        value.BaseWorkflowIdValue.ShouldBe("wf");
        value.VersionNumberValue.ShouldBe(3);
        value.EnvironmentValue.ShouldBe("production");
        value.RuntimeIdentifierValue.ShouldBe("linux-x64");
        value.BuildLabelOrNull.ShouldBe("nightly");
        value.CreatedAtValue.ShouldBe(Start);

        // A fresh job carries no progress fields.
        value.StartedAtValue.ShouldBeNull();
        value.CompletedAtValue.ShouldBeNull();
        value.FailureReasonOrNull.ShouldBeNull();
        value.ClaimedByOrNull.ShouldBeNull();
    }

    [TestMethod]
    public async Task Enqueue_is_idempotent_per_target_tuple()
    {
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(new TestTimeProvider(Start));

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        // The two enqueues target the same tuple, so they map to one row rather than two.
        using PooledDocumentList<NativeBuildJob> all = await store.ListAsync(default, default);
        all.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Enqueue_resets_an_existing_completed_job_back_to_queued()
    {
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(new TestTimeProvider(Start));
        string id = NativeBuildJob.DeriveId("wf", 1, "dev", "linux-x64");

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        WorkflowEtag etag = await ClaimAndGetEtagAsync(store);
        using (await store.CompleteAsync(id, new NativeBuildJobCompletion(NativeBuildJobStatus.Failed, "boom"), etag, default))
        {
        }

        // Re-enqueuing the same target is a rebuild: it resets the job to Queued and clears the progress fields.
        using ParsedJsonDocument<NativeBuildJob> requeued = await EnqueueAsync(store, "wf", 1, "dev", "linux-x64");
        NativeBuildJob value = requeued.RootElement;
        value.HasStatus(NativeBuildJobStatus.Queued).ShouldBeTrue();
        value.StartedAtValue.ShouldBeNull();
        value.CompletedAtValue.ShouldBeNull();
        value.FailureReasonOrNull.ShouldBeNull();
        value.ClaimedByOrNull.ShouldBeNull();
    }

    [TestMethod]
    public async Task ClaimNextQueued_moves_the_oldest_queued_job_to_building_then_returns_null()
    {
        var time = new TestTimeProvider(Start);
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(time);

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        time.Advance(TimeSpan.FromMinutes(1));
        using (await EnqueueAsync(store, "wf", 2, "dev", "linux-x64"))
        {
        }

        // The oldest queued job is claimed first and transitions to Building with the worker + start stamps.
        using (ParsedJsonDocument<NativeBuildJob>? first = await store.ClaimNextQueuedAsync("worker-a", TimeSpan.FromMinutes(5), default))
        {
            first.ShouldNotBeNull();
            first.RootElement.VersionNumberValue.ShouldBe(1);
            first.RootElement.HasStatus(NativeBuildJobStatus.Building).ShouldBeTrue();
            first.RootElement.ClaimedByOrNull.ShouldBe("worker-a");
            first.RootElement.StartedAtValue.ShouldNotBeNull();
        }

        using (ParsedJsonDocument<NativeBuildJob>? second = await store.ClaimNextQueuedAsync("worker-b", TimeSpan.FromMinutes(5), default))
        {
            second.ShouldNotBeNull();
            second.RootElement.VersionNumberValue.ShouldBe(2);
        }

        // Nothing is queued now, so the claim yields null.
        using ParsedJsonDocument<NativeBuildJob>? none = await store.ClaimNextQueuedAsync("worker-c", TimeSpan.FromMinutes(5), default);
        none.ShouldBeNull();
    }

    [TestMethod]
    public async Task Complete_transitions_building_to_ready()
    {
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(new TestTimeProvider(Start));
        string id = NativeBuildJob.DeriveId("wf", 1, "dev", "linux-x64");

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        WorkflowEtag etag = await ClaimAndGetEtagAsync(store);

        using ParsedJsonDocument<NativeBuildJob>? done = await store.CompleteAsync(id, new NativeBuildJobCompletion(NativeBuildJobStatus.Ready), etag, default);
        done.ShouldNotBeNull();
        done.RootElement.HasStatus(NativeBuildJobStatus.Ready).ShouldBeTrue();
        done.RootElement.CompletedAtValue.ShouldNotBeNull();
        done.RootElement.FailureReasonOrNull.ShouldBeNull();
    }

    [TestMethod]
    public async Task Complete_transitions_building_to_failed_with_a_reason()
    {
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(new TestTimeProvider(Start));
        string id = NativeBuildJob.DeriveId("wf", 1, "dev", "linux-x64");

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        WorkflowEtag etag = await ClaimAndGetEtagAsync(store);

        using ParsedJsonDocument<NativeBuildJob>? done = await store.CompleteAsync(id, new NativeBuildJobCompletion(NativeBuildJobStatus.Failed, "the compiler crashed"), etag, default);
        done.ShouldNotBeNull();
        done.RootElement.HasStatus(NativeBuildJobStatus.Failed).ShouldBeTrue();
        done.RootElement.CompletedAtValue.ShouldNotBeNull();
        done.RootElement.FailureReasonOrNull.ShouldBe("the compiler crashed");
    }

    [TestMethod]
    public async Task Complete_with_a_stale_etag_throws_a_conflict()
    {
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(new TestTimeProvider(Start));
        string id = NativeBuildJob.DeriveId("wf", 1, "dev", "linux-x64");

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        // Claim it (so it is Building), then complete under an etag that no longer matches.
        _ = await ClaimAndGetEtagAsync(store);

        await Should.ThrowAsync<NativeBuildJobConflictException>(async () =>
        {
            _ = await store.CompleteAsync(id, new NativeBuildJobCompletion(NativeBuildJobStatus.Ready), new WorkflowEtag("stale"), default);
        });
    }

    [TestMethod]
    public async Task Complete_from_a_non_building_state_throws_a_state_error()
    {
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(new TestTimeProvider(Start));
        string id = NativeBuildJob.DeriveId("wf", 1, "dev", "linux-x64");

        // Enqueued but never claimed, so the job is Queued rather than Building.
        WorkflowEtag etag;
        using (ParsedJsonDocument<NativeBuildJob> queued = await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
            etag = queued.RootElement.EtagValue;
        }

        await Should.ThrowAsync<NativeBuildJobStateException>(async () =>
        {
            _ = await store.CompleteAsync(id, new NativeBuildJobCompletion(NativeBuildJobStatus.Ready), etag, default);
        });
    }

    [TestMethod]
    public async Task IsTargetReady_is_true_only_after_the_build_is_ready()
    {
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(new TestTimeProvider(Start));
        string id = NativeBuildJob.DeriveId("wf", 1, "dev", "linux-x64");

        // Absent target.
        (await store.IsTargetReadyAsync("wf", 1, "dev", "linux-x64", default)).ShouldBeFalse();

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        // Queued.
        (await store.IsTargetReadyAsync("wf", 1, "dev", "linux-x64", default)).ShouldBeFalse();

        WorkflowEtag etag = await ClaimAndGetEtagAsync(store);

        // Building.
        (await store.IsTargetReadyAsync("wf", 1, "dev", "linux-x64", default)).ShouldBeFalse();

        using (await store.CompleteAsync(id, new NativeBuildJobCompletion(NativeBuildJobStatus.Ready), etag, default))
        {
        }

        // Ready.
        (await store.IsTargetReadyAsync("wf", 1, "dev", "linux-x64", default)).ShouldBeTrue();

        // A different target is not ready.
        (await store.IsTargetReadyAsync("wf", 2, "dev", "linux-x64", default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task List_and_paging_return_jobs_oldest_first()
    {
        var time = new TestTimeProvider(Start);
        INativeBuildJobStore store = new InMemoryNativeBuildJobStore(time);

        for (int version = 1; version <= 3; version++)
        {
            using (await EnqueueAsync(store, "wf", version, "dev", "linux-x64"))
            {
            }

            time.Advance(TimeSpan.FromMinutes(1));
        }

        // The full read is oldest-first by (createdAt, id).
        using (PooledDocumentList<NativeBuildJob> all = await store.ListAsync(default, default))
        {
            all.Count.ShouldBe(3);
            all[0].VersionNumberValue.ShouldBe(1);
            all[1].VersionNumberValue.ShouldBe(2);
            all[2].VersionNumberValue.ShouldBe(3);
        }

        // The keyset pager walks the same order across page boundaries.
        var seen = new List<int>();
        byte[]? token = null;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using NativeBuildJobPage page = await store.ListAsync(default, 2, tokenDoc?.RootElement ?? default, default);
            for (int i = 0; i < page.Jobs.Count; i++)
            {
                seen.Add(page.Jobs[i].VersionNumberValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
        }
        while (token is not null);

        seen.ShouldBe([1, 2, 3]);
    }

    // Enqueues a Queued job for the target tuple and returns the store's pooled record.
    private static async Task<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(INativeBuildJobStore store, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, string? buildLabel = null)
    {
        using ParsedJsonDocument<NativeBuildJob> draft = NativeBuildJob.Draft(baseWorkflowId, versionNumber, environment, runtimeIdentifier, buildLabel);
        return await store.EnqueueAsync(draft.RootElement, "alice", default);
    }

    // Claims the next queued job and returns its post-claim (Building) etag for a subsequent completion.
    private static async Task<WorkflowEtag> ClaimAndGetEtagAsync(INativeBuildJobStore store)
    {
        using ParsedJsonDocument<NativeBuildJob>? claimed = await store.ClaimNextQueuedAsync("worker", TimeSpan.FromMinutes(5), default);
        claimed.ShouldNotBeNull();
        return claimed.RootElement.EtagValue;
    }

    // Wraps an opaque page token's UTF-8 as the JSON string value a request carries it as (mirroring HTTP).
    private static ParsedJsonDocument<JsonString> AsPageToken(ReadOnlySpan<byte> tokenUtf8)
    {
        byte[] quoted = new byte[tokenUtf8.Length + 2];
        quoted[0] = (byte)'"';
        tokenUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<JsonString>.Parse(quoted);
    }
}