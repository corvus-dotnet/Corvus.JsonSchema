// <copyright file="RunnerRunCoordinatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Tests;

/// <summary>
/// Coverage of the runner API's store-facing rules (ADR 0065): which runs a principal is offered, that the lease it is
/// granted is owned by that principal and no other, and that a lease which has stopped being current can neither renew
/// itself nor authorise an operation on the run.
/// </summary>
[TestClass]
public sealed class RunnerRunCoordinatorTests
{
    private const string Runner = "runner-a";
    private const string Peer = "runner-b";
    private const string Production = "production";
    private const string Staging = "staging";
    private const string Version = "adopt-v3";

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task A_pending_run_is_claimed_with_its_workflow_environment_and_lease()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);

        ClaimedRunRecord? claimed = await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default);

        claimed.ShouldNotBeNull();
        claimed.Value.RunId.ShouldBe(new WorkflowRunId("run-1"));
        claimed.Value.WorkflowId.ShouldBe(Version);
        claimed.Value.Environment.ShouldBe(Production);
        claimed.Value.Lease.Token.ShouldNotBeNullOrEmpty();
        claimed.Value.Lease.Epoch.ShouldBeGreaterThan(0);
        claimed.Value.Lease.ExpiresAt.ShouldBe(T0 + TimeSpan.FromMinutes(1));
    }

    [TestMethod]
    public async Task A_due_run_in_an_environment_the_principal_is_not_bound_to_is_never_resumed()
    {
        // The same non-disclosure rule as dispatch, and it has to be: a timer firing in another tenant's environment
        // must not become a way to reach that tenant's run.
        var fixture = await Fixture.EmptyAsync();
        await fixture.SeedWaitingAsync("run-1", Staging, WorkflowWait.Timer(T0));

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        (await fixture.Coordinator.ClaimDueAsync(Runner, [Version], null, null, default)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_message_for_a_run_in_an_environment_the_principal_is_not_bound_to_is_never_delivered()
    {
        // Two environments can have runs awaiting the same channel name. The channel is not a namespace, so the
        // binding is what keeps one tenant's message from resuming another's run.
        var fixture = await Fixture.EmptyAsync();
        await fixture.SeedWaitingAsync("run-1", Staging, WorkflowWait.Message("orders", null));

        (await fixture.Coordinator.ClaimAwaitingAsync(Runner, "orders", null, [Version], null, null, default)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_principal_bound_to_nothing_is_offered_no_waiting_run()
    {
        // A runner whose authorization is pending or revoked resolves to no environments, and is answered as though
        // there were nothing to resume rather than told that there is.
        var fixture = await Fixture.EmptyAsync();
        await fixture.SeedWaitingAsync("run-1", Production, WorkflowWait.Message("orders", null));

        (await fixture.Coordinator.ClaimAwaitingAsync("stranger", "orders", null, [Version], null, null, default)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_sweep_claims_no_more_than_the_deployment_allows()
    {
        var fixture = await Fixture.EmptyAsync(new RunnerApiOptions { MaximumSweep = 2 });
        await fixture.SeedWaitingAsync("run-1", Production, WorkflowWait.Message("orders", null));
        await fixture.SeedWaitingAsync("run-2", Production, WorkflowWait.Message("orders", null));
        await fixture.SeedWaitingAsync("run-3", Production, WorkflowWait.Message("orders", null));

        // Asking for more than the deployment permits is bounded rather than refused, exactly as a lease request is.
        (await fixture.Coordinator.ClaimAwaitingAsync(Runner, "orders", null, [Version], 99, null, default)).Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_waiting_run_is_leased_to_the_claiming_principal()
    {
        var fixture = await Fixture.EmptyAsync();
        await fixture.SeedWaitingAsync("run-1", Production, WorkflowWait.Message("orders", null));

        IReadOnlyList<ClaimedRunRecord> claims = await fixture.Coordinator.ClaimAwaitingAsync(Runner, "orders", null, [Version], null, null, default);

        claims.Count.ShouldBe(1);
        claims[0].Environment.ShouldBe(Production);
        claims[0].Lease.Epoch.ShouldBeGreaterThan(0);

        // Held by this runner, so a peer sweeping the same channel is offered nothing.
        (await fixture.Coordinator.ClaimAwaitingAsync(Peer, "orders", null, [Version], null, null, default)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task An_idle_runner_is_told_nothing_is_claimable_rather_than_refused()
    {
        var fixture = await Fixture.EmptyAsync();

        (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_run_in_an_environment_the_principal_is_not_bound_to_is_never_offered()
    {
        // The environment is resolved from the bindings and never from the request, so there is no way to ask for this
        // run at all — which is the point: a claim cannot be aimed at another tenant's work.
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Staging);

        (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_run_for_a_version_the_runner_does_not_host_is_never_offered()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);

        (await fixture.Coordinator.TryClaimAsync(Runner, ["something-else-v1"], null, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_completed_run_is_not_claimed_and_its_lease_is_handed_straight_back()
    {
        // The dispatch index answers from its own projection; the claim re-reads under the lease, which is what stops a
        // terminal run being handed out because a marker lingered.
        var fixture = await Fixture.EmptyAsync();
        await fixture.SeedAsync("run-1", WorkflowRunStatus.Completed, Production, resumeRequested: true);

        (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default)).ShouldBeNull();

        // The lease taken to look at it was released, so a peer can take the run immediately rather than waiting a TTL.
        (await fixture.Store.AcquireLeaseAsync("run-1", Peer, TimeSpan.FromMinutes(1), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task An_orphaned_running_run_is_reclaimable_once_its_lease_lapses()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Running, Production);
        await fixture.Store.AcquireLeaseAsync("run-1", Peer, TimeSpan.FromSeconds(30), default);

        // Held by a live peer: not claimable.
        (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default)).ShouldBeNull();

        // The peer crashed, so its lease lapses and the run must be reclaimable — otherwise an interrupted run would
        // never resume.
        fixture.Clock.Advance(TimeSpan.FromSeconds(31));
        (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_requested_lease_is_bounded_by_the_deployment()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);

        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], TimeSpan.FromDays(7), default))!.Value;

        // Asking for more than the deployment grants is answered with the maximum rather than refused: the request is a
        // preference, and refusing it would strand a runner that merely wants fewer renewals.
        claimed.Lease.ExpiresAt.ShouldBe(T0 + TimeSpan.FromHours(1));
    }

    [TestMethod]
    public async Task A_held_lease_renews_without_changing_its_token_or_epoch()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        RunnerLeaseGrant? renewed = await fixture.Coordinator.TryRenewAsync(Runner, claimed.RunId, claimed.Lease.Token, TimeSpan.FromMinutes(5), default);

        renewed.ShouldNotBeNull();
        renewed.Value.Token.ShouldBe(claimed.Lease.Token);

        // One epoch per grant, not one per extension: renewing must not invalidate a checkpoint already written under it.
        renewed.Value.Epoch.ShouldBe(claimed.Lease.Epoch);
        renewed.Value.ExpiresAt.ShouldBe(T0 + TimeSpan.FromSeconds(30) + TimeSpan.FromMinutes(5));
    }

    [TestMethod]
    public async Task An_expired_lease_is_not_renewed_into_a_fresh_one()
    {
        // The failure this exists to prevent: the run may already have been claimed by another runner, so quietly
        // minting a new lease would put two runners on it.
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        fixture.Clock.Advance(TimeSpan.FromMinutes(2));

        (await fixture.Coordinator.TryRenewAsync(Runner, claimed.RunId, claimed.Lease.Token, null, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Another_principal_presenting_the_token_neither_renews_nor_holds_the_lease()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        // The owner is the authenticated principal, so a leaked token is not a lease.
        (await fixture.Coordinator.TryRenewAsync(Peer, claimed.RunId, claimed.Lease.Token, null, default)).ShouldBeNull();
        (await fixture.Coordinator.HoldsLeaseAsync(Peer, claimed.RunId, claimed.Lease.Token, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_lease_presented_with_an_epoch_above_the_grant_authorises_nothing()
    {
        // ADR 0065 §6's first refusal rule: an epoch above the current grant names a grant this holder never held. The
        // header is plaintext and the runner writes the epoch it is given into the checkpoint it authors, so an epoch
        // the server does not check is an epoch the runner chooses.
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        string inflated = WithEpoch(claimed.Lease.Token, claimed.Lease.Epoch + 1);

        (await fixture.Coordinator.TryRenewAsync(Runner, claimed.RunId, inflated, null, default)).ShouldBeNull();
        (await fixture.Coordinator.HoldsLeaseAsync(Runner, claimed.RunId, inflated, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_lease_presented_with_an_epoch_below_the_grant_authorises_nothing()
    {
        // The second rule, a rollback: an epoch below the run's high-water re-presents a grant the run has moved past.
        // In phase A the lease header is the epoch's only carrier, so both rules are decided by the same comparison
        // against the persisted grant; phase B separates them, when the runner's own MAC'd region carries an epoch
        // independently of the header.
        //
        // The superseded epoch is a real one this run really issued, carried on the current grant's own store token,
        // rather than an arbitrary lower number. An arbitrary one would be refused for being unparseable or for naming
        // a lease nobody holds, and would pass whether or not anything compares epochs — which is no evidence at all.
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord first = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;
        await fixture.Coordinator.ReleaseAsync(Runner, first.RunId, first.Lease.Token, default);
        ClaimedRunRecord current = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        current.Lease.Epoch.ShouldBeGreaterThan(first.Lease.Epoch);
        string rolledBack = WithEpoch(current.Lease.Token, first.Lease.Epoch);

        (await fixture.Coordinator.TryRenewAsync(Runner, current.RunId, rolledBack, null, default)).ShouldBeNull();
        (await fixture.Coordinator.HoldsLeaseAsync(Runner, current.RunId, rolledBack, default)).ShouldBeFalse();

        // And the grant itself still works, so what was refused is the epoch and not the run's whole lease.
        (await fixture.Coordinator.HoldsLeaseAsync(Runner, current.RunId, current.Lease.Token, default)).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_second_control_plane_instance_over_the_same_store_never_re_issues_a_runs_epoch()
    {
        // The runner API is deployed as several instances and a lease outlives the instance that granted it, so an
        // epoch minted from process-local state is not monotonic for the run — a restart, or simply a second replica,
        // re-issues values already spent. The store is the only thing that spans both, which is why the counter lives
        // there.
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord first = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;
        await fixture.Coordinator.ReleaseAsync(Runner, first.RunId, first.Lease.Token, default);

        var second = new RunnerRunCoordinator(fixture.Store, Bindings(), timeProvider: fixture.Clock);
        ClaimedRunRecord next = (await second.TryClaimAsync(Runner, [Version], null, default))!.Value;

        next.Lease.Epoch.ShouldBeGreaterThan(first.Lease.Epoch);
    }

    [TestMethod]
    public async Task A_revoked_runner_cannot_renew_the_lease_it_already_holds()
    {
        // ADR 0027 and ADR 0065 decision 2: revocation takes effect within the binding cache window, whether or not the
        // runner cooperates. Renewal is where that matters most — an unchecked renewal extends the lease indefinitely,
        // so a revoked runner keeps its run for as long as it keeps asking.
        var bindings = new RevocableBindings(Production);
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production, bindings);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        bindings.Revoke();

        (await fixture.Coordinator.TryRenewAsync(Runner, claimed.RunId, claimed.Lease.Token, null, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_revoked_runner_cannot_read_or_write_the_checkpoint_of_a_run_it_holds()
    {
        // HoldsLeaseAsync is the gate on both checkpoint operations, so re-resolving here is what stops a revoked runner
        // continuing to read tenant plaintext and overwrite run state under a lease acquired before the revocation.
        var bindings = new RevocableBindings(Production);
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production, bindings);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        (await fixture.Coordinator.HoldsLeaseAsync(Runner, claimed.RunId, claimed.Lease.Token, default)).ShouldBeTrue();

        bindings.Revoke();

        (await fixture.Coordinator.HoldsLeaseAsync(Runner, claimed.RunId, claimed.Lease.Token, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_revoked_runner_may_still_hand_its_run_back()
    {
        // Release stays unguarded on purpose. Refusing it would strand the lease on a runner that is trying to give the
        // work up, which is the one thing a revoked runner can still do that the platform wants.
        var bindings = new RevocableBindings(Production);
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production, bindings);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        bindings.Revoke();
        await fixture.Coordinator.ReleaseAsync(Runner, claimed.RunId, claimed.Lease.Token, default);

        (await fixture.Store.AcquireLeaseAsync("run-1", Peer, TimeSpan.FromMinutes(1), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_release_by_another_principal_does_not_free_the_holders_run()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        await fixture.Coordinator.ReleaseAsync(Peer, claimed.RunId, claimed.Lease.Token, default);

        // The store matches a release on the run and the token alone, so without the ownership check this would have
        // handed one runner's in-flight run to another.
        (await fixture.Coordinator.HoldsLeaseAsync(Runner, claimed.RunId, claimed.Lease.Token, default)).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Releasing_a_held_run_makes_it_claimable_at_once()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        await fixture.Coordinator.ReleaseAsync(Runner, claimed.RunId, claimed.Lease.Token, default);

        (await fixture.Coordinator.HoldsLeaseAsync(Runner, claimed.RunId, claimed.Lease.Token, default)).ShouldBeFalse();
        (await fixture.Store.AcquireLeaseAsync("run-1", Peer, TimeSpan.FromMinutes(1), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Holding_a_lease_does_not_extend_it()
    {
        // The check an operation performed under a lease makes. If it renewed, the renewal a runner is required to make
        // would be decorative and a crashed holder's run would never be reclaimed.
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        (await fixture.Coordinator.HoldsLeaseAsync(Runner, claimed.RunId, claimed.Lease.Token, default)).ShouldBeTrue();

        fixture.Clock.Advance(TimeSpan.FromSeconds(31));
        (await fixture.Coordinator.HoldsLeaseAsync(Runner, claimed.RunId, claimed.Lease.Token, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_run_that_does_not_exist_and_one_held_by_a_peer_are_refused_identically()
    {
        // The whole of the non-disclosure rule for run operations: the two cases must be indistinguishable, or the
        // surface becomes a way to learn which runs another tenant holds.
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        ClaimedRunRecord claimed = (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default))!.Value;

        bool peersRun = await fixture.Coordinator.HoldsLeaseAsync(Peer, claimed.RunId, claimed.Lease.Token, default);
        bool noSuchRun = await fixture.Coordinator.HoldsLeaseAsync(Peer, new WorkflowRunId("never"), claimed.Lease.Token, default);

        peersRun.ShouldBe(noSuchRun);
        peersRun.ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_malformed_lease_value_authorises_nothing()
    {
        var fixture = await Fixture.WithRunAsync("run-1", WorkflowRunStatus.Pending, Production);
        await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default);

        foreach (string? malformed in new[] { null, string.Empty, "no-separator", ".leading", "trailing.", "007.token", "-1.token" })
        {
            (await fixture.Coordinator.HoldsLeaseAsync(Runner, new WorkflowRunId("run-1"), malformed, default)).ShouldBeFalse();
            (await fixture.Coordinator.TryRenewAsync(Runner, new WorkflowRunId("run-1"), malformed, null, default)).ShouldBeNull();
        }
    }

    [TestMethod]
    public async Task A_claim_stops_after_the_configured_number_of_candidates()
    {
        // A busy queue must not let one request walk the whole index. Every candidate here is held by a peer, so the
        // claim tries the cap and gives up rather than scanning on.
        var fixture = await Fixture.EmptyAsync(new RunnerApiOptions { ClaimCandidates = 2 });
        for (int i = 0; i < 5; i++)
        {
            await fixture.SeedAsync($"run-{i}", WorkflowRunStatus.Pending, Production);
            await fixture.Store.AcquireLeaseAsync($"run-{i}", Peer, TimeSpan.FromMinutes(5), default);
        }

        int beforeClaim = fixture.Store.LeaseAttempts;
        (await fixture.Coordinator.TryClaimAsync(Runner, [Version], null, default)).ShouldBeNull();
        (fixture.Store.LeaseAttempts - beforeClaim).ShouldBe(2);
    }

    [TestMethod]
    public void A_store_without_a_dispatch_index_is_rejected()
    {
        Should.Throw<ArgumentException>(() => new RunnerRunCoordinator(new CheckpointOnlyStore(), Bindings()));
    }

    private static DeclaredRunnerEnvironmentBindings Bindings()
        => new(new Dictionary<string, IReadOnlyList<string>> { [Runner] = [Production] });

    // Rewrites the epoch a lease header states, leaving the store's own token untouched — what a runner that keeps its
    // grant but chooses its own epoch presents.
    private static string WithEpoch(string leaseToken, long epoch)
    {
        RunnerLeaseToken.TryParse(leaseToken, out _, out string storeToken).ShouldBeTrue();
        return RunnerLeaseToken.Issue(epoch, storeToken);
    }

    // Bindings a test can withdraw mid-run, standing in for an administrator revoking the runner's authorization while
    // it holds a lease. The real resolver reads the authorization rows per request behind a bounded cache; what matters
    // to these tests is only that the answer can change between one operation and the next.
    private sealed class RevocableBindings(string environment) : IRunnerEnvironmentBindings
    {
        private readonly string[] bound = [environment];
        private volatile bool revoked;

        public void Revoke() => this.revoked = true;

        public ValueTask<RunnerBindings> ResolveAsync(string principal, CancellationToken cancellationToken)
            => ValueTask.FromResult(this.revoked || !string.Equals(principal, Runner, StringComparison.Ordinal)
                ? RunnerBindings.None
                : new RunnerBindings(this.bound, null));
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }

    // An in-memory store that counts lease attempts, so a test can assert the claim stopped rather than inferring it.
    // A real store is both indexes, so the double is too. Counting only the lease attempts is the only thing it adds.
    private sealed class CountingStore(TimeProvider timeProvider) : IWorkflowStateStore, IWorkflowDispatchIndex, IWorkflowWaitIndex
    {
        private readonly InMemoryWorkflowStateStore inner = new(timeProvider);

        public int LeaseAttempts { get; private set; }

        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
            => this.inner.SaveAsync(id, checkpointUtf8, index, expected, cancellationToken);

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
            => this.inner.LoadAsync(id, cancellationToken);

        public ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
        {
            this.LeaseAttempts++;
            return this.inner.AcquireLeaseAsync(id, owner, ttl, cancellationToken);
        }

        public ValueTask<WorkflowLease?> TryExtendLeaseAsync(WorkflowLease lease, TimeSpan extension, CancellationToken cancellationToken)
            => this.inner.TryExtendLeaseAsync(lease, extension, cancellationToken);

        public IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, CancellationToken cancellationToken)
            => this.inner.QueryDueAsync(before, cancellationToken);

        public IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, string? runnerEnvironment, CancellationToken cancellationToken)
            => this.inner.QueryDueAsync(before, runnerEnvironment, cancellationToken);

        public IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, CancellationToken cancellationToken)
            => this.inner.QueryAwaitingAsync(channel, correlationId, cancellationToken);

        public IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, string? runnerEnvironment, CancellationToken cancellationToken)
            => this.inner.QueryAwaitingAsync(channel, correlationId, runnerEnvironment, cancellationToken);

        public ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
            => this.inner.QueryAsync(query, cancellationToken);

        public ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
            => this.inner.ReleaseLeaseAsync(lease, cancellationToken);

        public ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
            => this.inner.DeleteAsync(id, cancellationToken);

        public IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken)
            => this.inner.QueryClaimableAsync(hostedWorkflowIds, now, cancellationToken);

        public IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, string? runnerEnvironment, DateTimeOffset now, CancellationToken cancellationToken)
            => this.inner.QueryClaimableAsync(hostedWorkflowIds, runnerEnvironment, now, cancellationToken);
    }

    private sealed class CheckpointOnlyStore : IWorkflowStateStore
    {
        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
            => ValueTask.FromResult(WorkflowEtag.None);

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
            => ValueTask.FromResult<WorkflowCheckpoint?>(null);

        public ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
            => ValueTask.FromResult<WorkflowLease?>(null);

        public ValueTask<WorkflowLease?> TryExtendLeaseAsync(WorkflowLease lease, TimeSpan extension, CancellationToken cancellationToken)
            => ValueTask.FromResult<WorkflowLease?>(null);

        public ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken) => default;

        public ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken) => default;
    }

    private sealed class Fixture
    {
        private Fixture(CountingStore store, TestClock clock, RunnerRunCoordinator coordinator)
        {
            this.Store = store;
            this.Clock = clock;
            this.Coordinator = coordinator;
        }

        public CountingStore Store { get; }

        public TestClock Clock { get; }

        public RunnerRunCoordinator Coordinator { get; }

        public static ValueTask<Fixture> EmptyAsync(RunnerApiOptions? options = null, IRunnerEnvironmentBindings? bindings = null)
        {
            var clock = new TestClock(T0);
            var store = new CountingStore(clock);
            var coordinator = new RunnerRunCoordinator(store, bindings ?? Bindings(), options, clock);
            return ValueTask.FromResult(new Fixture(store, clock, coordinator));
        }

        public static async ValueTask<Fixture> WithRunAsync(string runId, WorkflowRunStatus status, string environment, IRunnerEnvironmentBindings? bindings = null)
        {
            Fixture fixture = await EmptyAsync(bindings: bindings);
            await fixture.SeedAsync(runId, status, environment);
            return fixture;
        }

        public async ValueTask SeedWaitingAsync(string runId, string environment, WorkflowWait wait)
        {
            byte[] checkpoint = Checkpoint(runId, WorkflowRunStatus.Suspended, environment, null, wait);
            await this.Store.SaveAsync(
                new WorkflowRunId(runId),
                checkpoint,
                WorkflowCheckpointSerializer.ProjectIndex(checkpoint),
                WorkflowEtag.None,
                default);
        }

        public async ValueTask SeedAsync(string runId, WorkflowRunStatus status, string environment, bool resumeRequested = false)
        {
            DateTimeOffset resumeRequestedAt = this.Clock.GetUtcNow();
            byte[] checkpoint = Checkpoint(runId, status, environment, resumeRequested ? resumeRequestedAt : null);
            await this.Store.SaveAsync(
                new WorkflowRunId(runId),
                checkpoint,
                WorkflowCheckpointSerializer.ProjectIndex(checkpoint),
                WorkflowEtag.None,
                default);
        }

        private static byte[] Checkpoint(string runId, WorkflowRunStatus status, string environment, DateTimeOffset? resumeRequestedAt, WorkflowWait? wait = null)
        {
            using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
            using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
            return WorkflowCheckpointSerializer.Serialize(
                new WorkflowRunId(runId),
                Version,
                status,
                cursor: 0,
                sequence: 1,
                T0,
                retryCounters,
                new Dictionary<string, byte[]>(),
                inputs: default,
                stepOutputs,
                outputs: default,
                wait: wait,
                environment: environment,
                resumeRequestedAt: resumeRequestedAt,
                updatedAt: T0);
        }
    }
}