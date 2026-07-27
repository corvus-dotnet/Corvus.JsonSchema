// <copyright file="WorkflowDeploymentInMemoryStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Behavioural tests for the reference <see cref="InMemoryWorkflowDeploymentStore"/> (ADR 0055): enqueue is idempotent per
/// target tuple, the worker claim picks the oldest queued deployment, completion is state- and etag-gated, target-readiness
/// tracks the terminal Deployed state, and the list pages oldest-first.
/// </summary>
[TestClass]
public sealed class WorkflowDeploymentInMemoryStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Enqueue_creates_a_queued_deployment_stamped_from_the_target_tuple()
    {
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(new TestTimeProvider(Start));

        using ParsedJsonDocument<WorkflowDeployment> deployment = await EnqueueAsync(store, "wf", 3, "production", "linux-x64");

        WorkflowDeployment value = deployment.RootElement;
        value.HasStatus(WorkflowDeploymentStatus.Queued).ShouldBeTrue();
        value.IdValue.ShouldBe(WorkflowDeployment.DeriveId("wf", 3, "production", "linux-x64"));
        value.BaseWorkflowIdValue.ShouldBe("wf");
        value.VersionNumberValue.ShouldBe(3);
        value.EnvironmentValue.ShouldBe("production");
        value.RuntimeIdentifierValue.ShouldBe("linux-x64");
        value.CreatedAtValue.ShouldBe(Start);

        // A fresh deployment carries no progress fields.
        value.StartedAtValue.ShouldBeNull();
        value.CompletedAtValue.ShouldBeNull();
        value.FailureReasonOrNull.ShouldBeNull();
        value.FunctionUrlOrNull.ShouldBeNull();
        value.ClaimedByOrNull.ShouldBeNull();
    }

    [TestMethod]
    public async Task Enqueue_is_idempotent_per_target_tuple()
    {
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(new TestTimeProvider(Start));

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        // The two enqueues target the same tuple, so they map to one row rather than two.
        using PooledDocumentList<WorkflowDeployment> all = await store.ListAsync(default, default);
        all.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Enqueue_resets_an_existing_completed_deployment_back_to_queued()
    {
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(new TestTimeProvider(Start));
        string id = WorkflowDeployment.DeriveId("wf", 1, "dev", "linux-x64");

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        WorkflowEtag etag = await ClaimAndGetEtagAsync(store);
        using (await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/wf"), etag, default))
        {
        }

        // Re-enqueuing the same target is a redeploy: it resets the deployment to Queued and clears the progress fields.
        using ParsedJsonDocument<WorkflowDeployment> requeued = await EnqueueAsync(store, "wf", 1, "dev", "linux-x64");
        WorkflowDeployment value = requeued.RootElement;
        value.HasStatus(WorkflowDeploymentStatus.Queued).ShouldBeTrue();
        value.StartedAtValue.ShouldBeNull();
        value.CompletedAtValue.ShouldBeNull();
        value.FailureReasonOrNull.ShouldBeNull();
        value.FunctionUrlOrNull.ShouldBeNull();
        value.ClaimedByOrNull.ShouldBeNull();
    }

    [TestMethod]
    public async Task ClaimNextQueued_moves_the_oldest_queued_deployment_to_deploying_then_returns_null()
    {
        var time = new TestTimeProvider(Start);
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(time);

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        time.Advance(TimeSpan.FromMinutes(1));
        using (await EnqueueAsync(store, "wf", 2, "dev", "linux-x64"))
        {
        }

        // The oldest queued deployment is claimed first and transitions to Deploying with the worker + start stamps.
        using (ParsedJsonDocument<WorkflowDeployment>? first = await store.ClaimNextQueuedAsync("worker-a", TimeSpan.FromMinutes(5), default))
        {
            first.ShouldNotBeNull();
            first.RootElement.VersionNumberValue.ShouldBe(1);
            first.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying).ShouldBeTrue();
            first.RootElement.ClaimedByOrNull.ShouldBe("worker-a");
            first.RootElement.StartedAtValue.ShouldNotBeNull();
        }

        using (ParsedJsonDocument<WorkflowDeployment>? second = await store.ClaimNextQueuedAsync("worker-b", TimeSpan.FromMinutes(5), default))
        {
            second.ShouldNotBeNull();
            second.RootElement.VersionNumberValue.ShouldBe(2);
        }

        // Nothing is queued now, so the claim yields null.
        using ParsedJsonDocument<WorkflowDeployment>? none = await store.ClaimNextQueuedAsync("worker-c", TimeSpan.FromMinutes(5), default);
        none.ShouldBeNull();
    }

    [TestMethod]
    public async Task Complete_transitions_deploying_to_deployed()
    {
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(new TestTimeProvider(Start));
        string id = WorkflowDeployment.DeriveId("wf", 1, "dev", "linux-x64");

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        WorkflowEtag etag = await ClaimAndGetEtagAsync(store);

        using ParsedJsonDocument<WorkflowDeployment>? done = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/wf"), etag, default);
        done.ShouldNotBeNull();
        done.RootElement.HasStatus(WorkflowDeploymentStatus.Deployed).ShouldBeTrue();
        done.RootElement.CompletedAtValue.ShouldNotBeNull();
        done.RootElement.FunctionUrlOrNull.ShouldBe("https://fn.example/wf");
        done.RootElement.FailureReasonOrNull.ShouldBeNull();
    }

    [TestMethod]
    public async Task Complete_transitions_deploying_to_failed_with_a_reason()
    {
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(new TestTimeProvider(Start));
        string id = WorkflowDeployment.DeriveId("wf", 1, "dev", "linux-x64");

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        WorkflowEtag etag = await ClaimAndGetEtagAsync(store);

        using ParsedJsonDocument<WorkflowDeployment>? done = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Failed, FailureReason: "the endpoint provisioning failed"), etag, default);
        done.ShouldNotBeNull();
        done.RootElement.HasStatus(WorkflowDeploymentStatus.Failed).ShouldBeTrue();
        done.RootElement.CompletedAtValue.ShouldNotBeNull();
        done.RootElement.FailureReasonOrNull.ShouldBe("the endpoint provisioning failed");
        done.RootElement.FunctionUrlOrNull.ShouldBeNull();
    }

    [TestMethod]
    public async Task Complete_with_a_stale_etag_throws_a_conflict()
    {
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(new TestTimeProvider(Start));
        string id = WorkflowDeployment.DeriveId("wf", 1, "dev", "linux-x64");

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        // Claim it (so it is Deploying), then complete under an etag that no longer matches.
        _ = await ClaimAndGetEtagAsync(store);

        await Should.ThrowAsync<WorkflowDeploymentConflictException>(async () =>
        {
            _ = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/wf"), new WorkflowEtag("stale"), default);
        });
    }

    [TestMethod]
    public async Task Complete_from_a_non_deploying_state_throws_a_state_error()
    {
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(new TestTimeProvider(Start));
        string id = WorkflowDeployment.DeriveId("wf", 1, "dev", "linux-x64");

        // Enqueued but never claimed, so the deployment is Queued rather than Deploying.
        WorkflowEtag etag;
        using (ParsedJsonDocument<WorkflowDeployment> queued = await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
            etag = queued.RootElement.EtagValue;
        }

        await Should.ThrowAsync<WorkflowDeploymentStateException>(async () =>
        {
            _ = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/wf"), etag, default);
        });
    }

    [TestMethod]
    public async Task IsDeployed_is_true_only_after_the_deploy_is_deployed()
    {
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(new TestTimeProvider(Start));
        string id = WorkflowDeployment.DeriveId("wf", 1, "dev", "linux-x64");

        // Absent target.
        (await store.IsDeployedAsync("wf", 1, "dev", "linux-x64", default)).ShouldBeFalse();

        using (await EnqueueAsync(store, "wf", 1, "dev", "linux-x64"))
        {
        }

        // Queued.
        (await store.IsDeployedAsync("wf", 1, "dev", "linux-x64", default)).ShouldBeFalse();

        WorkflowEtag etag = await ClaimAndGetEtagAsync(store);

        // Deploying.
        (await store.IsDeployedAsync("wf", 1, "dev", "linux-x64", default)).ShouldBeFalse();

        using (await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/wf"), etag, default))
        {
        }

        // Deployed.
        (await store.IsDeployedAsync("wf", 1, "dev", "linux-x64", default)).ShouldBeTrue();

        // A different target is not deployed.
        (await store.IsDeployedAsync("wf", 2, "dev", "linux-x64", default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task List_and_paging_return_deployments_oldest_first()
    {
        var time = new TestTimeProvider(Start);
        IWorkflowDeploymentStore store = new InMemoryWorkflowDeploymentStore(time);

        for (int version = 1; version <= 3; version++)
        {
            using (await EnqueueAsync(store, "wf", version, "dev", "linux-x64"))
            {
            }

            time.Advance(TimeSpan.FromMinutes(1));
        }

        // The full read is oldest-first by (createdAt, id).
        using (PooledDocumentList<WorkflowDeployment> all = await store.ListAsync(default, default))
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
            using WorkflowDeploymentPage page = await store.ListAsync(default, 2, tokenDoc?.RootElement ?? default, default);
            for (int i = 0; i < page.Deployments.Count; i++)
            {
                seen.Add(page.Deployments[i].VersionNumberValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
        }
        while (token is not null);

        seen.ShouldBe([1, 2, 3]);
    }

    // Enqueues a Queued deployment for the target tuple and returns the store's pooled record.
    private static async Task<ParsedJsonDocument<WorkflowDeployment>> EnqueueAsync(IWorkflowDeploymentStore store, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier)
    {
        using ParsedJsonDocument<WorkflowDeployment> draft = WorkflowDeployment.Draft(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        return await store.EnqueueAsync(draft.RootElement, "alice", default);
    }

    // Claims the next queued deployment and returns its post-claim (Deploying) etag for a subsequent completion.
    private static async Task<WorkflowEtag> ClaimAndGetEtagAsync(IWorkflowDeploymentStore store)
    {
        using ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker", TimeSpan.FromMinutes(5), default);
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