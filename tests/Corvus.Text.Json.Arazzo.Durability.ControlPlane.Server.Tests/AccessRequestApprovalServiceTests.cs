// <copyright file="AccessRequestApprovalServiceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of <see cref="AccessRequestApprovalService"/> (design §16.5): the §15-admin gate, the platform cap
/// (run access only), self-elevation, time-boxed grant + early revoke, the workflow-id injection guard, and the
/// end-to-end check that an approval grants exactly the target workflow.
/// </summary>
[TestClass]
public sealed class AccessRequestApprovalServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly SecurityTagSet Boss = SecurityTagSet.FromTags([new("sys:tenant", "boss")]);
    private static readonly SecurityTagSet Mallory = SecurityTagSet.FromTags([new("sys:tenant", "mallory")]);

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)).ToList(), "test"));

    // Submits the (pooled, disposable) draft request, disposing the draft once the service has read it; the created
    // document is returned for the caller to assert on and dispose.
    private static async Task<ParsedJsonDocument<AccessRequest>> SubmitRequestAsync(IAccessRequestApprovalService service, ParsedJsonDocument<AccessRequest> draft, string actor, bool eligibleForSelfElevation, CancellationToken cancellationToken = default)
    {
        using (draft)
        {
            return await service.SubmitAsync(draft.RootElement, actor, eligibleForSelfElevation, cancellationToken);
        }
    }

    [TestMethod]
    public async Task Approving_writes_a_time_boxed_run_grant_scoped_to_the_workflow()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write", "runs:read"], requestedDurationSeconds: 3600);

        using (ParsedJsonDocument<AccessRequest>? approved = await h.Service.ApproveAsync(id, Boss, "boss", "looks good", default))
        {
            approved.ShouldNotBeNull();
            approved!.RootElement.StatusValue.ShouldBe("Approved");
            approved.RootElement.DecidedByOrNull.ShouldBe("boss");
            approved.RootElement.GrantedBindingIdOrNull.ShouldNotBeNull();
            approved.RootElement.GrantedUntilValue.ShouldBe(Now.AddSeconds(3600));
        }

        // The entitlement actually grants: a fresh policy over the same store admits alice to this workflow's runs only.
        PersistentRowSecurityPolicy policy = await h.RefreshedPolicyAsync();
        ClaimsPrincipal alice = Principal(("sub", "alice"));
        policy.ResolveGrantedScopes(alice).ShouldBe(["runs:write", "runs:read"], ignoreOrder: true);
        policy.Resolve(alice).Admits(AccessVerb.Write, SecurityTagSet.FromTags([new("sys:workflow", "nightly-reconcile")])).ShouldBeTrue();
        policy.Resolve(alice).Admits(AccessVerb.Write, SecurityTagSet.FromTags([new("sys:workflow", "other-flow")])).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_non_administrator_cannot_approve()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        await Should.ThrowAsync<WorkflowAdministrationException>(async () => await h.Service.ApproveAsync(id, Mallory, "mallory", null, default));

        // Still pending, no grant written.
        using ParsedJsonDocument<AccessRequest>? still = await h.Requests.GetAsync(id, default);
        still!.RootElement.StatusValue.ShouldBe("Pending");
        PersistentRowSecurityPolicy policy = await h.RefreshedPolicyAsync();
        policy.ResolveGrantedScopes(Principal(("sub", "alice"))).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task An_approval_is_capped_to_run_access()
    {
        Harness h = await Harness.CreateAsync();

        // Over-ask: only runs:read/write are grantable; security:write and runs:purge are never granted.
        string id = await h.SubmitPendingAsync(["runs:write", "security:write", "runs:purge", "runs:read"]);
        using (await h.Service.ApproveAsync(id, Boss, "boss", null, default))
        {
        }

        PersistentRowSecurityPolicy policy = await h.RefreshedPolicyAsync();
        policy.ResolveGrantedScopes(Principal(("sub", "alice"))).ShouldBe(["runs:read", "runs:write"], ignoreOrder: true);
    }

    [TestMethod]
    public async Task Approving_a_view_request_grants_catalog_read_with_read_reach_only()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["catalog:read"]);
        using (await h.Service.ApproveAsync(id, Boss, "boss", null, default))
        {
        }

        // The §17.3 view grant: catalog:read scope + READ reach to the workflow's rows (the reviewer sees its catalog
        // entry), but NOT write reach, and scoped to this workflow only — visibility without operation or administration.
        PersistentRowSecurityPolicy policy = await h.RefreshedPolicyAsync();
        ClaimsPrincipal alice = Principal(("sub", "alice"));
        policy.ResolveGrantedScopes(alice).ShouldBe(["catalog:read"]);
        SecurityTagSet thisFlow = SecurityTagSet.FromTags([new("sys:workflow", "nightly-reconcile")]);
        policy.Resolve(alice).Admits(AccessVerb.Read, thisFlow).ShouldBeTrue();
        policy.Resolve(alice).Admits(AccessVerb.Write, thisFlow).ShouldBeFalse();
        policy.Resolve(alice).Admits(AccessVerb.Read, SecurityTagSet.FromTags([new("sys:workflow", "other-flow")])).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_request_with_no_grantable_scope_is_rejected()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["security:write"]);

        await Should.ThrowAsync<AccessRequestStateException>(async () => await h.Service.ApproveAsync(id, Boss, "boss", null, default));
    }

    [TestMethod]
    public async Task An_eligible_requester_self_elevates_without_an_approver()
    {
        Harness h = await Harness.CreateAsync();

        // alice is NOT an administrator, but is eligible to self-elevate → the request is auto-approved.
        using (ParsedJsonDocument<AccessRequest> submitted = await SubmitRequestAsync(h.Service, AccessRequest.Draft("nightly-reconcile", ["runs:write"], "sub", "alice"),
            "alice",
            eligibleForSelfElevation: true,
            default))
        {
            submitted.RootElement.StatusValue.ShouldBe("Approved");
            submitted.RootElement.GrantedBindingIdOrNull.ShouldNotBeNull();
        }

        PersistentRowSecurityPolicy policy = await h.RefreshedPolicyAsync();
        policy.ResolveGrantedScopes(Principal(("sub", "alice"))).ShouldContain("runs:write");
    }

    [TestMethod]
    public async Task Revoking_deletes_the_grant_and_marks_the_request_revoked()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        string bindingId;
        using (ParsedJsonDocument<AccessRequest>? approved = await h.Service.ApproveAsync(id, Boss, "boss", null, default))
        {
            bindingId = approved!.RootElement.GrantedBindingIdOrNull!;
        }

        using (ParsedJsonDocument<AccessRequest>? revoked = await h.Service.RevokeAsync(id, Boss, "boss", "no longer needed", default))
        {
            revoked!.RootElement.StatusValue.ShouldBe("Revoked");
            revoked.RootElement.GrantedBindingIdOrNull.ShouldBe(bindingId); // kept for audit
        }

        // The binding is gone and access has stopped.
        (await h.Policy.GetBindingAsync(bindingId, default)).ShouldBeNull();
        PersistentRowSecurityPolicy policy = await h.RefreshedPolicyAsync();
        policy.ResolveGrantedScopes(Principal(("sub", "alice"))).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Only_the_requester_may_withdraw_and_only_while_pending()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        await Should.ThrowAsync<AccessRequestStateException>(async () => await h.Service.WithdrawAsync(id, "sub", "mallory", "mallory", default));

        using (ParsedJsonDocument<AccessRequest>? withdrawn = await h.Service.WithdrawAsync(id, "sub", "alice", "alice", default))
        {
            withdrawn!.RootElement.StatusValue.ShouldBe("Withdrawn");
        }
    }

    [TestMethod]
    public async Task Denying_requires_an_administrator_and_marks_the_request_denied()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        await Should.ThrowAsync<WorkflowAdministrationException>(async () => await h.Service.DenyAsync(id, Mallory, "mallory", null, default));

        using ParsedJsonDocument<AccessRequest>? denied = await h.Service.DenyAsync(id, Boss, "boss", "not now", default);
        denied!.RootElement.StatusValue.ShouldBe("Denied");
    }

    [TestMethod]
    public async Task A_workflow_id_that_could_inject_the_rule_grammar_is_rejected()
    {
        Harness h = await Harness.CreateAsync();

        // A crafted id containing a quote would break out of the sys:workflow == '...' literal — rejected up front.
        await Should.ThrowAsync<AccessRequestStateException>(async () => await SubmitRequestAsync(h.Service, AccessRequest.Draft("evil' || $claims.superset || 'x", ["runs:write"], "sub", "alice"),
            "alice",
            eligibleForSelfElevation: false,
            default));
    }

    [TestMethod]
    public async Task Approving_as_eligible_writes_durable_eligibility_that_confers_nothing_active()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write", "runs:read"]);

        using (ParsedJsonDocument<AccessRequest>? eligible = await h.Service.ApproveAsEligibleAsync(id, Boss, "boss", "PIM assignment", eligibilityWindow: null, default))
        {
            eligible.ShouldNotBeNull();
            eligible!.RootElement.StatusValue.ShouldBe("Eligible");
            eligible.RootElement.DecidedByOrNull.ShouldBe("boss");
            eligible.RootElement.GrantedBindingIdOrNull.ShouldNotBeNull();
        }

        // The eligibleOnly binding is invisible to the resolver — alice holds no active capability from it.
        PersistentRowSecurityPolicy policy = await h.RefreshedPolicyAsync();
        ClaimsPrincipal alice = Principal(("sub", "alice"));
        policy.ResolveGrantedScopes(alice).ShouldBeEmpty();
        policy.Resolve(alice).Admits(AccessVerb.Write, SecurityTagSet.FromTags([new("sys:workflow", "nightly-reconcile")])).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_non_administrator_cannot_grant_eligibility()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        await Should.ThrowAsync<WorkflowAdministrationException>(async () => await h.Service.ApproveAsEligibleAsync(id, Mallory, "mallory", null, eligibilityWindow: null, default));
    }

    [TestMethod]
    public async Task A_principal_with_stored_eligibility_self_elevates_without_an_approver()
    {
        Harness h = await Harness.CreateAsync();

        // boss grants alice durable eligibility for run access on the workflow — no active grant yet.
        string eligId = await h.SubmitPendingAsync(["runs:write"]);
        using (await h.Service.ApproveAsEligibleAsync(eligId, Boss, "boss", "you may self-serve", eligibilityWindow: null, default))
        {
        }

        // Eligibility alone confers nothing active.
        PersistentRowSecurityPolicy before = await h.RefreshedPolicyAsync();
        before.ResolveGrantedScopes(Principal(("sub", "alice"))).ShouldBeEmpty();

        // alice self-elevates (NOT claims-eligible) — the stored eligibility auto-approves a fresh active grant.
        using (ParsedJsonDocument<AccessRequest> activated = await SubmitRequestAsync(h.Service, AccessRequest.Draft("nightly-reconcile", ["runs:write"], "sub", "alice"),
            "alice",
            eligibleForSelfElevation: false,
            default))
        {
            activated.RootElement.StatusValue.ShouldBe("Approved");
            activated.RootElement.GrantedBindingIdOrNull.ShouldNotBeNull();
        }

        PersistentRowSecurityPolicy after = await h.RefreshedPolicyAsync();
        after.ResolveGrantedScopes(Principal(("sub", "alice"))).ShouldContain("runs:write");
    }

    [TestMethod]
    public async Task Stored_eligibility_only_auto_approves_the_scopes_it_covers()
    {
        Harness h = await Harness.CreateAsync();

        // alice is made eligible for runs:read only.
        string id = await h.SubmitPendingAsync(["runs:read"]);
        using (await h.Service.ApproveAsEligibleAsync(id, Boss, "boss", null, eligibilityWindow: null, default))
        {
        }

        // A self-elevation for runs:write is not covered → it stays pending for a human approver.
        using (ParsedJsonDocument<AccessRequest> over = await SubmitRequestAsync(h.Service, AccessRequest.Draft("nightly-reconcile", ["runs:write"], "sub", "alice"),
            "alice",
            eligibleForSelfElevation: false,
            default))
        {
            over.RootElement.StatusValue.ShouldBe("Pending");
        }

        // A self-elevation for runs:read is covered → auto-approved.
        using (ParsedJsonDocument<AccessRequest> ok = await SubmitRequestAsync(h.Service, AccessRequest.Draft("nightly-reconcile", ["runs:read"], "sub", "alice"),
            "alice",
            eligibleForSelfElevation: false,
            default))
        {
            ok.RootElement.StatusValue.ShouldBe("Approved");
        }
    }

    [TestMethod]
    public async Task Revoking_eligibility_denies_future_self_elevation()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        string bindingId;
        using (ParsedJsonDocument<AccessRequest>? eligible = await h.Service.ApproveAsEligibleAsync(id, Boss, "boss", null, eligibilityWindow: null, default))
        {
            bindingId = eligible!.RootElement.GrantedBindingIdOrNull!;
        }

        using (ParsedJsonDocument<AccessRequest>? revoked = await h.Service.RevokeAsync(id, Boss, "boss", "no longer eligible", default))
        {
            revoked!.RootElement.StatusValue.ShouldBe("Revoked");
        }

        // The eligibility assignment is gone — a later self-elevation matches nothing and stays pending.
        (await h.Policy.GetBindingAsync(bindingId, default)).ShouldBeNull();
        using (ParsedJsonDocument<AccessRequest> later = await SubmitRequestAsync(h.Service, AccessRequest.Draft("nightly-reconcile", ["runs:write"], "sub", "alice"),
            "alice",
            eligibleForSelfElevation: false,
            default))
        {
            later.RootElement.StatusValue.ShouldBe("Pending");
        }
    }

    // A clock fixed at a known instant so grant expiry is deterministic.
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // The in-process harness: a real SecuredWorkflowCatalog (with boss as the workflow's §15 administrator), the
    // in-memory access-request + security-policy stores, and the service under test.
    private sealed class Harness
    {
        private const string Workflow = "nightly-reconcile";

        private Harness(AccessRequestApprovalService service, IAccessRequestStore requests, ISecurityPolicyStore policy, TimeProvider clock)
        {
            this.Service = service;
            this.Requests = requests;
            this.Policy = policy;
            this.Clock = clock;
        }

        public AccessRequestApprovalService Service { get; }

        public IAccessRequestStore Requests { get; }

        public ISecurityPolicyStore Policy { get; }

        public TimeProvider Clock { get; }

        public static async Task<Harness> CreateAsync()
        {
            var clock = new FixedClock(Now);
            var stateStore = new InMemoryWorkflowStateStore(clock);
            var adminStore = new InMemoryWorkflowAdministratorStore();
            var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(clock), stateStore, "ops", administrators: adminStore);
            await adminStore.PutAsync(Workflow, [Boss], WorkflowEtag.None, "seed", default);

            var requests = new InMemoryAccessRequestStore(clock);
            var policy = new InMemorySecurityPolicyStore(clock);
            var service = new AccessRequestApprovalService(requests, policy, catalog, clock);
            return new Harness(service, requests, policy, clock);
        }

        public async Task<string> SubmitPendingAsync(IReadOnlyList<string> scopes, long? requestedDurationSeconds = null)
        {
            using ParsedJsonDocument<AccessRequest> submitted = await SubmitRequestAsync(this.Service, AccessRequest.Draft(Workflow, scopes, "sub", "alice", requestedDurationSeconds: requestedDurationSeconds),
                "alice",
                eligibleForSelfElevation: false,
                default);
            submitted.RootElement.StatusValue.ShouldBe("Pending");
            return submitted.RootElement.IdValue;
        }

        public async Task<PersistentRowSecurityPolicy> RefreshedPolicyAsync()
        {
            var resolver = new PersistentRowSecurityPolicy(this.Policy, timeProvider: this.Clock);
            await resolver.RefreshAsync();
            return resolver;
        }
    }
}