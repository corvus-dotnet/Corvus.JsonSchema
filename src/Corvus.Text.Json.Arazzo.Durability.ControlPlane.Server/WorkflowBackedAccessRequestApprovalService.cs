// <copyright file="WorkflowBackedAccessRequestApprovalService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Logging;
using SwModels = Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows.Models;
using SwString = Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows.JsonString;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The workflow-backed access-request approval strategy (design §16.5.1): Arazzo governs its own approvals. It composes
/// the built-in <see cref="AccessRequestApprovalService"/> (which owns the request store, the platform ceiling, and the
/// two system grant paths the approval workflow calls) and adds the run orchestration.
/// </summary>
/// <remarks>
/// <para>Submitting a request that needs a human decision starts a run of the bootstrapped <c>access-approval</c>
/// workflow, keyed on the request id. The <c>approve</c> / <c>approveAsEligible</c> / <c>deny</c> / <c>withdraw</c>
/// touchpoints each publish the resolution as an <c>access.decision</c> message that resumes the suspended run; the
/// workflow, running on a control-plane system runner under its §13 system credential, calls the bounded grant operation
/// to enact an approval or eligibility. The approver is authenticated exactly as the built-in does (a §15 administrator
/// of the target workflow for approve/deny) before the decision is published rather than granted inline.</para>
/// <para>Because the decision is enacted asynchronously by the workflow, <c>approve</c> and <c>approveAsEligible</c>
/// return the request in its still-pending state; it reaches Approved or Eligible when the workflow completes the grant.
/// <c>deny</c> and <c>withdraw</c> mark the request through the built-in (there is no grant to write) and then resume the
/// run so its notify path runs and it does not linger suspended. A requester's withdrawal is just another resolution
/// event on the same channel. Self-elevation is unchanged: an eligible requester is auto-approved by the built-in with
/// no approval run.</para>
/// </remarks>
public sealed class WorkflowBackedAccessRequestApprovalService : IAccessRequestApprovalService
{
    private const string OutcomeApproved = "approved";
    private const string OutcomeEligible = "eligible";
    private const string OutcomeRejected = "rejected";
    private const string OutcomeWithdrawn = "withdrawn";

    private readonly AccessRequestApprovalService inner;
    private readonly IAccessRequestStore requests;
    private readonly ISecuredWorkflowManagement management;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly PublishAccessDecisionProducer decisions;
    private readonly string approvalWorkflowId;
    private readonly string environment;
    private readonly SecurityTagSet runSecurityTags;
    private readonly ILogger? logger;

