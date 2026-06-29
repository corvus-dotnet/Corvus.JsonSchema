// <copyright file="ArazzoControlPlaneAvailabilityRequestsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiAvailabilityRequestsHandler"/> over the <see cref="IAvailabilityRequestStore"/>
/// — the control-plane surface for "promotion" requests (design §7.8): a principal who cannot make a version available
/// directly raises a request, which the target environment's administrators approve or deny (the requester may withdraw
/// their own). These operations are authorized by the per-environment administrator gate, mirroring the §16.5 access-request
/// inbox parameterised by environment, not a global capability scope.
/// </summary>
/// <remarks>
/// <para>Approval is folded into the handler (unlike §16.5, which writes a security binding): approving makes the version
/// available — gated by current-administration of the target environment (403) and the §7.7 readiness check (409 if a
/// referenced source has no credential there) — then records the decision under optimistic concurrency so two
/// administrators cannot double-decide. The requesting/approving identity is the caller's audit actor (the configured
/// subject claim, falling back to the authentication name); the unforgeable governance identity is the <c>sys:</c> tag set
/// the row-security policy stamps (<see cref="ControlPlaneAccess.InternalTags"/>).</para>
/// </remarks>
public sealed class ArazzoControlPlaneAvailabilityRequestsHandler : IApiAvailabilityRequestsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IAvailabilityRequestStore requests;
    private readonly IAvailabilityStore availability;
    private readonly IEnvironmentStore environments;
    private readonly SecuredEnvironmentAdministration administration;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly ISourceCredentialStore credentials;
    private readonly ControlPlaneAccess access;
    private readonly string subjectClaimType;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneAvailabilityRequestsHandler"/> class.</summary>
    /// <param name="requests">The availability-request store (the request lifecycle).</param>
    /// <param name="availability">The availability matrix store (made available on approval).</param>
    /// <param name="environments">The environment store (target-environment visibility for the governance gate).</param>
    /// <param name="administration">The environment-administration governance service (current-administrator gating + the reverse index for the inbox).</param>
    /// <param name="catalog">The workflow catalog (version existence + its sources, for readiness on approval).</param>
    /// <param name="credentials">The source-credential store (readiness: a credential per source × environment).</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/>, deployment identity, and principal per request.</param>
    /// <param name="subjectClaimType">The claim type identifying the requesting subject (the audit actor / "mine" key); default <c>sub</c>.</param>
    internal ArazzoControlPlaneAvailabilityRequestsHandler(
        IAvailabilityRequestStore requests,
        IAvailabilityStore availability,
        IEnvironmentStore environments,
        SecuredEnvironmentAdministration administration,
        ISecuredWorkflowCatalog catalog,
        ISourceCredentialStore credentials,
        ControlPlaneAccess access,
        string subjectClaimType = "sub")
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentException.ThrowIfNullOrEmpty(subjectClaimType);
        this.requests = requests;
        this.availability = availability;
        this.environments = environments;
        this.administration = administration;
        this.catalog = catalog;
        this.credentials = credentials;
        this.access = access;
        this.subjectClaimType = subjectClaimType;
    }

    /// <inheritdoc/>
    public async ValueTask<SubmitAvailabilityRequestResult> HandleSubmitAvailabilityRequestAsync(SubmitAvailabilityRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.Body.BaseWorkflowId;
        int versionNumber = (int)parameters.Body.VersionNumber;
        string environment = (string)parameters.Body.Environment;

        // The target environment must be in reach, and the version must exist/be readable — a request for an unknown
        // environment or version is rejected (400; the contract has no 404 on submit).
        using (ParsedJsonDocument<Environment>? environmentDoc = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (environmentDoc is null)
            {
                return SubmitAvailabilityRequestResult.BadRequest(UnknownEnvironmentProblem(environment), workspace);
            }
        }

        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return SubmitAvailabilityRequestResult.BadRequest(UnknownVersionProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        string? reason = parameters.Body.Reason.IsNotUndefined() ? (string)parameters.Body.Reason : null;
        using ParsedJsonDocument<AvailabilityRequest> draft = AvailabilityRequest.Draft(baseWorkflowId, versionNumber, environment, reason);
        ParsedJsonDocument<AvailabilityRequest> created = await this.requests.CreateAsync(draft.RootElement, this.CallerActor(), cancellationToken).ConfigureAwait(false);
        workspace.TakeOwnership(created);
        return SubmitAvailabilityRequestResult.Created(ToView(created.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListAvailabilityRequestsResult> HandleListAvailabilityRequestsAsync(ListAvailabilityRequestsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        AvailabilityRequestStatus? status = ParseStatus(parameters.Status);
        string? environment = parameters.Environment.IsNotUndefined() ? (string)parameters.Environment : null;

        AvailabilityRequestQuery query;
        if (environment is not null)
        {
            // The environment queue — only a current administrator of it may list it (an unknown/out-of-reach environment
            // and a non-administrator are refused identically, 403).
            if (await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false) != GovernanceGate.Authorized)
            {
                return ListAvailabilityRequestsResult.Forbidden(NotAdministratorProblem(environment), workspace);
            }

            query = new AvailabilityRequestQuery(status, Environment: environment);
        }
        else if (IsQueueScope(parameters.Scope))
        {
            // The approver inbox (§7.8): every request across the environments the caller administers, resolved from the
            // reverse administration index. A caller who administers nothing gets an empty page — the store never sees an
            // empty administered set.
            IReadOnlyList<string> administered = await this.administration.ListAdministeredEnvironmentsAsync(this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            if (administered.Count == 0)
            {
                return ListAvailabilityRequestsResult.Ok(EmptyList(), workspace);
            }

            query = new AvailabilityRequestQuery(status, AdministeredEnvironments: administered);
        }
        else
        {
            // The caller's own requests ("mine"), keyed on the audit actor recorded as createdBy.
            query = new AvailabilityRequestQuery(status, CreatedBy: this.CallerActor());
        }

        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using AvailabilityRequestPage page = await this.requests.ListAsync(query, limit, pageToken, cancellationToken).ConfigureAwait(false);

        // Each item is a whole-document AvailabilityRequestView.From wrap (the congruent projection ToView uses), so it
        // references its pooled document; hand the batch to the workspace for the response's lifetime. The list body is
        // built closure-free and inlined (the list Build is ref-scoped to its `in` argument).
        page.Requests.TransferOwnershipTo(workspace);
        IReadOnlyList<AvailabilityRequest> requestList = page.Requests;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.AvailabilityRequestList.Source<IReadOnlyList<AvailabilityRequest>> body = Models.AvailabilityRequestList.Build(
            in requestList,
            availabilityRequests: Models.AvailabilityRequestList.AvailabilityRequestViewArray.Build(in requestList, BuildAvailabilityRequestViews),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return ListAvailabilityRequestsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetAvailabilityRequestResult> HandleGetAvailabilityRequestAsync(GetAvailabilityRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        ParsedJsonDocument<AvailabilityRequest>? fetched = await this.requests.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return GetAvailabilityRequestResult.NotFound(NotFoundProblem(id), workspace);
        }

        // Take ownership before the (awaiting) visibility check so a throw cannot leak the rented buffer.
        workspace.TakeOwnership(fetched);
        AvailabilityRequest request = fetched.RootElement;
        if (!this.IsRequester(request) && await this.AuthorizeEnvironmentAdminAsync(request.EnvironmentValue, cancellationToken).ConfigureAwait(false) != GovernanceGate.Authorized)
        {
            return GetAvailabilityRequestResult.Forbidden(NotVisibleProblem(id), workspace);
        }

        return GetAvailabilityRequestResult.Ok(ToView(request), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ApproveAvailabilityRequestResult> HandleApproveAvailabilityRequestAsync(ApproveAvailabilityRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        string environment;
        string baseWorkflowId;
        int versionNumber;
        WorkflowEtag expectedEtag;
        string statusValue;
        using (ParsedJsonDocument<AvailabilityRequest>? fetched = await this.requests.GetAsync(id, cancellationToken).ConfigureAwait(false))
        {
            if (fetched is null)
            {
                return ApproveAvailabilityRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            AvailabilityRequest request = fetched.RootElement;
            environment = request.EnvironmentValue;
            baseWorkflowId = request.BaseWorkflowIdValue;
            versionNumber = request.VersionNumberValue;
            expectedEtag = request.EtagValue;
            statusValue = request.StatusValue;
        }

        // Governance: the caller must be a current administrator of the request's target environment.
        if (await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false) != GovernanceGate.Authorized)
        {
            return ApproveAvailabilityRequestResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        if (!IsPending(statusValue))
        {
            return ApproveAvailabilityRequestResult.Conflict(NotPendingProblem(id, statusValue), workspace);
        }

        // The version must still exist; its sources drive the readiness gate (§7.7) — a missing credential for even one
        // source blocks approval (409), exactly as a direct make-available would.
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return ApproveAvailabilityRequestResult.Conflict(VersionGoneProblem(baseWorkflowId, versionNumber), workspace);
            }

            List<string> missing = await this.MissingSourcesAsync(version.RootElement, environment, cancellationToken).ConfigureAwait(false);
            if (missing.Count > 0)
            {
                return ApproveAvailabilityRequestResult.Conflict(NotReadyProblem(baseWorkflowId, versionNumber, environment, missing), workspace);
            }
        }

        // Make the version available (idempotent), then record the decision under optimistic concurrency so a second
        // administrator racing on the same pending request conflicts (409) rather than double-deciding.
        (ParsedJsonDocument<AvailabilityEntry> entry, _) = await this.availability.MakeAvailableAsync(baseWorkflowId, versionNumber, environment, this.CallerActor(), cancellationToken).ConfigureAwait(false);
        entry.Dispose();
        try
        {
            ParsedJsonDocument<AvailabilityRequest>? decided = await this.requests.DecideAsync(id, new AvailabilityRequestDecision(AvailabilityRequestStatus.Approved, NoteReason(parameters.Body)), expectedEtag, this.CallerActor(), cancellationToken).ConfigureAwait(false);
            if (decided is null)
            {
                return ApproveAvailabilityRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(decided);
            return ApproveAvailabilityRequestResult.Ok(ToView(decided.RootElement), workspace);
        }
        catch (AvailabilityRequestConflictException)
        {
            return ApproveAvailabilityRequestResult.Conflict(ConcurrentDecisionProblem(id), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DenyAvailabilityRequestResult> HandleDenyAvailabilityRequestAsync(DenyAvailabilityRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        string environment;
        WorkflowEtag expectedEtag;
        string statusValue;
        using (ParsedJsonDocument<AvailabilityRequest>? fetched = await this.requests.GetAsync(id, cancellationToken).ConfigureAwait(false))
        {
            if (fetched is null)
            {
                return DenyAvailabilityRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            AvailabilityRequest request = fetched.RootElement;
            environment = request.EnvironmentValue;
            expectedEtag = request.EtagValue;
            statusValue = request.StatusValue;
        }

        if (await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false) != GovernanceGate.Authorized)
        {
            return DenyAvailabilityRequestResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        if (!IsPending(statusValue))
        {
            return DenyAvailabilityRequestResult.Conflict(NotPendingProblem(id, statusValue), workspace);
        }

        try
        {
            ParsedJsonDocument<AvailabilityRequest>? decided = await this.requests.DecideAsync(id, new AvailabilityRequestDecision(AvailabilityRequestStatus.Denied, NoteReason(parameters.Body)), expectedEtag, this.CallerActor(), cancellationToken).ConfigureAwait(false);
            if (decided is null)
            {
                return DenyAvailabilityRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(decided);
            return DenyAvailabilityRequestResult.Ok(ToView(decided.RootElement), workspace);
        }
        catch (AvailabilityRequestConflictException)
        {
            return DenyAvailabilityRequestResult.Conflict(ConcurrentDecisionProblem(id), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WithdrawAvailabilityRequestResult> HandleWithdrawAvailabilityRequestAsync(WithdrawAvailabilityRequestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.RequestId;
        bool isRequester;
        WorkflowEtag expectedEtag;
        string statusValue;
        using (ParsedJsonDocument<AvailabilityRequest>? fetched = await this.requests.GetAsync(id, cancellationToken).ConfigureAwait(false))
        {
            if (fetched is null)
            {
                return WithdrawAvailabilityRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            AvailabilityRequest request = fetched.RootElement;
            isRequester = this.IsRequester(request);
            expectedEtag = request.EtagValue;
            statusValue = request.StatusValue;
        }

        // Only the requester may withdraw their own request (refused 403, distinct from a wrong-state conflict).
        if (!isRequester)
        {
            return WithdrawAvailabilityRequestResult.Forbidden(NotRequesterProblem(), workspace);
        }

        if (!IsPending(statusValue))
        {
            return WithdrawAvailabilityRequestResult.Conflict(NotPendingProblem(id, statusValue), workspace);
        }

        try
        {
            ParsedJsonDocument<AvailabilityRequest>? decided = await this.requests.DecideAsync(id, new AvailabilityRequestDecision(AvailabilityRequestStatus.Withdrawn, NoteReason(parameters.Body)), expectedEtag, this.CallerActor(), cancellationToken).ConfigureAwait(false);
            if (decided is null)
            {
                return WithdrawAvailabilityRequestResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(decided);
            return WithdrawAvailabilityRequestResult.Ok(ToView(decided.RootElement), workspace);
        }
        catch (AvailabilityRequestConflictException)
        {
            return WithdrawAvailabilityRequestResult.Conflict(ConcurrentDecisionProblem(id), workspace);
        }
    }

    private static bool IsPending(string statusValue) => string.Equals(statusValue, AvailabilityRequestStatusNames.Pending, StringComparison.Ordinal);

    private static AvailabilityRequestStatus? ParseStatus(Models.GetAvailabilityRequestsStatus status)
        => status.IsNotUndefined() && Enum.TryParse((string)status, out AvailabilityRequestStatus parsed) ? parsed : null;

    // Whether the caller asked for the approver inbox (scope=queue) rather than their own requests (scope=mine/absent).
    private static bool IsQueueScope(Models.GetAvailabilityRequestsScope scope)
        => scope.IsNotUndefined() && string.Equals((string)scope, "queue", StringComparison.Ordinal);

    private static string? NoteReason(Models.AvailabilityRequestDecisionNote body)
        => body.IsNotUndefined() && body.Reason.IsNotUndefined() ? (string)body.Reason : null;

    // A single-document response wraps the stored element with no materialization: AvailabilityRequestView is congruent
    // with the persisted AvailabilityRequest (identical property names, types, and required set), so From() is a pointer
    // reinterpret and the body serializes the backing verbatim. The wrapped value references the pooled document, so the
    // caller hands that document to the workspace (TakeOwnership) for the response's lifetime.
    private static Models.AvailabilityRequestView ToView(AvailabilityRequest request) => Models.AvailabilityRequestView.From(request);

    // Each list item is the same whole-document AvailabilityRequestView.From wrap (see ToView), referencing its pooled
    // document; the build is closure-free (the request list is threaded as the context) and inlined in the handler.
    private static void BuildAvailabilityRequestViews(in IReadOnlyList<AvailabilityRequest> requests, ref Models.AvailabilityRequestList.AvailabilityRequestViewArray.Builder array)
    {
        foreach (AvailabilityRequest request in requests)
        {
            array.AddItem(Models.AvailabilityRequestView.From(request));
        }
    }

    private static Models.AvailabilityRequestList.Source EmptyList()
        => new((ref Models.AvailabilityRequestList.Builder b) => b.Create(
            availabilityRequests: new Models.AvailabilityRequestList.AvailabilityRequestViewArray.Source((ref Models.AvailabilityRequestList.AvailabilityRequestViewArray.Builder array) => { })));

    // The sources the version references that have no usable credential in the target environment (readiness, §7.7).
    private async ValueTask<List<string>> MissingSourcesAsync(CatalogVersion version, string environment, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        foreach (CatalogSourceRef source in version.SourcesValue.ToList())
        {
            using ParsedJsonDocument<SourceCredentialBinding>? binding = await this.credentials.GetAsync(source.Name, environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (binding is null)
            {
                missing.Add(source.Name);
            }
        }

        return missing;
    }

    // Visibility-then-membership gate on an environment (mirrors the availability handler): an environment outside reach is
    // not found; a non-administrator is not authorized.
    private async ValueTask<GovernanceGate> AuthorizeEnvironmentAdminAsync(string environment, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<Environment>? environmentDoc = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (environmentDoc is null)
        {
            return GovernanceGate.NotFound;
        }

        using ParsedJsonDocument<EnvironmentAdministrators>? record = await this.administration.GetAdministratorsAsync(environment, cancellationToken).ConfigureAwait(false);
        return record?.RootElement.IsAdministeredBy(this.CallerIdentity()) == true
            ? GovernanceGate.Authorized
            : GovernanceGate.Forbidden;
    }

    private SecurityTagSet CallerIdentity() => SecurityTagSet.FromTags(this.access.InternalTags());

    // The audit actor recorded on a request (createdBy / decidedBy) and the "mine" filter key: the principal's configured
    // subject claim, falling back to the authentication name, then "anonymous".
    private string CallerActor() => this.SubjectOf(this.access.CurrentPrincipal) ?? this.access.CurrentPrincipal?.Identity?.Name ?? "anonymous";

    private string? SubjectOf(ClaimsPrincipal? principal) => principal?.FindFirst(this.subjectClaimType)?.Value;

    private bool IsRequester(AvailabilityRequest request)
        => string.Equals(request.CreatedByValue, this.CallerActor(), StringComparison.Ordinal);

    private static Models.ProblemDetails.Source NotFoundProblem(string id)
        => Problem("availability-request-not-found", "Availability request not found", 404, $"No availability request '{id}' exists.");

    private static Models.ProblemDetails.Source NotVisibleProblem(string id)
        => Problem("availability-request-not-visible", "Not visible", 403, $"You may not view availability request '{id}'.");

    private static Models.ProblemDetails.Source NotRequesterProblem()
        => Problem("not-requester", "Not the requester", 403, "Only the requester may withdraw their request.");

    private static Models.ProblemDetails.Source NotAdministratorProblem(string environment)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of environment '{environment}'.");

    private static Models.ProblemDetails.Source NotPendingProblem(string id, string statusValue)
        => Problem("invalid-state", "Request is not pending", 409, $"Availability request '{id}' is '{statusValue}', not pending, so it cannot be decided.");

    private static Models.ProblemDetails.Source ConcurrentDecisionProblem(string id)
        => Problem("concurrent-decision", "Concurrent decision", 409, $"Availability request '{id}' was decided concurrently; reload it and retry.");

    private static Models.ProblemDetails.Source UnknownEnvironmentProblem(string environment)
        => Problem("unknown-environment", "Unknown environment", 400, $"No environment named '{environment}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source UnknownVersionProblem(string baseWorkflowId, int versionNumber)
        => Problem("unknown-version", "Unknown workflow version", 400, $"No version {versionNumber} of workflow '{baseWorkflowId}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source VersionGoneProblem(string baseWorkflowId, int versionNumber)
        => Problem("version-gone", "Workflow version no longer exists", 409, $"Version {versionNumber} of workflow '{baseWorkflowId}' no longer exists, so the request cannot be approved.");

    private static Models.ProblemDetails.Source NotReadyProblem(string baseWorkflowId, int versionNumber, string environment, IReadOnlyList<string> missing)
        => Problem("environment-not-ready", "Environment not ready", 409, $"Version {versionNumber} of workflow '{baseWorkflowId}' cannot be made available in '{environment}': no credential for {string.Join(", ", missing)}.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    private enum GovernanceGate
    {
        NotFound,
        Forbidden,
        Authorized,
    }
}