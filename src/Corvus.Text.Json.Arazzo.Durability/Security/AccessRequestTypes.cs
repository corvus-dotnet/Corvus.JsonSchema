// <copyright file="AccessRequestTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>The lifecycle state of an <see cref="AccessRequest"/> (design §16.5).</summary>
public enum AccessRequestStatus
{
    /// <summary>Awaiting an administrator's decision.</summary>
    Pending,

    /// <summary>Approved; an entitlement was written (see <see cref="AccessRequest.GrantedBindingIdOrNull"/>).</summary>
    Approved,

    /// <summary>Declined by an administrator.</summary>
    Denied,

    /// <summary>Withdrawn by the requester before a decision.</summary>
    Withdrawn,

    /// <summary>An approved grant revoked early by an administrator (the entitlement was deleted).</summary>
    Revoked,
}

/// <summary>The content of a new access request (supplied on create).</summary>
/// <param name="BaseWorkflowId">The base workflow id the request targets (approval routes to its §15 administrators).</param>
/// <param name="RequestedScopes">The capability scopes requested (e.g. <c>runs:write</c>); at least one.</param>
/// <param name="SubjectClaimType">The principal claim type the eventual grant keys on (e.g. <c>sub</c>).</param>
/// <param name="SubjectClaimValue">The requester's value for <paramref name="SubjectClaimType"/>.</param>
/// <param name="RequesterLabel">An optional human-friendly label for the requester (display only).</param>
/// <param name="Reason">An optional justification.</param>
/// <param name="RequestedDurationSeconds">The optional time-bound (PIM) duration the requester proposes, in seconds; <see langword="null"/> defaults to the deployment maximum TTL.</param>
public readonly record struct AccessRequestDefinition(
    string BaseWorkflowId,
    IReadOnlyList<string> RequestedScopes,
    string SubjectClaimType,
    string SubjectClaimValue,
    string? RequesterLabel = null,
    string? Reason = null,
    long? RequestedDurationSeconds = null);

/// <summary>A decision applied to a pending request (a terminal transition).</summary>
/// <param name="Status">The terminal status (<see cref="AccessRequestStatus.Approved"/>, <see cref="AccessRequestStatus.Denied"/>, or <see cref="AccessRequestStatus.Withdrawn"/>).</param>
/// <param name="DecisionReason">An optional note recorded with the decision.</param>
/// <param name="GrantedBindingId">On approval, the id of the security-policy binding written to confer the grant.</param>
/// <param name="GrantedUntil">On approval of a time-bound grant, when it expires; <see langword="null"/> for a standing grant.</param>
public readonly record struct AccessRequestDecision(
    AccessRequestStatus Status,
    string? DecisionReason = null,
    string? GrantedBindingId = null,
    DateTimeOffset? GrantedUntil = null);

/// <summary>A filter over access requests (all criteria optional; an absent criterion matches anything).</summary>
/// <param name="Status">Only requests in this state.</param>
/// <param name="BaseWorkflowId">Only requests targeting this base workflow id.</param>
/// <param name="SubjectClaimType">Only requests whose subject keys on this claim type (with <paramref name="SubjectClaimValue"/>, "mine").</param>
/// <param name="SubjectClaimValue">Only requests whose subject value matches.</param>
public readonly record struct AccessRequestQuery(
    AccessRequestStatus? Status = null,
    string? BaseWorkflowId = null,
    string? SubjectClaimType = null,
    string? SubjectClaimValue = null);

/// <summary>The wire (persisted) names for <see cref="AccessRequestStatus"/>.</summary>
public static class AccessRequestStatusNames
{
    /// <summary>The persisted name for <see cref="AccessRequestStatus.Pending"/>.</summary>
    public const string Pending = "Pending";

    /// <summary>The persisted name for <see cref="AccessRequestStatus.Approved"/>.</summary>
    public const string Approved = "Approved";

    /// <summary>The persisted name for <see cref="AccessRequestStatus.Denied"/>.</summary>
    public const string Denied = "Denied";

    /// <summary>The persisted name for <see cref="AccessRequestStatus.Withdrawn"/>.</summary>
    public const string Withdrawn = "Withdrawn";

    /// <summary>The persisted name for <see cref="AccessRequestStatus.Revoked"/>.</summary>
    public const string Revoked = "Revoked";

    /// <summary>Gets the persisted wire name for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(AccessRequestStatus status) => status switch
    {
        AccessRequestStatus.Pending => Pending,
        AccessRequestStatus.Approved => Approved,
        AccessRequestStatus.Denied => Denied,
        AccessRequestStatus.Withdrawn => Withdrawn,
        AccessRequestStatus.Revoked => Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}

/// <summary>Thrown when an access request cannot be acted on as asked — a wrong-state transition (e.g. approving a
/// request that is not pending) or a request whose scopes are not grantable by the platform cap.</summary>
public sealed class AccessRequestStateException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AccessRequestStateException"/> class.</summary>
    /// <param name="requestId">The request id (or workflow id) the operation concerned.</param>
    /// <param name="message">The reason the operation is not permitted in the request's current state.</param>
    public AccessRequestStateException(string requestId, string message)
        : base(message)
        => this.RequestId = requestId;

    /// <summary>Gets the request id (or workflow id) the operation concerned.</summary>
    public string RequestId { get; }
}

/// <summary>Thrown when an access request's expected etag no longer matches (an optimistic-concurrency conflict).</summary>
public sealed class AccessRequestConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AccessRequestConflictException"/> class.</summary>
    /// <param name="requestId">The conflicting request id.</param>
    /// <param name="expectedEtag">The caller's expected etag.</param>
    public AccessRequestConflictException(string requestId, WorkflowEtag expectedEtag)
        : base($"The access request '{requestId}' was modified concurrently (expected etag '{expectedEtag.Value}').")
    {
        this.RequestId = requestId;
        this.ExpectedEtag = expectedEtag;
    }

    /// <summary>Gets the conflicting request id.</summary>
    public string RequestId { get; }

    /// <summary>Gets the caller's expected etag.</summary>
    public WorkflowEtag ExpectedEtag { get; }
}