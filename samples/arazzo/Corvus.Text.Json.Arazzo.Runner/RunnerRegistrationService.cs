// <copyright file="RunnerRegistrationService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Runner.Client;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

namespace Corvus.Text.Json.Arazzo.Runner;

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
    SecuredWorkflowCatalog? catalog,
    RunnerOptions options,
    ILogger<RunnerRegistrationService> logger,
    RunIsolationModel providedIsolation,
    ControlPlaneRunnerRegistrar? registrar = null,
    TimeSpan? heartbeatInterval = null,
    ArazzoRunnerClient? runnerApi = null) : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    // The advertised isolation the runner declares to the control plane: only the explicit "Isolated" advertises Isolated;
    // absent (or anything else) is the in-process default — mirroring how the registration and the start gate interpret it.
    private static RunIsolationModel ParseIsolationModel(string? isolationModel)
        => string.Equals(isolationModel, "Isolated", StringComparison.Ordinal) ? RunIsolationModel.Isolated : RunIsolationModel.InProcess;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ADR 0058 advertise-vs-wire fence, fatal (before the non-fatal registration loop below). The runner advertises
        // RunnerOptions.IsolationModel to the control plane, but the isolation it ACTUALLY provides is its wired resumer's
        // (providedIsolation, from the IRunExecutionBackend). A runner that advertises more than it provides — Isolated while
        // wired to an in-process resumer — would pass every control-plane check yet run isolated-required work in-process.
        // That is a deploy misconfiguration, not a transient fault, so the runner refuses to start rather than register a lie.
        // Advertising at or below what it provides is safe (the runner is simply not matched to environments it could not
        // serve), so only over-advertising is fatal.
        RunIsolationModel advertised = ParseIsolationModel(options.IsolationModel);
        if (advertised > providedIsolation)
        {
            throw new InvalidOperationException(
                $"Runner '{options.RunnerId}' advertises {advertised} isolation but its execution backend provides only {providedIsolation} (ADR 0058): a runner must not advertise more isolation than it provides. Set RunnerOptions.IsolationModel to at most {providedIsolation}, or wire an execution backend that provides {advertised}.");
        }

        // The two registration topologies need different things, and a host that supplies neither would otherwise fail
        // on the first heartbeat rather than at start-up. Fatal for the same reason the check above is: it is a deploy
        // misconfiguration, not a transient fault.
        if (registrar is not null && runnerApi is null)
        {
            throw new InvalidOperationException(
                $"Runner '{options.RunnerId}' registers through the control plane but was given no runner-API client. The versions it advertises are the ones the control plane resolves for its machine principal (ADR 0065), so it needs the runner API to ask. Pass an ArazzoRunnerClient, or drop the registrar to use the store-direct topology.");
        }

        if (registrar is null && catalog is null)
        {
            throw new InvalidOperationException(
                $"Runner '{options.RunnerId}' registers store-direct but was given no catalog. That topology has no control plane to ask, so it reads the catalog itself. Pass a SecuredWorkflowCatalog, or configure a registrar and a runner-API client.");
        }

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        try
        {
            // Registration failure is non-fatal: the runner stays alive (healthy, heartbeating) and retries on the next
            // heartbeat tick rather than crashing the process — a transient control-plane / Keycloak hiccup at startup must
            // not take the runner down.
            await this.TryRegisterAsync(startedAt, stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(heartbeatInterval ?? HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                // A failed heartbeat is as non-fatal as a failed registration: a transient store fault (a Postgres read
                // timeout, a paused container) is logged and the next tick retries — the worst case is a stale lease,
                // and the !known path below re-establishes the registration once the store answers again. The host's
                // default BackgroundServiceExceptionBehavior is StopHost, so letting the exception escape here would
                // terminate the whole runner.
                try
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
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Runner {RunnerId} heartbeat failed; the runner stays alive and retries on the next tick.", options.RunnerId);
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
        if (registrar is not null)
        {
            // The versions this runner advertises are the ones the control plane says it may execute, asked for over the
            // runner API under this runner's own machine principal (ADR 0065). It reads no catalog and holds no store
            // credential to do it.
            //
            // This is also what makes the advertisement true. Reading the catalog directly advertised every version in
            // the deployment, while dispatch only ever offers the binding-resolved ones — so a runner routinely claimed
            // to host versions it would never be given, and the control plane's IsVersionHostedAsync believed it. Asking
            // the same surface that decides what may be claimed makes the two answers the same answer.
            //
            // Before an administrator authorizes this runner the list is legitimately empty, and advertising nothing is
            // correct: an unauthorized runner is not dispatchable, so claiming to host anything would be the lie. The
            // heartbeat re-registers once the authorization lands.
            IReadOnlyList<RunnerHostedVersion> hosted = await runnerApi!.ListHostedVersionsAsync(cancellationToken).ConfigureAwait(false);

            // Authenticated registration (design §5.5/§16.4): the runner authenticates as its machine principal and POSTs its
            // self-description. The control plane derives the trusted principal from the token and stamps the environment, the
            // reach tags (from the environment's managementTags), and the last-seen instant — so the runner supplies none of
            // those. A different principal re-registering the same runnerId is refused server-side (409).
            byte[] body = BuildRegistrationBody(startedAt, hosted);
            string status = await registrar.RegisterAsync(body, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Runner {RunnerId} registered with the control plane as an authenticated machine principal (hosting {Count} version(s) it is bound to); its authorization to serve '{Environment}' is {Status}.",
                options.RunnerId,
                hosted.Count,
                options.Environment,
                status);
            return;
        }

        // Store-direct fallback (a bare two-process run with no control-plane API / Keycloak): the runner writes its own
        // registration and an idempotent Pending authorization straight into the shared store. No machine principal is
        // bound, and there is no runner API to ask, so the catalog is read here and only here. The system context is
        // honest in this topology: the runner already holds the store.
        CatalogPage page = await catalog!.SearchAsync(new CatalogQuery(Limit: 1000), AccessContext.System, cancellationToken).ConfigureAwait(false);
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
    private byte[] BuildRegistrationBody(DateTimeOffset startedAt, IReadOnlyList<RunnerHostedVersion> hosted)
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
            WriteHostedVersions(writer, hosted);
            if (options.ServesSchedules)
            {
                writer.WriteBoolean("servesSchedules", true);
            }

            if (options.IsolationModel is { Length: > 0 } isolationModel)
            {
                writer.WriteString("isolationModel", isolationModel);
            }

            // The enrolment token, where the deployment gave this runner one (ADR 0065 decision 2). A runner holds a
            // token rather than the secret that mints them: the secret would let it enrol any id for as long as it held
            // it, which is the standing capability the token exists to bound. A runner whose id an administrator has
            // already pre-authorized needs none, and sends none.
            if (options.EnrolmentToken is { Length: > 0 } enrolmentToken)
            {
                writer.WriteString("enrolmentToken", enrolmentToken);
            }

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
            if (options.ServesSchedules)
            {
                writer.WriteBoolean("servesSchedules", true);
            }

            if (options.IsolationModel is { Length: > 0 } isolationModel)
            {
                writer.WriteString("isolationModel", isolationModel);
            }

            writer.WriteEndObject();
        }

        return RunnerRegistration.FromJson(buffer.WrittenMemory);
    }

    // The versions the control plane resolved for this runner, advertised hosted-and-loaded. "loaded" means this runner
    // re-enters the version's compiled executor.dll to execute its runs — the real HostedWorkflowResumer path — and the
    // control plane's IsVersionHostedAsync uses it to confirm a live host before accepting a trigger.
    private static void WriteHostedVersions(Utf8JsonWriter writer, IReadOnlyList<RunnerHostedVersion> hosted)
    {
        writer.WriteStartArray("hostedVersions");
        foreach (RunnerHostedVersion version in hosted)
        {
            writer.WriteStartObject();
            writer.WriteString("baseWorkflowId", version.BaseWorkflowId);
            writer.WriteNumber("versionNumber", version.VersionNumber);
            writer.WriteString("hash", version.Hash);
            writer.WriteBoolean("loaded", true);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    // The store-direct path's counterpart, over a catalog page. Same shape on the wire.
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