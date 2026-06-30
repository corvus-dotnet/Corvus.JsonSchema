// <copyright file="RunnerRegistrationService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Runner.Demo;

/// <summary>
/// Registers this runner in the shared <see cref="IRunnerRegistry"/> on start-up and heartbeats while it is
/// alive (design §5.4) — the only thing a runner pushes to the control plane; everything else flows through the
/// shared store. The control plane reads the registry to answer <c>GET /runners</c> and to gate triggers on a
/// live host. If a heartbeat finds the runner was pruned (it went stale), it re-registers.
/// </summary>
public sealed class RunnerRegistrationService(
    IRunnerRegistry registry,
    SecuredWorkflowCatalog catalog,
    RunnerOptions options,
    ILogger<RunnerRegistrationService> logger) : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        try
        {
            RunnerRegistration registration = await this.BuildRegistrationAsync(startedAt, stoppingToken).ConfigureAwait(false);
            await registry.RegisterAsync(registration, stoppingToken).ConfigureAwait(false);
            logger.LogInformation(
                "Runner {RunnerId} registered, hosting {Count} catalog version(s).",
                options.RunnerId,
                registration.HostedVersions.GetArrayLength());

            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                bool known = await registry.HeartbeatAsync(options.RunnerId, DateTimeOffset.UtcNow, stoppingToken).ConfigureAwait(false);
                if (!known)
                {
                    // We were pruned for going stale (e.g. a long GC pause) — re-establish the registration.
                    await registry.RegisterAsync(await this.BuildRegistrationAsync(startedAt, stoppingToken).ConfigureAwait(false), stoppingToken).ConfigureAwait(false);
                    logger.LogInformation("Runner {RunnerId} re-registered after a stale heartbeat.", options.RunnerId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
    }

    private async Task<RunnerRegistration> BuildRegistrationAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        // Snapshot the catalog: the runner advertises every catalogued version as hosted-and-loaded. With live
        // execution still paused (no compiled executor.dll to load), "loaded" means "this runner processes the
        // version's runs" — via the stub resumer for now; real ALC loading replaces that. The control plane's
        // IsVersionHostedAsync uses loaded == true to confirm a live host before accepting a trigger.
        CatalogPage page = await catalog.SearchAsync(new CatalogQuery(Limit: 1000), AccessContext.System, cancellationToken).ConfigureAwait(false);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            writer.WriteStartObject();
            writer.WriteString("runnerId", options.RunnerId);
            writer.WriteString("environment", options.Environment);
            writer.WriteString("startedAt", startedAt.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("lastSeenAt", now.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteNumber("maxConcurrency", options.MaxConcurrency);

            // No inbound transports are bound yet (HTTP triggers belong to the control plane; the runner's own
            // message/schedule transports arrive with live execution). An empty array is a valid registration.
            writer.WriteStartArray("transports");
            writer.WriteEndArray();

            writer.WriteStartArray("hostedVersions");
            foreach (CatalogVersion version in page.Versions)
            {
                writer.WriteStartObject();
                writer.WriteString("baseWorkflowId", (string)version.BaseWorkflowId);
                writer.WriteNumber("versionNumber", version.VersionNumber);
                writer.WriteString("hash", (string)version.Hash);
                writer.WriteBoolean("loaded", true);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return RunnerRegistration.FromJson(buffer.WrittenMemory);
    }
}
