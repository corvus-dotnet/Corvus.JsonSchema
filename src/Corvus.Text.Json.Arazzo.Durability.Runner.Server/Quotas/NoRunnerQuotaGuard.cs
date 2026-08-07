// <copyright file="NoRunnerQuotaGuard.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// An <see cref="IRunnerQuotaGuard"/> that admits everything.
/// </summary>
/// <remarks>
/// This exists so that enforcing no quotas is something a deployment states rather than something it gets by omitting
/// an argument. The runner API meters by default, because a quota a deployment must opt into is one most deployments
/// will not have, and the load this bounds arrives without warning.
/// </remarks>
public sealed class NoRunnerQuotaGuard : IRunnerQuotaGuard
{
    private NoRunnerQuotaGuard()
    {
    }

    /// <summary>Gets the instance.</summary>
    public static NoRunnerQuotaGuard Instance { get; } = new();

    /// <inheritdoc/>
    public ValueTask<RunnerQuotaRejection?> TryAcquireAsync(RunnerQuotaKind kind, string? tenant, string principal, long cost, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<RunnerQuotaRejection?>((RunnerQuotaRejection?)null);
    }

    /// <inheritdoc/>
    public ValueTask SpendAsync(RunnerQuotaKind kind, string? tenant, string principal, long cost, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}