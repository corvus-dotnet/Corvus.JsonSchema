// <copyright file="WorkflowDeploymentStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Linq;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IWorkflowDeploymentStore"/> must satisfy (ADR 0055): an idempotent enqueue-upsert
/// keyed by the target tuple's derived id, a point read, the atomic claim of the oldest queued deployment (which two
/// concurrent workers must never win for the same deployment), the terminal completion transition under optimistic
/// concurrency (so a deployment cannot be completed twice or from a non-deploying state), the Deployed target predicate, and
/// oldest-first list / keyset paging / count. A backend's test project derives a concrete <see cref="TestClassAttribute"/>
/// and implements <see cref="CreateStoreAsync"/>; the in-memory store is the reference implementation and runs the same
/// suite.
/// </summary>
public abstract class WorkflowDeploymentStoreConformance
{
    // A lease long enough that a just-claimed deployment is never reclaimed within a synchronous test flow (the clock only
    // moves where a test advances a StepClock), so the claim/complete tests are unaffected by the lease; the reclaim tests
    // use explicit short TTLs and advance the clock past them.
    private static readonly TimeSpan DefaultLeaseTtl = TimeSpan.FromMinutes(5);

    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IWorkflowDeploymentStore> CreateStoreAsync(TimeProvider timeProvider);

    /// <summary>Disposes any stores created during the test.</summary>
    /// <returns>A task that completes when cleanup is done.</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (IAsyncDisposable disposable in this.disposables)
        {
            await disposable.DisposeAsync();
        }

