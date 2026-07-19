// <copyright file="WorkflowBackedAccessRequestApprovalServiceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.AsyncApi.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using SwModels = Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of <see cref="WorkflowBackedAccessRequestApprovalService"/> (design §16.5.1): submitting a pending request
/// starts the approval run; the decision touchpoints publish the matching outcome on the access.decision channel and do
/// not grant inline; self-elevation still skips the run; a non-administrator cannot approve.
/// </summary>
[TestClass]
public sealed class WorkflowBackedAccessRequestApprovalServiceTests
{
    private const string DecisionChannel = "access.decision";
    private const string ApprovalWorkflowId = "access-approval-v1";
    private const string InternalEnv = "system";
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly SecurityTagSet Boss = SecurityTagSet.FromTags([new("sys:tenant", "boss")]);
    private static readonly SecurityTagSet Mallory = SecurityTagSet.FromTags([new("sys:tenant", "mallory")]);

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)).ToList(), "test"));

    [TestMethod]
    public async Task Submitting_a_pending_request_starts_the_approval_run()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        h.Management.Starts.Count.ShouldBe(1);
        h.Management.Starts[0].WorkflowId.ShouldBe(ApprovalWorkflowId);
        h.Management.Starts[0].Environment.ShouldBe(InternalEnv);
        h.Management.Starts[0].InputsJson.ShouldContain(id); // the request id is the correlation key carried into the run
    }

    [TestMethod]
    public async Task The_approval_run_carries_the_workflow_identity_for_credential_use()
    {
        Harness h = await Harness.CreateAsync();
        await h.SubmitPendingAsync(["runs:write"]);

        // The run must carry sys:workflow=<base id> so the §13 usage gate admits the runner's 'controlplane' OAuth2
        // credential (usage-scoped to that identity) when the run calls grantAccessRequest. Without it the credential does
        // not resolve, the grant call is unauthenticated, and the run faults (proven by the podman live-verify).
        var tags = new List<SecurityTag>();
        foreach (SecurityTag tag in h.Management.Starts[0].SecurityTags)
        {
            tags.Add(tag);
        }

        tags.ShouldContain(new SecurityTag(WorkflowIdentity.WorkflowTagKey, "access-approval"));
    }

    [TestMethod]
    public async Task A_self_elevated_submit_does_not_start_a_run()
    {
        // The requester is claims-eligible, so the built-in auto-approves without a human decision — no approval run.
        Harness h = await Harness.CreateAsync(selfElevationEligibility: static (_, _) => true);

        using (ParsedJsonDocument<AccessRequest> submitted = await h.SubmitAsync(["runs:write"], Principal(("sub", "alice"))))
        {
            submitted.RootElement.StatusValue.ShouldBe("Approved");
        }

        h.Management.Starts.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Approving_publishes_an_approved_decision_and_does_not_grant_inline()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        using (ParsedJsonDocument<AccessRequest>? result = await h.Service.ApproveAsync(id, Boss, "boss", "looks good", default))
        {
            // The decision is enacted by the workflow asynchronously, so the request is still pending here.
            result.ShouldNotBeNull();
            result!.RootElement.StatusValue.ShouldBe("Pending");
            result.RootElement.GrantedBindingIdOrNull.ShouldBeNull();
        }

        (string Outcome, string RequestId, string DecidedBy)? decision = h.LastDecision();
        decision.ShouldNotBeNull();
        decision!.Value.Outcome.ShouldBe("approved");
        decision.Value.RequestId.ShouldBe(id);
        decision.Value.DecidedBy.ShouldBe("boss");
    }

    [TestMethod]
    public async Task Approving_as_eligible_publishes_an_eligible_decision()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        using (await h.Service.ApproveAsEligibleAsync(id, Boss, "boss", null, eligibilityWindow: null, default))
        {
        }

        h.LastDecision()!.Value.Outcome.ShouldBe("eligible");
        h.LastDecision()!.Value.RequestId.ShouldBe(id);
    }

    [TestMethod]
    public async Task Denying_marks_the_request_denied_and_publishes_a_rejected_decision()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        using (ParsedJsonDocument<AccessRequest>? denied = await h.Service.DenyAsync(id, Boss, "boss", "not now", default))
        {
            denied!.RootElement.StatusValue.ShouldBe("Denied");
        }

        h.LastDecision()!.Value.Outcome.ShouldBe("rejected");
    }

    [TestMethod]
    public async Task Withdrawing_marks_the_request_withdrawn_and_publishes_a_withdrawn_decision()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        using (ParsedJsonDocument<AccessRequest>? withdrawn = await h.Service.WithdrawAsync(id, "sub", "alice", "alice", default))
        {
            withdrawn!.RootElement.StatusValue.ShouldBe("Withdrawn");
        }

        h.LastDecision()!.Value.Outcome.ShouldBe("withdrawn");
    }

    [TestMethod]
    public async Task A_non_administrator_cannot_approve_and_nothing_is_published()
    {
        Harness h = await Harness.CreateAsync();
        string id = await h.SubmitPendingAsync(["runs:write"]);

        await Should.ThrowAsync<WorkflowAdministrationException>(async () => await h.Service.ApproveAsync(id, Mallory, "mallory", null, default));

        h.Transport.PublishedMessages.ShouldNotContain(m => m.Channel == DecisionChannel);
    }

    // A clock fixed at a known instant so the built-in's grant expiry is deterministic.
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Records the approval runs the strategy starts; every other management operation is unused by the strategy.
    private sealed class RecordingManagement : ISecuredWorkflowManagement
    {
        public List<(string WorkflowId, string Environment, string InputsJson, SecurityTagSet SecurityTags)> Starts { get; } = [];

        public ValueTask<WorkflowRunId> StartAsync(string workflowId, JsonElement inputs, string? correlationId, TagSet tags, SecurityTagSet securityTags, string environment, CancellationToken cancellationToken)
        {
            this.Starts.Add((workflowId, environment, inputs.ToString() ?? string.Empty, securityTags));
            return new ValueTask<WorkflowRunId>(new WorkflowRunId("run-" + this.Starts.Count));
        }

        public ValueTask<WorkflowRunId> StartIdempotentAsync(string workflowId, JsonElement inputs, string idempotencyKey, string environment, string? correlationId = null, TagSet tags = default, SecurityTagSet securityTags = default, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<WorkflowRunPage> ListAsync(WorkflowQuery query, AccessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<(int Count, bool Capped)> CountAsync(WorkflowQuery query, AccessContext context, int cap, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<WorkflowRunDetail?> GetAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ReadOnlyMemory<byte>?> GetStepJournalAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<bool> ResumeAsync(WorkflowRunId id, ResumeOptions options, AccessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<bool> RequestFaultedResumeAsync(WorkflowRunId id, ResumeOptions options, AccessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<bool> CancelAsync(WorkflowRunId id, string reason, AccessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<int> PurgeAsync(WorkflowPurgeQuery query, AccessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Harness
    {
        private const string Workflow = "nightly-reconcile";

        private Harness(WorkflowBackedAccessRequestApprovalService service, IAccessRequestStore requests, InMemoryMessageTransport transport, RecordingManagement management)
        {
            this.Service = service;
            this.Requests = requests;
            this.Transport = transport;
            this.Management = management;
        }

        public WorkflowBackedAccessRequestApprovalService Service { get; }

        public IAccessRequestStore Requests { get; }

        public InMemoryMessageTransport Transport { get; }

        public RecordingManagement Management { get; }

        public static async Task<Harness> CreateAsync(Func<ClaimsPrincipal, AccessRequest, bool>? selfElevationEligibility = null)
        {
            var clock = new FixedClock(Now);
            var stateStore = new InMemoryWorkflowStateStore(clock);
            var adminStore = new InMemoryWorkflowAdministratorStore();
            var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(clock), stateStore, "ops", administrators: adminStore);
            using (JsonWorkspace seedWorkspace = JsonWorkspace.CreateUnrented())
            {
                await adminStore.PutAsync(Workflow, [WorkflowAdministrators.BuildIdentity(seedWorkspace, Boss, default, hasKind: false, default, hasLabel: false)], WorkflowEtag.None, "seed", default);
            }

            var requests = new InMemoryAccessRequestStore(clock);
            var policy = new InMemorySecurityPolicyStore(clock);
            var inner = new AccessRequestApprovalService(requests, policy, catalog, clock, selfElevationEligibility: selfElevationEligibility);
            var transport = new InMemoryMessageTransport();
            var producer = new PublishAccessDecisionProducer(transport);
            var management = new RecordingManagement();
            var service = new WorkflowBackedAccessRequestApprovalService(inner, requests, management, catalog, producer, ApprovalWorkflowId, InternalEnv);
            return new Harness(service, requests, transport, management);
        }

        public async Task<ParsedJsonDocument<AccessRequest>> SubmitAsync(IReadOnlyList<string> scopes, ClaimsPrincipal principal)
        {
            using ParsedJsonDocument<AccessRequest> draft = AccessRequest.Draft(Workflow, scopes, "sub", "alice");
            return await this.Service.SubmitAsync(draft.RootElement, "alice", principal, default);
        }

        public async Task<string> SubmitPendingAsync(IReadOnlyList<string> scopes)
        {
            using ParsedJsonDocument<AccessRequest> submitted = await this.SubmitAsync(scopes, Principal(("sub", "alice")));
            submitted.RootElement.StatusValue.ShouldBe("Pending");
            return submitted.RootElement.IdValue;
        }

        public (string Outcome, string RequestId, string DecidedBy)? LastDecision()
        {
            PublishedMessage? message = this.Transport.PublishedMessages.LastOrDefault(m => m.Channel == DecisionChannel);
            if (message is null)
            {
                return null;
            }

            using ParsedJsonDocument<SwModels.AccessDecisionPayload> doc = ParsedJsonDocument<SwModels.AccessDecisionPayload>.Parse(message.PayloadBytes);
            SwModels.AccessDecisionPayload payload = doc.RootElement;
            return ((string)payload.Outcome, (string)payload.RequestId, (string)payload.DecidedBy);
        }
    }
}