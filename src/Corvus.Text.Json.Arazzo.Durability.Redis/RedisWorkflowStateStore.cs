// <copyright file="RedisWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>. Each run is a hash
/// holding the opaque checkpoint plus the projected index fields; optimistic concurrency and the single-owner
/// lease are atomic Lua scripts, and due timers are a sorted set scored by due-time.
/// </summary>
/// <remarks>
/// Targets a single Redis instance (or a primary): the index-maintenance Lua touches several keys derived
/// from the run, which is not Redis-Cluster slot-safe. Create instances with <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> (or <see cref="Connect(StackExchange.Redis.IConnectionMultiplexer, TimeProvider?)"/>).
/// </remarks>
public sealed class RedisWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IAsyncDisposable
{
    private const string Prefix = "arazzo:";
    private const string AllKey = Prefix + "runs";
    private const string DueKey = Prefix + "due";
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);

    // Create-or-update under optimistic concurrency, maintaining the all/due/awaiting indexes. Returns the new
    // version, or -1 on an etag conflict. KEYS: run hash, all-set, due-zset. ARGV: id, expected ("" = create),
    // checkpoint, status, workflowId, createdAt, updatedAt, dueAt|"", awaitingChannel|"", awaitingCorrelationId|"", errorType|"", correlationId|"", tagsJson|"".
    private const string SaveScript =
        """
        local cur = redis.call('HGET', KEYS[1], 'version')
        local newVersion
        if ARGV[2] == '' then
            if cur then return -1 end
            newVersion = 1
        else
            if (not cur) or (cur ~= ARGV[2]) then return -1 end
            newVersion = tonumber(cur) + 1
        end
        local oldChannel = redis.call('HGET', KEYS[1], 'awaiting_channel')
        redis.call('HSET', KEYS[1], 'checkpoint', ARGV[3], 'version', newVersion, 'status', ARGV[4], 'workflow_id', ARGV[5], 'created_at', ARGV[6], 'updated_at', ARGV[7])
        if ARGV[8] ~= '' then redis.call('HSET', KEYS[1], 'due_at', ARGV[8]) else redis.call('HDEL', KEYS[1], 'due_at') end
        if ARGV[9] ~= '' then redis.call('HSET', KEYS[1], 'awaiting_channel', ARGV[9]) else redis.call('HDEL', KEYS[1], 'awaiting_channel') end
        if ARGV[10] ~= '' then redis.call('HSET', KEYS[1], 'awaiting_correlation_id', ARGV[10]) else redis.call('HDEL', KEYS[1], 'awaiting_correlation_id') end
        if ARGV[11] ~= '' then redis.call('HSET', KEYS[1], 'error_type', ARGV[11]) else redis.call('HDEL', KEYS[1], 'error_type') end
        if ARGV[12] ~= '' then redis.call('HSET', KEYS[1], 'correlation_id', ARGV[12]) else redis.call('HDEL', KEYS[1], 'correlation_id') end
        if ARGV[13] ~= '' then redis.call('HSET', KEYS[1], 'tags_json', ARGV[13]) else redis.call('HDEL', KEYS[1], 'tags_json') end
        redis.call('SADD', KEYS[2], ARGV[1])
        if ARGV[4] == 'Suspended' and ARGV[8] ~= '' then redis.call('ZADD', KEYS[3], ARGV[8], ARGV[1]) else redis.call('ZREM', KEYS[3], ARGV[1]) end
        if oldChannel then redis.call('SREM', 'arazzo:awaiting:' .. oldChannel, ARGV[1]) end
        if ARGV[4] == 'Suspended' and ARGV[9] ~= '' then redis.call('SADD', 'arazzo:awaiting:' .. ARGV[9], ARGV[1]) end
        return newVersion
        """;

    // Acquire if the lease is absent, expired, or already ours. KEYS: lease hash. ARGV: owner, token, expiresAt, now.
    private const string AcquireLeaseScript =
        """
        local owner = redis.call('HGET', KEYS[1], 'owner')
        local exp = redis.call('HGET', KEYS[1], 'expires_at')
        if (not owner) or (tonumber(exp) <= tonumber(ARGV[4])) or (owner == ARGV[1]) then
            redis.call('HSET', KEYS[1], 'owner', ARGV[1], 'token', ARGV[2], 'expires_at', ARGV[3])
            return 1
        end
        return 0
        """;

    // Release only if we still hold the lease. KEYS: lease hash. ARGV: token.
    private const string ReleaseLeaseScript =
        """
        if redis.call('HGET', KEYS[1], 'token') == ARGV[1] then redis.call('DEL', KEYS[1]); return 1 end
        return 0
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisWorkflowStateStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
        this.ownsConnection = ownsConnection;
    }

    /// <summary>Verifies the store can be reached; Redis needs no schema provisioning.</summary>
    /// <remarks>
    /// Redis has no schema — keys appear on first write — so there is nothing to provision. This method is
    /// offered for symmetry with the other backends and to fail fast at deploy time if the server is
    /// unreachable; it opens and closes a connection. The provisioning concern for Redis is configuring ACLs
    /// (the operational principal needs only the commands and key patterns the store uses), done on the server.
    /// </remarks>
    /// <param name="configuration">A StackExchange.Redis configuration string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once connectivity is confirmed.</returns>
    public static async ValueTask PrepareAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        await using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }

    /// <summary>Opens a store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisWorkflowStateStore> ConnectAsync(
        string configuration,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisWorkflowStateStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates a store over an existing connection (the caller keeps ownership).</summary>
    /// <remarks>
    /// Supply a connection the caller configured — for example one authenticated with an operational-only
    /// Redis ACL user — so the store runs under a least-privileged principal.
    /// </remarks>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisWorkflowStateStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisWorkflowStateStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, checkpointUtf8.ToArray(), index, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, byte[] checkpoint, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue[] argv =
        [
            id.Value,
            expected.IsNone ? string.Empty : expected.Value!,
            checkpoint,
            index.Status.ToString(),
            index.WorkflowId,
            index.CreatedAt.ToUnixTimeMilliseconds(),
            index.UpdatedAt.ToUnixTimeMilliseconds(),
            index.DueAt is { } due ? due.ToUnixTimeMilliseconds() : string.Empty,
            index.AwaitingChannel ?? string.Empty,
            index.AwaitingCorrelationId ?? string.Empty,
            index.ErrorType ?? string.Empty,
            index.CorrelationId ?? string.Empty,
            index.Tags is { Count: > 0 } t ? System.Text.Json.JsonSerializer.Serialize(t) : string.Empty,
        ];

        RedisResult result = await this.database.ScriptEvaluateAsync(SaveScript, [RunKey(id.Value), AllKey, DueKey], argv).ConfigureAwait(false);
        long version = (long)result;
        if (version < 0)
        {
            throw new WorkflowConflictException(id, expected);
        }

        return new WorkflowEtag(version.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue[] values = await this.database.HashGetAsync(RunKey(id.Value), ["checkpoint", "version"]).ConfigureAwait(false);
        if (values[0].IsNull)
        {
            return null;
        }

        byte[] checkpoint = (byte[])values[0]!;
        var etag = new WorkflowEtag(((long)values[1]).ToString(CultureInfo.InvariantCulture));
        return new WorkflowCheckpoint(checkpoint, etag);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");

        RedisResult result = await this.database.ScriptEvaluateAsync(
            AcquireLeaseScript,
            [LeaseKey(id.Value)],
            [owner, token, expiresAt.ToUnixTimeMilliseconds(), now.ToUnixTimeMilliseconds()]).ConfigureAwait(false);
        return (long)result == 1 ? new WorkflowLease(id, owner, token, expiresAt) : null;
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await this.database.ScriptEvaluateAsync(ReleaseLeaseScript, [LeaseKey(lease.RunId.Value)], [lease.Token]).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue channel = await this.database.HashGetAsync(RunKey(id.Value), "awaiting_channel").ConfigureAwait(false);
        await this.database.KeyDeleteAsync(RunKey(id.Value)).ConfigureAwait(false);
        await this.database.KeyDeleteAsync(LeaseKey(id.Value)).ConfigureAwait(false);
        await this.database.SetRemoveAsync(AllKey, id.Value).ConfigureAwait(false);
        await this.database.SortedSetRemoveAsync(DueKey, id.Value).ConfigureAwait(false);
        if (!channel.IsNull)
        {
            await this.database.SetRemoveAsync(AwaitingKey(channel!), id.Value).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RedisValue[] members = await this.database.SortedSetRangeByScoreAsync(DueKey, double.NegativeInfinity, before.ToUnixTimeMilliseconds()).ConfigureAwait(false);
        foreach (RedisValue member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new WorkflowRunId(member!);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        RedisValue[] candidates = await this.database.SetMembersAsync(AwaitingKey(channel)).ConfigureAwait(false);
        foreach (RedisValue candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (correlationId is null)
            {
                yield return new WorkflowRunId(candidate!);
                continue;
            }

            RedisValue stored = await this.database.HashGetAsync(RunKey((string)candidate!), "awaiting_correlation_id").ConfigureAwait(false);
            if (stored.IsNull || stored == correlationId)
            {
                yield return new WorkflowRunId(candidate!);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        // Redis has no server-side ordering over the run set, so sort the ids and keyset-page client-side.
        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        RedisValue[] members = await this.database.SetMembersAsync(AllKey).ConfigureAwait(false);
        string[] ids = [.. members.Select(v => (string)v!).OrderBy(static s => s, StringComparer.Ordinal)];

        var runs = new List<WorkflowRunListing>();
        foreach (string id in ids)
        {
            if (after is not null && string.CompareOrdinal(id, after) <= 0)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            HashEntry[] entries = await this.database.HashGetAllAsync(RunKey(id)).ConfigureAwait(false);
            if (entries.Length == 0)
            {
                continue;
            }

            var fields = entries.ToDictionary(e => (string)e.Name!, e => e.Value);
            var status = Enum.Parse<WorkflowRunStatus>((string)fields["status"]!);
            if (query.Status is { } wantStatus && status != wantStatus)
            {
                continue;
            }

            string workflowId = (string)fields["workflow_id"]!;
            if (query.WorkflowId is { } wantWorkflow && workflowId != wantWorkflow)
            {
                continue;
            }

            long createdAt = (long)fields["created_at"];
            if (query.CreatedAfter is { } createdAfter && createdAt < createdAfter.ToUnixTimeMilliseconds())
            {
                continue;
            }

            if (query.CreatedBefore is { } createdBefore && createdAt >= createdBefore.ToUnixTimeMilliseconds())
            {
                continue;
            }

            long updatedAt = (long)fields["updated_at"];
            if (query.UpdatedAfter is { } updatedAfter && updatedAt < updatedAfter.ToUnixTimeMilliseconds())
            {
                continue;
            }

            if (query.UpdatedBefore is { } updatedBefore && updatedAt >= updatedBefore.ToUnixTimeMilliseconds())
            {
                continue;
            }

            string? correlationId = fields.TryGetValue("correlation_id", out RedisValue cidV) && !cidV.IsNull ? (string)cidV! : null;
            if (query.CorrelationId is { } wantCid && correlationId != wantCid)
            {
                continue;
            }

            IReadOnlyList<string>? tags = fields.TryGetValue("tags_json", out RedisValue tagsV) && !tagsV.IsNull && ((string)tagsV!).Length > 0 ? System.Text.Json.JsonSerializer.Deserialize<List<string>>((string)tagsV!) : null;
            if (query.Tags is { Count: > 0 } wantTags && (tags is null || !wantTags.All(tags.Contains)))
            {
                continue;
            }

            var entry = new WorkflowRunIndexEntry(
                workflowId,
                status,
                DateTimeOffset.FromUnixTimeMilliseconds(createdAt),
                DateTimeOffset.FromUnixTimeMilliseconds(updatedAt),
                fields.TryGetValue("due_at", out RedisValue dueAt) ? DateTimeOffset.FromUnixTimeMilliseconds((long)dueAt) : null,
                fields.TryGetValue("awaiting_channel", out RedisValue ch) ? (string)ch! : null,
                fields.TryGetValue("awaiting_correlation_id", out RedisValue corr) ? (string)corr! : null,
                fields.TryGetValue("error_type", out RedisValue err) ? (string)err! : null,
                CorrelationId: correlationId,
                Tags: tags);
            runs.Add(new WorkflowRunListing(new WorkflowRunId(id), entry));
            if (runs.Count > query.Limit)
            {
                break;
            }
        }

        return WorkflowContinuationToken.Paginate(runs, query.Limit);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RedisKey RunKey(string id) => Prefix + "run:" + id;

    private static RedisKey LeaseKey(string id) => Prefix + "lease:" + id;

    private static RedisKey AwaitingKey(string channel) => Prefix + "awaiting:" + channel;
}