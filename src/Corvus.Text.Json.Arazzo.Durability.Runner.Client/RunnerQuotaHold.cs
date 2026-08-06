// <copyright file="RunnerQuotaHold.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// One advance's remaining allowance for waiting out quota refusals (ADR 0065 decision 3).
/// </summary>
/// <remarks>
/// The budget is per run rather than per request, because a runner holds a run's lease for exactly as long as it is
/// advancing it: the lease is taken at the claim and given up when the advance ends, so "while this lease is held" and
/// "during this advance" are the same interval. A per-request budget would let a server multiply the stall by however
/// many operations an advance happens to make.
/// </remarks>
internal sealed class RunnerQuotaHold
{
    private readonly RunnerQuotaHoldOptions options;
    private readonly TimeProvider timeProvider;
    private TimeSpan held;
    private int attempts;

    internal RunnerQuotaHold(RunnerQuotaHoldOptions options, TimeProvider timeProvider)
    {
        this.options = options;
        this.timeProvider = timeProvider;
    }

    /// <summary>Waits out one refusal, or reports that this advance's allowance is spent.</summary>
    /// <param name="retryAfterSeconds">The seconds the refusal asked for, or <see langword="null"/> when it carried none.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the caller should resend; <see langword="false"/> when the advance must fail.</returns>
    internal async ValueTask<bool> TryWaitAsync(long? retryAfterSeconds, CancellationToken cancellationToken)
    {
        TimeSpan wait = this.options.BoundWait(retryAfterSeconds);

        // Both bounds are checked before waiting rather than after, so the last permitted attempt is one that can still
        // succeed. Checking afterwards would spend the whole allowance and then refuse anyway.
        if (this.attempts >= this.options.MaximumAttempts || this.held + wait > this.options.MaximumHold)
        {
            return false;
        }

        ++this.attempts;
        this.held += wait;

        await Task.Delay(wait, this.timeProvider, cancellationToken).ConfigureAwait(false);
        return true;
    }
}