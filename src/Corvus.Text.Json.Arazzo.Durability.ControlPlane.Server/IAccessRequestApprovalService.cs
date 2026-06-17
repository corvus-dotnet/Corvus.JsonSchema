// <copyright file="IAccessRequestApprovalService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
    /// <summary>Submits a request; auto-approves it (self-elevation) when the requester is eligible.</summary>
    /// <param name="definition">The request content (subject = the requester).</param>
    /// <param name="actor">The requester's audit identity.</param>
    /// <param name="eligibleForSelfElevation">Whether the requester is eligible to self-elevate this request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created request — pending, or already approved when self-elevated.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>> SubmitAsync(AccessRequestDefinition definition, string actor, bool eligibleForSelfElevation, CancellationToken cancellationToken);

    /// <summary>Approves a pending request, writing the time-boxed entitlement (the approver must be a §15 administrator of the target workflow).</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverIdentity">The approver's unforgeable identity tags.</param>
    /// <param name="actor">The approver's audit identity.</param>
    /// <param name="reason">An optional approval note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The approved request, or <see langword="null"/> if absent.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> ApproveAsync(string requestId, SecurityTagSet approverIdentity, string actor, string? reason, CancellationToken cancellationToken);

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