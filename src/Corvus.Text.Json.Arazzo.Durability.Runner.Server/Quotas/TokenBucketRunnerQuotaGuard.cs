// <copyright file="TokenBucketRunnerQuotaGuard.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// An <see cref="IRunnerQuotaGuard"/> that meters in this process, with one token bucket per dimension per counter.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This meters per instance, not per deployment.</strong> A runner API running behind N instances therefore
/// admits up to N times each configured rate, because each instance holds its own buckets and they never compare notes.
/// For a single-instance deployment that is the aggregate ADR 0065 decision 3 asks for; for any other, it is a
/// containment measure rather than the aggregate, and a deployment that means the aggregate literally supplies a guard
/// backed by shared state. This is stated rather than worked around because a limiter that silently admits N times its
/// setting is worse than one whose scope is known.
/// </para>
/// <para>
/// What it does give, at any instance count, is containment of a single runaway caller: a runner in a retry loop, or a
/// tenant whose fleet has gone wrong, is refused here without reaching the store, which is the failure this exists to
/// stop turning into an outage for everyone else.
/// </para>
/// </remarks>
public sealed class TokenBucketRunnerQuotaGuard : IRunnerQuotaGuard
{
    private readonly RunnerQuotaOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<(RunnerQuotaKind Kind, RunnerQuotaScope Scope, string? Counter), Bucket> buckets = new();

    /// <summary>Initializes a new instance of the <see cref="TokenBucketRunnerQuotaGuard"/> class.</summary>
    /// <param name="options">The deployment's quota settings; defaults are used when omitted.</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    public TokenBucketRunnerQuotaGuard(RunnerQuotaOptions? options = null, TimeProvider? timeProvider = null)
    {
        this.options = options ?? new RunnerQuotaOptions();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public ValueTask<RunnerQuotaRejection?> TryAcquireAsync(RunnerQuotaKind kind, string? tenant, string principal, long cost, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);
        cancellationToken.ThrowIfCancellationRequested();

        if (cost <= 0)
        {
            // A zero-byte body is still a request, and the request itself is charged under its own dimension. Charging
            // nothing here keeps an empty save from being free of the volume quota and double-charged by the count one.
            return new ValueTask<RunnerQuotaRejection?>((RunnerQuotaRejection?)null);
        }

        // The tenant aggregate is tested first so that a tenant at its limit is told so, rather than being told about
        // whichever of its runners happened to arrive at the moment the aggregate was already exhausted.
        RunnerQuotaRejection? refusal =
            this.Test(kind, RunnerQuotaScope.Tenant, tenant, cost)
            ?? this.Test(kind, RunnerQuotaScope.Runner, principal, cost);

        if (refusal is not null)
        {
            return new ValueTask<RunnerQuotaRejection?>(refusal);
        }

        // Nothing was spent above: a refusal by the second scope must not leave the first scope's tokens gone, or a
        // caller pinned by its per-runner limit would drain its tenant's aggregate while never completing a request.
        this.Spend(kind, RunnerQuotaScope.Tenant, tenant, cost);
        this.Spend(kind, RunnerQuotaScope.Runner, principal, cost);
        return new ValueTask<RunnerQuotaRejection?>((RunnerQuotaRejection?)null);
    }

    /// <inheritdoc/>
    public ValueTask SpendAsync(RunnerQuotaKind kind, string? tenant, string principal, long cost, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);
        cancellationToken.ThrowIfCancellationRequested();

        if (cost > 0)
        {
            // Both scopes, unconditionally. The bucket is allowed to go negative: the overshoot is what refuses the
            // caller's next request, which is how a cost discovered too late to refuse still has an effect.
            this.Spend(kind, RunnerQuotaScope.Tenant, tenant, cost);
            this.Spend(kind, RunnerQuotaScope.Runner, principal, cost);
        }

