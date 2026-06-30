// <copyright file="AccessRequestApprovalService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The built-in (default-strategy) access-request approval service (design §16.5): it turns an approved request into
/// a single time-boxed entitlement, behind three guardrails.
/// </summary>
/// <remarks>
/// <para><b>§15 gate.</b> Only a §15 administrator of the target workflow may approve, deny, or revoke a request
/// (<see cref="ISecuredWorkflowCatalog.GetAdministratorsAsync"/> + <see cref="WorkflowIdentity.SameAdministrator"/>).
/// <b>Self-elevation</b> (an eligible requester) skips the human approver — eligibility <em>is</em> the
/// authorization — but is otherwise identical.</para>
/// <para><b>Platform cap.</b> An approval grants <em>at most</em> the requested scopes intersected with the
/// deployment allowlist (run access only — <c>runs:read</c>/<c>runs:write</c>); the subject is fixed to the requester
/// (no third party), the reach is fixed to the target workflow (<c>sys:workflow == '&lt;id&gt;'</c>, never system
/// reach), and the expiry is capped at the deployment max TTL. Security, purge, administration, and escalation are
/// never grantable this way.</para>
/// <para><b>Time-bound + revocable.</b> The grant is a time-boxed active binding (§16.5.2); a §15 admin may revoke it
/// early — the binding is deleted (access stops at the next resolution, fail-safe) before the request is marked
/// revoked.</para>
/// <para>The workflow id is validated against a strict character set before it is woven into a rule expression, so a
/// requester-supplied id can never inject into the security-rule grammar.</para>
/// </remarks>
public sealed class AccessRequestApprovalService : IAccessRequestApprovalService
{
    private const string SelfElevationReason = "self-elevation (eligible)";

    private readonly IAccessRequestStore requests;
    private readonly ISecurityPolicyStore policy;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly TimeProvider timeProvider;
    private readonly AccessRequestApprovalOptions options;
    private readonly PersistentRowSecurityPolicy? rowSecurity;

