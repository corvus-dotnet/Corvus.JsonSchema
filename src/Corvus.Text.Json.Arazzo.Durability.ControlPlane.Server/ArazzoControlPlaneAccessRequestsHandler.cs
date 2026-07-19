// <copyright file="ArazzoControlPlaneAccessRequestsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiAccessRequestsHandler"/> over the <see cref="IAccessRequestApprovalService"/>
/// (the approval flow) and the <see cref="IAccessRequestStore"/> (the read paths) — the control-plane access-request
/// surface (design §16.5). These operations are authorized by the per-workflow §15-administrator gate the service
/// enforces (and a row-level visibility check on the read paths), not a global capability scope.
/// </summary>
/// <remarks>
/// The requesting subject is read from the current principal (the subject claim the deployment configures), and the
/// whole principal is handed to the approval service, which resolves self-elevation eligibility itself (§16.5.3); the
/// approver/visibility identity is the unforgeable <c>sys:</c> identity the row-security policy stamps
/// (<see cref="ControlPlaneAccess.InternalTags"/>). A grant can therefore never target a third party, and only the
/// requester or an administrator of the target workflow can see a request.
/// </remarks>
public sealed class ArazzoControlPlaneAccessRequestsHandler : IApiAccessRequestsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    // The count operation's bound: badges/footers need "is there work / roughly how much", not exact-beyond-99.
    private const int CountCap = 100;

    private readonly IAccessRequestApprovalService approval;
    private readonly IAccessRequestStore requests;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly ControlPlaneAccess access;
    private readonly string subjectClaimType;
    private readonly ILogger? auditLogger;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneAccessRequestsHandler"/> class.</summary>
    /// <param name="approval">The approval service the submit/approve/deny/withdraw/revoke operations delegate to (it resolves self-elevation eligibility from the principal, §16.5.3).</param>
    /// <param name="requests">The access-request store the list/get read paths use.</param>
    /// <param name="catalog">The catalog client, for the §15-administrator visibility checks on the read paths.</param>
    /// <param name="access">Resolves the caller's deployment identity and the current principal per request.</param>
    /// <param name="subjectClaimType">The claim type identifying the requesting subject (and that a grant keys on); default <c>sub</c>.</param>
    /// <param name="auditLogger">The logger for the §850 governance-decision audit (who decided which request, with what outcome); the audit span rides the always-registered <see cref="ArazzoTelemetry.ActivitySource"/> regardless.</param>
    internal ArazzoControlPlaneAccessRequestsHandler(
        IAccessRequestApprovalService approval,
        IAccessRequestStore requests,
        ISecuredWorkflowCatalog catalog,
        ControlPlaneAccess access,
        string subjectClaimType = "sub",
        ILogger? auditLogger = null)
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
        this.auditLogger = auditLogger;
    }

    // The audited resource kind for every action on this surface (design §850).
    private const string TargetKind = "access-request";

    /// <inheritdoc/>
    public async ValueTask<SubmitAccessRequestResult> HandleSubmitAccessRequestAsync(SubmitAccessRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal? principal = this.access.CurrentPrincipal;
        string? subject = this.SubjectOf(principal);
        if (principal is null || string.IsNullOrEmpty(subject))
        {
            return SubmitAccessRequestResult.BadRequest(Problem("no-subject", "No requesting subject", 400, $"The caller has no '{this.subjectClaimType}' claim to scope a grant to."), workspace);
        }

        // The draft request the approval pipeline + store carry: the body's already-parsed JSON values
        // (baseWorkflowId/requestedScopes/reason) copied bytes-to-bytes — no List<string>, no per-field strings — plus the
        // principal-derived subject/label. It is a pooled, disposable document SubmitAsync reads; the store stamps
        // id/etag/created.
        using ParsedJsonDocument<AccessRequest> draft = AccessRequest.Draft(
            (JsonElement)parameters.Body.BaseWorkflowId,
            (JsonElement)parameters.Body.RequestedScopes,
            this.subjectClaimType,
            subject,
            PrincipalDisplayName.Resolve(principal),
            (JsonElement)parameters.Body.Reason,
            parameters.Body.RequestedDurationSeconds.IsNotUndefined() ? (long)parameters.Body.RequestedDurationSeconds : null);

        // The service resolves self-elevation eligibility from the principal itself (§16.5.3: claims ∪ stored).
        try
        {
            ParsedJsonDocument<AccessRequest> created = await this.approval.SubmitAsync(draft.RootElement, ActorOf(principal), principal, cancellationToken).ConfigureAwait(false);
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

            // Carry the base workflow id to the store as its request JSON value (reified only inside the store at its own
            // leaf — a DB parameter / a span compare); the string above is the handler's own leaf for the admin check + error.
            query = new AccessRequestQuery(status, JsonString.From(parameters.BaseWorkflowId));
        }
        else if (IsQueueScope(parameters.Scope))
        {
            // The approver inbox (§16.5): every request across the workflows the caller administers, resolved from the
            // reverse administration index (§15.4). A caller who administers nothing gets an empty page — so the store
            // never sees an empty administered set. The set is a server-derived leaf, reified per backend at its own seam.
            IReadOnlyList<string> administered = await this.catalog.ListAdministeredWorkflowsAsync(this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            if (administered.Count == 0)
            {
                return ListAccessRequestsResult.Ok(EmptyList(), workspace);
            }

            query = new AccessRequestQuery(status, AdministeredBaseWorkflowIds: administered);
        }
        else
        {
            // The caller's own requests.
            string? subject = this.SubjectOf(this.access.CurrentPrincipal);
            if (string.IsNullOrEmpty(subject))
            {
                return ListAccessRequestsResult.Ok(EmptyList(), workspace);
            }

            query = new AccessRequestQuery(status, default, this.subjectClaimType, subject);
        }

        // An absent limit passes 0 — the contract's "use the store's default page size" sentinel; the page token flows to
        // the store as its JSON value (From() rewraps parameters.PageToken — free, no managed string), decoded
        // bytes-native into one keyset page (bounded — never the whole queue).
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using AccessRequestPage page = await this.requests.ListAsync(query, limit, pageToken, cancellationToken).ConfigureAwait(false);

        // Each item is a whole-document AccessRequestView.From wrap (the congruent projection ToView uses), so it
        // references its pooled document; the body is validated/serialized after this handler returns, so hand the
        // documents to the workspace (it disposes them at request end; `using page` then only returns the batch's backing
        // array + the token buffer). The list body is built closure-free; the continuation token is copied verbatim from
        // the page's UTF-8 (the Ok materialisation copies the scalar before dispose).
        page.Requests.TransferOwnershipTo(workspace);
        IReadOnlyList<AccessRequest> requestList = page.Requests;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.AccessRequestList.Source<IReadOnlyList<AccessRequest>> body = Models.AccessRequestList.Build(
            in requestList,
            accessRequests: Models.AccessRequestList.AccessRequestViewArray.Build(in requestList, BuildAccessRequestViews),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return ListAccessRequestsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CountAccessRequestsResult> HandleCountAccessRequestsAsync(CountAccessRequestsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Mirrors HandleListAccessRequestsAsync's reach/query build exactly (so the count can't drift from the list it
        // annotates), minus paging — the store returns only a bounded total (design §16.5: badges/footers), never rows.
        AccessRequestStatus? status = ParseCountStatus(parameters.Status);
        string? baseWorkflowId = parameters.BaseWorkflowId.IsNotUndefined() ? (string)parameters.BaseWorkflowId : null;

        AccessRequestQuery query;
        if (baseWorkflowId is not null)
        {
            // The approver queue for a workflow — only an administrator of it may count it (the list's 403).
            if (!await this.IsAdministratorAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false))
            {
                return CountAccessRequestsResult.Forbidden(NotAdministratorProblem(baseWorkflowId), workspace);
            }

            query = new AccessRequestQuery(status, JsonString.From(parameters.BaseWorkflowId));
        }
        else if (IsCountQueueScope(parameters.Scope))
        {
            // The approver inbox (§16.5): a caller who administers nothing counts zero — the store never sees an empty set.
            IReadOnlyList<string> administered = await this.catalog.ListAdministeredWorkflowsAsync(this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            if (administered.Count == 0)
            {
                return CountResult(0, false, workspace);
            }

            query = new AccessRequestQuery(status, AdministeredBaseWorkflowIds: administered);
        }
        else
        {
            // The caller's own requests.
            string? subject = this.SubjectOf(this.access.CurrentPrincipal);
            if (string.IsNullOrEmpty(subject))
            {
                return CountResult(0, false, workspace);
            }

            query = new AccessRequestQuery(status, default, this.subjectClaimType, subject);
        }

        (int count, bool capped) = await this.requests.CountAsync(query, CountCap, cancellationToken).ConfigureAwait(false);
        return CountResult(count, capped, workspace);
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
        if (await this.IsOwnRequestAsync(id, cancellationToken).ConfigureAwait(false))
        {
            GovernanceAudit.Mutation(this.auditLogger, "access-request.approve", this.CallerActor(), TargetKind, id, "refused-own-request");
            return ApproveAccessRequestResult.Forbidden(OwnRequestProblem(), workspace);
        }

        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.ApproveAsync(id, this.CallerIdentity(), this.CallerActor(), NoteReason(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return ApproveAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            GovernanceAudit.Mutation(this.auditLogger, "access-request.approve", this.CallerActor(), TargetKind, id, "granted");
            workspace.TakeOwnership(result);
            return ApproveAccessRequestResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            GovernanceAudit.Mutation(this.auditLogger, "access-request.approve", this.CallerActor(), TargetKind, id, "refused-not-administrator");
            return ApproveAccessRequestResult.Forbidden(NotAdministratorProblem(id), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return ApproveAccessRequestResult.Conflict(Problem("invalid-state", "Cannot approve the request", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GrantAccessRequestResult> HandleGrantAccessRequestAsync(GrantAccessRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;

        // No own-request or §15-administrator check: the caller is the approval workflow's §13 system credential (gated by
        // the narrow accessRequests:grant capability), not the approver in person. The approval DECISION was made inside
        // the workflow; this only enacts the ceiling-bounded grant it authorized (design §16.5.1). The platform ceiling in
        // the approval service still applies unconditionally (run access only, bound to the requester, workflow-scoped).
        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.GrantRequestAsync(id, this.CallerActor(), NoteReason(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return GrantAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            GovernanceAudit.Mutation(this.auditLogger, "access-request.grant", this.CallerActor(), TargetKind, id, "granted");
            workspace.TakeOwnership(result);
            return GrantAccessRequestResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return GrantAccessRequestResult.Conflict(Problem("invalid-state", "Cannot grant the request", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GrantAccessRequestAsEligibleResult> HandleGrantAccessRequestAsEligibleAsync(GrantAccessRequestAsEligibleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;

        // The sibling of HandleGrantAccessRequestAsync, writing standing eligibility (design §16.5.3): no own-request or
        // §15-administrator check (the caller is the approval workflow's §13 system credential); the decision was made
        // inside the workflow. The platform ceiling still applies (run access only, bound to the requester, workflow-scoped).
        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.GrantRequestAsEligibleAsync(id, this.CallerActor(), NoteReason(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return GrantAccessRequestAsEligibleResult.NotFound(NotFoundProblem(id), workspace);
            }

            GovernanceAudit.Mutation(this.auditLogger, "access-request.grant-eligible", this.CallerActor(), TargetKind, id, "eligible");
            workspace.TakeOwnership(result);
            return GrantAccessRequestAsEligibleResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return GrantAccessRequestAsEligibleResult.Conflict(Problem("invalid-state", "Cannot grant eligibility for the request", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ApproveAccessRequestAsEligibleResult> HandleApproveAccessRequestAsEligibleAsync(ApproveAccessRequestAsEligibleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        if (await this.IsOwnRequestAsync(id, cancellationToken).ConfigureAwait(false))
        {
            GovernanceAudit.Mutation(this.auditLogger, "access-request.approve-eligible", this.CallerActor(), TargetKind, id, "refused-own-request");
            return ApproveAccessRequestAsEligibleResult.Forbidden(OwnRequestProblem(), workspace);
        }

        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.ApproveAsEligibleAsync(id, this.CallerIdentity(), this.CallerActor(), EligibilityReason(parameters.Body), EligibilityWindow(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return ApproveAccessRequestAsEligibleResult.NotFound(NotFoundProblem(id), workspace);
            }

            GovernanceAudit.Mutation(this.auditLogger, "access-request.approve-eligible", this.CallerActor(), TargetKind, id, "eligible");
            workspace.TakeOwnership(result);
            return ApproveAccessRequestAsEligibleResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            GovernanceAudit.Mutation(this.auditLogger, "access-request.approve-eligible", this.CallerActor(), TargetKind, id, "refused-not-administrator");
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
        if (await this.IsOwnRequestAsync(id, cancellationToken).ConfigureAwait(false))
        {
            GovernanceAudit.Mutation(this.auditLogger, "access-request.deny", this.CallerActor(), TargetKind, id, "refused-own-request");
            return DenyAccessRequestResult.Forbidden(OwnRequestProblem(), workspace);
        }

        try
        {
            ParsedJsonDocument<AccessRequest>? result = await this.approval.DenyAsync(id, this.CallerIdentity(), this.CallerActor(), NoteReason(parameters.Body), cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return DenyAccessRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            GovernanceAudit.Mutation(this.auditLogger, "access-request.deny", this.CallerActor(), TargetKind, id, "denied");
            workspace.TakeOwnership(result);
            return DenyAccessRequestResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            GovernanceAudit.Mutation(this.auditLogger, "access-request.deny", this.CallerActor(), TargetKind, id, "refused-not-administrator");
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
                GovernanceAudit.Mutation(this.auditLogger, "access-request.withdraw", this.CallerActor(), TargetKind, id, "refused-not-requester");
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

            GovernanceAudit.Mutation(this.auditLogger, "access-request.withdraw", this.CallerActor(), TargetKind, id, "withdrawn");
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

            GovernanceAudit.Mutation(this.auditLogger, "access-request.revoke", this.CallerActor(), TargetKind, id, "revoked");
            workspace.TakeOwnership(result);
            return RevokeAccessRequestResult.Ok(ToView(result.RootElement), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            GovernanceAudit.Mutation(this.auditLogger, "access-request.revoke", this.CallerActor(), TargetKind, id, "refused-not-administrator");
            return RevokeAccessRequestResult.Forbidden(NotAdministratorProblem(id), workspace);
        }
        catch (AccessRequestStateException ex)
        {
            return RevokeAccessRequestResult.Conflict(Problem("invalid-state", "Cannot revoke the grant", 409, ex.Message), workspace);
        }
    }

    private static AccessRequestStatus? ParseStatus(Models.GetAccessRequestsStatus status)
        => status.IsNotUndefined() && Enum.TryParse((string)status, out AccessRequestStatus parsed) ? parsed : null;

    // Whether the caller asked for the approver inbox (scope=queue) rather than their own requests (scope=mine/absent).
    private static bool IsQueueScope(Models.GetAccessRequestsScope scope)
        => scope.IsNotUndefined() && string.Equals((string)scope, "queue", StringComparison.Ordinal);

    // The count operation's own status/scope parameter types (distinct generated enums, same members as the list's).
    private static AccessRequestStatus? ParseCountStatus(Models.GetAccessRequestsCountStatus status)
        => status.IsNotUndefined() && Enum.TryParse((string)status, out AccessRequestStatus parsed) ? parsed : null;

    private static bool IsCountQueueScope(Models.GetAccessRequestsCountScope scope)
        => scope.IsNotUndefined() && string.Equals((string)scope, "queue", StringComparison.Ordinal);

    // A bounded count body: the store reports at most CountCap, and Capped tells the console to render e.g. "100+".
    private static CountAccessRequestsResult CountResult(int count, bool capped, JsonWorkspace workspace)
        => CountAccessRequestsResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);

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

    // AccessRequestView is congruent with the stored AccessRequest (see ToView), so a list item is the same whole-document
    // AccessRequestView.From wrap — no field-copy, no per-field From() ternary, no requestedScopes rebuild. Each wrap
    // references its pooled document, so HandleListAccessRequestsAsync hands the batch to the workspace
    // (TransferOwnershipTo) for the response's lifetime. The build is closure-free (the request list is threaded as the
    // context) and inlined in the handler (the list Build is ref-scoped to its `in` argument).
    private static void BuildAccessRequestViews(in IReadOnlyList<AccessRequest> requests, ref Models.AccessRequestList.AccessRequestViewArray.Builder array)
    {
        foreach (AccessRequest request in requests)
        {
            array.AddItem(Models.AccessRequestView.From(request));
        }
    }

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
            && request.SubjectClaimTypeEquals(this.subjectClaimType)
            && request.SubjectClaimValueEquals(subject);
    }

    // Independent decision (§16.5): a request whose SUBJECT is the caller is never decided by the caller —
    // approving it would mint the caller their own grant (the privilege-escalation case), and denying it is
    // withdraw wearing a decision's clothes. Keyed on the subject claim, not the audit actor, because the
    // subject is who the decision empowers. Self-ELEVATION of an already-approved eligibility is untouched:
    // that is the §16.5.3 design, and the eligibility itself was independently decided.
    private async ValueTask<bool> IsOwnRequestAsync(string id, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return fetched is { } doc && this.IsRequester(doc.RootElement);
    }

    private static Models.ProblemDetails.Source OwnRequestProblem()
        => Problem("own-request", "Own request", 403, "This request would grant you access, so another administrator must decide it; you may withdraw it instead.");

    private async ValueTask<bool> IsAdministratorAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<WorkflowAdministrators>? record = await this.catalog.GetAdministratorsAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        return record?.RootElement.IsAdministeredBy(this.CallerIdentity()) == true;
    }
}