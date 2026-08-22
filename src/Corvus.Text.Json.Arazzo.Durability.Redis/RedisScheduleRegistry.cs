// <copyright file="RedisScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Schedules;

using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IScheduleRegistry"/>: one hash per <c>scheduleId</c> whose atomic
/// register-if-absent script enforces the schedules contract's deployment-global uniqueness. Under the
/// composite <c>(environment, runId)</c> run key (ADR 0065 decision 9) the registry is the sole guardian of
/// that uniqueness — a run-key collision can no longer observe a schedule created in another environment — so
/// a Redis deployment needs this durable registry, not the in-memory one, or every control-plane restart
/// forgets which environment holds each schedule id.
/// </summary>
/// <remarks>
/// Create instances with <see cref="ConnectAsync(string, CancellationToken)"/> (or
/// <see cref="Connect(StackExchange.Redis.IConnectionMultiplexer)"/>). Redis has no schema, so there is
/// nothing to provision beyond connectivity, which <see cref="PrepareAsync(string, CancellationToken)"/>
/// verifies for deploy-time symmetry with the other backends.
/// </remarks>
public sealed class RedisScheduleRegistry : IScheduleRegistry, IAsyncDisposable
{
    private const string KeyPrefix = "arazzo:schedule:";

    // Register-if-absent, idempotent for the identical registration: 1 = registered or converged, 0 = occupied
    // by a different registration. KEYS: registration hash. ARGV: environment, runId.
    private const string RegisterScript =
        """
        local env = redis.call('HGET', KEYS[1], 'environment')
        if not env then
            redis.call('HSET', KEYS[1], 'environment', ARGV[1], 'run_id', ARGV[2])
            return 1
        end
        if env == ARGV[1] and redis.call('HGET', KEYS[1], 'run_id') == ARGV[2] then return 1 end
        return 0
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly bool ownsConnection;

    private RedisScheduleRegistry(IConnectionMultiplexer connection, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.ownsConnection = ownsConnection;
    }

    /// <summary>Verifies the registry can be reached; Redis needs no schema provisioning.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once connectivity is confirmed.</returns>
    public static async ValueTask PrepareAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        await using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }

    /// <summary>Opens a registry over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisScheduleRegistry> ConnectAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisScheduleRegistry(connection, ownsConnection: true);
    }

    /// <summary>Creates a registry over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <returns>The registry.</returns>
    public static RedisScheduleRegistry Connect(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisScheduleRegistry(connection, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        cancellationToken.ThrowIfCancellationRequested();

        // The atomic script IS the uniqueness gate: register-if-absent, converge on the identical registration
        // (the crash-retry path), refuse anything else without disclosing the occupant (the caller may have no
        // reach over its environment).
        RedisResult result = await this.database.ScriptEvaluateAsync(
            RegisterScript,
            [RegistrationKey(scheduleId)],
            [registration.Environment, registration.RunId.Value]).ConfigureAwait(false);
        if ((long)result == 0)
        {
            ThrowHelper.ThrowScheduleRegistrationConflict(scheduleId);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        cancellationToken.ThrowIfCancellationRequested();

        RedisValue[] fields = await this.database.HashGetAsync(RegistrationKey(scheduleId), ["environment", "run_id"]).ConfigureAwait(false);
        return fields[0].IsNull
            ? null
            : new ScheduleRegistration((string)fields[0]!, new WorkflowRunId((string)fields[1]!));
    }

    /// <inheritdoc/>
    public async ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        cancellationToken.ThrowIfCancellationRequested();

        await this.database.KeyDeleteAsync(RegistrationKey(scheduleId)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RedisKey RegistrationKey(string scheduleId) => KeyPrefix + scheduleId;
}