    /// <summary>Initializes a new instance of the <see cref="AccessRequestApprovalService"/> class.</summary>
    /// <param name="requests">The access-request store.</param>
    /// <param name="policy">The security-policy store the entitlement is written to.</param>
    /// <param name="catalog">The catalog client, for the §15 administrator lookup.</param>
    /// <param name="timeProvider">The time source for grant expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="options">The platform cap (max TTL + grantable scopes); defaults to run-access-only, 8h.</param>
    /// <param name="rowSecurity">The in-process policy to refresh after a grant/revoke so it takes effect immediately; optional.</param>
    public AccessRequestApprovalService(
        IAccessRequestStore requests,
        ISecurityPolicyStore policy,
        ISecuredWorkflowCatalog catalog,
        TimeProvider? timeProvider = null,
        AccessRequestApprovalOptions? options = null,
        PersistentRowSecurityPolicy? rowSecurity = null)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(catalog);
        this.requests = requests;
        this.policy = policy;
        this.catalog = catalog;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.options = options ?? new AccessRequestApprovalOptions();
        this.rowSecurity = rowSecurity;
    }

    /// <summary>
    /// Submits a request. Any authenticated principal may submit; the caller sets the request's subject to the
    /// requester (so a grant can never target a third party). When <paramref name="eligibleForSelfElevation"/> is
    /// <see langword="true"/> — the requester is eligible to self-elevate exactly this — the request is auto-approved
    /// (self-elevation, no human approver); otherwise it is created pending an administrator's decision.
    /// </summary>
    /// <param name="draft">The draft request carrying the create-content (subject = the requester) as JSON values.</param>
    /// <param name="actor">The requester's audit identity.</param>
    /// <param name="eligibleForSelfElevation">Whether the requester is eligible to self-elevate this request (caller-resolved from claims for now; §16.5.3).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created request — pending, or already approved when self-elevated.</returns>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> SubmitAsync(AccessRequest draft, string actor, bool eligibleForSelfElevation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ValidateWorkflowId(draft.BaseWorkflowIdValue);

        ParsedJsonDocument<AccessRequest> created = await this.requests.CreateAsync(draft, actor, cancellationToken).ConfigureAwait(false);

        // Self-elevation eligibility is symmetric to capability (§16.5.3): claims ∪ stored eligibility. The caller
        // resolves the IdP-coarse claims part; here we add the approver-granted part — a stored eligibility assignment
        // (an eligibleOnly binding) for this subject + workflow + scopes. Either source auto-approves into a fresh,
        // time-boxed active grant; otherwise the request stays pending for a human approver.
        bool eligible = eligibleForSelfElevation || await this.IsStoredEligibleAsync(draft, cancellationToken).ConfigureAwait(false);
        if (!eligible)
        {
            return created;
        }

        try
        {
            AccessRequest request = created.RootElement;
            return await this.GrantAndDecideAsync(request, request.EtagValue, actor, SelfElevationReason, cancellationToken).ConfigureAwait(false)
                ?? throw new AccessRequestStateException(created.RootElement.IdValue, "The self-elevation could not be completed.");
        }
        finally
        {
            created.Dispose();
        }
    }

    /// <summary>Approves a pending request, writing the time-boxed entitlement. The approver must be a §15 administrator of the target workflow.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The approver's unforgeable identity tags.</param>
    /// <param name="actor">The approver's audit identity.</param>
    /// <param name="reason">An optional approval note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The approved request, or <see langword="null"/> if no request with that id exists.</returns>
    /// <exception cref="WorkflowAdministrationException">The approver is not an administrator of the target workflow.</exception>
    /// <exception cref="AccessRequestStateException">The request is not pending, or none of its scopes is grantable.</exception>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> ApproveAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(actor);
        using ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return null;
        }

        AccessRequest request = fetched.RootElement;
        RequireStatus(request, AccessRequestStatus.Pending);
        await this.EnsureAdministratorAsync(request.BaseWorkflowIdValue, approverIdentity, cancellationToken).ConfigureAwait(false);
        return await this.GrantAndDecideAsync(request, request.EtagValue, actor, reason, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Approves a pending request as <em>durable eligibility</em> (§16.5.3): writes an eligibility assignment
    /// (an <c>eligibleOnly</c> binding) rather than a live grant, so the requester may thereafter self-elevate this
    /// capability JIT without re-approval. The approver must be a §15 administrator of the target workflow.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The approver's unforgeable identity tags.</param>
    /// <param name="actor">The approver's audit identity.</param>
    /// <param name="reason">An optional approval note.</param>
    /// <param name="eligibilityWindow">How long the eligibility itself lasts; <see langword="null"/> is standing eligibility. Each activation is independently capped at the deployment max TTL.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The request marked <see cref="AccessRequestStatus.Eligible"/>, or <see langword="null"/> if no request with that id exists.</returns>
    /// <exception cref="WorkflowAdministrationException">The approver is not an administrator of the target workflow.</exception>
    /// <exception cref="AccessRequestStateException">The request is not pending, or none of its scopes is grantable.</exception>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> ApproveAsEligibleAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, TimeSpan? eligibilityWindow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(actor);
        using ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return null;
        }

        AccessRequest request = fetched.RootElement;
        RequireStatus(request, AccessRequestStatus.Pending);
        await this.EnsureAdministratorAsync(request.BaseWorkflowIdValue, approverIdentity, cancellationToken).ConfigureAwait(false);

        List<string> granted = this.CapScopes(request.RequestedScopesArray());
        if (granted.Count == 0)
        {
            throw new AccessRequestStateException(request.IdValue, "None of the requested scopes is grantable (eligibility may cover only run access).");
        }

        DateTimeOffset? expiresAt = eligibilityWindow is { } window ? this.timeProvider.GetUtcNow().Add(window) : null;
        string bindingId = await this.WriteBindingAsync(request, granted, actor, eligibleOnly: true, expiresAt: expiresAt, cancellationToken).ConfigureAwait(false);
        try
        {
            // The eligibility assignment confers nothing active (the resolver ignores eligibleOnly bindings), so there is
            // no in-process policy refresh — only the self-elevation strategy reads it, from the store.
            ParsedJsonDocument<AccessRequest>? decided = await this.requests.DecideAsync(
                request.IdValue,
                new AccessRequestDecision(AccessRequestStatus.Eligible, reason, bindingId, expiresAt),
                request.EtagValue,
                actor,
                cancellationToken).ConfigureAwait(false);

            if (decided is null)
            {
                await this.RevokeBindingAsync(bindingId, cancellationToken).ConfigureAwait(false);
            }

            return decided;
        }
        catch (AccessRequestConflictException)
        {
            await this.RevokeBindingAsync(bindingId, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Denies a pending request. The decider must be a §15 administrator of the target workflow.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The administrator's unforgeable identity tags.</param>
    /// <param name="actor">The administrator's audit identity.</param>
    /// <param name="reason">An optional denial note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The denied request, or <see langword="null"/> if absent.</returns>
    /// <exception cref="WorkflowAdministrationException">The decider is not an administrator of the target workflow.</exception>
    /// <exception cref="AccessRequestStateException">The request is not pending.</exception>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DenyAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(actor);
        using ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return null;
        }

        AccessRequest request = fetched.RootElement;
        RequireStatus(request, AccessRequestStatus.Pending);
        await this.EnsureAdministratorAsync(request.BaseWorkflowIdValue, approverIdentity, cancellationToken).ConfigureAwait(false);
        return await this.requests.DecideAsync(requestId, new AccessRequestDecision(AccessRequestStatus.Denied, reason), request.EtagValue, actor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Withdraws a pending request. Only the requester (the request's own subject) may withdraw it.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="subjectClaimType">The withdrawing principal's subject claim type.</param>
    /// <param name="subjectClaimValue">The withdrawing principal's subject claim value.</param>
    /// <param name="actor">The requester's audit identity.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The withdrawn request, or <see langword="null"/> if absent.</returns>
    /// <exception cref="AccessRequestStateException">The request is not pending, or the caller is not the requester.</exception>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> WithdrawAsync(string requestId, string subjectClaimType, string subjectClaimValue, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(actor);
        using ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return null;
        }

        AccessRequest request = fetched.RootElement;
        RequireStatus(request, AccessRequestStatus.Pending);
        if (!request.SubjectClaimTypeEquals(subjectClaimType)
            || !request.SubjectClaimValueEquals(subjectClaimValue))
        {
            throw new AccessRequestStateException(requestId, "Only the requester may withdraw their own request.");
        }

        return await this.requests.DecideAsync(requestId, new AccessRequestDecision(AccessRequestStatus.Withdrawn), request.EtagValue, actor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Revokes an approved grant or an eligibility assignment early. The revoker must be a §15 administrator
    /// of the target workflow; the granted binding is deleted (an active grant stops at the next resolution, fail-safe;
    /// an eligibility assignment can no longer be activated) before the request is marked revoked.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The administrator's unforgeable identity tags.</param>
    /// <param name="actor">The administrator's audit identity.</param>
    /// <param name="reason">An optional revocation note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The revoked request, or <see langword="null"/> if absent.</returns>
    /// <exception cref="WorkflowAdministrationException">The revoker is not an administrator of the target workflow.</exception>
    /// <exception cref="AccessRequestStateException">The request is not an approved grant or an eligibility assignment.</exception>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> RevokeAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(actor);
        using ParsedJsonDocument<AccessRequest>? fetched = await this.requests.GetAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            return null;
        }

        AccessRequest request = fetched.RootElement;
        RequireRevocable(request);
        await this.EnsureAdministratorAsync(request.BaseWorkflowIdValue, approverIdentity, cancellationToken).ConfigureAwait(false);

        // Security first: drop the granted binding so access stops immediately (an eligibility assignment can no longer
        // be activated), then record the revocation.
        if (request.GrantedBindingIdOrNull is { } bindingId)
        {
            await this.RevokeBindingAsync(bindingId, cancellationToken).ConfigureAwait(false);
        }

        return await this.requests.DecideAsync(
            requestId,
            new AccessRequestDecision(AccessRequestStatus.Revoked, reason, request.GrantedBindingIdOrNull, request.GrantedUntilValue),
            request.EtagValue,
            actor,
            cancellationToken).ConfigureAwait(false);
    }

    // The status precondition is checked string-free (the JSON value's bytes are compared, no status string is realised);
    // the human-readable expected/actual names are realised only on the throw path.
    private static void RequireStatus(AccessRequest request, AccessRequestStatus expected)
    {
        if (!request.HasStatus(expected))
        {
            throw new AccessRequestStateException(request.IdValue, $"The request is {request.StatusValue}, not {AccessRequestStatusNames.ToWire(expected)}.");
        }
    }

    // Only a live grant (Approved) or a standing eligibility assignment (Eligible) can be revoked. Checked string-free; the
    // actual status name is realised only on the throw path.
    private static void RequireRevocable(AccessRequest request)
    {
        if (!request.HasStatus(AccessRequestStatus.Approved) && !request.HasStatus(AccessRequestStatus.Eligible))
        {
            throw new AccessRequestStateException(request.IdValue, $"The request is {request.StatusValue}; only an approved grant or an eligibility assignment can be revoked.");
        }
    }

    // The workflow id is woven verbatim into a security-rule literal (sys:workflow == '<id>'). The rule grammar does
    // not escape quotes, so reject any id outside a strict identifier set — a requester can never inject into the rule.
    private static void ValidateWorkflowId(string baseWorkflowId)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        foreach (char c in baseWorkflowId)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.' or ':'))
            {
                throw new AccessRequestStateException(baseWorkflowId, $"The workflow id '{baseWorkflowId}' is not a permitted access-request target.");
            }
        }
    }

    // Writes the time-boxed entitlement (a per-workflow reach rule + a per-requester binding) and decides the request
    // Approved. Race-safe: the binding is written first, then the decision claims the request under its etag — if the
    // decision is lost (e.g. two administrators) or the request has vanished, the just-written grant is compensated away.
    private async ValueTask<ParsedJsonDocument<AccessRequest>?> GrantAndDecideAsync(AccessRequest request, WorkflowEtag etag, string actor, string? decisionReason, CancellationToken cancellationToken)
    {
        List<string> granted = this.CapScopes(request);
        if (granted.Count == 0)
        {
            throw new AccessRequestStateException(request.IdValue, "None of the requested scopes is grantable (an approval may grant only run access).");
        }

        (string bindingId, DateTimeOffset expiresAt) = await this.WriteGrantAsync(request, granted, actor, cancellationToken).ConfigureAwait(false);
        try
        {
            ParsedJsonDocument<AccessRequest>? decided = await this.requests.DecideAsync(
                request.IdValue,
                new AccessRequestDecision(AccessRequestStatus.Approved, decisionReason, bindingId, expiresAt),
                etag,
                actor,
                cancellationToken).ConfigureAwait(false);

            if (decided is null)
            {
                // The request vanished between read and decide — drop the orphaned grant.
                await this.RevokeBindingAsync(bindingId, cancellationToken).ConfigureAwait(false);
                return null;
            }

            // The grant is now consistent with the decision — make it effective in-process.
            await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return decided;
        }
        catch (AccessRequestConflictException)
        {
            // Lost the decision race — compensate by removing the grant this attempt wrote.
            await this.RevokeBindingAsync(bindingId, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    // The active-grant path: time-box the entitlement at min(requested, max TTL) from now, then write it.
    private async ValueTask<(string BindingId, DateTimeOffset ExpiresAt)> WriteGrantAsync(AccessRequest request, IReadOnlyList<string> granted, string actor, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        long maxSeconds = (long)this.options.MaxTtl.TotalSeconds;
        long requested = request.RequestedDurationSecondsOrNull ?? maxSeconds;
        long seconds = Math.Clamp(requested, 1, maxSeconds);
        DateTimeOffset expiresAt = now.AddSeconds(seconds);

        string bindingId = await this.WriteBindingAsync(request, granted, actor, eligibleOnly: false, expiresAt: expiresAt, cancellationToken).ConfigureAwait(false);
        return (bindingId, expiresAt);
    }

    // Writes a single security-policy binding for the requester on the target workflow — the shared shape of an active
    // grant and an eligibility assignment. The capability scope and its matching row reach are granted together: a read
    // scope (runs:read OR catalog:read — the §17.3 "view" grant) → read-reach to the workflow's rows (the same
    // sys:workflow rule; the scope distinguishes run vs catalog visibility at the authorization layer), runs:write →
    // write-reach; purge is never granted. An eligibleOnly binding confers nothing active (the resolver ignores it) — it
    // records the eligibility the self-elevation strategy reads.
    private async ValueTask<string> WriteBindingAsync(AccessRequest request, IReadOnlyList<string> granted, string actor, bool eligibleOnly, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
    {
        string baseWorkflowId = request.BaseWorkflowIdValue;
        ValidateWorkflowId(baseWorkflowId);
        string ruleName = await this.EnsureWorkflowRuleAsync(baseWorkflowId, actor, cancellationToken).ConfigureAwait(false);

        VerbGrant reach = VerbGrant.Rules(ruleName);
        bool grantsRead = granted.Contains(ControlPlaneScopes.RunsRead) || granted.Contains(ControlPlaneScopes.CatalogRead);
        using ParsedJsonDocument<SecurityBindingDocument> draft = SecurityBindingDocument.Draft(
            request.SubjectClaimTypeValue,
            request.SubjectClaimValueValue,
            read: grantsRead ? reach : VerbGrant.None,
            write: granted.Contains(ControlPlaneScopes.RunsWrite) ? reach : VerbGrant.None,
            purge: VerbGrant.None,
            description: (eligibleOnly ? "Eligibility for access request " : "Access request ") + request.IdValue,
            scopes: granted,
            expiresAt: expiresAt,
            eligibleOnly: eligibleOnly);

        using ParsedJsonDocument<SecurityBindingDocument> binding = await this.policy.AddBindingAsync(draft.RootElement, actor, cancellationToken).ConfigureAwait(false);
        return binding.RootElement.IdValue;
    }

    // Ensures the per-workflow reach rule (sys:workflow == '<id>') exists, idempotently, and returns its name.
    private async ValueTask<string> EnsureWorkflowRuleAsync(string baseWorkflowId, string actor, CancellationToken cancellationToken)
    {
        string ruleName = WorkflowRuleName(baseWorkflowId);
        using (ParsedJsonDocument<SecurityRuleDocument>? existing = await this.policy.GetRuleAsync(ruleName, cancellationToken).ConfigureAwait(false))
        {
            if (existing is not null)
            {
                return ruleName;
            }
        }

        try
        {
            using ParsedJsonDocument<SecurityRuleDocument> ruleDraft = SecurityRuleDocument.Draft(WorkflowIdentity.WorkflowTagKey + " == '" + baseWorkflowId + "'", "Run access to workflow " + baseWorkflowId + ".");
            using (await this.policy.AddRuleAsync(
                ruleName,
                ruleDraft.RootElement,
                actor,
                cancellationToken).ConfigureAwait(false))
            {
            }
        }
        catch (InvalidOperationException)
        {
            // Raced with a concurrent grant for the same workflow; the rule now exists.
        }

        return ruleName;
    }

    private async ValueTask RevokeBindingAsync(string bindingId, CancellationToken cancellationToken)
    {
        await this.policy.DeleteBindingAsync(bindingId, WorkflowEtag.None, cancellationToken).ConfigureAwait(false);
        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureAdministratorAsync(string baseWorkflowId, SecurityTagSet approverIdentity, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<WorkflowAdministrators>? record = await this.catalog.GetAdministratorsAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        if (record?.RootElement.IsAdministeredBy(approverIdentity) != true)
        {
            throw new WorkflowAdministrationException(baseWorkflowId);
        }
    }

    // The platform cap on scopes: at most the requested scopes that the deployment allows (run access only).
    private List<string> CapScopes(AccessRequest request) => this.CapScopes(request.RequestedScopesArray());

    private List<string> CapScopes(IReadOnlyList<string> requested)
    {
        var granted = new List<string>(this.options.GrantableScopes.Count);
        foreach (string allowed in this.options.GrantableScopes)
        {
            if (requested.Contains(allowed) && !granted.Contains(allowed))
            {
                granted.Add(allowed);
            }
        }

        return granted;
    }

    // Approver-granted eligibility (§16.5.3): is there an eligibleOnly binding for this subject that covers the
    // requested workflow + (capped) scopes and has not lapsed? Read from the store directly — the resolver excludes
    // eligibility from active resolution. A cold submit-path scan over the bindings; a by-claim query is a later refinement.
    private async ValueTask<bool> IsStoredEligibleAsync(AccessRequest draft, CancellationToken cancellationToken)
    {
        List<string> capped = this.CapScopes(draft.RequestedScopesArray());
        if (capped.Count == 0)
        {
            return false;
        }

        string ruleName = WorkflowRuleName(draft.BaseWorkflowIdValue);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        using PooledDocumentList<SecurityBindingDocument> bindings = await this.policy.ListBindingsAsync(cancellationToken).ConfigureAwait(false);
        foreach (SecurityBindingDocument binding in bindings)
        {
            if (!binding.EligibleOnlyValue
                || !string.Equals(binding.ClaimTypeValue, draft.SubjectClaimTypeValue, StringComparison.Ordinal)
                || !string.Equals(binding.ClaimValueOrNull, draft.SubjectClaimValueValue, StringComparison.Ordinal))
            {
                continue;
            }

            // A lapsed eligibility window grants nothing — fail-safe, the same shape as an expired active grant.
            if (binding.ExpiresAtValue is { } expiresAt && expiresAt <= now)
            {
                continue;
            }

            // The assignment must be for this workflow (its reach names the workflow rule) and cover every capped scope.
            if ((GrantsRule(binding.Read, ruleName) || GrantsRule(binding.Write, ruleName)) && CoversAll(binding.ScopesArray(), capped))
            {
                return true;
            }
        }

        return false;
    }

    // Whether a per-verb grant names the given rule (an eligibility/grant binding's reach is the workflow's access rule).
    private static bool GrantsRule(VerbGrant grant, string ruleName)
    {
        if (!grant.HasRuleNames)
        {
            return false;
        }

        foreach (JsonString name in grant.RuleNames.EnumerateArray())
        {
            if (((JsonElement)name).EqualsString(ruleName))
            {
                return true;
            }
        }

        return false;
    }

    // Whether the eligible binding's granted scopes cover every (capped) requested scope.
    private static bool CoversAll(string[] eligibleScopes, IReadOnlyList<string> required)
    {
        foreach (string scope in required)
        {
            if (Array.IndexOf(eligibleScopes, scope) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string WorkflowRuleName(string baseWorkflowId) => "workflow-access:" + baseWorkflowId;

    private ValueTask RefreshAsync(CancellationToken cancellationToken) => this.rowSecurity?.RefreshAsync(cancellationToken) ?? default;
}

/// <summary>The platform cap applied by <see cref="AccessRequestApprovalService"/> (design §16.5 guardrail 2).</summary>
public sealed class AccessRequestApprovalOptions
{
    /// <summary>Gets the maximum lifetime of a granted entitlement; a request may propose a shorter one. Defaults to 8 hours.</summary>
    public TimeSpan MaxTtl { get; init; } = TimeSpan.FromHours(8);

    /// <summary>Gets the scopes an approval may grant — at most these, intersected with the request. Defaults to run access (<c>runs:read</c>, <c>runs:write</c>) plus the §17.3 view grant (<c>catalog:read</c>) — letting a reviewer see one workflow without joining its domain or administering it; security, purge, administration, and escalation are never grantable.</summary>
    public IReadOnlyList<string> GrantableScopes { get; init; } = [ControlPlaneScopes.RunsRead, ControlPlaneScopes.RunsWrite, ControlPlaneScopes.CatalogRead];
}