        return ValueTask.CompletedTask;
    }

    // Whether this scope would admit the charge, without spending it.
    private RunnerQuotaRejection? Test(RunnerQuotaKind kind, RunnerQuotaScope scope, string? counter, long cost)
    {
        RunnerQuotaLimit limit = this.options.For(kind, scope);
        if (!limit.IsEnabled)
        {
            return null;
        }

        double deficit = this.BucketFor(kind, scope, counter, limit).Deficit(limit, cost, this.timeProvider.GetTimestamp(), this.timeProvider.TimestampFrequency);
        if (deficit <= 0)
        {
            return null;
        }

        // Rounded up, and never below a second: a Retry-After of zero invites an immediate retry that cannot succeed,
        // which is a spin rather than a hold.
        double seconds = Math.Max(1, Math.Ceiling(deficit / limit.PerSecond));
        return new RunnerQuotaRejection(RunnerQuotaNames.Of(kind, scope), RunnerQuotaNames.CounterOf(scope, counter), TimeSpan.FromSeconds(seconds));
    }

    private void Spend(RunnerQuotaKind kind, RunnerQuotaScope scope, string? counter, long cost)
    {
        RunnerQuotaLimit limit = this.options.For(kind, scope);
        if (limit.IsEnabled)
        {
            this.BucketFor(kind, scope, counter, limit).Spend(limit, cost, this.timeProvider.GetTimestamp(), this.timeProvider.TimestampFrequency);
        }
    }

    private Bucket BucketFor(RunnerQuotaKind kind, RunnerQuotaScope scope, string? counter, in RunnerQuotaLimit limit)
    {
        (RunnerQuotaKind, RunnerQuotaScope, string?) key = (kind, scope, counter);
        if (this.buckets.TryGetValue(key, out Bucket? existing))
        {
            return existing;
        }

        // Evict wholesale rather than by age. Every bucket refills at the same rate, so a full table is one whose
        // entries are mostly full anyway, and tracking per-bucket recency to choose a victim would cost more on the hot
        // path than it saves. Clearing forgives whatever was outstanding, which errs towards admitting rather than
        // refusing: a table that overflowed is not evidence about any one caller.
        if (this.buckets.Count >= this.options.MaximumCounters)
        {
            this.buckets.Clear();
        }

        // A bucket starts full. Starting empty would refuse the first request to every new counter, which happens on
        // every scale-out and on every table eviction, and is indistinguishable to the caller from a real overload.
        return this.buckets.GetOrAdd(key, static (_, s) => new Bucket(s.Tokens, s.Now), (Tokens: limit.EffectiveBurst, Now: this.timeProvider.GetTimestamp()));
    }

    // One counter's tokens. Refill is computed from elapsed time on each touch rather than by a timer, so an idle bucket
    // costs nothing and there is no background work proportional to the number of counters.
    //
    // Test and Spend are separate, and deliberately not one atomic operation. The guard must decide against every scope
    // before it spends against any, or a request refused by the per-runner limit would still have drained its tenant's
    // aggregate. The cost is that two concurrent callers can both pass the same test and both spend, overshooting a
    // limit by up to the concurrency level. That self-corrects on the next request, since the tokens are already gone
    // and the deficit is what the next caller sees.
    private sealed class Bucket(double initialTokens, long createdAt)
    {
        private readonly object gate = new();
        private double tokens = initialTokens;
        private long lastTouched = createdAt;

        public double Deficit(in RunnerQuotaLimit limit, long cost, long now, long frequency)
        {
            lock (this.gate)
            {
                this.Refill(limit, now, frequency);
                return cost - this.tokens;
            }
        }

        public void Spend(in RunnerQuotaLimit limit, long cost, long now, long frequency)
        {
            lock (this.gate)
            {
                this.Refill(limit, now, frequency);
                this.tokens -= cost;
            }
        }

        // The timestamps are subtracted as integers and only then converted to seconds. Converting each to seconds
        // first and subtracting loses the interval to catastrophic cancellation: a Unix-epoch timestamp is around
        // 1.8e9 seconds, where a double's spacing is about 2.4e-7, so a 200ms gap comes out a few hundred nanoseconds
        // short. That is enough to leave a bucket a fraction of a token below the cost and refuse a request that has
        // waited exactly long enough, which is the one thing a Retry-After promises will not happen.
        private void Refill(in RunnerQuotaLimit limit, long now, long frequency)
        {
            long ticks = now - this.lastTouched;
            if (ticks <= 0)
            {
                return;
            }

            this.lastTouched = now;
            this.tokens = Math.Min(limit.EffectiveBurst, this.tokens + (limit.PerSecond * ticks / frequency));
        }
    }
}