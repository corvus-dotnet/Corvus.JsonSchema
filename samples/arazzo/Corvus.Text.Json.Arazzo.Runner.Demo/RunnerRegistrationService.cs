// <copyright file="RunnerRegistrationService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

namespace Corvus.Text.Json.Arazzo.Runner.Demo;

/// <summary>
/// Registers this runner in the shared <see cref="IRunnerRegistry"/> on start-up and heartbeats while it is
/// alive (design §5.4) — the only thing a runner pushes to the control plane; everything else flows through the
/// shared store. The control plane reads the registry to answer <c>GET /runners</c> and to gate triggers on a
/// live host. If a heartbeat finds the runner was pruned (it went stale), it re-registers.
/// </summary>
public sealed class RunnerRegistrationService(
    IRunnerRegistry registry,
    IEnvironmentStore environments,
    IEnvironmentRunnerAuthorizationStore runnerAuthorizations,
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

            // A runner may not self-assert into an environment (design §5.5): receiving its runs means receiving its
            // credentials. So registration only records the runner's intent — an idempotent Pending authorization keyed on
            // (environment, runnerId). It becomes dispatchable only once an administrator of that environment authorizes it;
            // re-registering (incl. after a stale-heartbeat prune) leaves an existing Authorized/Revoked decision intact.
            using (ParsedJsonDocument<EnvironmentRunnerAuthorization> authorization =
                await runnerAuthorizations.EnsurePendingAsync(options.Environment, options.RunnerId, options.RunnerId, stoppingToken).ConfigureAwait(false))
            {
                logger.LogInformation(
                    "Runner {RunnerId} authorization to serve environment '{Environment}' is {Status}.",
                    options.RunnerId,
                    options.Environment,
                    authorization.RootElement.StatusValue);
            }

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

        // A runner's row-security reach is its environment's (design §5.5): stamp the serving environment's
        // managementTags onto the registration as reachTags, so the control plane's reach-filtered GET /runners shows
        // this runner only to callers whose reach admits that environment. Read as the trusted runner (System); if the
        // environment is unknown or unscoped the runner registers with no reachTags (visible only to unrestricted reach).
        SecurityTagSet reachTags = SecurityTagSet.Empty;
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.Environments.Environment>? environmentDoc = await environments.GetAsync(options.Environment, AccessContext.System, cancellationToken).ConfigureAwait(false))
        {
            if (environmentDoc is { } doc)
            {
                reachTags = doc.RootElement.ManagementTagsValue;
            }
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            writer.WriteStartObject();
            writer.WriteString("runnerId", options.RunnerId);
            writer.WriteString("environment", options.Environment);
            if (!reachTags.IsEmpty)
            {
                writer.WritePropertyName("reachTags"u8);
                reachTags.WriteTo(writer);
            }

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
