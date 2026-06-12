// <copyright file="RedisRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored verbatim as its
/// JSON document under a per-runner key <c>arazzo:runner:{runnerId}</c>, and a single set <c>arazzo:runners</c>
/// holds the runner ids so the registry can be enumerated — the same authoritative-store-with-index pattern
/// <see cref="RedisWorkflowCatalogStore"/> uses for catalog versions.
/// </summary>
/// <remarks>
/// Targets a single Redis instance (or a primary): registering and pruning touch both a per-runner key and the
/// shared index set, which is not Redis-Cluster slot-safe. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/> (or <see cref="Connect(IConnectionMultiplexer)"/>).
/// </remarks>
public sealed class RedisRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string Prefix = "arazzo:runner:";
    private const string IndexKey = "arazzo:runners";

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly bool ownsConnection;

    private RedisRunnerRegistry(IConnectionMultiplexer connection, bool ownsConnection)
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

    /// <summary>Opens a runner registry over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisRunnerRegistry> ConnectAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisRunnerRegistry(connection, ownsConnection: true);
    }

    /// <summary>Creates a runner registry over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <returns>The registry.</returns>
    public static RedisRunnerRegistry Connect(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisRunnerRegistry(connection, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string runnerId = registration.RunnerIdValue;
        await this.database.StringSetAsync(RunnerKey(runnerId), registration.ToJsonBytes()).ConfigureAwait(false);
        await this.database.SetAddAsync(IndexKey, runnerId).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        cancellationToken.ThrowIfCancellationRequested();

        RedisValue value = await this.database.StringGetAsync(RunnerKey(runnerId)).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return false;
        }

        RunnerRegistration updated = RunnerRegistration.FromJson((byte[])value!).WithLastSeenAt(at);
        await this.database.StringSetAsync(RunnerKey(runnerId), updated.ToJsonBytes()).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue[] runnerIds = await this.database.SetMembersAsync(IndexKey).ConfigureAwait(false);
        var result = new List<RunnerRegistration>();
        foreach (RedisValue member in runnerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RedisValue value = await this.database.StringGetAsync(RunnerKey((string)member!)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            result.Add(RunnerRegistration.FromJson((byte[])value!));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue[] runnerIds = await this.database.SetMembersAsync(IndexKey).ConfigureAwait(false);
        int removed = 0;
        foreach (RedisValue member in runnerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string runnerId = (string)member!;
            RedisValue value = await this.database.StringGetAsync(RunnerKey(runnerId)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            RunnerRegistration registration = RunnerRegistration.FromJson((byte[])value!);
            if (registration.LastSeenAtValue < deadBefore)
            {
                await this.database.KeyDeleteAsync(RunnerKey(runnerId)).ConfigureAwait(false);
                await this.database.SetRemoveAsync(IndexKey, runnerId).ConfigureAwait(false);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Disposes the connection if this registry created it (from a configuration string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RedisKey RunnerKey(string runnerId)
        => Prefix + runnerId;
}