        this.disposables.Clear();
    }

    [TestMethod]
    public async Task An_enqueued_deployment_round_trips_through_get_and_list()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string id;
        using (ParsedJsonDocument<WorkflowDeployment> enqueued = await EnqueueAsync(store, WorkflowDeployment.Draft("checkout", 3, "production", "linux-x64"), "alice", default))
        {
            id = enqueued.RootElement.IdValue;
            id.ShouldBe(WorkflowDeployment.DeriveId("checkout", 3, "production", "linux-x64"));
            enqueued.RootElement.BaseWorkflowIdValue.ShouldBe("checkout");
            enqueued.RootElement.VersionNumberValue.ShouldBe(3);
            enqueued.RootElement.EnvironmentValue.ShouldBe("production");
            enqueued.RootElement.RuntimeIdentifierValue.ShouldBe("linux-x64");
            enqueued.RootElement.StatusValue.ShouldBe("Queued");
            enqueued.RootElement.EtagValue.IsNone.ShouldBeFalse();
            enqueued.RootElement.StartedAtValue.ShouldBeNull();
            enqueued.RootElement.CompletedAtValue.ShouldBeNull();
            enqueued.RootElement.ClaimedByOrNull.ShouldBeNull();
            enqueued.RootElement.FailureReasonOrNull.ShouldBeNull();
            enqueued.RootElement.FunctionUrlOrNull.ShouldBeNull();
        }

        using (ParsedJsonDocument<WorkflowDeployment>? fetched = await store.GetAsync(id, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.RuntimeIdentifierValue.ShouldBe("linux-x64");
        }

        using (PooledDocumentList<WorkflowDeployment> list = await store.ListAsync(default, default))
        {
            list.Select(d => d.IdValue).ShouldBe([id]);
        }

        (await store.GetAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Enqueue_is_idempotent_per_target_tuple()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string first = await this.EnqueueAsync(store, "checkout", 3, "production", "linux-x64", "alice");
        string second = await this.EnqueueAsync(store, "checkout", 3, "production", "linux-x64", "bob");

        // The id is derived from the target tuple, so a repeated enqueue for the same target is the same deployment, not a duplicate.
        second.ShouldBe(first);
        using (PooledDocumentList<WorkflowDeployment> list = await store.ListAsync(default, default))
        {
            list.Count.ShouldBe(1);
        }

        // A different tuple is a different deployment.
        string other = await this.EnqueueAsync(store, "checkout", 3, "production", "win-x64", "alice");
        other.ShouldNotBe(first);
        (await store.CountAsync(default, 10, default)).ShouldBe((2, false));
    }

    [TestMethod]
    public async Task Re_enqueue_resets_a_completed_deployment_to_queued()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string id = await this.EnqueueAsync(store, "checkout", 1, "production", "linux-x64", "alice");

        // Drive it to Deployed.
        WorkflowEtag deployingEtag;
        using (ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker-1", DefaultLeaseTtl, default))
        {
            deployingEtag = claimed!.RootElement.EtagValue;
        }

        using (await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/checkout"), deployingEtag, default))
        {
        }

        // Re-enqueue the same target: it resets to Queued and clears the in-flight/terminal fields (a redeploy).
        using ParsedJsonDocument<WorkflowDeployment> reset = await EnqueueAsync(store, WorkflowDeployment.Draft("checkout", 1, "production", "linux-x64"), "alice", default);
        reset.RootElement.IdValue.ShouldBe(id);
        reset.RootElement.StatusValue.ShouldBe("Queued");
        reset.RootElement.StartedAtValue.ShouldBeNull();
        reset.RootElement.CompletedAtValue.ShouldBeNull();
        reset.RootElement.ClaimedByOrNull.ShouldBeNull();
        reset.RootElement.FailureReasonOrNull.ShouldBeNull();
        reset.RootElement.FunctionUrlOrNull.ShouldBeNull();
    }

    [TestMethod]
    public async Task Claiming_moves_the_oldest_queued_to_deploying_then_returns_null()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IWorkflowDeploymentStore store = await this.NewStoreAsync(clock);

        string first = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");
        clock.Advance(TimeSpan.FromSeconds(1));
        string second = await this.EnqueueAsync(store, "w", 2, "production", "linux-x64", "alice");
        clock.Advance(TimeSpan.FromSeconds(1));
        string third = await this.EnqueueAsync(store, "w", 3, "production", "linux-x64", "alice");

        using (ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker-7", DefaultLeaseTtl, default))
        {
            claimed.ShouldNotBeNull();
            claimed!.RootElement.IdValue.ShouldBe(first); // the oldest queued
            claimed.RootElement.StatusValue.ShouldBe("Deploying");
            claimed.RootElement.ClaimedByOrNull.ShouldBe("worker-7");
            claimed.RootElement.StartedAtValue.ShouldNotBeNull();
        }

        (await this.ClaimIdAsync(store)).ShouldBe(second);
        (await this.ClaimIdAsync(store)).ShouldBe(third);

        // Nothing left queued.
        (await store.ClaimNextQueuedAsync("worker-7", DefaultLeaseTtl, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Two_concurrent_claims_never_return_the_same_deployment()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IWorkflowDeploymentStore store = await this.NewStoreAsync(clock);

        const int deployments = 16;
        var enqueued = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < deployments; i++)
        {
            enqueued.Add(await this.EnqueueAsync(store, "w", i + 1, "production", "linux-x64", "alice"));
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        // Drain concurrently from several workers: each claimed id must be unique (no deployment claimed twice).
        var claimedIds = new ConcurrentBag<string>();
        async Task DrainAsync(string worker)
        {
            while (true)
            {
                using ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync(worker, DefaultLeaseTtl, default);
                if (claimed is null)
                {
                    return;
                }

                claimedIds.Add(claimed.RootElement.IdValue);
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 4).Select(i => DrainAsync($"worker-{i}")));

        // A final single-threaded sweep picks up anything a worker missed under contention (a lost race that exhausted its
        // bounded retries), so the union is complete; the correctness property is that no id was ever returned twice.
        string? id;
        while ((id = await this.ClaimIdAsync(store)) is not null)
        {
            claimedIds.Add(id);
        }

        claimedIds.Count.ShouldBe(deployments); // every deployment claimed exactly once — no duplicates
        claimedIds.ToHashSet(StringComparer.Ordinal).SetEquals(enqueued).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Completing_records_deployed_with_a_function_url()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string id = await this.EnqueueAsync(store, "checkout", 3, "production", "linux-x64", "alice");
        WorkflowEtag deployingEtag = await this.ClaimEtagAsync(store);

        using ParsedJsonDocument<WorkflowDeployment>? completed = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/checkout"), deployingEtag, default);
        completed.ShouldNotBeNull();
        completed!.RootElement.StatusValue.ShouldBe("Deployed");
        completed.RootElement.CompletedAtValue.ShouldNotBeNull();
        completed.RootElement.FunctionUrlOrNull.ShouldBe("https://fn.example/checkout");
        completed.RootElement.FailureReasonOrNull.ShouldBeNull();

        // The target content carries through the completion unchanged.
        completed.RootElement.BaseWorkflowIdValue.ShouldBe("checkout");
        completed.RootElement.VersionNumberValue.ShouldBe(3);
        completed.RootElement.RuntimeIdentifierValue.ShouldBe("linux-x64");
    }

    [TestMethod]
    public async Task Completing_records_failed_with_a_reason()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string id = await this.EnqueueAsync(store, "checkout", 3, "production", "linux-x64", "alice");
        WorkflowEtag deployingEtag = await this.ClaimEtagAsync(store);

        using ParsedJsonDocument<WorkflowDeployment>? failed = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Failed, FailureReason: "endpoint provisioning failed"), deployingEtag, default);
        failed.ShouldNotBeNull();
        failed!.RootElement.StatusValue.ShouldBe("Failed");
        failed.RootElement.FailureReasonOrNull.ShouldBe("endpoint provisioning failed");
        failed.RootElement.FunctionUrlOrNull.ShouldBeNull();
        failed.RootElement.CompletedAtValue.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Completing_a_missing_deployment_returns_null()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        (await store.CompleteAsync("missing", new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/missing"), WorkflowEtag.None, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_etag_on_complete_conflicts_so_a_deployment_cannot_be_completed_twice()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string id = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");
        WorkflowEtag deployingEtag = await this.ClaimEtagAsync(store);

        // A completer racing on the wrong (stale) etag conflicts while the deployment is still deploying.
        await Should.ThrowAsync<WorkflowDeploymentConflictException>(async () =>
            await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/w"), new WorkflowEtag("stale-etag"), default));

        // The correct etag completes it.
        using ParsedJsonDocument<WorkflowDeployment>? completed = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/w"), deployingEtag, default);
        completed!.RootElement.StatusValue.ShouldBe("Deployed");
    }

    [TestMethod]
    public async Task Completing_a_deployment_that_is_not_deploying_is_a_wrong_state()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string id = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");

        // The deployment is still Queued (never claimed), so it cannot be completed — even unconditionally.
        await Should.ThrowAsync<WorkflowDeploymentStateException>(async () =>
            await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/w"), WorkflowEtag.None, default));
    }

    [TestMethod]
    public async Task An_orphaned_deploying_deployment_is_reclaimed_once_its_lease_expires()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IWorkflowDeploymentStore store = await this.NewStoreAsync(clock);
        string id = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");

        // worker-1 claims with a 60s lease, then "crashes" (never renews, never completes).
        WorkflowEtag firstClaim;
        using (ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker-1", TimeSpan.FromSeconds(60), default))
        {
            claimed!.RootElement.ClaimedByOrNull.ShouldBe("worker-1");
            firstClaim = claimed.RootElement.EtagValue;
        }

        // While the lease is live the Deploying deployment is not reclaimable — a second worker finds nothing to claim.
        clock.Advance(TimeSpan.FromSeconds(30));
        (await store.ClaimNextQueuedAsync("worker-2", TimeSpan.FromSeconds(60), default)).ShouldBeNull();

        // Once the lease expires, worker-2 reclaims the orphan: the same deployment, re-Deploying under the new claimant,
        // with a fresh startedAt and a bumped etag that supersedes the crashed worker.
        clock.Advance(TimeSpan.FromSeconds(31));
        using ParsedJsonDocument<WorkflowDeployment>? reclaimed = await store.ClaimNextQueuedAsync("worker-2", TimeSpan.FromSeconds(60), default);
        reclaimed.ShouldNotBeNull();
        reclaimed!.RootElement.IdValue.ShouldBe(id);
        reclaimed.RootElement.StatusValue.ShouldBe("Deploying");
        reclaimed.RootElement.ClaimedByOrNull.ShouldBe("worker-2");
        reclaimed.RootElement.EtagValue.ShouldNotBe(firstClaim);
    }

    [TestMethod]
    public async Task A_reclaim_supersedes_the_orphaned_workers_completion()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IWorkflowDeploymentStore store = await this.NewStoreAsync(clock);
        string id = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");

        WorkflowEtag orphanEtag;
        using (ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker-1", TimeSpan.FromSeconds(60), default))
        {
            orphanEtag = claimed!.RootElement.EtagValue;
        }

        // The lease expires and worker-2 reclaims it, taking a new etag.
        clock.Advance(TimeSpan.FromSeconds(61));
        WorkflowEtag reclaimEtag;
        using (ParsedJsonDocument<WorkflowDeployment>? reclaimed = await store.ClaimNextQueuedAsync("worker-2", TimeSpan.FromSeconds(60), default))
        {
            reclaimEtag = reclaimed!.RootElement.EtagValue;
        }

        // The orphaned worker-1 finishing late completes on its now-stale etag → conflict (its work is abandoned). Correctness
        // rests on the etag, not the lease: only one completion wins even during a lease handoff.
        await Should.ThrowAsync<WorkflowDeploymentConflictException>(async () =>
            await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/w"), orphanEtag, default));

        // worker-2 completes on the reclaim etag → succeeds.
        using ParsedJsonDocument<WorkflowDeployment>? completed = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/w"), reclaimEtag, default);
        completed!.RootElement.StatusValue.ShouldBe("Deployed");
    }

    [TestMethod]
    public async Task Renewing_the_lease_extends_it_so_the_deployment_is_not_reclaimed()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IWorkflowDeploymentStore store = await this.NewStoreAsync(clock);
        string id = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");

        WorkflowEtag etag;
        using (ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker-1", TimeSpan.FromSeconds(60), default))
        {
            etag = claimed!.RootElement.EtagValue;
        }

        // Near expiry the owner renews for another 60s: status/claimedBy/startedAt carry through, the etag advances.
        clock.Advance(TimeSpan.FromSeconds(50));
        WorkflowEtag renewedEtag;
        using (ParsedJsonDocument<WorkflowDeployment>? renewed = await store.RenewLeaseAsync(id, etag, TimeSpan.FromSeconds(60), default))
        {
            renewed.ShouldNotBeNull();
            renewed!.RootElement.StatusValue.ShouldBe("Deploying");
            renewed.RootElement.ClaimedByOrNull.ShouldBe("worker-1");
            renewedEtag = renewed.RootElement.EtagValue;
            renewedEtag.ShouldNotBe(etag);
        }

        // Past the ORIGINAL lease but within the renewed one, the deployment is still not reclaimable.
        clock.Advance(TimeSpan.FromSeconds(20));
        (await store.ClaimNextQueuedAsync("worker-2", TimeSpan.FromSeconds(60), default)).ShouldBeNull();

        // The owner still completes on the renewed etag.
        using ParsedJsonDocument<WorkflowDeployment>? completed = await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/w"), renewedEtag, default);
        completed!.RootElement.StatusValue.ShouldBe("Deployed");
    }

    [TestMethod]
    public async Task Renewing_with_a_stale_etag_conflicts_after_a_reclaim()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IWorkflowDeploymentStore store = await this.NewStoreAsync(clock);
        string id = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");

        WorkflowEtag orphanEtag;
        using (ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker-1", TimeSpan.FromSeconds(60), default))
        {
            orphanEtag = claimed!.RootElement.EtagValue;
        }

        clock.Advance(TimeSpan.FromSeconds(61));
        using (await store.ClaimNextQueuedAsync("worker-2", TimeSpan.FromSeconds(60), default))
        {
        }

        // worker-1's heartbeat lands after it was reclaimed: its expected etag no longer matches, so the renewal conflicts —
        // which is how a superseded worker learns it lost the lease and should stop deploying.
        await Should.ThrowAsync<WorkflowDeploymentConflictException>(async () =>
            await store.RenewLeaseAsync(id, orphanEtag, TimeSpan.FromSeconds(60), default));
    }

    [TestMethod]
    public async Task Renewing_a_missing_deployment_returns_null()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        (await store.RenewLeaseAsync("missing", WorkflowEtag.None, TimeSpan.FromSeconds(60), default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Renewing_a_deployment_that_is_not_deploying_is_a_wrong_state()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string id = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");

        // The deployment is still Queued (never claimed), so there is no lease to renew — even unconditionally.
        await Should.ThrowAsync<WorkflowDeploymentStateException>(async () =>
            await store.RenewLeaseAsync(id, WorkflowEtag.None, TimeSpan.FromSeconds(60), default));
    }

    [TestMethod]
    public async Task Is_deployed_only_after_the_deploy_reaches_deployed()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string id = await this.EnqueueAsync(store, "checkout", 3, "production", "linux-x64", "alice");

        (await store.IsDeployedAsync("checkout", 3, "production", "linux-x64", default)).ShouldBeFalse(); // Queued
        WorkflowEtag deployingEtag = await this.ClaimEtagAsync(store);
        (await store.IsDeployedAsync("checkout", 3, "production", "linux-x64", default)).ShouldBeFalse(); // Deploying

        using (await store.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/checkout"), deployingEtag, default))
        {
        }

        (await store.IsDeployedAsync("checkout", 3, "production", "linux-x64", default)).ShouldBeTrue(); // Deployed

        // A never-deployed target is not deployed, and a Failed one is not deployed.
        (await store.IsDeployedAsync("checkout", 3, "production", "win-x64", default)).ShouldBeFalse();
        string failedId = await this.EnqueueAsync(store, "billing", 1, "production", "linux-x64", "alice");
        WorkflowEtag failEtag = await this.ClaimEtagAsync(store);
        using (await store.CompleteAsync(failedId, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Failed, FailureReason: "nope"), failEtag, default))
        {
        }

        (await store.IsDeployedAsync("billing", 1, "production", "linux-x64", default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Deployments_list_oldest_first()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IWorkflowDeploymentStore store = await this.NewStoreAsync(clock);

        string first = await this.EnqueueAsync(store, "w", 1, "production", "linux-x64", "alice");
        clock.Advance(TimeSpan.FromSeconds(1));
        string second = await this.EnqueueAsync(store, "w", 2, "production", "linux-x64", "alice");
        clock.Advance(TimeSpan.FromSeconds(1));
        string third = await this.EnqueueAsync(store, "w", 3, "production", "linux-x64", "alice");

        (await this.IdsAsync(store, default)).ShouldBe([first, second, third]);
    }

    [TestMethod]
    public async Task Listing_filters_by_status_and_target()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string checkoutProdLinux = await this.EnqueueAsync(store, "checkout", 1, "production", "linux-x64", "alice");
        string checkoutProdWin = await this.EnqueueAsync(store, "checkout", 1, "production", "win-x64", "alice");
        string checkoutStaging = await this.EnqueueAsync(store, "checkout", 2, "staging", "linux-x64", "alice");
        string billingProd = await this.EnqueueAsync(store, "billing", 1, "production", "linux-x64", "alice");

        // By base workflow.
        (await this.IdsAsync(store, new WorkflowDeploymentQuery(BaseWorkflowId: "checkout"))).ShouldBe([checkoutProdLinux, checkoutProdWin, checkoutStaging], ignoreOrder: true);

        // By environment.
        (await this.IdsAsync(store, new WorkflowDeploymentQuery(Environment: "production"))).ShouldBe([checkoutProdLinux, checkoutProdWin, billingProd], ignoreOrder: true);

        // By runtime identifier.
        (await this.IdsAsync(store, new WorkflowDeploymentQuery(RuntimeIdentifier: "win-x64"))).ShouldBe([checkoutProdWin]);

        // By the full target tuple.
        (await this.IdsAsync(store, new WorkflowDeploymentQuery(BaseWorkflowId: "checkout", VersionNumber: 1, Environment: "production", RuntimeIdentifier: "linux-x64"))).ShouldBe([checkoutProdLinux]);

        // By status: all are Queued until claimed; claim + complete one and filter.
        WorkflowEtag etag = await this.ClaimEtagAsync(store); // claims the oldest — checkoutProdLinux
        using (await store.CompleteAsync(checkoutProdLinux, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/checkout"), etag, default))
        {
        }

        (await this.IdsAsync(store, new WorkflowDeploymentQuery(Status: WorkflowDeploymentStatus.Deployed))).ShouldBe([checkoutProdLinux]);
        (await this.IdsAsync(store, new WorkflowDeploymentQuery(Status: WorkflowDeploymentStatus.Queued))).ShouldBe([checkoutProdWin, checkoutStaging, billingProd], ignoreOrder: true);
    }

    [TestMethod]
    public async Task Listing_keyset_pages_oldest_first_without_gaps_or_duplicates()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IWorkflowDeploymentStore store = await this.NewStoreAsync(clock);

        var expected = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            expected.Add(await this.EnqueueAsync(store, "w", i + 1, "production", "linux-x64", "alice"));
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using WorkflowDeploymentPage page = await store.ListAsync(default, 3, tokenDoc?.RootElement ?? default, default);
            page.Deployments.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (WorkflowDeployment deployment in page.Deployments)
            {
                seen.Add(deployment.IdValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        pages.ShouldBe(3); // 8 items, 3 per page
        seen.ShouldBe(expected);

        // A malformed token is rejected (rather than silently restarting from the first page).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<JsonString> badToken = AsPageToken("this~is~not~a~token"u8);
            using WorkflowDeploymentPage bad = await store.ListAsync(default, 3, badToken.RootElement, default);
        });
    }

    [TestMethod]
    public async Task Counting_is_bounded_by_the_cap_reporting_capped_only_beyond_it()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        await this.EnqueueAsync(store, "checkout", 1, "production", "linux-x64", "alice");
        await this.EnqueueAsync(store, "checkout", 1, "production", "win-x64", "alice");
        await this.EnqueueAsync(store, "checkout", 2, "staging", "linux-x64", "alice");

        (await store.CountAsync(default, 10, default)).ShouldBe((3, false));
        (await store.CountAsync(default, 3, default)).ShouldBe((3, false));
        (await store.CountAsync(default, 2, default)).ShouldBe((2, true));
        (await store.CountAsync(default, 1, default)).ShouldBe((1, true));

        IWorkflowDeploymentStore empty = await this.NewStoreAsync();
        (await empty.CountAsync(default, 5, default)).ShouldBe((0, false));
    }

    [TestMethod]
    public async Task Counting_honours_the_same_filters_as_the_list()
    {
        IWorkflowDeploymentStore store = await this.NewStoreAsync();
        string checkoutProdLinux = await this.EnqueueAsync(store, "checkout", 1, "production", "linux-x64", "alice");
        await this.EnqueueAsync(store, "checkout", 1, "production", "win-x64", "alice");
        await this.EnqueueAsync(store, "checkout", 2, "staging", "linux-x64", "alice");
        await this.EnqueueAsync(store, "billing", 1, "production", "linux-x64", "alice");

        // By environment.
        (await store.CountAsync(new WorkflowDeploymentQuery(Environment: "production"), 10, default)).ShouldBe((3, false));

        // By base workflow.
        (await store.CountAsync(new WorkflowDeploymentQuery(BaseWorkflowId: "checkout"), 10, default)).ShouldBe((3, false));

        // By runtime identifier.
        (await store.CountAsync(new WorkflowDeploymentQuery(RuntimeIdentifier: "linux-x64"), 10, default)).ShouldBe((3, false));

        // By status (claim + complete one, then count each state).
        WorkflowEtag etag = await this.ClaimEtagAsync(store); // the oldest — checkoutProdLinux
        using (await store.CompleteAsync(checkoutProdLinux, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, "https://fn.example/checkout"), etag, default))
        {
        }

        (await store.CountAsync(new WorkflowDeploymentQuery(Status: WorkflowDeploymentStatus.Queued), 10, default)).ShouldBe((3, false));
        (await store.CountAsync(new WorkflowDeploymentQuery(Status: WorkflowDeploymentStatus.Deployed), 10, default)).ShouldBe((1, false));

        // Filter + cap compose: three Queued, capped at 2.
        (await store.CountAsync(new WorkflowDeploymentQuery(Status: WorkflowDeploymentStatus.Queued), 2, default)).ShouldBe((2, true));
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

    // Enqueues the (pooled, disposable) draft deployment, disposing the draft once the store has read it; the enqueued
    // document is returned for the caller to assert on and dispose.
    private static async Task<ParsedJsonDocument<WorkflowDeployment>> EnqueueAsync(IWorkflowDeploymentStore store, ParsedJsonDocument<WorkflowDeployment> draft, string actor, CancellationToken cancellationToken = default)
    {
        using (draft)
        {
            return await store.EnqueueAsync(draft.RootElement, actor, cancellationToken);
        }
    }

    private async ValueTask<IWorkflowDeploymentStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IWorkflowDeploymentStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }

    private async ValueTask<string> EnqueueAsync(IWorkflowDeploymentStore store, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, string actor)
    {
        using ParsedJsonDocument<WorkflowDeployment> enqueued = await EnqueueAsync(store, WorkflowDeployment.Draft(baseWorkflowId, versionNumber, environment, runtimeIdentifier), actor, default);
        return enqueued.RootElement.IdValue;
    }

    // Claims the next queued deployment and returns its id (or null if none), disposing the claimed document.
    private async ValueTask<string?> ClaimIdAsync(IWorkflowDeploymentStore store)
    {
        using ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker", DefaultLeaseTtl, default);
        return claimed?.RootElement.IdValue;
    }

    // Claims the next queued deployment and returns the etag it was stamped with (for a subsequent optimistic completion).
    private async ValueTask<WorkflowEtag> ClaimEtagAsync(IWorkflowDeploymentStore store)
    {
        using ParsedJsonDocument<WorkflowDeployment>? claimed = await store.ClaimNextQueuedAsync("worker", DefaultLeaseTtl, default);
        return claimed!.RootElement.EtagValue;
    }

    private async ValueTask<List<string>> IdsAsync(IWorkflowDeploymentStore store, WorkflowDeploymentQuery query)
    {
        using PooledDocumentList<WorkflowDeployment> list = await store.ListAsync(query, default);
        return list.Select(d => d.IdValue).ToList();
    }

    // A controllable clock so the oldest-first ordering test is deterministic even where the id tiebreak is not creation order.
    private sealed class StepClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}