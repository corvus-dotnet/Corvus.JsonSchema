// <copyright file="RedisWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>. Each run is a hash
/// keyed by the delimited composite <c>{environment}/{runId}</c> (ADR 0065 decision 9): the environment-name
/// grammar admits no <c>/</c>, so the first slash unambiguously splits the two halves, the same run id in two
/// environments names two distinct hashes, and every index-set member carries the same composite so an
/// answered address never needs a hash read. The hash holds the opaque checkpoint plus the projected index
/// fields; optimistic concurrency and the single-owner lease are atomic Lua scripts, and due timers are a
/// sorted set scored by due-time.
/// </summary>
/// <remarks>
/// Targets a single Redis instance (or a primary): the index-maintenance Lua touches several keys derived
/// from the run, which is not Redis-Cluster slot-safe. Create instances with <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> (or <see cref="Connect(StackExchange.Redis.IConnectionMultiplexer, TimeProvider?)"/>).
/// </remarks>
public sealed class RedisWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, IWorkflowLeaseAdministration, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string Prefix = "arazzo:";
    private const string AllKey = Prefix + "runs";
    private const string DueKey = Prefix + "due";
    private const string RunKeyPrefix = Prefix + "run:";
    private const string LeaseKeyPrefix = Prefix + "lease:";
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string PendingStatus = nameof(WorkflowRunStatus.Pending);
    private const string RunningStatus = nameof(WorkflowRunStatus.Running);
    private const string FaultedStatus = nameof(WorkflowRunStatus.Faulted);

    // Create-or-update under optimistic concurrency, maintaining the all/due/awaiting indexes and the §14.4
    // security-label sets. Returns the new version, or -1 on an etag conflict. KEYS: run hash, all-set, due-zset.
    // ARGV: id, expected ("" = create), checkpoint, status, workflowId, createdAt, updatedAt, dueAt|"", awaitingChannel|"", awaitingCorrelationId|"", errorType|"", correlationId|"", tagsJson|"", securityTagsJson|"", environment|"", resumeRequestedAt|"".
    //
    // The label diff decodes the persisted tag JSON with cjson only when it changed; a run's security tags are
    // fixed at creation, so the common checkpoint path is one string comparison and no set touches. The set
    // names ('arazzo:label:v:' .. byte-length .. ':' .. key .. value, and 'arazzo:label:k:' .. key) must match
    // RedisSecurityLabels byte-for-byte — Lua's # is the UTF-8 byte length of the decoded tag string, which is
    // why the C# side prefixes with GetByteCount, and the length prefix is what keeps the concatenation
    // injective. Running inside the atomic script, the diff cannot be interrupted between row and label, which
    // is stronger than the add-before-visible/remove-after ordering the non-atomic backends rely on.
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
        local oldSecurity = redis.call('HGET', KEYS[1], 'security_tags_json') or ''
        redis.call('HSET', KEYS[1], 'checkpoint', ARGV[3], 'version', newVersion, 'status', ARGV[4], 'workflow_id', ARGV[5], 'created_at', ARGV[6], 'updated_at', ARGV[7])
        if ARGV[8] ~= '' then redis.call('HSET', KEYS[1], 'due_at', ARGV[8]) else redis.call('HDEL', KEYS[1], 'due_at') end
        if ARGV[9] ~= '' then redis.call('HSET', KEYS[1], 'awaiting_channel', ARGV[9]) else redis.call('HDEL', KEYS[1], 'awaiting_channel') end
        if ARGV[10] ~= '' then redis.call('HSET', KEYS[1], 'awaiting_correlation_id', ARGV[10]) else redis.call('HDEL', KEYS[1], 'awaiting_correlation_id') end
        if ARGV[11] ~= '' then redis.call('HSET', KEYS[1], 'error_type', ARGV[11]) else redis.call('HDEL', KEYS[1], 'error_type') end
        if ARGV[12] ~= '' then redis.call('HSET', KEYS[1], 'correlation_id', ARGV[12]) else redis.call('HDEL', KEYS[1], 'correlation_id') end
        if ARGV[13] ~= '' then redis.call('HSET', KEYS[1], 'tags_json', ARGV[13]) else redis.call('HDEL', KEYS[1], 'tags_json') end
        if ARGV[14] ~= '' then redis.call('HSET', KEYS[1], 'security_tags_json', ARGV[14]) else redis.call('HDEL', KEYS[1], 'security_tags_json') end
        if ARGV[15] ~= '' then redis.call('HSET', KEYS[1], 'environment', ARGV[15]) else redis.call('HDEL', KEYS[1], 'environment') end
        if ARGV[16] ~= '' then redis.call('HSET', KEYS[1], 'resume_requested_at', ARGV[16]) else redis.call('HDEL', KEYS[1], 'resume_requested_at') end
        redis.call('SADD', KEYS[2], ARGV[1])
        if ARGV[4] == 'Suspended' and ARGV[8] ~= '' then redis.call('ZADD', KEYS[3], ARGV[8], ARGV[1]) else redis.call('ZREM', KEYS[3], ARGV[1]) end
        if oldChannel then redis.call('SREM', 'arazzo:awaiting:' .. oldChannel, ARGV[1]) end
        if ARGV[4] == 'Suspended' and ARGV[9] ~= '' then redis.call('SADD', 'arazzo:awaiting:' .. ARGV[9], ARGV[1]) end
        if oldSecurity ~= ARGV[14] then
            local function labelKeys(json)
                local keys = {}
                if json ~= '' then
                    for _, tag in ipairs(cjson.decode(json)) do
                        keys['arazzo:label:v:' .. #tag.key .. ':' .. tag.key .. tag.value] = true
                        keys['arazzo:label:k:' .. tag.key] = true
                    end
                end
                return keys
            end
            local oldKeys = labelKeys(oldSecurity)
            local newKeys = labelKeys(ARGV[14])
            for k in pairs(newKeys) do if not oldKeys[k] then redis.call('SADD', k, ARGV[1]) end end
            for k in pairs(oldKeys) do if not newKeys[k] then redis.call('SREM', k, ARGV[1]) end end
        end
        return newVersion
        """;

    // Acquire if the lease is absent, expired, or already ours, and mint the run's next epoch (ADR 0065 §6) — HINCRBY
    // creates the field at 1, so a run's first grant is the contract's floor. The granted epoch is the return value,
    // because it is minted by the same atomic script that takes the lease and a later read could not tell this grant's
    // epoch from the next one's. Zero means the lease was not taken; the store never mints zero.
    // KEYS: lease hash. ARGV: owner, token, expiresAt, now.
    private const string AcquireLeaseScript =
        """
        local owner = redis.call('HGET', KEYS[1], 'owner')
        local exp = redis.call('HGET', KEYS[1], 'expires_at')
        if (not owner) or (tonumber(exp) <= tonumber(ARGV[4])) or (owner == ARGV[1]) then
            redis.call('HSET', KEYS[1], 'owner', ARGV[1], 'token', ARGV[2], 'expires_at', ARGV[3])
            return redis.call('HINCRBY', KEYS[1], 'epoch', 1)
        end
        return 0
        """;

    // Extend only the lease we still hold, and report its expiry and the grant's epoch either way. The three conjuncts
    // are the three ways a presented lease stops being current — superseded, stolen, and lapsed (an administrative fence
    // expires in place, so it lands on the third). An extension of 0 verifies without writing, which is what an
    // operation performed under a lease needs. The epoch reported is the stored one, never the presented one: an
    // extension does not mint an epoch, so what the caller gets back is the grant's own.
    // KEYS: lease hash. ARGV: owner, token, extended expiry, now, whether to write.
    private const string ExtendLeaseScript =
        """
        local owner = redis.call('HGET', KEYS[1], 'owner')
        local token = redis.call('HGET', KEYS[1], 'token')
        local exp = redis.call('HGET', KEYS[1], 'expires_at')
        if (not owner) or (owner ~= ARGV[1]) or (token ~= ARGV[2]) or (tonumber(exp) <= tonumber(ARGV[4])) then
            return {-1, 0}
        end
        local epoch = tonumber(redis.call('HGET', KEYS[1], 'epoch'))
        if ARGV[5] == '1' then
            redis.call('HSET', KEYS[1], 'expires_at', ARGV[3])
            return {tonumber(ARGV[3]), epoch}
        end
        return {tonumber(exp), epoch}
        """;

    // Release only if we still hold the lease. Expired in place rather than deleted, so the run's epoch survives its
    // holder handing it back — the ordinary way a grant ends, and the one a counter kept only alongside a live lease
    // would forget. The hash is re-acquirable at once: every reader tests expires_at > now. DeleteAsync still removes it
    // with the run. KEYS: lease hash. ARGV: token, now.
    private const string ReleaseLeaseScript =
        """
        if redis.call('HGET', KEYS[1], 'token') == ARGV[1] then redis.call('HSET', KEYS[1], 'expires_at', ARGV[2]); return 1 end
        return 0
        """;

    // The revocation fence's per-lease step: expire in place only a lease this owner still holds live, atomically, so a
    // lease re-granted between the SCAN and this script belongs to its new grant and is left alone.
    // KEYS: lease hash. ARGV: owner, now.
    private const string FenceLeaseScript =
        """
        local owner = redis.call('HGET', KEYS[1], 'owner')
        local exp = redis.call('HGET', KEYS[1], 'expires_at')
        if owner == ARGV[1] and exp and tonumber(exp) > tonumber(ARGV[2]) then
            redis.call('HSET', KEYS[1], 'expires_at', ARGV[2])
            return 1
        end
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

    /// <inheritdoc/>
    public bool SupportsRowSecurityFilter => true;

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
        WorkflowRunAddress address,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(address, checkpointUtf8, index, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpoint, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The opaque checkpoint binds straight to a RedisValue from its ReadOnlyMemory (RedisValue carries the exact
        // length) — no per-save GC array on the run-state hot path. It is written into the run hash's 'checkpoint'
        // field. ARGV[1] is the composite {environment}/{runId} member every index set carries; the 'environment'
        // hash field is kept alongside it so the persisted row states both key halves explicitly.
        RedisValue[] argv =
        [
            CompositeId(address),
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
            index.Tags.ToJsonStringOrNull() ?? string.Empty,
            index.SecurityTags.ToJsonStringOrNull() ?? string.Empty,
            address.Environment,
            index.ResumeRequestedAt is { } resume ? resume.ToUnixTimeMilliseconds() : string.Empty,
        ];

        RedisResult result = await this.database.ScriptEvaluateAsync(SaveScript, [RunKey(address), AllKey, DueKey], argv).ConfigureAwait(false);
        long version = (long)result;
        if (version < 0)
        {
            throw new WorkflowConflictException(address, expected);
        }

        return new WorkflowEtag(version.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue[] values = await this.database.HashGetAsync(RunKey(address), ["checkpoint", "version"]).ConfigureAwait(false);
        if (values[0].IsNull)
        {
            return null;
        }

        byte[] checkpoint = (byte[])values[0]!;
        var etag = new WorkflowEtag(((long)values[1]).ToString(CultureInfo.InvariantCulture));
        return new WorkflowCheckpoint(checkpoint, etag);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunAddress address, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");

        RedisResult result = await this.database.ScriptEvaluateAsync(
            AcquireLeaseScript,
            [LeaseKey(address)],
            [owner, token, expiresAt.ToUnixTimeMilliseconds(), now.ToUnixTimeMilliseconds()]).ConfigureAwait(false);
        long epoch = (long)result;
        return epoch > 0 ? new WorkflowLease(address, owner, token, expiresAt, epoch) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> TryExtendLeaseAsync(WorkflowLease lease, TimeSpan extension, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(extension, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        bool write = extension > TimeSpan.Zero;
        RedisResult result = await this.database.ScriptEvaluateAsync(
            ExtendLeaseScript,
            [LeaseKey(lease.Address)],
            [lease.Owner, lease.Token, (now + extension).ToUnixTimeMilliseconds(), now.ToUnixTimeMilliseconds(), write ? "1" : "0"]).ConfigureAwait(false);

        long[] held = (long[])result!;
        return held[0] < 0 ? null : new WorkflowLease(lease.Address, lease.Owner, lease.Token, DateTimeOffset.FromUnixTimeMilliseconds(held[0]), held[1]);
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await this.database.ScriptEvaluateAsync(
            ReleaseLeaseScript,
            [LeaseKey(lease.Address)],
            [lease.Token, this.timeProvider.GetUtcNow().ToUnixTimeMilliseconds()]).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> ExpireLeasesForOwnerAsync(string owner, string? environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);

        // The control-plane revocation fence (§5.5): expire every live lease this owner holds in place, so an
        // authorized peer reclaims its in-flight runs at the next poll rather than after the TTL. The scope is
        // the environment being withdrawn (ADR 0065 decision 9), narrowed by the delimited key's own environment
        // half in the SCAN pattern — another environment's leases are never even enumerated; a null environment
        // fences the owner everywhere. Each candidate is expired by the atomic per-lease script, so a lease
        // re-granted between the SCAN and the script belongs to its new grant; the count is the leases fenced.
        long nowMs = this.timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        string pattern = environment is null ? LeaseKeyPrefix + "*" : LeaseKeyPrefix + environment + "/*";
        IServer server = this.connection.GetServer(this.connection.GetEndPoints()[0]);
        int fenced = 0;
        await foreach (RedisKey key in server.KeysAsync(pattern: pattern).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            RedisResult result = await this.database.ScriptEvaluateAsync(FenceLeaseScript, [key], [owner, nowMs]).ConfigureAwait(false);
            fenced += (int)(long)result;
        }

        return fenced;
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read the run's labels while its hash still exists, then drop the hash before its label entries — the
        // §14.4 ordering: an interrupted delete leaves a stale entry, harmless when nothing loads behind it,
        // whereas dropping labels first would strand a still-visible run.
        string composite = CompositeId(address);
        RedisValue[] pre = await this.database.HashGetAsync(RunKey(address), ["awaiting_channel", "security_tags_json"]).ConfigureAwait(false);
        RedisValue channel = pre[0];
        SecurityTagSet securityTags = pre[1].IsNull ? default : SecurityTagSet.FromJsonStringOrEmpty((string)pre[1]!);

        await this.database.KeyDeleteAsync(RunKey(address)).ConfigureAwait(false);
        await this.database.KeyDeleteAsync(LeaseKey(address)).ConfigureAwait(false);
        await this.database.SetRemoveAsync(AllKey, composite).ConfigureAwait(false);
        await this.database.SortedSetRemoveAsync(DueKey, composite).ConfigureAwait(false);
        if (!channel.IsNull)
        {
            await this.database.SetRemoveAsync(AwaitingKey(channel!), composite).ConfigureAwait(false);
        }

        foreach (string setKey in RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.RunLabelPrefix, securityTags))
        {
            await this.database.SetRemoveAsync(setKey, composite).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunAddress> QueryDueAsync(DateTimeOffset before, CancellationToken cancellationToken) => this.QueryDueAsync(before, null, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunAddress> QueryDueAsync(DateTimeOffset before, string? runnerEnvironment, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RedisValue[] members = await this.database.SortedSetRangeByScoreAsync(DueKey, double.NegativeInfinity, before.ToUnixTimeMilliseconds()).ConfigureAwait(false);
        foreach (RedisValue member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // §5.5 environment-scoped timer-resume: a real runner (non-null runnerEnvironment) resumes a due run only when it
            // is pinned to EXACTLY its environment (a differently-pinned run is skipped); a null runnerEnvironment is the
            // env-agnostic base overload (return every due run regardless of environment), never a runner. The member IS the
            // composite address, so no hash read is needed to answer it.
            WorkflowRunAddress address = ParseComposite((string)member!);
            if (MatchesEnvironment(address.Environment, runnerEnvironment))
            {
                yield return address;
            }
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunAddress> QueryAwaitingAsync(string channel, string? correlationId, CancellationToken cancellationToken) => this.QueryAwaitingAsync(channel, correlationId, null, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunAddress> QueryAwaitingAsync(string channel, string? correlationId, string? runnerEnvironment, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        RedisValue[] candidates = await this.database.SetMembersAsync(AwaitingKey(channel)).ConfigureAwait(false);
        foreach (RedisValue candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // §5.5 environment-scoped event-resume. A real runner (non-null runnerEnvironment) resumes an awaiting run only
            // when it is pinned to EXACTLY its environment (a differently-pinned run is skipped). A null runnerEnvironment is
            // the env-agnostic base overload (return every awaiting run regardless of environment), never a runner. The
            // member IS the composite address; only the correlation gate needs a hash read.
            WorkflowRunAddress address = ParseComposite((string)candidate!);
            if (!MatchesEnvironment(address.Environment, runnerEnvironment))
            {
                continue;
            }

            if (correlationId is not null)
            {
                RedisValue awaitingCorrelation = await this.database.HashGetAsync(RunKey(address), "awaiting_correlation_id").ConfigureAwait(false);
                if (!awaitingCorrelation.IsNull && awaitingCorrelation != correlationId)
                {
                    continue;
                }
            }

            yield return address;
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunAddress> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken)
        => this.QueryClaimableAsync(hostedWorkflowIds, null, now, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunAddress> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, string? runnerEnvironment, DateTimeOffset now, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        if (hostedWorkflowIds.Count == 0)
        {
            yield break;
        }

        var hosted = new HashSet<string>(hostedWorkflowIds, StringComparer.Ordinal);
        long nowMs = now.ToUnixTimeMilliseconds();

        // Redis has no server-side status query, so SCAN the run hashes and filter client-side. The key suffix IS
        // the composite address; a runner's environment scope narrows the SCAN pattern itself, so another
        // environment's runs are never even enumerated.
        IServer server = this.connection.GetServer(this.connection.GetEndPoints()[0]);
        string pattern = runnerEnvironment is null ? RunKeyPrefix + "*" : RunKeyPrefix + runnerEnvironment + "/*";
        await foreach (RedisKey key in server.KeysAsync(pattern: pattern).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            WorkflowRunAddress address = ParseComposite(((string)key!)[RunKeyPrefix.Length..]);
            if (!MatchesEnvironment(address.Environment, runnerEnvironment))
            {
                continue;
            }

            RedisValue[] fields = await this.database.HashGetAsync(RunKey(address), ["status", "workflow_id", "resume_requested_at"]).ConfigureAwait(false);
            RedisValue status = fields[0];
            RedisValue workflowId = fields[1];
            if (workflowId.IsNull || !hosted.Contains((string)workflowId!))
            {
                continue;
            }

            // §18: a paused (or faulted) run the control plane marked resume-claimable (resume_requested_at present) also
            // surfaces here, so a separate runner can claim and advance it; the marker is cleared on its first checkpoint.
            if (!fields[2].IsNull && (status == SuspendedStatus || status == FaultedStatus))
            {
                yield return address;
                continue;
            }

            if (status == PendingStatus)
            {
                yield return address;
                continue;
            }

            if (status == RunningStatus)
            {
                RedisValue exp = await this.database.HashGetAsync(LeaseKey(address), "expires_at").ConfigureAwait(false);
                bool leaseLive = !exp.IsNull && (long)exp > nowMs;
                if (!leaseLive)
                {
                    yield return address;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        // Redis has no server-side ordering over the run set, so collect the matching listings first, then sort by
        // the composite address (environment, then run id, both ordinal), cursor-filter and keyset-page client-side.
        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        WorkflowRunAddress? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        // §14.4: narrow to the candidate runs the label sets admit before reading anything. A null answer means
        // the index could not narrow and the sweep runs as it always did; an empty one means no run qualifies,
        // so the page is empty without a single hash read. With candidates in hand the all-runs set is not even
        // consulted — the candidates ARE the ids to consider, and a stale label entry resolves to a hash that no
        // longer exists, which the empty-hash skip below already discards.
        IReadOnlySet<string>? candidates = await RedisSecurityLabels.ResolveReachCandidatesAsync(this.database, RedisSecurityLabels.RunLabelPrefix, query.Security).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return WorkflowContinuationToken.Paginate([], query.Limit);
        }

        IEnumerable<string> ids;
        if (candidates is null)
        {
            RedisValue[] members = await this.database.SetMembersAsync(AllKey).ConfigureAwait(false);
            ids = members.Select(v => (string)v!);
        }
        else
        {
            ids = candidates;
        }

        // The members carry the composite addresses, but the sort still needs every match collected first — no
        // early break: an id enumerated later can order first.
        var runs = new List<WorkflowRunListing>();
        foreach (string composite in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkflowRunAddress address = ParseComposite(composite);
            HashEntry[] entries = await this.database.HashGetAllAsync(RunKey(address)).ConfigureAwait(false);
            if (entries.Length == 0)
            {
                continue;
            }

            var fields = entries.ToDictionary(e => (string)e.Name!, e => e.Value);
            if (!Matches(query, address.RunId.Value, fields))
            {
                continue;
            }

            var entry = new WorkflowRunIndexEntry(
                (string)fields["workflow_id"]!,
                Enum.Parse<WorkflowRunStatus>((string)fields["status"]!),
                DateTimeOffset.FromUnixTimeMilliseconds((long)fields["created_at"]),
                DateTimeOffset.FromUnixTimeMilliseconds((long)fields["updated_at"]),
                fields.TryGetValue("due_at", out RedisValue dueAt) ? DateTimeOffset.FromUnixTimeMilliseconds((long)dueAt) : null,
                fields.TryGetValue("awaiting_channel", out RedisValue ch) ? (string)ch! : null,
                fields.TryGetValue("awaiting_correlation_id", out RedisValue corr) ? (string)corr! : null,
                fields.TryGetValue("error_type", out RedisValue err) ? (string)err! : null,
                CorrelationId: fields.TryGetValue("correlation_id", out RedisValue cidV) && !cidV.IsNull ? (string)cidV! : null,
                Tags: fields.TryGetValue("tags_json", out RedisValue tagsV) && !tagsV.IsNull ? TagSet.FromJsonStringOrEmpty((string?)tagsV) : default,
                SecurityTags: fields.TryGetValue("security_tags_json", out RedisValue secV) && !secV.IsNull ? SecurityTagSet.FromJsonStringOrEmpty((string)secV!) : default);
            runs.Add(new WorkflowRunListing(address, entry));
        }

        // Sort by ascending address, drop everything at or before the cursor (the sorted prefix), and truncate to
        // Limit + 1 — the extra row is what tells Paginate a next page exists.
        runs.Sort(static (left, right) => WorkflowRunAddress.Compare(left.Address, right.Address));
        if (after is { } cursor)
        {
            int skip = 0;
            while (skip < runs.Count && WorkflowRunAddress.Compare(runs[skip].Address, cursor) <= 0)
            {
                skip++;
            }

            runs.RemoveRange(0, skip);
        }

        int cap = query.Limit + 1;
        if (runs.Count > cap)
        {
            runs.RemoveRange(cap, runs.Count - cap);
        }

        return WorkflowContinuationToken.Paginate(runs, query.Limit);
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(WorkflowQuery query, int cap, CancellationToken cancellationToken)
    {
        // Same narrowing as the list, so a count can never consider a run the list would not have shown (§14.4).
        IReadOnlySet<string>? candidates = await RedisSecurityLabels.ResolveReachCandidatesAsync(this.database, RedisSecurityLabels.RunLabelPrefix, query.Security).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return (0, false);
        }

        IEnumerable<string> ids;
        if (candidates is null)
        {
            RedisValue[] members = await this.database.SetMembersAsync(AllKey).ConfigureAwait(false);
            ids = members.Select(v => (string)v!);
        }
        else
        {
            ids = candidates;
        }

        // Bounded scan: count matches with the SAME filter the list applies (so the §14.2 reach cannot drift), stopping
        // one past the cap. No client-side sort needed — a count is order-independent.
        int count = 0;
        foreach (string member in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkflowRunAddress address = ParseComposite(member);
            HashEntry[] entries = await this.database.HashGetAllAsync(RunKey(address)).ConfigureAwait(false);
            if (entries.Length == 0)
            {
                continue;
            }

            var fields = entries.ToDictionary(e => (string)e.Name!, e => e.Value);
            if (Matches(query, address.RunId.Value, fields) && ++count > cap)
            {
                return (cap, true);
            }
        }

        return (count, false);
    }

    // The shared visibility filter (run-id point lookup / status / workflow / draft-exclusion / timestamps /
    // correlation / tags / §14.2 security reach) over a run's hash fields, WITHOUT the keyset cursor: QueryAsync
    // adds the cursor + builds the listing, CountAsync scans with just this. Both share the one predicate so the
    // reach filter cannot drift.
    private static bool Matches(WorkflowQuery query, string runId, Dictionary<string, RedisValue> fields)
    {
        if (query.RunId is { } pointRunId && !string.Equals(runId, pointRunId, StringComparison.Ordinal))
        {
            return false;
        }

        var status = Enum.Parse<WorkflowRunStatus>((string)fields["status"]!);
        if (query.Status is { } wantStatus && status != wantStatus)
        {
            return false;
        }

        string workflowId = (string)fields["workflow_id"]!;
        if (query.WorkflowId is { } wantWorkflow && workflowId != wantWorkflow)
        {
            return false;
        }

        // §18: an unfiltered visibility query never surfaces draft runs — a caller must name the reserved $draft id.
        // #896: schedule runs (the reserved $schedule kind) are hidden the same way. ADR 0065 §9 (C4): a point
        // lookup names its run — as explicit as naming the reserved id — so it sees the reserved kinds too.
        if (query.WorkflowId is null
            && query.RunId is null
            && (string.Equals(workflowId, DraftRuns.RunWorkflowId, StringComparison.Ordinal)
                || string.Equals(workflowId, ScheduleHostedWorkflow.ScheduleWorkflowId, StringComparison.Ordinal)))
        {
            return false;
        }

        long createdAt = (long)fields["created_at"];
        if (query.CreatedAfter is { } createdAfter && createdAt < createdAfter.ToUnixTimeMilliseconds())
        {
            return false;
        }

        if (query.CreatedBefore is { } createdBefore && createdAt >= createdBefore.ToUnixTimeMilliseconds())
        {
            return false;
        }

        long updatedAt = (long)fields["updated_at"];
        if (query.UpdatedAfter is { } updatedAfter && updatedAt < updatedAfter.ToUnixTimeMilliseconds())
        {
            return false;
        }

        if (query.UpdatedBefore is { } updatedBefore && updatedAt >= updatedBefore.ToUnixTimeMilliseconds())
        {
            return false;
        }

        string? correlationId = fields.TryGetValue("correlation_id", out RedisValue cidV) && !cidV.IsNull ? (string)cidV! : null;
        if (query.CorrelationId is { } wantCid && correlationId != wantCid)
        {
            return false;
        }

        TagSet tags = fields.TryGetValue("tags_json", out RedisValue tagsV) && !tagsV.IsNull ? TagSet.FromJsonStringOrEmpty((string?)tagsV) : default;
        if (!query.Tags.AllContainedIn(tags))
        {
            return false;
        }

        // Row-security reach (§14.2): the exact evaluation over the run's persisted tags. Redis cannot filter
        // inside the serialized tag JSON, so the query first narrows to the candidates the label sets admit
        // (§14.4) and this check then decides each one — an imprecise plan costs throughput, never reach.
        SecurityTagSet securityTags = fields.TryGetValue("security_tags_json", out RedisValue secV) && !secV.IsNull ? SecurityTagSet.FromJsonStringOrEmpty((string)secV!) : default;
        return query.Security is not { } security || security.IsSatisfiedBy(securityTags);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    // The delimited composite {environment}/{runId} (ADR 0065 decision 9): the environment-name grammar admits
    // no '/', so the first slash unambiguously splits the two halves however loose the run id is.
    private static string CompositeId(in WorkflowRunAddress address) => address.Environment + "/" + address.RunId.Value;

    private static WorkflowRunAddress ParseComposite(string composite)
    {
        int separator = composite.IndexOf('/', StringComparison.Ordinal);
        return new WorkflowRunAddress(composite[..separator], new WorkflowRunId(composite[(separator + 1)..]));
    }

    private static RedisKey RunKey(in WorkflowRunAddress address) => RunKeyPrefix + CompositeId(address);

    private static RedisKey LeaseKey(in WorkflowRunAddress address) => LeaseKeyPrefix + CompositeId(address);

    private static RedisKey AwaitingKey(string channel) => Prefix + "awaiting:" + channel;

    // §5.5 env-match: a real runner (non-null runnerEnvironment) claims or resumes a run only when it is pinned to
    // EXACTLY its environment — a run pinned elsewhere is never claimed (the credential boundary). The environment is
    // read out of the run's hash but originates from the run's ADDRESS (ADR 0065 decision 9), so it is never absent on
    // a row this code wrote. A null runnerEnvironment is the env-agnostic base overload (list everything regardless of
    // environment), never a runner.
    private static bool MatchesEnvironment(string runEnvironment, string? runnerEnvironment)
        => runnerEnvironment is null || string.Equals(runEnvironment, runnerEnvironment, StringComparison.Ordinal);
}