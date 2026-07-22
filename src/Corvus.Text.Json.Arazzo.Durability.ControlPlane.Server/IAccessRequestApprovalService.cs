// <copyright file="IAccessRequestApprovalService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The access-request approval surface (design §16.5): submit, approve, deny, withdraw, and revoke. The approval
/// <em>decision process</em> is a pluggable strategy (§16.5.1) — the built-in default routes to a §15 administrator;
/// the bootstrapped workflow-backed approval is an alternative implementation of this same contract — so callers
/// (the control-plane handler) depend on this abstraction rather than a concrete strategy.
/// </summary>
public interface IAccessRequestApprovalService
{
    /// <summary>Submits a request; auto-approves it (self-elevation) when the requester is eligible. The service
    /// resolves eligibility itself (§16.5.3): the deployment's self-elevation predicate over the requester's claims,
    /// unioned with any stored approver-granted eligibility.</summary>
    /// <param name="draft">The draft request carrying the create-content (subject = the requester) as JSON values.</param>
    /// <param name="actor">The requester's audit identity.</param>
    /// <param name="principal">The requester's authenticated principal, whose claims the service tests against the deployment's self-elevation predicate; <see langword="null"/> resolves claims-eligibility to false (stored eligibility still applies).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created request — pending, or already approved when self-elevated.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>> SubmitAsync(AccessRequest draft, string actor, ClaimsPrincipal? principal, CancellationToken cancellationToken);

    /// <summary>Approves a pending request, writing the time-boxed entitlement (the approver must be a §15 administrator of the target workflow).</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The approver's unforgeable identity tags.</param>
    /// <param name="actor">The approver's audit identity.</param>
    /// <param name="reason">An optional approval note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The approved request, or <see langword="null"/> if absent.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> ApproveAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken);

    /// <summary>Grants a pending request under the platform ceiling <em>without</em> a §15-administrator check — the
    /// system-credentialed grant path (design §16.5.1). Where <see cref="ApproveAsync"/> makes an administrator's
    /// decision and writes the grant in one step, this <em>only</em> writes the grant: the approval decision has already
    /// been made by the bootstrapped approval workflow (a human approver drives it via the injected decision message),
    /// and the workflow's §13 system credential calls this to enact it. The ceiling is identical to
    /// <see cref="ApproveAsync"/> and is the hard boundary — at most the requested scopes intersected with the run-access
    /// allowlist, bound to the requester (never a third party), reach fixed to the target workflow, TTL capped at the
    /// deployment maximum — so it can never widen to an arbitrary binding. Callers reach this only with the narrow
    /// <c>accessRequests:grant</c> capability, which a deployment grants solely to the approval workflow's system
    /// credential.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="actor">The granting system principal's audit identity.</param>
    /// <param name="reason">An optional grant note (typically the approver's decision note carried through the workflow).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The granted (approved) request, or <see langword="null"/> if absent.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> GrantRequestAsync(string requestId, string actor, string? reason, CancellationToken cancellationToken);

    /// <summary>Grants a pending request as <em>durable eligibility</em> (§16.5.3) under the platform ceiling
    /// <em>without</em> a §15-administrator check — the system-credentialed grant path (design §16.5.1), the sibling of
    /// <see cref="GrantRequestAsync"/> used by the bootstrapped approval workflow when the decision is 'eligible'. Writes
    /// standing eligibility (the requester may self-elevate this access JIT thereafter) rather than a one-time grant;
    /// reachable only with the narrow <c>accessRequests:grant</c> capability.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="actor">The granting system principal's audit identity.</param>
    /// <param name="reason">An optional grant note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The request marked <see cref="AccessRequestStatus.Eligible"/>, or <see langword="null"/> if absent.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> GrantRequestAsEligibleAsync(string requestId, string actor, string? reason, CancellationToken cancellationToken);

    /// <summary>Approves a pending request as durable eligibility (§16.5.3) rather than a live grant — the requester may
    /// thereafter self-elevate JIT without re-approval (the approver must be a §15 administrator of the target workflow).</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The approver's unforgeable identity tags.</param>
    /// <param name="actor">The approver's audit identity.</param>
    /// <param name="reason">An optional approval note.</param>
    /// <param name="eligibilityWindow">How long the eligibility itself lasts; <see langword="null"/> is standing eligibility.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The request marked <see cref="AccessRequestStatus.Eligible"/>, or <see langword="null"/> if absent.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> ApproveAsEligibleAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, TimeSpan? eligibilityWindow, CancellationToken cancellationToken);

    /// <summary>Denies a pending request (the decider must be a §15 administrator of the target workflow).</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The administrator's unforgeable identity tags.</param>
    /// <param name="actor">The administrator's audit identity.</param>
    /// <param name="reason">An optional denial note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The denied request, or <see langword="null"/> if absent.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> DenyAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken);

    /// <summary>Withdraws a pending request (only the requester may withdraw their own).</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="subjectClaimType">The withdrawing principal's subject claim type.</param>
    /// <param name="subjectClaimValue">The withdrawing principal's subject claim value.</param>
    /// <param name="actor">The requester's audit identity.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The withdrawn request, or <see langword="null"/> if absent.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> WithdrawAsync(string requestId, string subjectClaimType, string subjectClaimValue, string actor, CancellationToken cancellationToken);

    /// <summary>Revokes an approved grant early (the revoker must be a §15 administrator of the target workflow); the entitlement is deleted before the request is marked revoked.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The administrator's unforgeable identity tags.</param>
    /// <param name="actor">The administrator's audit identity.</param>
    /// <param name="reason">An optional revocation note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The revoked request, or <see langword="null"/> if absent.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> RevokeAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken);
}