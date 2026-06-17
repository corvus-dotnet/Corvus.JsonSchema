// <copyright file="ArazzoControlPlaneAccessRequestsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiAccessRequestsHandler"/> over the <see cref="IAccessRequestApprovalService"/>
/// (the approval flow) and the <see cref="IAccessRequestStore"/> (the read paths) — the control-plane access-request
/// surface (design §16.5). These operations are authorized by the per-workflow §15-administrator gate the service
/// enforces (and a row-level visibility check on the read paths), not a global capability scope.
/// </summary>
/// <remarks>
/// The requesting subject and self-elevation eligibility are read from the current principal (the subject claim the
/// deployment configures, and an optional eligibility predicate); the approver/visibility identity is the unforgeable
/// <c>sys:</c> identity the row-security policy stamps (<see cref="ControlPlaneAccess.InternalTags"/>). A grant can
/// therefore never target a third party, and only the requester or an administrator of the target workflow can see a
/// request.
/// </remarks>
public sealed class ArazzoControlPlaneAccessRequestsHandler : IApiAccessRequestsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IAccessRequestApprovalService approval;
    private readonly IAccessRequestStore requests;
    private readonly IWorkflowCatalogClient catalog;
    private readonly ControlPlaneAccess access;
    private readonly string subjectClaimType;
    private readonly Func<ClaimsPrincipal, AccessRequestDefinition, bool>? eligibility;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneAccessRequestsHandler"/> class.</summary>
    /// <param name="approval">The approval service the submit/approve/deny/withdraw/revoke operations delegate to.</param>
    /// <param name="requests">The access-request store the list/get read paths use.</param>
    /// <param name="catalog">The catalog client, for the §15-administrator visibility checks on the read paths.</param>
    /// <param name="access">Resolves the caller's deployment identity and the current principal per request.</param>
    /// <param name="subjectClaimType">The claim type identifying the requesting subject (and that a grant keys on); default <c>sub</c>.</param>
    /// <param name="eligibility">An optional predicate deciding whether a requester is eligible to self-elevate a request (§16.5.3); default never eligible.</param>
    internal ArazzoControlPlaneAccessRequestsHandler(
        IAccessRequestApprovalService approval,
        IAccessRequestStore requests,
        IWorkflowCatalogClient catalog,
        ControlPlaneAccess access,
        string subjectClaimType = "sub",
        Func<ClaimsPrincipal, AccessRequestDefinition, bool>? eligibility = null)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentException.ThrowIfNullOrEmpty(subjectClaimType);
        this.approval = approval;
        this.requests = requests;
        this.catalog = catalog;
        this.access = access;
        this.subjectClaimType = subjectClaimType;
        this.eligibility = eligibility;
    }

    /// <inheritdoc/>
    public async ValueTask<SubmitAccessRequestResult> HandleSubmitAccessRequestAsync(SubmitAccessRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal? principal = this.access.CurrentPrincipal;
        string? subject = this.SubjectOf(principal);
        if (principal is null || string.IsNullOrEmpty(subject))
        {
            return SubmitAccessRequestResult.BadRequest(Problem("no-subject", "No requesting subject", 400, $"The caller has no '{this.subjectClaimType}' claim to scope a grant to."), workspace);
        }

        var scopes = new List<string>();
        foreach (Models.JsonString scope in parameters.Body.RequestedScopes.EnumerateArray())
        {
            scopes.Add((string)scope);
        }

        var definition = new AccessRequestDefinition(
            (string)parameters.Body.BaseWorkflowId,
            scopes,
            this.subjectClaimType,
            subject,
            principal.Identity?.Name,
            parameters.Body.Reason.IsNotUndefined() ? (string)parameters.Body.Reason : null,
            parameters.Body.RequestedDurationSeconds.IsNotUndefined() ? (long)parameters.Body.RequestedDurationSeconds : null);

        bool eligible = this.eligibility?.Invoke(principal, definition) ?? false;
        try
        {
            ParsedJsonDocument<AccessRequest> created = await this.approval.SubmitAsync(definition, ActorOf(principal), eligible, cancellationToken).ConfigureAwait(false);
            workspace.TakeOwnership(created);
            return SubmitAccessRequestResult.Created(ToView(created.RootElement), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return SubmitAccessRequestResult.BadRequest(Problem("invalid-request", "Invalid access request", 400, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ListAccessRequestsResult> HandleListAccessRequestsAsync(ListAccessRequestsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        AccessRequestStatus? status = ParseStatus(parameters.Status);
        string? baseWorkflowId = parameters.BaseWorkflowId.IsNotUndefined() ? (string)parameters.BaseWorkflowId : null;

        AccessRequestQuery query;
        if (baseWorkflowId is not null)
        {
            // The approver queue for a workflow — only an administrator of it may list it.
            if (!await this.IsAdministratorAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false))
            {
                return ListAccessRequestsResult.Forbidden(NotAdministratorProblem(baseWorkflowId), workspace);
            }

            query = new AccessRequestQuery(status, baseWorkflowId);
        }
        else
        {
            // The caller's own requests.
            string? subject = this.SubjectOf(this.access.CurrentPrincipal);
            if (string.IsNullOrEmpty(subject))
            {
                return ListAccessRequestsResult.Ok(EmptyList(), workspace);
            }

            query = new AccessRequestQuery(status, null, this.subjectClaimType, subject);
        }

        using PooledDocumentList<AccessRequest> list = await this.requests.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return ListAccessRequestsResult.Ok(ToList(list), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetAccessRequestResult> HandleGetAccessRequestAsync(GetAccessRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return GetAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
        }

        // The wrapped view references the pooled document, so the workspace owns it for the response's lifetime;
        // taking ownership before the (awaiting) visibility check also means a throw cannot leak the rented buffer.
        workspace.TakeOwnership(fetched);
        AccessRequest request = fetched.RootElement;
        if (!this.IsRequester(request) && !await this.IsAdministratorAsync(request.BaseWorkflowIdValue, cancellationToken).ConfigureAwait(false))
        {
            return GetAccessRequestResult.Forbidden(NotVisibleProblem(id), workspace);
        }

        return GetAccessRequestResult.Ok(ToView(request), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ApproveAccessRequestResult> HandleApproveAccessRequestAsync(ApproveAccessRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.ApproveAsync(id, this.CallerIdentity(), this.CallerActor(), NoteReason(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return ApproveAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(result);
            return ApproveAccessRequestResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return ApproveAccessRequestResult.Forbidden(NotAdministratorProblem(id), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return ApproveAccessRequestResult.Conflict(Problem("invalid-state", "Cannot approve the request", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ApproveAccessRequestAsEligibleResult> HandleApproveAccessRequestAsEligibleAsync(ApproveAccessRequestAsEligibleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.ApproveAsEligibleAsync(id, this.CallerIdentity(), this.CallerActor(), EligibilityReason(parameters.Body), EligibilityWindow(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return ApproveAccessRequestAsEligibleResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(result);
            return ApproveAccessRequestAsEligibleResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return ApproveAccessRequestAsEligibleResult.Forbidden(NotAdministratorProblem(id), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return ApproveAccessRequestAsEligibleResult.Conflict(Problem("invalid-state", "Cannot grant eligibility for the request", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DenyAccessRequestResult> HandleDenyAccessRequestAsync(DenyAccessRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.DenyAsync(id, this.CallerIdentity(), this.CallerActor(), NoteReason(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return DenyAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(result);
            return DenyAccessRequestResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return DenyAccessRequestResult.Forbidden(NotAdministratorProblem(id), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return DenyAccessRequestResult.Conflict(Problem("invalid-state", "Cannot deny the request", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WithdrawAccessRequestResult> HandleWithdrawAccessRequestAsync(WithdrawAccessRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        string? subject = this.SubjectOf(this.access.CurrentPrincipal);
        if (string.IsNullOrEmpty(subject))
        {
            return WithdrawAccessRequestResult.Forbidden(Problem("not-requester", "Not the requester", 403, "Only the requester may withdraw their request."), workspace);
        }

        // Pre-check requester ownership so a non-requester is refused (403) distinctly from a wrong-state conflict (409).
        using (ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(id, cancellationToken).ConfigureAwait(false))
        {
            if (fetched is null)
            {
                return WithdrawAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            if (!this.IsRequester(fetched.RootElement))
            {
                return WithdrawAccessRequestResult.Forbidden(Problem("not-requester", "Not the requester", 403, "Only the requester may withdraw their request."), workspace);
            }
        }

        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.WithdrawAsync(id, this.subjectClaimType, subject, this.CallerActor(), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return WithdrawAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(result);
            return WithdrawAccessRequestResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return WithdrawAccessRequestResult.Conflict(Problem("invalid-state", "Cannot withdraw the request", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RevokeAccessRequestResult> HandleRevokeAccessRequestAsync(RevokeAccessRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.RevokeAsync(id, this.CallerIdentity(), this.CallerActor(), NoteReason(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return RevokeAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(result);
            return RevokeAccessRequestResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return RevokeAccessRequestResult.Forbidden(NotAdministratorProblem(id), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return RevokeAccessRequestResult.Conflict(Problem("invalid-state", "Cannot revoke the grant", 409, ex.Message), workspace);
        }
    }

    private static AccessRequestStatus? ParseStatus(Models.GetAccessRequestsStatus status)
        => status.IsNotUndefined() && Enum.TryParse((string)status, out AccessRequestStatus parsed) ? parsed : null;

    private static string? NoteReason(Models.AccessRequestDecisionNote body)
        => body.IsNotUndefined() && body.Reason.IsNotUndefined() ? (string)body.Reason : null;

    private static string? EligibilityReason(Models.AccessRequestEligibilityNote body)
        => body.IsNotUndefined() && body.Reason.IsNotUndefined() ? (string)body.Reason : null;

    private static TimeSpan? EligibilityWindow(Models.AccessRequestEligibilityNote body)
        => body.IsNotUndefined() && body.EligibilityWindowSeconds.IsNotUndefined() ? TimeSpan.FromSeconds((long)body.EligibilityWindowSeconds) : null;

    // The audit actor recorded on a request (createdBy / decidedBy): the principal's configured subject claim — the
    // same canonical identity the grant keys on — falling back to the authentication name, then "anonymous". (The
    // unforgeable authorization identity is the sys: tag set from CallerIdentity; this is the human-facing audit name.)
    private string ActorOf(ClaimsPrincipal? principal) => this.SubjectOf(principal) ?? principal?.Identity?.Name ?? "anonymous";

    // A single-document response wraps the stored element with no materialization: AccessRequestView is an exact,
    // congruent projection of the persisted AccessRequest (identical property names, types, and required set — see
    // Schemas/AccessRequest.json), so From() is a pointer reinterpret and the body serializes the backing verbatim
    // (the catalog handler's CatalogVersionSummary.From). The wrapped value references the pooled document, so the
    // caller hands that document to the workspace (TakeOwnership) for the response's lifetime.
    private static Models.AccessRequestView ToView(AccessRequest request) => Models.AccessRequestView.From(request);

    // A list response is built from a PooledDocumentList whose documents are disposed when the handler returns, so
    // each item is materialized into the list builder's own arena as it is added — the security/credentials list
    // pattern (ArazzoControlPlaneSecurityHandler.ToRuleSource, ...CredentialsHandler.ToSummary). A whole-document
    // From() wrap cannot be used here: AddItem stores it as a reference into the pooled buffers, which the array's
    // validation/serialization (run after the handler returns and the batch is disposed) would then read.
    private static Models.AccessRequestView.Source ToViewSource(AccessRequest request)
        => new((ref Models.AccessRequestView.Builder b) =>
        {
            Models.JsonDateTime.Source decidedAt = default;
            if (request.DecidedAtValue is { } decidedAtValue)
            {
                decidedAt = decidedAtValue;
            }

            Models.JsonString.Source decidedBy = default;
            if (request.DecidedByOrNull is { } decidedByValue)
            {
                decidedBy = decidedByValue;
            }

            Models.JsonString.Source decisionReason = default;
            if (request.DecisionReasonOrNull is { } decisionReasonValue)
            {
                decisionReason = decisionReasonValue;
            }

            Models.JsonString.Source grantedBindingId = default;
            if (request.GrantedBindingIdOrNull is { } grantedBindingIdValue)
            {
                grantedBindingId = grantedBindingIdValue;
            }

            Models.JsonDateTime.Source grantedUntil = default;
            if (request.GrantedUntilValue is { } grantedUntilValue)
            {
                grantedUntil = grantedUntilValue;
            }

            Models.JsonString.Source reason = default;
            if (request.ReasonOrNull is { } reasonValue)
            {
                reason = reasonValue;
            }

            Models.JsonInt64.Source requestedDurationSeconds = default;
            if (request.RequestedDurationSecondsOrNull is { } durationValue)
            {
                requestedDurationSeconds = durationValue;
            }

            Models.JsonString.Source requesterLabel = default;
            if (request.RequesterLabelOrNull is { } requesterLabelValue)
            {
                requesterLabel = requesterLabelValue;
            }

            b.Create(
                baseWorkflowId: request.BaseWorkflowIdValue,
                createdAt: request.CreatedAtValue,
                createdBy: request.CreatedByValue,
                etag: request.EtagValue.Value ?? string.Empty,
                id: request.IdValue,
                requestedScopes: new Models.AccessRequestView.JsonStringArray.Source((ref Models.AccessRequestView.JsonStringArray.Builder array) =>
                {
                    foreach (JsonString scope in request.RequestedScopes.EnumerateArray())
                    {
                        array.AddItem((string)scope);
                    }
                }),
                status: request.StatusValue,
                subjectClaimType: request.SubjectClaimTypeValue,
                subjectClaimValue: request.SubjectClaimValueValue,
                decidedAt: decidedAt,
                decidedBy: decidedBy,
                decisionReason: decisionReason,
                grantedBindingId: grantedBindingId,
                grantedUntil: grantedUntil,
                reason: reason,
                requestedDurationSeconds: requestedDurationSeconds,
                requesterLabel: requesterLabel);
        });

    private static Models.AccessRequestList.Source ToList(PooledDocumentList<AccessRequest> list)
        => new((ref Models.AccessRequestList.Builder b) => b.Create(
            accessRequests: new Models.AccessRequestList.AccessRequestViewArray.Source((ref Models.AccessRequestList.AccessRequestViewArray.Builder array) =>
            {
                foreach (AccessRequest request in list)
                {
                    array.AddItem(ToViewSource(request));
                }
            })));

    private static Models.AccessRequestList.Source EmptyList()
        => new((ref Models.AccessRequestList.Builder b) => b.Create(
            accessRequests: new Models.AccessRequestList.AccessRequestViewArray.Source((ref Models.AccessRequestList.AccessRequestViewArray.Builder array) => { })));

    private static Models.ProblemDetails.Source NotFoundProblem(string id)
        => Problem("access-request-not-found", "Access request not found", 404, $"No access request '{id}' exists.");

    private static Models.ProblemDetails.Source NotAdministratorProblem(string target)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not an administrator of '{target}'.");

    private static Models.ProblemDetails.Source NotVisibleProblem(string id)
        => Problem("access-request-not-visible", "Not visible", 403, $"You may not view access request '{id}'.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    private SecurityTagSet CallerIdentity() => SecurityTagSet.FromTags(this.access.InternalTags());

    private string CallerActor() => ActorOf(this.access.CurrentPrincipal);

    private string? SubjectOf(ClaimsPrincipal? principal) => principal?.FindFirst(this.subjectClaimType)?.Value;

    private bool IsRequester(AccessRequest request)
    {
        string? subject = this.SubjectOf(this.access.CurrentPrincipal);
        return subject is not null
            && string.Equals(request.SubjectClaimTypeValue, this.subjectClaimType, StringComparison.Ordinal)
            && string.Equals(request.SubjectClaimValueValue, subject, StringComparison.Ordinal);
    }

    private async ValueTask<bool> IsAdministratorAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        SecurityTagSet caller = this.CallerIdentity();
        IReadOnlyList<SecurityTagSet> administrators = await this.catalog.GetAdministratorsAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        foreach (SecurityTagSet administrator in administrators)
        {
            if (WorkflowIdentity.SameAdministrator(administrator, caller))
            {
                return true;
            }
        }

        return false;
    }
}