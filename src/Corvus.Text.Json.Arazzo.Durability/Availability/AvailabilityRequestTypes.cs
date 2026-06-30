// <copyright file="AvailabilityRequestTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>The lifecycle state of an <see cref="AvailabilityRequest"/> (design §7.8).</summary>
public enum AvailabilityRequestStatus
{
    /// <summary>Awaiting an environment administrator's decision.</summary>
    Pending,

    /// <summary>Approved; the workflow version was made available in the environment.</summary>
    Approved,

    /// <summary>Declined by an environment administrator.</summary>
    Denied,

    /// <summary>Withdrawn by the requester before a decision.</summary>
    Withdrawn,
}

/// <summary>A decision applied to a request (a terminal transition).</summary>
/// <param name="Status">The terminal status — <see cref="AvailabilityRequestStatus.Approved"/>, <see cref="AvailabilityRequestStatus.Denied"/>, or <see cref="AvailabilityRequestStatus.Withdrawn"/>.</param>
/// <param name="DecisionReason">An optional note recorded with the decision.</param>
public readonly record struct AvailabilityRequestDecision(
    AvailabilityRequestStatus Status,
    string? DecisionReason = null);

/// <summary>A filter over availability requests (all criteria optional; an absent criterion matches anything).</summary>
/// <param name="Status">Only requests in this state.</param>
/// <param name="Environment">Only requests targeting this environment (ordinal); the environment queue. <see langword="null"/> matches anything.</param>
/// <param name="CreatedBy">Only requests created by this actor (the "mine" view); a server-derived (ClaimsPrincipal) string leaf. <see langword="null"/> matches anything.</param>
/// <param name="AdministeredEnvironments">The approver inbox (design §7.8): only requests targeting one of these environments
/// — the set the caller administers, resolved server-side from the reverse administration index (a genuine server-derived
/// leaf). <see langword="null"/> matches anything; a caller administering nothing is short-circuited to an empty page before
/// the store, so the set is never empty here.</param>
public readonly record struct AvailabilityRequestQuery(
    AvailabilityRequestStatus? Status = null,
    string? Environment = null,
    string? CreatedBy = null,
    IReadOnlyList<string>? AdministeredEnvironments = null)
{
    /// <summary>Whether a row passes the approver-inbox filter: <see langword="true"/> when no administered set is
    /// constrained, or when the set contains the row's environment. String-free — the candidate environment strings'
    /// bytes are compared against the row's JSON value (no environment string is realised from the document). The shared
    /// client-side membership test for stores that filter in memory (the in-memory store and the KV/table backends, which
    /// scan their keyset index and apply this per row).</summary>
    /// <param name="request">The candidate row.</param>
    /// <returns><see langword="true"/> if the row is admitted by the administered-set filter.</returns>
    public bool MatchesAdministeredSet(in AvailabilityRequest request)
    {
        if (this.AdministeredEnvironments is not { } administered)
        {
            return true;
        }

        foreach (string administeredEnvironment in administered)
        {
            if (request.EnvironmentEquals(administeredEnvironment))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>The wire (persisted) names for <see cref="AvailabilityRequestStatus"/>.</summary>
public static class AvailabilityRequestStatusNames
{
    /// <summary>The persisted name for <see cref="AvailabilityRequestStatus.Pending"/>.</summary>
    public const string Pending = "Pending";

    /// <summary>The persisted name for <see cref="AvailabilityRequestStatus.Approved"/>.</summary>
    public const string Approved = "Approved";

    /// <summary>The persisted name for <see cref="AvailabilityRequestStatus.Denied"/>.</summary>
    public const string Denied = "Denied";

    /// <summary>The persisted name for <see cref="AvailabilityRequestStatus.Withdrawn"/>.</summary>
    public const string Withdrawn = "Withdrawn";

    /// <summary>Gets the persisted wire name for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(AvailabilityRequestStatus status) => status switch
    {
        AvailabilityRequestStatus.Pending => Pending,
        AvailabilityRequestStatus.Approved => Approved,
        AvailabilityRequestStatus.Denied => Denied,
        AvailabilityRequestStatus.Withdrawn => Withdrawn,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}

/// <summary>Thrown when an availability request cannot be acted on as asked — a wrong-state transition (e.g. approving a
/// request that is not pending).</summary>
public sealed class AvailabilityRequestStateException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AvailabilityRequestStateException"/> class.</summary>
    /// <param name="requestId">The request id the operation concerned.</param>
    /// <param name="message">The reason the operation is not permitted in the request's current state.</param>
    public AvailabilityRequestStateException(string requestId, string message)
        : base(message)
        => this.RequestId = requestId;

    /// <summary>Gets the request id the operation concerned.</summary>
    public string RequestId { get; }
}

/// <summary>Thrown when an availability request's expected etag no longer matches (an optimistic-concurrency conflict).</summary>
public sealed class AvailabilityRequestConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AvailabilityRequestConflictException"/> class.</summary>
    /// <param name="requestId">The conflicting request id.</param>
    /// <param name="expectedEtag">The caller's expected etag.</param>
    public AvailabilityRequestConflictException(string requestId, WorkflowEtag expectedEtag)
        : base($"The availability request '{requestId}' was modified concurrently (expected etag '{expectedEtag.Value}').")
    {
        this.RequestId = requestId;
        this.ExpectedEtag = expectedEtag;
    }

    /// <summary>Gets the conflicting request id.</summary>
    public string RequestId { get; }

    /// <summary>Gets the caller's expected etag.</summary>
    public WorkflowEtag ExpectedEtag { get; }
}