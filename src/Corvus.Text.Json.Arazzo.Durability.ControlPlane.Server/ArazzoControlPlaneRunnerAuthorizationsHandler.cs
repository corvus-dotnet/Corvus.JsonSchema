// <copyright file="ArazzoControlPlaneRunnerAuthorizationsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Logging;
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

    // The count operation's bound: badges/footers need "is there work / roughly how much", not exact-beyond-99.
    private const int CountCap = 100;

    private readonly IEnvironmentRunnerAuthorizationStore authorizations;
    private readonly IEnvironmentStore environments;
    private readonly IRunnerRegistry runners;
    private readonly SecuredEnvironmentAdministration administration;
    private readonly ControlPlaneAccess access;
    private readonly IWorkflowLeaseAdministration? leaseAdministration;
    private readonly string subjectClaimType;
    private readonly ILogger? auditLogger;

    // The audited resource kind for every decision on this surface (design §850).
    private const string TargetKind = "runner";

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneRunnerAuthorizationsHandler"/> class.</summary>
    /// <param name="authorizations">The environment-runner authorization store (the authorization lifecycle).</param>
    /// <param name="environments">The environment store (target-environment visibility for the governance gate).</param>
    /// <param name="runners">The runner registry the authenticated registration endpoint writes (design §5.5/§16.4): a runner's liveness registration, keyed on its self-chosen runnerId, with reach stamped from the serving environment.</param>
    /// <param name="administration">The environment-administration governance service (current-administrator gating + the reverse index for the inbox).</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/>, deployment identity, and principal per request.</param>
    /// <param name="leaseAdministration">The workflow state store's lease-administration capability, if it has one (§5.5 revocation
    /// fence): revoking a runner expires the leases it holds so an authorized peer reclaims its in-flight runs at once. A
    /// deployment whose store lacks the capability (<see langword="null"/>) still stops all future dispatch on revoke; only the
    /// immediate in-flight fence is unavailable.</param>
    /// <param name="subjectClaimType">The claim type identifying the deciding subject (the audit actor); default <c>sub</c>.</param>
    /// <param name="auditLogger">The logger for the §850 runner-authorization audit (who authorized/quarantined/revoked which runner); the audit span rides the always-registered <see cref="ArazzoTelemetry.ActivitySource"/> regardless.</param>
    internal ArazzoControlPlaneRunnerAuthorizationsHandler(
        IEnvironmentRunnerAuthorizationStore authorizations,
        IEnvironmentStore environments,
        IRunnerRegistry runners,
        SecuredEnvironmentAdministration administration,
        ControlPlaneAccess access,
        IWorkflowLeaseAdministration? leaseAdministration = null,
        string subjectClaimType = "sub",
        ILogger? auditLogger = null)
    {
        ArgumentNullException.ThrowIfNull(authorizations);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(runners);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentException.ThrowIfNullOrEmpty(subjectClaimType);
        this.authorizations = authorizations;
        this.environments = environments;
        this.runners = runners;
        this.administration = administration;
        this.access = access;
        this.leaseAdministration = leaseAdministration;
        this.subjectClaimType = subjectClaimType;
        this.auditLogger = auditLogger;
    }

    // The (environment, runnerId) audit target key (design §850).
    private static string RunnerKey(string environment, string runnerId) => $"{runnerId}@{environment}";

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
            GovernanceAudit.Mutation(this.auditLogger, "runner.authorize", this.CallerActor(), TargetKind, RunnerKey(environment, runnerId), "refused-not-administrator");
            return AuthorizeRunnerResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        // Pre-authorization (design §5.5): an administrator may allow-list a runner BEFORE it registers, so an authorize
        // of an unknown runner creates the authorization now (attributed to the admin) rather than 404-ing. When the runner
        // later registers with a matching id, EnsurePendingAsync sees this already-Authorized row and leaves it unchanged,
        // so the runner is dispatchable immediately with no second approval. Register-then-approve is the same code path —
        // GetAsync then returns the runner's own Pending row.
        ParsedJsonDocument<EnvironmentRunnerAuthorization>? fetched = await this.authorizations.GetAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        bool preAuthorized = fetched is null;
        if (fetched is null)
        {
            // Pre-authorization binds no machine principal (principal: null): the administrator is allow-listing a runnerId by
            // name before any runner has authenticated a registration, so no verified §16.4 identity exists to stamp yet.
            fetched = await this.authorizations.EnsurePendingAsync(environment, runnerId, this.CallerActor(), principal: null, cancellationToken).ConfigureAwait(false);
        }

        // Idempotent — authorizing an already-Authorized runner returns the existing record (status compared string-free).
        if (fetched.RootElement.IsAuthorized)
        {
            workspace.TakeOwnership(fetched);
            return AuthorizeRunnerResult.Ok(ToView(fetched.RootElement), workspace);
        }

        // The prior state names the authorization act: pre-authorizing an unregistered runner, reinstating a quarantined
        // one, and re-admitting a revoked one are distinct governance decisions from authorizing a freshly-registered one.
        string authorizeOutcome = preAuthorized ? "pre-authorized" : fetched.RootElement.IsQuarantined ? "reinstated" : fetched.RootElement.IsRevoked ? "re-authorized" : "authorized";
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

            GovernanceAudit.Mutation(this.auditLogger, "runner.authorize", this.CallerActor(), TargetKind, RunnerKey(environment, runnerId), authorizeOutcome);
            workspace.TakeOwnership(decided);
            return AuthorizeRunnerResult.Ok(ToView(decided.RootElement), workspace);
        }
        catch (RunnerAuthorizationConflictException)
        {
            return AuthorizeRunnerResult.Conflict(ConcurrentDecisionProblem(environment, runnerId), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RegisterRunnerResult> HandleRegisterRunnerAsync(RegisterRunnerParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;
        Models.RunnerRegistrationRequest body = parameters.Body;
        string runnerId = (string)body.RunnerId;

        // The trusted machine principal (design §16.4) is derived from the authenticated token, never from the request body.
        // The runners:register scope policy has already required an authenticated principal; guard defensively regardless.
        string? principal = this.MachinePrincipal();
        if (string.IsNullOrEmpty(principal))
        {
            return RegisterRunnerResult.Conflict(UnidentifiedPrincipalProblem(), workspace);
        }

        // The environment must exist. It is read as the trusted System identity, not as the runner: the runner's reach is the
        // environment's (stamped below from its managementTags — never trusted from the runner), so registration is gated by
        // the runners:register scope plus the later administrator authorization, not by the runner's own reach. Unknown
        // environment is 404.
        SecurityTagSet reachTags;
        using (ParsedJsonDocument<Environment>? environmentDoc = await this.environments.GetAsync(environment, AccessContext.System, cancellationToken).ConfigureAwait(false))
        {
            if (environmentDoc is null)
            {
                return RegisterRunnerResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
            }

            reachTags = environmentDoc.RootElement.ManagementTagsValue;
        }

        // Record the runner's liveness registration (the runner's self-description, with the server stamping environment,
        // reach tags, and the last-seen instant), then bind its authorization to the trusted principal. EnsurePendingAsync
        // creates the Pending row and stamps the principal on first registration, returns the existing row unchanged when the
        // same principal re-registers, and throws when a different principal already owns this runnerId (→ 409).
        RunnerRegistration registration = BuildRegistration(body, environment, reachTags, DateTimeOffset.UtcNow);
        await this.runners.RegisterAsync(registration, cancellationToken).ConfigureAwait(false);

        try
        {
            ParsedJsonDocument<EnvironmentRunnerAuthorization> authorization =
                await this.authorizations.EnsurePendingAsync(environment, runnerId, principal, principal, cancellationToken).ConfigureAwait(false);
            GovernanceAudit.Mutation(this.auditLogger, "runner.register", principal, TargetKind, RunnerKey(environment, runnerId), authorization.RootElement.IsAuthorized ? "registered-authorized" : "registered-pending");
            workspace.TakeOwnership(authorization);
            return RegisterRunnerResult.Ok(ToView(authorization.RootElement), workspace);
        }
        catch (RunnerPrincipalConflictException)
        {
            GovernanceAudit.Mutation(this.auditLogger, "runner.register", principal, TargetKind, RunnerKey(environment, runnerId), "refused-principal-conflict");
            return RegisterRunnerResult.Conflict(PrincipalConflictProblem(environment, runnerId), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<QuarantineRunnerResult> HandleQuarantineRunnerAsync(QuarantineRunnerParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;
        string runnerId = (string)parameters.RunnerId;

        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return QuarantineRunnerResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            GovernanceAudit.Mutation(this.auditLogger, "runner.quarantine", this.CallerActor(), TargetKind, RunnerKey(environment, runnerId), "refused-not-administrator");
            return QuarantineRunnerResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        ParsedJsonDocument<EnvironmentRunnerAuthorization>? fetched = await this.authorizations.GetAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return QuarantineRunnerResult.NotFound(RunnerNotFoundProblem(environment, runnerId), workspace);
        }

        // Idempotent — quarantining an already-Quarantined runner returns the existing record (status compared string-free).
        if (fetched.RootElement.IsQuarantined)
        {
            workspace.TakeOwnership(fetched);
            return QuarantineRunnerResult.Ok(ToView(fetched.RootElement), workspace);
        }

        // Only an Authorized runner can be quarantined: a Pending runner is not dispatching (nothing to drain), and a Revoked
        // one is a permanent removal that quarantine must not silently downgrade to a temporary exclusion. Both conflict (409).
        if (!fetched.RootElement.IsAuthorized)
        {
            fetched.Dispose();
            return QuarantineRunnerResult.Conflict(NotAuthorizedToQuarantineProblem(environment, runnerId), workspace);
        }

        WorkflowEtag expectedEtag = fetched.RootElement.EtagValue;
        fetched.Dispose();
        try
        {
            // Quarantine drains — unlike revoke, it does NOT fence in-flight runs. The gate excludes a non-Authorized runner
            // from NEW and orphaned claims, so the faulted runner takes no new work while its current runs finish.
            ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await this.authorizations.DecideAsync(
                environment, runnerId, new RunnerAuthorizationDecision(RunnerAuthorizationStatus.Quarantined, NoteReason(parameters.Body)), expectedEtag, this.CallerActor(), cancellationToken).ConfigureAwait(false);
            if (decided is null)
            {
                return QuarantineRunnerResult.NotFound(RunnerNotFoundProblem(environment, runnerId), workspace);
            }

            GovernanceAudit.Mutation(this.auditLogger, "runner.quarantine", this.CallerActor(), TargetKind, RunnerKey(environment, runnerId), "quarantined");
            workspace.TakeOwnership(decided);
            return QuarantineRunnerResult.Ok(ToView(decided.RootElement), workspace);
        }
        catch (RunnerAuthorizationConflictException)
        {
            return QuarantineRunnerResult.Conflict(ConcurrentDecisionProblem(environment, runnerId), workspace);
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
            GovernanceAudit.Mutation(this.auditLogger, "runner.revoke", this.CallerActor(), TargetKind, RunnerKey(environment, runnerId), "refused-not-administrator");
            return RevokeRunnerResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        ParsedJsonDocument<EnvironmentRunnerAuthorization>? fetched = await this.authorizations.GetAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return RevokeRunnerResult.NotFound(RunnerNotFoundProblem(environment, runnerId), workspace);
        }

        // Idempotent — revoking an already-Revoked runner returns the existing record (status compared string-free), and
        // re-applies the fence in case the runner had re-leased anything (harmless if it holds no leases).
        if (fetched.RootElement.IsRevoked)
        {
            await this.FenceRevokedRunnerAsync(runnerId, cancellationToken).ConfigureAwait(false);
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

        // Fence in-flight work AFTER the Revoked status is durable: expire the runner's leases so an authorized peer reclaims
        // its in-flight runs at once (its own next checkpoint write then conflicts). A store without the lease-administration
        // capability skips this; the authorization gate still stops all future dispatch on the next poll.
        await this.FenceRevokedRunnerAsync(runnerId, cancellationToken).ConfigureAwait(false);

        // Revoke is a containment action — the most audit-worthy event on this surface — recorded once the removal is durable and fenced.
        GovernanceAudit.Mutation(this.auditLogger, "runner.revoke", this.CallerActor(), TargetKind, RunnerKey(environment, runnerId), "revoked");
        workspace.TakeOwnership(decided);
        return RevokeRunnerResult.Ok(ToView(decided.RootElement), workspace);
    }

    // The §5.5 in-flight revocation fence: expire every lease the runner holds so an authorized peer reclaims its in-flight
    // runs immediately and the revoked runner's own next optimistic-concurrency write conflicts. Control-plane-enforced (a
    // cooperative self-check is worthless against a compromised runner); a store without the capability cannot fence in-flight
    // work, but revoke still stops all future dispatch through the authorization gate. The lease owner is the runner id.
    private async ValueTask FenceRevokedRunnerAsync(string runnerId, CancellationToken cancellationToken)
    {
        if (this.leaseAdministration is { } admin)
        {
            await admin.ExpireLeasesForOwnerAsync(runnerId, cancellationToken).ConfigureAwait(false);
        }
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

    /// <inheritdoc/>
    public async ValueTask<CountRunnerAuthorizationsResult> HandleCountRunnerAuthorizationsAsync(CountRunnerAuthorizationsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Mirrors the inbox HandleListRunnerAuthorizationsAsync's reach exactly (so the count can't drift from the list it
        // annotates), minus paging — the store returns only a bounded total (§5.5 badges/footers), never rows. Like the
        // inbox, an absent status defaults to Pending — "what needs my attention".
        RunnerAuthorizationStatus status = ParseCountStatus(parameters.Status) ?? RunnerAuthorizationStatus.Pending;
        string? environment = parameters.Environment.IsNotUndefined() ? (string)parameters.Environment : null;

        RunnerAuthorizationQuery query;
        if (environment is not null)
        {
            // A single environment's queue — only a current administrator of it may count it (the list's 403).
            if (await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false) != GovernanceGate.Authorized)
            {
                return CountRunnerAuthorizationsResult.Forbidden(NotAdministratorProblem(environment), workspace);
            }

            query = new RunnerAuthorizationQuery(status, Environment: environment);
        }
        else
        {
            // The approver inbox: every authorization across the environments the caller administers. A caller who
            // administers nothing counts zero — the store never sees an empty administered set.
            IReadOnlyList<string> administered = await this.administration.ListAdministeredEnvironmentsAsync(this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            if (administered.Count == 0)
            {
                return CountResult(0, false, workspace);
            }

            query = new RunnerAuthorizationQuery(status, AdministeredEnvironments: administered);
        }

        (int count, bool capped) = await this.authorizations.CountAsync(query, CountCap, cancellationToken).ConfigureAwait(false);
        return CountResult(count, capped, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CountEnvironmentRunnerAuthorizationsResult> HandleCountEnvironmentRunnerAuthorizationsAsync(CountEnvironmentRunnerAuthorizationsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;

        // The per-environment runner-roster count behind that list's footer: the exact gate as
        // HandleListEnvironmentRunnerAuthorizationsAsync (only a current administrator may see it; unknown/out-of-reach is
        // 404, non-administrator is 403), minus paging — the store returns only a bounded total (§5.5), never rows. It reuses
        // the store's native CountAsync over the same single-environment query, and (like the list, unlike the inbox) does
        // NOT default an absent status to Pending — an omitted status counts every state.
        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return CountEnvironmentRunnerAuthorizationsResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            return CountEnvironmentRunnerAuthorizationsResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        var query = new RunnerAuthorizationQuery(ParseStatus(parameters.Status), Environment: environment);
        (int count, bool capped) = await this.authorizations.CountAsync(query, CountCap, cancellationToken).ConfigureAwait(false);
        return CountEnvironmentRunnerAuthorizationsResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);
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
            : status.ValueEquals("Quarantined"u8) ? RunnerAuthorizationStatus.Quarantined
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
            : status.ValueEquals("Quarantined"u8) ? RunnerAuthorizationStatus.Quarantined
            : status.ValueEquals("Revoked"u8) ? RunnerAuthorizationStatus.Revoked
            : null;
    }

    // The count operation's own status parameter type (a distinct generated enum, same members as the inbox list's).
    private static RunnerAuthorizationStatus? ParseCountStatus(Models.GetRunnerAuthorizationsCountStatus status)
    {
        if (!status.IsNotUndefined())
        {
            return null;
        }

        return status.ValueEquals("Pending"u8) ? RunnerAuthorizationStatus.Pending
            : status.ValueEquals("Authorized"u8) ? RunnerAuthorizationStatus.Authorized
            : status.ValueEquals("Quarantined"u8) ? RunnerAuthorizationStatus.Quarantined
            : status.ValueEquals("Revoked"u8) ? RunnerAuthorizationStatus.Revoked
            : null;
    }

    // The per-environment runner count's own status parameter type (a distinct generated enum, same members as the list's).
    private static RunnerAuthorizationStatus? ParseStatus(Models.GetEnvironmentsByNameRunnersCountStatus status)
    {
        if (!status.IsNotUndefined())
        {
            return null;
        }

        return status.ValueEquals("Pending"u8) ? RunnerAuthorizationStatus.Pending
            : status.ValueEquals("Authorized"u8) ? RunnerAuthorizationStatus.Authorized
            : status.ValueEquals("Quarantined"u8) ? RunnerAuthorizationStatus.Quarantined
            : status.ValueEquals("Revoked"u8) ? RunnerAuthorizationStatus.Revoked
            : null;
    }

    // A bounded count body: the store reports at most CountCap, and Capped tells the console to render e.g. "100+".
    private static CountRunnerAuthorizationsResult CountResult(int count, bool capped, JsonWorkspace workspace)
        => CountRunnerAuthorizationsResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);

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

    // The trusted machine principal bound to a runner's authorization at registration (design §16.4). A machine principal is
    // a Keycloak client, so the stable identity is the client id: prefer the authorized-party (azp) / client_id claim, then
    // the token subject, then the authentication name. Derived from the verified token — never from the request body.
    private string? MachinePrincipal()
    {
        ClaimsPrincipal? principal = this.access.CurrentPrincipal;
        if (principal is null)
        {
            return null;
        }

        return principal.FindFirst("azp")?.Value
            ?? principal.FindFirst("client_id")?.Value
            ?? this.SubjectOf(principal)
            ?? principal.Identity?.Name;
    }

    // Builds the runner's liveness RunnerRegistration server-side (design §5.5/§16.4): the runner's self-description is copied
    // bytes-to-bytes from the request through a pooled scratch writer, and the server stamps the environment (from the path),
    // the reach tags (from the environment's managementTags — never trusted from the runner), and the last-seen instant.
    private static RunnerRegistration BuildRegistration(Models.RunnerRegistrationRequest body, string environment, SecurityTagSet reachTags, DateTimeOffset lastSeenAt)
    {
        return RunnerRegistration.FromJson(PersistedJson.ToArray(
            (body, environment, reachTags, lastSeenAt),
            static (Utf8JsonWriter writer, in (Models.RunnerRegistrationRequest Body, string Environment, SecurityTagSet Reach, DateTimeOffset LastSeen) c) =>
            {
                writer.WriteStartObject();
                WriteCopied(writer, "runnerId"u8, (JsonElement)c.Body.RunnerId);
                writer.WriteString("environment"u8, c.Environment);
                if (!c.Reach.IsEmpty)
                {
                    writer.WritePropertyName("reachTags"u8);
                    c.Reach.WriteTo(writer);
                }

                if (c.Body.Address.IsNotUndefined())
                {
                    WriteCopied(writer, "address"u8, (JsonElement)c.Body.Address);
                }

                WriteCopied(writer, "startedAt"u8, (JsonElement)c.Body.StartedAt);
                writer.WriteString("lastSeenAt"u8, c.LastSeen);
                WriteCopied(writer, "maxConcurrency"u8, (JsonElement)c.Body.MaxConcurrency);
                WriteCopied(writer, "transports"u8, (JsonElement)c.Body.Transports);
                WriteCopied(writer, "hostedVersions"u8, (JsonElement)c.Body.HostedVersions);
                if (c.Body.HostsDraftRuns.IsNotUndefined())
                {
                    WriteCopied(writer, "hostsDraftRuns"u8, (JsonElement)c.Body.HostsDraftRuns);
                }

                writer.WriteEndObject();
            }));
    }

    // Copies a request property's JSON value to the registration writer bytes-to-bytes (no string is realised).
    private static void WriteCopied(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement value)
    {
        writer.WritePropertyName(name);
        value.WriteTo(writer);
    }

    private static Models.ProblemDetails.Source EnvironmentNotFoundProblem(string environment)
        => Problem("environment-not-found", "Environment not found", 404, $"No environment named '{environment}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source RunnerNotFoundProblem(string environment, string runnerId)
        => Problem("runner-authorization-not-found", "Runner authorization not found", 404, $"No runner '{runnerId}' has registered for environment '{environment}'.");

    private static Models.ProblemDetails.Source NotAdministratorProblem(string environment)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of environment '{environment}'.");

    private static Models.ProblemDetails.Source ConcurrentDecisionProblem(string environment, string runnerId)
        => Problem("concurrent-decision", "Concurrent decision", 409, $"The authorization of runner '{runnerId}' for environment '{environment}' was changed concurrently; reload it and retry.");

    private static Models.ProblemDetails.Source NotAuthorizedToQuarantineProblem(string environment, string runnerId)
        => Problem("not-authorized-to-quarantine", "Runner not authorized", 409, $"Runner '{runnerId}' is not currently authorized to serve environment '{environment}', so it cannot be quarantined. Only an authorized runner can be quarantined; authorize it first, or revoke it to remove it permanently.");

    private static Models.ProblemDetails.Source PrincipalConflictProblem(string environment, string runnerId)
        => Problem("runner-principal-conflict", "Runner already claimed", 409, $"Runner '{runnerId}' in environment '{environment}' is already bound to a different machine principal (design §16.4); a registration presenting a different principal cannot take it over. Choose a distinct runnerId, or have an administrator revoke the existing authorization.");

    private static Models.ProblemDetails.Source UnidentifiedPrincipalProblem()
        => Problem("unidentified-machine-principal", "Unidentified machine principal", 409, "The registration token carries no machine principal (design §16.4); a runner must authenticate as a machine principal (client-credentials, private-key-JWT, or mTLS) to register.");

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