    /// <summary>Initializes a new instance of the <see cref="WorkflowBackedAccessRequestApprovalService"/> class.</summary>
    /// <param name="inner">The built-in strategy this composes: it owns the store, the platform ceiling, self-elevation, and the two system grant paths (<see cref="AccessRequestApprovalService.GrantRequestAsync"/>/<see cref="AccessRequestApprovalService.GrantRequestAsEligibleAsync"/>) the approval workflow calls.</param>
    /// <param name="requests">The access-request store (for the read-then-publish decision paths).</param>
    /// <param name="management">The workflow management the approval run is started through.</param>
    /// <param name="catalog">The catalog client, for the §15-administrator check on approve/deny.</param>
    /// <param name="decisions">The generated producer that publishes an <c>access.decision</c> message.</param>
    /// <param name="approvalWorkflowId">The versioned id of the bootstrapped approval workflow to start (e.g. <c>access-approval-v1</c>).</param>
    /// <param name="environment">The control-plane internal environment the approval run executes in (served by the system runner).</param>
    /// <param name="logger">An optional logger for run-orchestration diagnostics.</param>
    public WorkflowBackedAccessRequestApprovalService(
        AccessRequestApprovalService inner,
        IAccessRequestStore requests,
        ISecuredWorkflowManagement management,
        ISecuredWorkflowCatalog catalog,
        PublishAccessDecisionProducer decisions,
        string approvalWorkflowId,
        string environment,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentException.ThrowIfNullOrEmpty(approvalWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        this.inner = inner;
        this.requests = requests;
        this.management = management;
        this.catalog = catalog;
        this.decisions = decisions;
        this.approvalWorkflowId = approvalWorkflowId;
        this.environment = environment;

        // The approval run carries the workflow's own identity (sys:workflow=<base id>), so the §13 usage gate at run time
        // admits the runner's 'controlplane' OAuth2 credential (usage-scoped to that identity): a run may present the
        // accessRequests:grant token only because it IS a run of the access-approval workflow, not any other workflow.
        this.runSecurityTags = SecurityTagSet.FromTags([WorkflowIdentity.WorkflowTag(CatalogPackage.StripVersionSuffix(approvalWorkflowId))]);
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> SubmitAsync(AccessRequest draft, string actor, ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        // The built-in creates the request and auto-approves it when the requester is eligible to self-elevate; only a
        // request left pending needs a human decision, so only then is an approval run started.
        ParsedJsonDocument<AccessRequest> created = await this.inner.SubmitAsync(draft, actor, principal, cancellationToken).ConfigureAwait(false);
        if (created.RootElement.HasStatus(AccessRequestStatus.Pending))
        {
            await this.StartApprovalRunAsync(created.RootElement, cancellationToken).ConfigureAwait(false);
        }

        return created;
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AccessRequest>?> ApproveAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken)
        => this.DecideByPublishingAsync(requestId, approverIdentity, actor, reason, OutcomeApproved, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AccessRequest>?> ApproveAsEligibleAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, TimeSpan? eligibilityWindow, CancellationToken cancellationToken)
        => this.DecideByPublishingAsync(requestId, approverIdentity, actor, reason, OutcomeEligible, cancellationToken);

    /// <inheritdoc/>
    // Deny is now workflow-backed exactly like approve: authenticate the §15 administrator and publish the 'rejected'
    // outcome, and the access-approval run enacts the denial through settleAccessRequest. The request is returned still
    // pending and reaches Denied when the run completes, so every approver decision flows through the one run instead of
    // deny short-circuiting it with a synchronous mark.
    public ValueTask<ParsedJsonDocument<AccessRequest>?> DenyAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken)
        => this.DecideByPublishingAsync(requestId, approverIdentity, actor, reason, OutcomeRejected, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> WithdrawAsync(string requestId, string subjectClaimType, string subjectClaimValue, string actor, CancellationToken cancellationToken)
    {
        // The built-in marks the request Withdrawn (only the requester may); then the run is resumed so its withdrawn
        // path runs, so a requester's withdrawal closes the approval run exactly like an approver's decision.
        ParsedJsonDocument<AccessRequest>? withdrawn = await this.inner.WithdrawAsync(requestId, subjectClaimType, subjectClaimValue, actor, cancellationToken).ConfigureAwait(false);
        if (withdrawn is not null)
        {
            await this.PublishDecisionAsync(requestId, OutcomeWithdrawn, actor, reason: null, cancellationToken).ConfigureAwait(false);
        }

        return withdrawn;
    }

    /// <inheritdoc/>
    // Revocation acts on an already-decided grant or eligibility assignment, so there is no suspended run to resume.
    public ValueTask<ParsedJsonDocument<AccessRequest>?> RevokeAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken)
        => this.inner.RevokeAsync(requestId, approverIdentity, actor, reason, cancellationToken);

    /// <inheritdoc/>
    // The approval workflow calls these (grantAccessRequest / grantAccessRequestAsEligible) to enact the decision; they
    // delegate to the built-in, which writes the ceiling-bounded grant with no administrator check.
    public ValueTask<ParsedJsonDocument<AccessRequest>?> GrantRequestAsync(string requestId, string actor, string? reason, CancellationToken cancellationToken)
        => this.inner.GrantRequestAsync(requestId, actor, reason, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AccessRequest>?> GrantRequestAsEligibleAsync(string requestId, string actor, string? reason, CancellationToken cancellationToken)
        => this.inner.GrantRequestAsEligibleAsync(requestId, actor, reason, cancellationToken);

    /// <inheritdoc/>
    // The single enactment the approval workflow calls: delegates to the built-in, which grants (approved/eligible) or
    // marks the request terminal (rejected/withdrawn) under the platform ceiling with no administrator check.
    public ValueTask<ParsedJsonDocument<AccessRequest>?> SettleRequestAsync(string requestId, string outcome, string actor, string? reason, CancellationToken cancellationToken)
        => this.inner.SettleRequestAsync(requestId, outcome, actor, reason, cancellationToken);

    // Authenticates the approver as a §15 administrator, publishes the decision outcome, and returns the request as it
    // now stands (still pending; it reaches its terminal state when the workflow enacts the grant). Used by approve and
    // approveAsEligible, which differ only in the outcome the workflow branches on.
    private async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideByPublishingAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, string outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(actor);
        ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return null;
        }

        try
        {
            AccessRequest request = fetched.RootElement;
            RequirePending(request);
            await this.EnsureAdministratorAsync(request.BaseWorkflowIdValue, approverIdentity, cancellationToken).ConfigureAwait(false);
            await this.PublishDecisionAsync(requestId, outcome, actor, reason, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            fetched.Dispose();
            throw;
        }

        // The request is returned in its still-pending state; the workflow enacts the terminal transition asynchronously.
        return fetched;
    }

    // Starts the approval run in the internal environment (served by the system runner), keyed on the request id. The
    // run carries no requester security tags: it executes under the workflow's own §13 system credential.
    private async ValueTask StartApprovalRunAsync(AccessRequest request, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<AccessApprovalInputs> inputs = BuildRunInputs(request);
        try
        {
            await this.management.StartAsync(this.approvalWorkflowId, inputs.RootElement, correlationId: null, tags: default, securityTags: this.runSecurityTags, environment: this.environment, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The request is created but its approval run could not start; surface it so the caller can retry rather than
            // leave a request whose approve/reject would publish to a run that is not listening.
            this.logger?.LogError(ex, "Failed to start the approval workflow run for access request {RequestId}.", request.IdValue);
            throw;
        }
    }

    // Builds the approval workflow's inputs ({requestId, baseWorkflowId, requestedScopes, requester, subject}) with the
    // generated typed model: each field is a From-copy of the request's own JSON value (no per-field string realised, the
    // scopes array copied whole), and Create(...) returns an owning ParsedJsonDocument that outlives the StartAsync await.
    private static ParsedJsonDocument<AccessApprovalInputs> BuildRunInputs(AccessRequest request)
        => AccessApprovalInputs.Create(
            requestId: SwString.From(request.Id),
            baseWorkflowId: SwString.From(request.BaseWorkflowId),
            requestedScopes: AccessApprovalInputs.JsonStringArray.From(request.RequestedScopes),
            requester: request.RequesterLabel.IsNotUndefined() ? SwString.From(request.RequesterLabel) : default,
            subject: SwString.From(request.SubjectClaimValue));

    private async ValueTask PublishDecisionAsync(string requestId, string outcome, string decidedBy, string? reason, CancellationToken cancellationToken)
    {
        // Create(...) returns an owning ParsedJsonDocument, so there is no workspace to keep alive across the publish await.
        SwModels.JsonString.Source reasonSource = reason is { } r ? r : default(SwModels.JsonString.Source);
        using ParsedJsonDocument<SwModels.AccessDecisionPayload> payload = SwModels.AccessDecisionPayload.Create(
            decidedBy: decidedBy,
            outcome: outcome,
            requestId: requestId,
            reason: reasonSource);
        await this.decisions.PublishAccessDecisionAsync(payload.RootElement, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureAdministratorAsync(string baseWorkflowId, SecurityTagSet approverIdentity, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<WorkflowAdministrators>? record = await this.catalog.GetAdministratorsAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        if (record?.RootElement.IsAdministeredBy(approverIdentity) != true)
        {
            throw new WorkflowAdministrationException(baseWorkflowId);
        }
    }

    // Checked string-free; the actual status name is realised only on the throw path.
    private static void RequirePending(AccessRequest request)
    {
        if (!request.HasStatus(AccessRequestStatus.Pending))
        {
            throw new AccessRequestStateException(request.IdValue, $"The request is {request.StatusValue}, not {AccessRequestStatusNames.ToWire(AccessRequestStatus.Pending)}.");
        }
    }
}