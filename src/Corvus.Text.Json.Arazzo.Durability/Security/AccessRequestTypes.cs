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

    /// <summary>Approved as durable eligibility (§16.5.3): an eligibility assignment was written (no active grant), so
    /// the requester may self-elevate this capability JIT without re-approval. Distinct from <see cref="Approved"/>,
    /// which writes a live, time-boxed grant.</summary>
    Eligible,
}

/// <summary>A decision applied to a request (a terminal transition).</summary>
/// <param name="Status">The terminal status — <see cref="AccessRequestStatus.Approved"/>, <see cref="AccessRequestStatus.Eligible"/>, <see cref="AccessRequestStatus.Denied"/>, <see cref="AccessRequestStatus.Withdrawn"/>, or <see cref="AccessRequestStatus.Revoked"/>.</param>
/// <param name="DecisionReason">An optional note recorded with the decision.</param>
/// <param name="GrantedBindingId">The id of the security-policy binding written to confer the grant (active) or the eligibility assignment.</param>
/// <param name="GrantedUntil">When the grant (or eligibility window) expires; <see langword="null"/> for a standing grant/eligibility.</param>
public readonly record struct AccessRequestDecision(
    AccessRequestStatus Status,
    string? DecisionReason = null,
    string? GrantedBindingId = null,
    DateTimeOffset? GrantedUntil = null);

/// <summary>A filter over access requests (all criteria optional; an absent criterion matches anything).</summary>
/// <param name="Status">Only requests in this state.</param>
/// <param name="BaseWorkflowId">Only requests targeting this base workflow id, carried as its request JSON value (undefined matches anything); the store reifies it at its own leaf (a DB parameter, or a bytes-native span compare).</param>
/// <param name="SubjectClaimType">Only requests whose subject keys on this claim type (with <paramref name="SubjectClaimValue"/>, "mine"); a server-derived/config string leaf.</param>
/// <param name="SubjectClaimValue">Only requests whose subject value matches; a server-derived (ClaimsPrincipal) string leaf.</param>
public readonly record struct AccessRequestQuery(
    AccessRequestStatus? Status = null,
    JsonString BaseWorkflowId = default,
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

    /// <summary>The persisted name for <see cref="AccessRequestStatus.Eligible"/>.</summary>
    public const string Eligible = "Eligible";

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
        AccessRequestStatus.Eligible => Eligible,
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