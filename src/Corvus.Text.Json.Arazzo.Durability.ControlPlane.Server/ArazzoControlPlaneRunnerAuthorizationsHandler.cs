// <copyright file="ArazzoControlPlaneRunnerAuthorizationsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiRunnerAuthorizationsHandler"/> over the
/// <see cref="IEnvironmentRunnerAuthorizationStore"/> — the control-plane surface for authorizing which runners may serve a
/// deployment environment (design §5.5). Receiving an environment's runs means receiving its credentials, so a runner may
/// not self-assert into an environment: it enters <c>Pending</c> on registration and is dispatchable only once an
/// administrator of that environment authorizes it; authorization can be revoked. These operations are authorized by the
/// per-environment administrator gate, mirroring the §15.4 availability-request inbox, not a global capability scope.
/// </summary>
/// <remarks>
/// <para>Authorize records the decision under optimistic concurrency so two administrators cannot double-decide the same
/// pending authorization (409 on a concurrent change); revoke is unconditional and idempotent (the administrator's revoke
/// always wins). The deciding identity is the caller's audit actor (the configured subject claim, falling back to the
/// authentication name); the unforgeable governance identity is the <c>sys:</c> tag set the row-security policy stamps
/// (<see cref="ControlPlaneAccess.InternalTags"/>).</para>
/// </remarks>
public sealed class ArazzoControlPlaneRunnerAuthorizationsHandler : IApiRunnerAuthorizationsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IEnvironmentRunnerAuthorizationStore authorizations;
    private readonly IEnvironmentStore environments;
    private readonly SecuredEnvironmentAdministration administration;
    private readonly ControlPlaneAccess access;
    private readonly string subjectClaimType;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneRunnerAuthorizationsHandler"/> class.</summary>
    /// <param name="authorizations">The environment-runner authorization store (the authorization lifecycle).</param>
    /// <param name="environments">The environment store (target-environment visibility for the governance gate).</param>
    /// <param name="administration">The environment-administration governance service (current-administrator gating + the reverse index for the inbox).</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/>, deployment identity, and principal per request.</param>
    /// <param name="subjectClaimType">The claim type identifying the deciding subject (the audit actor); default <c>sub</c>.</param>
    internal ArazzoControlPlaneRunnerAuthorizationsHandler(
        IEnvironmentRunnerAuthorizationStore authorizations,
        IEnvironmentStore environments,
        SecuredEnvironmentAdministration administration,
        ControlPlaneAccess access,
        string subjectClaimType = "sub")
    {
        ArgumentNullException.ThrowIfNull(authorizations);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentException.ThrowIfNullOrEmpty(subjectClaimType);
        this.authorizations = authorizations;
        this.environments = environments;
        this.administration = administration;
        this.access = access;
        this.subjectClaimType = subjectClaimType;
    }

    /// <inheritdoc/>
    public async ValueTask<ListEnvironmentRunnerAuthorizationsResult> HandleListEnvironmentRunnerAuthorizationsAsync(ListEnvironmentRunnerAuthorizationsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;

        // The environment's runner roster — only a current administrator of it may list it. An unknown/out-of-reach
        // environment is 404; a non-administrator is 403 (distinct, mirroring the availability surface).
        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return ListEnvironmentRunnerAuthorizationsResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            return ListEnvironmentRunnerAuthorizationsResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        var query = new RunnerAuthorizationQuery(ParseStatus(parameters.Status), Environment: environment);
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using EnvironmentRunnerAuthorizationPage page = await this.authorizations.ListAsync(query, limit, pageToken, cancellationToken).ConfigureAwait(false);

        // Each item is a whole-document View.From wrap referencing its pooled document, so ownership of the page's documents
        // transfers to the workspace for the response's lifetime; the list body is built closure-free and inlined here (the
        // list Build is ref-scoped to its `in` argument, so it cannot be hoisted into a helper).
        page.Authorizations.TransferOwnershipTo(workspace);
        IReadOnlyList<EnvironmentRunnerAuthorization> list = page.Authorizations;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.EnvironmentRunnerAuthorizationList.Source<IReadOnlyList<EnvironmentRunnerAuthorization>> body = Models.EnvironmentRunnerAuthorizationList.Build(
            in list,
            authorizations: Models.EnvironmentRunnerAuthorizationList.EnvironmentRunnerAuthorizationViewArray.Build(in list, BuildViews),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return ListEnvironmentRunnerAuthorizationsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<AuthorizeRunnerResult> HandleAuthorizeRunnerAsync(AuthorizeRunnerParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;
        string runnerId = (string)parameters.RunnerId;

        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return AuthorizeRunnerResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            return AuthorizeRunnerResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        // The runner must have registered for the environment (entered Pending) — there is nothing to authorize otherwise.
        ParsedJsonDocument<EnvironmentRunnerAuthorization>? fetched = await this.authorizations.GetAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return AuthorizeRunnerResult.NotFound(RunnerNotFoundProblem(environment, runnerId), workspace);
        }

        // Idempotent — authorizing an already-Authorized runner returns the existing record (status compared string-free).
        if (fetched.RootElement.IsAuthorized)
        {
            workspace.TakeOwnership(fetched);
            return AuthorizeRunnerResult.Ok(ToView(fetched.RootElement), workspace);
        }

        WorkflowEtag expectedEtag = fetched.RootElement.EtagValue;
        fetched.Dispose();
        try
        {
            ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await this.authorizations.DecideAsync(
                environment, runnerId, new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Authorized, NoteReason(parameters.Body)), expectedEtag, this.CallerActor(), cancellationToken).ConfigureAwait(false);
            if (decided is null)
            {
                return AuthorizeRunnerResult.NotFound(RunnerNotFoundProblem(environment, runnerId), workspace);
            }

            workspace.TakeOwnership(decided);
            return AuthorizeRunnerResult.Ok(ToView(decided.RootElement), workspace);
        }
        catch (RunnerAuthorizationConflictException)
        {
            return AuthorizeRunnerResult.Conflict(ConcurrentDecisionProblem(environment, runnerId), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RevokeRunnerResult> HandleRevokeRunnerAsync(RevokeRunnerParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;
        string runnerId = (string)parameters.RunnerId;

        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return RevokeRunnerResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            return RevokeRunnerResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        ParsedJsonDocument<EnvironmentRunnerAuthorization>? fetched = await this.authorizations.GetAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return RevokeRunnerResult.NotFound(RunnerNotFoundProblem(environment, runnerId), workspace);
        }

        // Idempotent — revoking an already-Revoked runner returns the existing record (status compared string-free).
        if (fetched.RootElement.IsRevoked)
        {
            workspace.TakeOwnership(fetched);
            return RevokeRunnerResult.Ok(ToView(fetched.RootElement), workspace);
        }

        fetched.Dispose();

        // Revoke is unconditional (the administrator's revoke always wins, and the end state is Revoked regardless of a
        // concurrent authorize); there is no 409 on this surface.
        ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await this.authorizations.DecideAsync(
            environment, runnerId, new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Revoked, NoteReason(parameters.Body)), WorkflowEtag.None, this.CallerActor(), cancellationToken).ConfigureAwait(false);
        if (decided is null)
        {
            return RevokeRunnerResult.NotFound(RunnerNotFoundProblem(environment, runnerId), workspace);
        }

        workspace.TakeOwnership(decided);
        return RevokeRunnerResult.Ok(ToView(decided.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListRunnerAuthorizationsResult> HandleListRunnerAuthorizationsAsync(ListRunnerAuthorizationsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // The approver inbox defaults to Pending — "what needs my attention" — when no explicit status is given.
        RunnerAuthorizationStatus status = ParseStatus(parameters.Status) ?? RunnerAuthorizationStatus.Pending;
        string? environment = parameters.Environment.IsNotUndefined() ? (string)parameters.Environment : null;

        RunnerAuthorizationQuery query;
        if (environment is not null)
        {
            // A single environment's queue — only a current administrator of it may list it. This surface has no 404, so an
            // unknown/out-of-reach environment and a non-administrator are refused identically (403).
            if (await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false) != GovernanceGate.Authorized)
            {
                return ListRunnerAuthorizationsResult.Forbidden(NotAdministratorProblem(environment), workspace);
            }

            query = new RunnerAuthorizationQuery(status, Environment: environment);
        }
        else
        {
            // The approver inbox: every authorization across the environments the caller administers, resolved from the
            // reverse administration index. A caller who administers nothing gets an empty page — the store never sees an
            // empty administered set.
            IReadOnlyList<string> administered = await this.administration.ListAdministeredEnvironmentsAsync(this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            if (administered.Count == 0)
            {
                return ListRunnerAuthorizationsResult.Ok(EmptyList(), workspace);
            }

            query = new RunnerAuthorizationQuery(status, AdministeredEnvironments: administered);
        }

        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using EnvironmentRunnerAuthorizationPage page = await this.authorizations.ListAsync(query, limit, pageToken, cancellationToken).ConfigureAwait(false);

        page.Authorizations.TransferOwnershipTo(workspace);
        IReadOnlyList<EnvironmentRunnerAuthorization> list = page.Authorizations;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.EnvironmentRunnerAuthorizationList.Source<IReadOnlyList<EnvironmentRunnerAuthorization>> body = Models.EnvironmentRunnerAuthorizationList.Build(
            in list,
            authorizations: Models.EnvironmentRunnerAuthorizationList.EnvironmentRunnerAuthorizationViewArray.Build(in list, BuildViews),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return ListRunnerAuthorizationsResult.Ok(body, workspace);
    }

    // The status query param is parsed string-free: the JSON value's bytes are matched against the wire literals rather
    // than realising it and running Enum.TryParse over a managed string.
    private static RunnerAuthorizationStatus? ParseStatus(Models.GetEnvironmentsByNameRunnersStatus status)
    {
        if (!status.IsNotUndefined())
        {
            return null;
        }

        return status.ValueEquals("Pending"u8) ? RunnerAuthorizationStatus.Pending
            : status.ValueEquals("Authorized"u8) ? RunnerAuthorizationStatus.Authorized
            : status.ValueEquals("Revoked"u8) ? RunnerAuthorizationStatus.Revoked
            : null;
    }

    private static RunnerAuthorizationStatus? ParseStatus(Models.GetRunnerAuthorizationsStatus status)
    {
        if (!status.IsNotUndefined())
        {
            return null;
        }

        return status.ValueEquals("Pending"u8) ? RunnerAuthorizationStatus.Pending
            : status.ValueEquals("Authorized"u8) ? RunnerAuthorizationStatus.Authorized
            : status.ValueEquals("Revoked"u8) ? RunnerAuthorizationStatus.Revoked
            : null;
    }

    private static string? NoteReason(Models.RunnerAuthorizationDecisionNote body)
        => body.IsNotUndefined() && body.Reason.IsNotUndefined() ? (string)body.Reason : null;

    // A single-document response wraps the stored element with no materialization: EnvironmentRunnerAuthorizationView is
    // congruent with the persisted EnvironmentRunnerAuthorization (identical property names, types, and required set), so
    // From() is a pointer reinterpret and the body serializes the backing verbatim. The wrapped value references the pooled
    // document, so the caller hands that document to the workspace (TakeOwnership) for the response's lifetime.
    private static Models.EnvironmentRunnerAuthorizationView ToView(EnvironmentRunnerAuthorization authorization)
        => Models.EnvironmentRunnerAuthorizationView.From(authorization);

    private static void BuildViews(in IReadOnlyList<EnvironmentRunnerAuthorization> authorizations, ref Models.EnvironmentRunnerAuthorizationList.EnvironmentRunnerAuthorizationViewArray.Builder array)
    {
        foreach (EnvironmentRunnerAuthorization authorization in authorizations)
        {
            array.AddItem(Models.EnvironmentRunnerAuthorizationView.From(authorization));
        }
    }

    private static Models.EnvironmentRunnerAuthorizationList.Source EmptyList()
        => new((ref Models.EnvironmentRunnerAuthorizationList.Builder b) => b.Create(
            authorizations: new Models.EnvironmentRunnerAuthorizationList.EnvironmentRunnerAuthorizationViewArray.Source((ref Models.EnvironmentRunnerAuthorizationList.EnvironmentRunnerAuthorizationViewArray.Builder array) => { })));

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

    // The audit actor recorded on a decision (decidedBy): the principal's configured subject claim, falling back to the
    // authentication name, then "anonymous".
    private string CallerActor() => this.SubjectOf(this.access.CurrentPrincipal) ?? this.access.CurrentPrincipal?.Identity?.Name ?? "anonymous";

    private string? SubjectOf(ClaimsPrincipal? principal) => principal?.FindFirst(this.subjectClaimType)?.Value;

    private static Models.ProblemDetails.Source EnvironmentNotFoundProblem(string environment)
        => Problem("environment-not-found", "Environment not found", 404, $"No environment named '{environment}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source RunnerNotFoundProblem(string environment, string runnerId)
        => Problem("runner-authorization-not-found", "Runner authorization not found", 404, $"No runner '{runnerId}' has registered for environment '{environment}'.");

    private static Models.ProblemDetails.Source NotAdministratorProblem(string environment)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of environment '{environment}'.");

    private static Models.ProblemDetails.Source ConcurrentDecisionProblem(string environment, string runnerId)
        => Problem("concurrent-decision", "Concurrent decision", 409, $"The authorization of runner '{runnerId}' for environment '{environment}' was changed concurrently; reload it and retry.");

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