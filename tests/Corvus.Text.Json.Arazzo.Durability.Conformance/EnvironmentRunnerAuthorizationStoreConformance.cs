// <copyright file="EnvironmentRunnerAuthorizationStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IEnvironmentRunnerAuthorizationStore"/> must satisfy (design §5.5): idempotent
/// ensure-pending, get with filtering (status / environment / administered set) and <c>(environment, runnerId)</c> ordering,
/// the authorize/revoke decision transitions, optimistic concurrency via etag (so two administrators cannot double-decide),
/// keyset paging, and the approver inbox filtered by the administered-environment set. A backend's test project derives a
/// concrete <see cref="TestClassAttribute"/> and implements <see cref="CreateStoreAsync"/>; the in-memory store is the
/// reference implementation and runs the same suite.
/// </summary>
public abstract class EnvironmentRunnerAuthorizationStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IEnvironmentRunnerAuthorizationStore> CreateStoreAsync();

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
    public async Task EnsurePending_creates_a_pending_authorization()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();

        using ParsedJsonDocument<EnvironmentRunnerAuthorization> created = await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default);
        created.RootElement.EnvironmentValue.ShouldBe("production");
        created.RootElement.RunnerIdValue.ShouldBe("runner-1");
        created.RootElement.StatusValue.ShouldBe("Pending");
        created.RootElement.CreatedByValue.ShouldBe("runner-1");
        created.RootElement.EtagValue.IsNone.ShouldBeFalse();
        created.RootElement.DecidedByOrNull.ShouldBeNull();
        created.RootElement.ReasonOrNull.ShouldBeNull();

        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? fetched = await store.GetAsync("production", "runner-1", default);
        fetched.ShouldNotBeNull();
        fetched!.RootElement.StatusValue.ShouldBe("Pending");
    }

    [TestMethod]
    public async Task EnsurePending_is_idempotent_and_never_resets_an_authorized_runner()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();

        WorkflowEtag firstEtag;
        using (ParsedJsonDocument<EnvironmentRunnerAuthorization> first = await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default))
        {
            firstEtag = first.RootElement.EtagValue;
        }

        // A second ensure-pending on a still-Pending record returns it unchanged (same status, same etag — no new record).
        using (ParsedJsonDocument<EnvironmentRunnerAuthorization> second = await store.EnsurePendingAsync("production", "runner-1", "someone-else", null, default))
        {
            second.RootElement.StatusValue.ShouldBe("Pending");
            second.RootElement.EtagValue.ShouldBe(firstEtag);
            second.RootElement.CreatedByValue.ShouldBe("runner-1");
        }

        // Authorize the runner, then ensure-pending again: it must NOT reset to Pending.
        using (await store.DecideAsync("production", "runner-1", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized), WorkflowEtag.None, "admin", default))
        {
        }

        using ParsedJsonDocument<EnvironmentRunnerAuthorization> reEnsured = await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default);
        reEnsured.RootElement.StatusValue.ShouldBe("Authorized");
    }

    [TestMethod]
    public async Task Authorizing_records_the_decision()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        using (await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default))
        {
        }

        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await store.DecideAsync(
            "production",
            "runner-1",
            new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized, Reason: "trusted runner"),
            WorkflowEtag.None,
            "boss",
            default);

        decided.ShouldNotBeNull();
        decided!.RootElement.StatusValue.ShouldBe("Authorized");
        decided.RootElement.DecidedByOrNull.ShouldBe("boss");
        decided.RootElement.ReasonOrNull.ShouldBe("trusted runner");
        decided.RootElement.DecidedAtValue.ShouldNotBeNull();

        // The content fields carry through the decision unchanged.
        decided.RootElement.EnvironmentValue.ShouldBe("production");
        decided.RootElement.RunnerIdValue.ShouldBe("runner-1");
    }

    [TestMethod]
    public async Task Revoking_records_the_decision()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        using (await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default))
        {
        }

        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await store.DecideAsync(
            "production",
            "runner-1",
            new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Revoked, Reason: "compromised"),
            WorkflowEtag.None,
            "boss",
            default);

        decided.ShouldNotBeNull();
        decided!.RootElement.StatusValue.ShouldBe("Revoked");
        decided.RootElement.DecidedByOrNull.ShouldBe("boss");
        decided.RootElement.ReasonOrNull.ShouldBe("compromised");
    }

    [TestMethod]
    public async Task Quarantining_records_the_decision_and_is_not_dispatchable()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        using (await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default))
        {
        }

        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await store.DecideAsync(
            "production",
            "runner-1",
            new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Quarantined, Reason: "faulted"),
            WorkflowEtag.None,
            "boss",
            default);

        decided.ShouldNotBeNull();
        decided!.RootElement.StatusValue.ShouldBe("Quarantined");
        decided.RootElement.DecidedByOrNull.ShouldBe("boss");
        decided.RootElement.ReasonOrNull.ShouldBe("faulted");

        // A quarantined runner is excluded from dispatch (only Authorized is dispatchable) — the load-bearing invariant that
        // the dispatch gate reads through IsAuthorized, so quarantine stops new work without any gate change.
        decided.RootElement.IsQuarantined.ShouldBeTrue();
        decided.RootElement.IsAuthorized.ShouldBeFalse();
        decided.RootElement.HasStatus(RunnerAuthorizationStatus.Quarantined).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Deciding_a_missing_authorization_returns_null()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        (await store.DecideAsync("production", "missing", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized), WorkflowEtag.None, "admin", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Getting_a_missing_authorization_returns_null()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        (await store.GetAsync("production", "missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_etag_on_decide_conflicts_so_two_admins_cannot_double_decide()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        WorkflowEtag created;
        using (ParsedJsonDocument<EnvironmentRunnerAuthorization> authorization = await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default))
        {
            created = authorization.RootElement.EtagValue;
        }

        // The first administrator authorizes (etag advances).
        using (await store.DecideAsync("production", "runner-1", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized), created, "first", default))
        {
        }

        // A second administrator racing on the original etag conflicts.
        await Should.ThrowAsync<RunnerAuthorizationConflictException>(async () =>
            await store.DecideAsync("production", "runner-1", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Revoked), created, "second", default));

        // WorkflowEtag.None applies unconditionally (administrator override).
        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? overridden = await store.DecideAsync("production", "runner-1", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Revoked), WorkflowEtag.None, "ops", default);
        overridden!.RootElement.StatusValue.ShouldBe("Revoked");
    }

    [TestMethod]
    public async Task Listing_by_environment_returns_that_environments_runners()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default);
        await store.EnsurePendingAsync("production", "runner-2", "runner-2", null, default);
        await store.EnsurePendingAsync("staging", "runner-3", "runner-3", null, default);

        (await this.KeysAsync(store, new RunnerAuthorizationQuery(Environment: "production")))
            .ShouldBe([("production", "runner-1"), ("production", "runner-2")], ignoreOrder: true);
    }

    [TestMethod]
    public async Task Listing_filters_by_the_administered_environment_set_for_the_approver_inbox()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("envA", "runner-1", "runner-1", null, default);
        await store.EnsurePendingAsync("envA", "runner-2", "runner-2", null, default);
        await store.EnsurePendingAsync("envB", "runner-3", "runner-3", null, default);

        // The approver inbox: only envA's authorizations, never envB.
        (await this.KeysAsync(store, new RunnerAuthorizationQuery(AdministeredEnvironments: ["envA"])))
            .ShouldBe([("envA", "runner-1"), ("envA", "runner-2")], ignoreOrder: true);
    }

    [TestMethod]
    public async Task Listing_filters_by_status()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default);
        await store.EnsurePendingAsync("production", "runner-2", "runner-2", null, default);
        await store.DecideAsync("production", "runner-2", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized), WorkflowEtag.None, "admin", default);

        (await this.KeysAsync(store, new RunnerAuthorizationQuery(Status: RunnerAuthorizationStatus.Pending)))
            .ShouldBe([("production", "runner-1")]);
        (await this.KeysAsync(store, new RunnerAuthorizationQuery(Status: RunnerAuthorizationStatus.Authorized)))
            .ShouldBe([("production", "runner-2")]);
    }

    [TestMethod]
    public async Task Listing_filters_by_the_bound_machine_principal()
    {
        // How the runner API resolves what a principal may execute (ADR 0065 decision 2): the environments it is bound
        // to are the intersection a claim's candidate set is taken against, so the store must answer by principal
        // rather than have the caller read every row and sift.
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "runner-1", "machine-a", default);
        await store.EnsurePendingAsync("staging", "runner-2", "runner-2", "machine-a", default);
        await store.EnsurePendingAsync("production", "runner-3", "runner-3", "machine-b", default);

        (await this.KeysAsync(store, new RunnerAuthorizationQuery(Principal: "machine-a")))
            .ShouldBe([("production", "runner-1"), ("staging", "runner-2")]);
        (await this.KeysAsync(store, new RunnerAuthorizationQuery(Principal: "machine-b")))
            .ShouldBe([("production", "runner-3")]);
        (await this.KeysAsync(store, new RunnerAuthorizationQuery(Principal: "machine-nobody")))
            .ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_principal_filter_composes_with_the_status_filter()
    {
        // The pair the runner API actually asks for: only an Authorized record binds, so a revoked or still-pending
        // runner resolves to nothing and is offered no work.
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "runner-1", "machine-a", default);
        await store.EnsurePendingAsync("staging", "runner-2", "runner-2", "machine-a", default);
        await store.DecideAsync("staging", "runner-2", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized), WorkflowEtag.None, "admin", default);

        (await this.KeysAsync(store, new RunnerAuthorizationQuery(RunnerAuthorizationStatus.Authorized, Principal: "machine-a")))
            .ShouldBe([("staging", "runner-2")]);

        // Revoking it takes the binding away, which is the fence: the same query now answers nothing.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? authorized = await store.GetAsync("staging", "runner-2", default);
        await store.DecideAsync("staging", "runner-2", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Revoked), authorized!.RootElement.EtagValue, "admin", default);

        (await this.KeysAsync(store, new RunnerAuthorizationQuery(RunnerAuthorizationStatus.Authorized, Principal: "machine-a")))
            .ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_decision_keeps_the_bound_principal()
    {
        // Several backends replace the whole row on a decision, so the mirrored principal has to carry through. Losing
        // it would silently unbind the runner the moment an administrator authorized it — the exact operation that is
        // supposed to let it work.
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "runner-1", "machine-a", default);
        await store.DecideAsync("production", "runner-1", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized), WorkflowEtag.None, "admin", default);

        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await store.GetAsync("production", "runner-1", default);
        decided!.RootElement.PrincipalEquals("machine-a").ShouldBeTrue();

        (await this.KeysAsync(store, new RunnerAuthorizationQuery(RunnerAuthorizationStatus.Authorized, Principal: "machine-a")))
            .ShouldBe([("production", "runner-1")]);
    }

    [TestMethod]
    public async Task A_pre_authorized_row_binds_no_principal_and_is_matched_by_none()
    {
        // An administrator pre-authorizing by runner id binds nothing, so no principal resolves it. That is the correct
        // reading of a runner that has not proved ownership of its id, and it is why ADR 0065 decision 2 requires
        // pre-authorization to name an expected principal rather than a bare id.
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "admin", null, default);

        (await this.KeysAsync(store, new RunnerAuthorizationQuery(Principal: "machine-a"))).ShouldBeEmpty();
        (await this.KeysAsync(store, new RunnerAuthorizationQuery())).ShouldBe([("production", "runner-1")]);
    }

    [TestMethod]
    public async Task Withdrawing_removes_the_record_and_frees_the_runner_id()
    {
        // The one situation withdrawal exists for (ADR 0027): a pre-authorization that named the wrong principal. The
        // principal never moves once bound, so without removal the id would be permanently unusable by the runner it
        // was meant for.
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "admin", "typo-principal", default);

        (await store.TryWithdrawAsync("production", "runner-1", WorkflowEtag.None, default)).ShouldBeTrue();

        (await store.GetAsync("production", "runner-1", default)).ShouldBeNull();
        (await this.KeysAsync(store, new RunnerAuthorizationQuery())).ShouldBeEmpty();

        // The id is free: a fresh pre-authorization naming the right principal takes it.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> corrected = await store.EnsurePendingAsync("production", "runner-1", "admin", "svc-runner-a", default);
        corrected.RootElement.PrincipalEquals("svc-runner-a").ShouldBeTrue();
    }

    [TestMethod]
    public async Task Withdrawing_what_is_not_there_reports_it_rather_than_throwing()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();

        (await store.TryWithdrawAsync("production", "never", WorkflowEtag.None, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Withdrawing_under_a_stale_etag_conflicts_so_a_decision_is_not_discarded()
    {
        // An administrator reads the record, another authorizes it, and the first withdraws. Without the precondition
        // the withdrawal would silently discard a decision its caller never saw.
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        WorkflowEtag stale;
        using (ParsedJsonDocument<EnvironmentRunnerAuthorization> created = await store.EnsurePendingAsync("production", "runner-1", "admin", "svc-runner-a", default))
        {
            stale = created.RootElement.EtagValue;
        }

        using (await store.DecideAsync("production", "runner-1", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized), WorkflowEtag.None, "admin", default))
        {
        }

        await Should.ThrowAsync<RunnerAuthorizationConflictException>(
            async () => await store.TryWithdrawAsync("production", "runner-1", stale, default));

        // Nothing was removed.
        (await store.GetAsync("production", "runner-1", default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Withdrawing_leaves_other_environments_records_alone()
    {
        // The record is keyed by (environment, runnerId), so withdrawing one environment's must not disturb another's —
        // including the keyset index a backend maintains beside it.
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "admin", "svc-runner-a", default);
        await store.EnsurePendingAsync("staging", "runner-1", "admin", "svc-runner-a", default);

        (await store.TryWithdrawAsync("production", "runner-1", WorkflowEtag.None, default)).ShouldBeTrue();

        (await this.KeysAsync(store, new RunnerAuthorizationQuery())).ShouldBe([("staging", "runner-1")]);
        (await this.KeysAsync(store, new RunnerAuthorizationQuery(Principal: "svc-runner-a"))).ShouldBe([("staging", "runner-1")]);
    }

    [TestMethod]
    public async Task Listing_keyset_pages_by_environment_and_runner_without_gaps_or_duplicates()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();

        // Several rows across two environments; the page order is (environment, runnerId) ordinal.
        var expected = new List<(string Environment, string RunnerId)>();
        foreach (string environment in (string[])["alpha", "beta"])
        {
            for (int i = 0; i < 4; i++)
            {
                string runnerId = $"runner-{i}";
                await store.EnsurePendingAsync(environment, runnerId, runnerId, null, default);
                expected.Add((environment, runnerId));
            }
        }

        var seen = new List<(string Environment, string RunnerId)>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using EnvironmentRunnerAuthorizationPage page = await store.ListAsync(default, 3, tokenDoc?.RootElement ?? default, default);
            page.Authorizations.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (EnvironmentRunnerAuthorization authorization in page.Authorizations)
            {
                seen.Add((authorization.EnvironmentValue, authorization.RunnerIdValue));
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        pages.ShouldBe(3); // 8 items, 3 per page
        seen.ShouldBe(expected); // ordered (environment, runnerId), every row exactly once

        // A malformed token is rejected (rather than silently restarting from the first page).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<JsonString> badToken = AsPageToken("this~is~not~a~token"u8);
            using EnvironmentRunnerAuthorizationPage bad = await store.ListAsync(default, 3, badToken.RootElement, default);
        });
    }

    [TestMethod]
    public async Task Counting_is_bounded_by_the_cap_reporting_capped_only_beyond_it()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default);
        await store.EnsurePendingAsync("production", "runner-2", "runner-2", null, default);
        await store.EnsurePendingAsync("staging", "runner-3", "runner-3", null, default);

        // Below the true total: the exact count, not capped (matches the list length).
        (await store.CountAsync(default, 10, default)).ShouldBe((3, false));

        // Exactly at the cap: the (cap+1)th row does not exist, so the count is exact and NOT capped.
        (await store.CountAsync(default, 3, default)).ShouldBe((3, false));

        // The true total exceeds the cap: the count is the cap and Capped is set (the console renders "N+").
        (await store.CountAsync(default, 2, default)).ShouldBe((2, true));
        (await store.CountAsync(default, 1, default)).ShouldBe((1, true));

        // An empty store counts zero, never capped.
        IEnvironmentRunnerAuthorizationStore empty = await this.NewStoreAsync();
        (await empty.CountAsync(default, 5, default)).ShouldBe((0, false));
    }

    [TestMethod]
    public async Task Counting_honours_the_same_filters_as_the_list()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        await store.EnsurePendingAsync("production", "runner-1", "runner-1", null, default);
        await store.EnsurePendingAsync("production", "runner-2", "runner-2", null, default);
        await store.EnsurePendingAsync("staging", "runner-3", "runner-3", null, default);
        await store.EnsurePendingAsync("qa", "runner-4", "runner-4", null, default);

        // By environment.
        (await store.CountAsync(new RunnerAuthorizationQuery(Environment: "production"), 10, default)).ShouldBe((2, false));

        // The approver inbox: every authorization across the administered environment set; staging is outside it.
        (await store.CountAsync(new RunnerAuthorizationQuery(AdministeredEnvironments: ["production", "qa"]), 10, default)).ShouldBe((3, false));

        // By status (authorize one, then count each state).
        await store.DecideAsync("production", "runner-2", new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized), WorkflowEtag.None, "admin", default);
        (await store.CountAsync(new RunnerAuthorizationQuery(Status: RunnerAuthorizationStatus.Pending), 10, default)).ShouldBe((3, false));
        (await store.CountAsync(new RunnerAuthorizationQuery(Status: RunnerAuthorizationStatus.Authorized), 10, default)).ShouldBe((1, false));

        // Filter + cap compose: two Pending in the administered set, capped at 1.
        (await store.CountAsync(new RunnerAuthorizationQuery(Status: RunnerAuthorizationStatus.Pending, AdministeredEnvironments: ["production", "qa"]), 1, default)).ShouldBe((1, true));
    }

    [TestMethod]
    public async Task EnsurePending_stamps_the_machine_principal_on_create()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();

        // A runner self-registering through the authenticated endpoint (design §16.4) presents its trusted machine principal;
        // it is stamped on the created record so the authorization binds to a verified identity, not the self-asserted runnerId.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> created = await store.EnsurePendingAsync("production", "runner-1", "svc:runner-a", "svc:runner-a", default);
        created.RootElement.PrincipalOrNull.ShouldBe("svc:runner-a");
        created.RootElement.PrincipalEquals("svc:runner-a").ShouldBeTrue();
        created.RootElement.HasPrincipal.ShouldBeTrue();
    }

    [TestMethod]
    public async Task EnsurePending_without_a_principal_leaves_it_absent()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();

        // The administrator pre-authorization path (and any caller that does not bind an identity) passes no principal: the
        // record carries none, and a later registration presenting one is still accepted (pre-authorization is the
        // administrator's deliberate name-based allow-listing, so it never binds).
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> created = await store.EnsurePendingAsync("production", "runner-1", "admin", null, default);
        created.RootElement.PrincipalOrNull.ShouldBeNull();
        created.RootElement.HasPrincipal.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Re_registering_with_the_same_principal_is_idempotent()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();

        WorkflowEtag firstEtag;
        using (ParsedJsonDocument<EnvironmentRunnerAuthorization> first = await store.EnsurePendingAsync("production", "runner-1", "svc:runner-a", "svc:runner-a", default))
        {
            firstEtag = first.RootElement.EtagValue;
        }

        // The steady-state re-registration: the same runner presents its own principal, so the row returns unchanged (same
        // etag, still bound) — the string-free match path, no write.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> again = await store.EnsurePendingAsync("production", "runner-1", "svc:runner-a", "svc:runner-a", default);
        again.RootElement.EtagValue.ShouldBe(firstEtag);
        again.RootElement.PrincipalEquals("svc:runner-a").ShouldBeTrue();
    }

    [TestMethod]
    public async Task Re_registering_with_a_different_principal_is_refused()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();
        using (await store.EnsurePendingAsync("production", "runner-1", "svc:runner-a", "svc:runner-a", default))
        {
        }

        // A different authenticated machine cannot take over a runnerId a principal already owns (§16.4): the registration is
        // refused. The bound row is untouched.
        RunnerPrincipalConflictException conflict = await Should.ThrowAsync<RunnerPrincipalConflictException>(
            async () => await store.EnsurePendingAsync("production", "runner-1", "svc:runner-b", "svc:runner-b", default));
        conflict.RunnerId.ShouldBe("runner-1");
        conflict.PresentedPrincipal.ShouldBe("svc:runner-b");

        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? unchanged = await store.GetAsync("production", "runner-1", default);
        unchanged!.RootElement.PrincipalEquals("svc:runner-a").ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_pre_authorized_runner_accepts_a_registration_without_binding_a_principal()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.NewStoreAsync();

        // An administrator pre-authorized this runnerId by name (no principal). When the runner registers presenting a
        // principal, it is accepted and the row is returned unchanged — a pre-authorized row is never bound, so its trust
        // stays the administrator's explicit name-based allow-listing (the documented §16.4 residual).
        WorkflowEtag preAuthEtag;
        using (ParsedJsonDocument<EnvironmentRunnerAuthorization> preAuth = await store.EnsurePendingAsync("production", "runner-1", "admin", null, default))
        {
            preAuthEtag = preAuth.RootElement.EtagValue;
        }

        using ParsedJsonDocument<EnvironmentRunnerAuthorization> registered = await store.EnsurePendingAsync("production", "runner-1", "svc:runner-a", "svc:runner-a", default);
        registered.RootElement.EtagValue.ShouldBe(preAuthEtag);
        registered.RootElement.HasPrincipal.ShouldBeFalse();
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

    private async ValueTask<IEnvironmentRunnerAuthorizationStore> NewStoreAsync()
    {
        IEnvironmentRunnerAuthorizationStore store = await this.CreateStoreAsync();
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }

    private async ValueTask<List<(string Environment, string RunnerId)>> KeysAsync(IEnvironmentRunnerAuthorizationStore store, RunnerAuthorizationQuery query)
    {
        using PooledDocumentList<EnvironmentRunnerAuthorization> list = await store.ListAsync(query, default);
        return list.Select(a => (a.EnvironmentValue, a.RunnerIdValue)).ToList();
    }
}