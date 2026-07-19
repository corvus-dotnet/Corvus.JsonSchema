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
/// Registers this runner on start-up and heartbeats while it is alive (design §5.4/§5.5). When a control-plane registrar is
/// configured (the real topology), the runner authenticates as its machine principal and registers through the control
/// plane's authenticated HTTP API (design §16.4), so the control plane derives the trusted principal from the token and
/// binds the runner's authorization to it. Without one (a bare two-process run), it falls back to writing its own
/// registration and an idempotent Pending authorization straight into the shared store. Either way the runner is dispatchable
/// only once an administrator of its environment authorizes it, and the heartbeat (and re-registration after a stale-prune)
/// keeps the registry current.
/// </summary>
public sealed class RunnerRegistrationService(
    IRunnerRegistry registry,
    IEnvironmentStore environments,
    IEnvironmentRunnerAuthorizationStore runnerAuthorizations,
    SecuredWorkflowCatalog catalog,
    RunnerOptions options,
    ILogger<RunnerRegistrationService> logger,
    ControlPlaneRunnerRegistrar? registrar = null) : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        try
        {
            // Registration failure is non-fatal: the runner stays alive (healthy, heartbeating) and retries on the next
            // heartbeat tick rather than crashing the process — a transient control-plane / Keycloak hiccup at startup must
            // not take the runner down.
            await this.TryRegisterAsync(startedAt, stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                // Heartbeat stays store-direct (the store-as-queue residual, §5.5): liveness only bumps the registry row's
                // lastSeenAt; the identity binding happened at registration.
                bool known = await registry.HeartbeatAsync(options.RunnerId, DateTimeOffset.UtcNow, stoppingToken).ConfigureAwait(false);
                if (!known)
                {
                    // Unknown to the registry — pruned for going stale (e.g. a long GC pause), or an initial registration
                    // that has not yet succeeded. Re-establish it through the same path.
                    await this.TryRegisterAsync(startedAt, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
    }

    // Registers, treating any failure (a control-plane 4xx/5xx, a token error, a transient network fault) as non-fatal: it is
    // logged and the runner retries on the next heartbeat. Only cancellation propagates (a graceful shutdown).
    private async Task TryRegisterAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        try
        {
            await this.RegisterAsync(startedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Runner {RunnerId} registration attempt failed; the runner stays alive and retries on the next heartbeat.", options.RunnerId);
        }
    }

    private async Task RegisterAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        // Snapshot the catalog: the runner advertises every catalogued version as hosted-and-loaded. "loaded" means this
        // runner re-enters the version's compiled executor.dll to execute its runs — the real HostedWorkflowResumer path.
        // The control plane's IsVersionHostedAsync uses loaded == true to confirm a live host before accepting a trigger.
        CatalogPage page = await catalog.SearchAsync(new CatalogQuery(Limit: 1000), AccessContext.System, cancellationToken).ConfigureAwait(false);

        if (registrar is not null)
        {
            // Authenticated registration (design §5.5/§16.4): the runner authenticates as its machine principal and POSTs its
            // self-description. The control plane derives the trusted principal from the token and stamps the environment, the
            // reach tags (from the environment's managementTags), and the last-seen instant — so the runner supplies none of
            // those. A different principal re-registering the same runnerId is refused server-side (409).
            byte[] body = BuildRegistrationBody(startedAt, page);
            string status = await registrar.RegisterAsync(body, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Runner {RunnerId} registered with the control plane as an authenticated machine principal (hosting {Count} catalog version(s)); its authorization to serve '{Environment}' is {Status}.",
                options.RunnerId,
                page.Versions.Count,
                options.Environment,
                status);
            return;
        }

        // Store-direct fallback (a bare two-process run with no control-plane API / Keycloak): the runner writes its own
        // registration and an idempotent Pending authorization straight into the shared store. No machine principal is bound.
        RunnerRegistration registration = await this.BuildRegistrationAsync(startedAt, page, cancellationToken).ConfigureAwait(false);
        await registry.RegisterAsync(registration, cancellationToken).ConfigureAwait(false);
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> authorization =
            await runnerAuthorizations.EnsurePendingAsync(options.Environment, options.RunnerId, options.RunnerId, principal: null, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Runner {RunnerId} registered (store-direct), hosting {Count} catalog version(s); its authorization to serve '{Environment}' is {Status}.",
            options.RunnerId,
            registration.HostedVersions.GetArrayLength(),
            options.Environment,
            authorization.RootElement.StatusValue);
    }

    // The authenticated-registration request body (RunnerRegistrationRequest): the runner's self-description only. The
    // control plane stamps the environment, reach tags, and last-seen instant, so they are deliberately absent here.
    private byte[] BuildRegistrationBody(DateTimeOffset startedAt, CatalogPage page)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("runnerId", options.RunnerId);
            writer.WriteString("startedAt", startedAt.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteNumber("maxConcurrency", options.MaxConcurrency);
            writer.WriteStartArray("transports");
            writer.WriteEndArray();
            WriteHostedVersions(writer, page);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private async Task<RunnerRegistration> BuildRegistrationAsync(DateTimeOffset startedAt, CatalogPage page, CancellationToken cancellationToken)
    {
        // A runner's row-security reach is its environment's (design §5.5): stamp the serving environment's managementTags
        // onto the registration as reachTags. Read as the trusted runner (System); if the environment is unknown or unscoped
        // the runner registers with no reachTags (visible only to unrestricted reach). This client-side stamp is the
        // store-direct fallback only; the authenticated path has the control plane stamp reach instead.
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
            writer.WriteStartArray("transports");
            writer.WriteEndArray();
            WriteHostedVersions(writer, page);
            writer.WriteEndObject();
        }

        return RunnerRegistration.FromJson(buffer.WrittenMemory);
    }

    // The catalog versions this runner hosts (advertised hosted-and-loaded), written identically for both registration paths.
    private static void WriteHostedVersions(Utf8JsonWriter writer, CatalogPage page)
    {
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
    }
}
