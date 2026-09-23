// <copyright file="RowSecurityPolicyRefreshService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Refreshes a <see cref="PersistentRowSecurityPolicy"/> from its store on a bounded interval, so a rule or binding
/// revoked through another replica takes effect on this one within that interval: the policy refresh bound. The
/// in-process security API and approval service refresh after their own writes; this service is what carries every
/// other replica's writes across. A refresh that fails is logged and retried on the next tick, and the last compiled
/// policy stays in force meanwhile; the service never stops on its own. A control plane in a reach-enforcing posture
/// refuses to map a persistent policy that has no such service registered (<see cref="RowSecurityPolicyRefreshExtensions"/>).
/// </summary>
public sealed class RowSecurityPolicyRefreshService : BackgroundService
{
    /// <summary>The default refresh bound: a revocation made on another replica takes effect here within this interval.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    private readonly PersistentRowSecurityPolicy policy;
    private readonly TimeSpan interval;
    private readonly ILogger? logger;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="RowSecurityPolicyRefreshService"/> class.</summary>
    /// <param name="policy">The policy to refresh.</param>
    /// <param name="interval">The refresh bound; must be positive.</param>
    /// <param name="logger">The logger a failed refresh is recorded on; <see langword="null"/> where the host registers none.</param>
    /// <param name="timeProvider">The clock the interval is measured on; defaults to <see cref="TimeProvider.System"/>.</param>
    public RowSecurityPolicyRefreshService(PersistentRowSecurityPolicy policy, TimeSpan interval, ILogger? logger, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        this.policy = policy;
        this.interval = interval;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the policy this service refreshes.</summary>
    public PersistentRowSecurityPolicy Policy => this.policy;

    /// <summary>Gets the refresh bound.</summary>
    public TimeSpan Interval => this.interval;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(this.interval, this.timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await this.policy.RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // The store could not be read. The policy compiled last stays in force, which is the fail-closed side of
                // this seam (a revocation is late, never a grant early), and the next tick tries again.
                this.logger?.LogWarning(exception, "The row-security policy could not be refreshed; the last compiled policy stays in force until the next attempt in {Interval}.", this.interval);
            }
        }
    }
}