// <copyright file="AuditSinkHealth.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// What the last append to the audit sink came to (ADR 0069), for a host's health check. While the last append failed,
/// governance mutations are being applied and refused, so the deployment is unhealthy for authoring until one succeeds.
/// </summary>
public sealed class AuditSinkHealth
{
    private readonly TimeProvider timeProvider;
    private long failuresSinceSuccess;
    private long lastFailureTicks;
    private long headFailuresSinceSigned;

    /// <summary>Initializes a new instance of the <see cref="AuditSinkHealth"/> class.</summary>
    /// <param name="timeProvider">The clock failures are stamped from.</param>
    internal AuditSinkHealth(TimeProvider timeProvider) => this.timeProvider = timeProvider;

    /// <summary>Gets a value indicating whether the last append succeeded and the last head was signed, or none has been attempted.</summary>
    public bool IsHealthy => Interlocked.Read(ref this.failuresSinceSuccess) == 0 && Interlocked.Read(ref this.headFailuresSinceSigned) == 0;

    /// <summary>Gets the number of heads that could not be signed or stored since the last one that was. While it is not zero the chain's unsigned window is growing.</summary>
    public long HeadFailuresSinceSigned => Interlocked.Read(ref this.headFailuresSinceSigned);

    /// <summary>Gets the number of appends that have failed since the last one that succeeded.</summary>
    public long FailuresSinceSuccess => Interlocked.Read(ref this.failuresSinceSuccess);

    /// <summary>Gets when an append or a head last failed, or <see langword="null"/> where none has.</summary>
    public DateTimeOffset? LastFailureAt
        => Interlocked.Read(ref this.lastFailureTicks) is long ticks and not 0 ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

    /// <summary>Notes a failed append.</summary>
    internal void Failed()
    {
        Interlocked.Exchange(ref this.lastFailureTicks, this.timeProvider.GetUtcNow().UtcTicks);
        Interlocked.Increment(ref this.failuresSinceSuccess);
    }

    /// <summary>Notes a head that could not be signed or stored.</summary>
    internal void HeadFailed()
    {
        Interlocked.Exchange(ref this.lastFailureTicks, this.timeProvider.GetUtcNow().UtcTicks);
        Interlocked.Increment(ref this.headFailuresSinceSigned);
    }

    /// <summary>Notes a head that was signed and stored.</summary>
    internal void HeadSigned() => Interlocked.Exchange(ref this.headFailuresSinceSigned, 0);

    /// <summary>Notes an append that succeeded.</summary>
    internal void Succeeded() => Interlocked.Exchange(ref this.failuresSinceSuccess, 0);
}