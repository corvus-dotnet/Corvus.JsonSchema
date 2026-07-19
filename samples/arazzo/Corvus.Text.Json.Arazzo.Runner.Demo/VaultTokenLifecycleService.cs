// <copyright file="VaultTokenLifecycleService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VaultSharp;

namespace Corvus.Text.Json.Arazzo.Runner.Demo;

/// <summary>
/// Keeps the runner's AppRole-issued Vault token valid for the whole life of the process (design §13.5.1). The runner
/// authenticates once at startup for a short-TTL token (the role's <c>token_ttl</c>, renewable only up to
/// <c>token_max_ttl</c>); VaultSharp does <strong>not</strong> renew or re-authenticate on its own, so a runner that
/// outlives <c>token_max_ttl</c> would lose <em>all</em> Vault access — every credential read returns <c>403</c> until
/// the process restarts. This service re-authenticates via AppRole on a timer, well inside the token TTL, so the token
/// is always fresh and never approaches <c>token_max_ttl</c>.
/// </summary>
/// <remarks>
/// Re-authentication reuses the retained RoleID + SecretID the runner unwrapped at startup (the role's
/// <c>secret_id_num_uses=0</c> makes the SecretID reusable): <see cref="IAuthMethod.ResetVaultToken"/> clears the cached
/// token so the next request re-logs in via the AppRole auth method, and the immediate <c>lookup-self</c> forces that
/// login now, so credential reads always find a fresh token. If a refresh fails transiently (a Vault blip), the token is
/// left cleared and the next actual read re-authenticates itself — the service self-heals on the following cycle.
/// <see cref="RefreshInterval"/> is deliberately shorter than the role's <c>token_ttl</c> so a token never expires
/// between refreshes.
/// </remarks>
public sealed class VaultTokenLifecycleService(IVaultClient client, ILogger<VaultTokenLifecycleService> logger) : BackgroundService
{
    // Comfortably inside the demo role's 20m token_ttl: a token is always well under its TTL when the next refresh
    // mints a replacement, and never lives long enough to approach token_max_ttl.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                client.V1.Auth.ResetVaultToken();
                await client.V1.Auth.Token.LookupSelfAsync().ConfigureAwait(false);
                logger.LogDebug("Refreshed the runner's Vault token via AppRole re-authentication.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A transient failure leaves the token cleared; the next credential read re-authenticates itself, and
                // the next cycle retries. Logged as a warning, not an error: a single missed refresh is recoverable.
                logger.LogWarning(ex, "Failed to refresh the runner's Vault token; the next credential read or cycle will retry.");
            }
        }
    }
}
