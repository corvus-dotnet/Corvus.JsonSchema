// <copyright file="RedisEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StackExchange.Redis;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IEnvironmentStore"/> (design §7.7): deployment environments persisted as their
/// Corvus.Text.Json <see cref="Environment"/> documents. Each environment is stored verbatim under a per-environment key
/// composed from (Name and a discriminator over its immutable management tags) so reach-isolated environments that share
/// a name coexist; its etag travels inside the document, so optimistic concurrency is a read-compare-write.
/// </summary>
/// <remarks>
/// <para>Point management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) in memory
/// over the small candidate set for a name, enumerated through index sets the way the other Redis stores maintain
/// theirs. List/count narrow through the store's §14.4 security-label sets first — resolved server-side in one
/// read-only Lua evaluation — so only candidate rows are ever read, and the exact reach then decides each one; a
/// re-tagging update re-points the label entries in the same atomic script as the document write.</para>
/// <para>Targets a single Redis instance (or a primary): an add touches the per-environment key, two index sets and the
/// label sets, which is not Redis-Cluster slot-safe.</para>
/// </remarks>
public sealed class RedisEnvironmentStore : IEnvironmentStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:env:";

    // Per-environment value key: holds the Environment document bytes verbatim.
    private const string BindPrefix = Prefix + "bind:";

    // Index set per name: members are the environment discriminators, so the management path can enumerate the small
    // candidate set for a name.
    private const string NameIndexPrefix = Prefix + "name:";

    // Global index set: members are the full identity tuples "{name}{discriminator}", so ListAsync can enumerate every
    // environment.
    private const string AllIndexKey = Prefix + "all";

    // The control character SourceCredentialKey uses to separate canonical tag sets; reused here to join identity parts
    // into keys/members because it cannot appear in a name or tag.
    private const char Separator = '\u0001';

    // Write the environment only if its per-environment key does not already exist, and maintain the two index sets and
    // the §14.4 label-set entries, all atomically. KEYS: bind key, name index, all index.
    // ARGV: document bytes, discriminator, all-index member, then the label-set keys (the member of each is the
    // all-index member, the same id the reach resolution yields).
    // Returns 1 on success, 0 if an environment with that identity already exists.
    private const string AddScript =
        """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
        redis.call('SET', KEYS[1], ARGV[1])
        redis.call('SADD', KEYS[2], ARGV[2])
        redis.call('SADD', KEYS[3], ARGV[3])
        for i = 4, #ARGV do redis.call('SADD', ARGV[i], ARGV[3]) end
        return 1
        """;

    // Overwrite the document bytes and re-point the §14.4 label-set entries by the given diff, atomically — so a
    // re-tagging update can never leave the label index deciding by the old tags while the document carries the new.
    // KEYS: bind key. ARGV: document bytes, all-index member, added count, the added label-set keys, then the removed.
    private const string UpdateScript =
        """
        redis.call('SET', KEYS[1], ARGV[1])
        local added = tonumber(ARGV[3])
        for i = 4, 3 + added do redis.call('SADD', ARGV[i], ARGV[2]) end
        for i = 4 + added, #ARGV do redis.call('SREM', ARGV[i], ARGV[2]) end
        return 1
        """;

    // The deployment's single tenancy-ledger hash (ADR 0065): field `doc` holds the document bytes, field `etag` the
    // concurrency token the compare-and-swap tests. A hash rather than two keys so the compare and the swap touch one
    // key, which is also what keeps the script slot-safe.
    private const string TenancyLedgerKey = Prefix + "tenancy";

    // Write the ledger only if no row exists. KEYS: ledger hash. ARGV: document bytes, new etag.
    // Returns 1 on success, 0 if a row already exists (another governed write got there first).
    private const string TenancyInsertScript =
        """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
        redis.call('HSET', KEYS[1], 'doc', ARGV[1], 'etag', ARGV[2])
        return 1
        """;

    // Replace the ledger only if it still carries the etag the caller decided against — the compare and the swap in one
    // atomic script. KEYS: ledger hash. ARGV: document bytes, new etag, expected etag.
    // Returns 1 on success, 0 if another governed write landed first (including a row that has since vanished, where
    // HGET answers false and the comparison fails).
    private const string TenancyCommitScript =
        """
        if redis.call('HGET', KEYS[1], 'etag') ~= ARGV[3] then return 0 end
        redis.call('HSET', KEYS[1], 'doc', ARGV[1], 'etag', ARGV[2])
        return 1
        """;

    // Remove the environment and prune it from the two index sets and its §14.4 label-set entries, atomically.
    // KEYS: bind key, name index, all index. ARGV: discriminator, all-index member, then the label-set keys.
    private const string DeleteScript =
        """
        redis.call('DEL', KEYS[1])
        redis.call('SREM', KEYS[2], ARGV[1])
        redis.call('SREM', KEYS[3], ARGV[2])
        for i = 3, #ARGV do redis.call('SREM', ARGV[i], ARGV[2]) end
        return 1
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisEnvironmentStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
        this.ownsConnection = ownsConnection;
    }

    /// <summary>Verifies the store can be reached; Redis needs no schema provisioning.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once connectivity is confirmed.</returns>
    public static async ValueTask PrepareAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        await using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }

    /// <summary>Opens an environment store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisEnvironmentStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisEnvironmentStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates an environment store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisEnvironmentStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisEnvironmentStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string discriminator = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);

        RedisKey[] keys =
        [
            BindKey(draft.NameValue, discriminator),
            NameIndexKey(draft.NameValue),
            AllIndexKey,
        ];
        HashSet<string> labelSetKeys = RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.EnvironmentLabelPrefix, draft.ManagementTagsValue);
        RedisValue[] argv =
        [
            json,
            discriminator,
            AllIndexMember(draft.NameValue, discriminator),
            .. labelSetKeys.Select(static k => (RedisValue)k),
        ];

        RedisResult result = await this.database.ScriptEvaluateAsync(AddScript, keys, argv).ConfigureAwait(false);
        if ((long)result == 0)
        {
            ThrowHelper.ThrowEnvironmentAlreadyExists(draft.NameValue);
        }

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = EnvironmentContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // §14.4: narrow to the candidate identities the label sets admit before reading anything. A null answer means
        // the index could not narrow and the all-index sweep runs as it always did; an empty one means no environment
        // qualifies, so the page is empty without a single doc read. With candidates in hand the all index is not even
        // consulted — a candidate id IS the identity tuple — and a stale entry resolving to a deleted row falls into
        // the existing null-value skip. The exact reach below still decides every candidate (the plan is a sound
        // over-approximation), so an imprecise plan costs throughput and can never widen reach.
        IReadOnlySet<string>? candidates = await RedisSecurityLabels.ResolveReachCandidatesAsync(this.database, RedisSecurityLabels.EnvironmentLabelPrefix, context.Reach(AccessVerb.Read)).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return EnvironmentPage.Create(PooledDocumentList<Environment>.Empty);
        }

        // Redis has no server-side ordering for a SET, so the keyset scan runs over the candidate identity tuples in
        // memory (no documents): sort them into the stable total order (name, discriminator), seek past the cursor,
        // then fetch each candidate's document lazily — only up to what the page needs, never the whole table.
        RedisValue[] rawMembers = candidates is null
            ? await this.database.SetMembersAsync(AllIndexKey).ConfigureAwait(false)
            : [.. candidates.Select(static s => (RedisValue)s)];
        var members = new (string Name, string Discriminator)[rawMembers.Length];
        for (int i = 0; i < rawMembers.Length; i++)
        {
            members[i] = SplitAllIndexMember((string)rawMembers[i]!);
        }

        Array.Sort(members, ByIdentity);

        // Index of the first member strictly past the cursor in (name, discriminator) order.
        int start = 0;
        if (hasCursor)
        {
            while (start < members.Length && ByIdentity.Compare(members[start], (cursor.Name, cursor.TieBreaker)) <= 0)
            {
                start++;
            }
        }

        var docs = new PooledDocumentList<Environment>(pageSize);
        bool hasMore = false;
        try
        {
            string lastName = string.Empty, lastDisc = string.Empty;
            for (int i = start; i < members.Length; i++)
            {
                (string name, string discriminator) = members[i];
                RedisValue value = await this.database.StringGetAsync(BindKey(name, discriminator)).ConfigureAwait(false);
                if (value.IsNullOrEmpty)
                {
                    continue;
                }

                byte[] json = (byte[])value!;
                ParsedJsonDocument<Environment> cand = PersistedJson.ToPooledDocument<Environment>(json);
                bool kept = false;
                try
                {
                    SecurityTagSet tags = cand.RootElement.ManagementTags.IsNotUndefined()
                        ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                        : SecurityTagSet.Empty;
                    if (!context.Admits(AccessVerb.Read, tags))
                    {
                        continue;
                    }

                    if (docs.Count == pageSize)
                    {
                        // A further visible row beyond the page exists: stop and hand back a cursor at the last included row.
                        hasMore = true;
                        break;
                    }

                    docs.Add(cand);
                    kept = true;
                    lastName = name;
                    lastDisc = discriminator;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
            }

            return hasMore ? EnvironmentPage.Create(docs, lastName, lastDisc) : EnvironmentPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> UpdateAsync(string name, Environment draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(name, AccessVerb.Write, context).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // The row is addressed by its frozen create-time discriminator (ADR 0067), so the per-environment key and the
        // name/all index sets are unchanged. A draft that supplies management tags re-tags the row's reach scope (a
        // store-level replace; an omitted set is carried forward), so the §14.4 label-set entries are re-pointed by the
        // diff in the same atomic script as the document write — left stale, a narrowed listing keeps deciding by the
        // old tags and drifts from this store's own get.
        if (!draft.ManagementTagsValue.IsEmpty)
        {
            SecurityTagSet previousTags;
            using (ParsedJsonDocument<Environment> current = PersistedJson.ToPooledDocument<Environment>(existing))
            {
                previousTags = SecurityTagSet.CopyFrom(current.RootElement.ManagementTags);
            }

            HashSet<string> previousKeys = RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.EnvironmentLabelPrefix, previousTags);
            HashSet<string> desiredKeys = RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.EnvironmentLabelPrefix, draft.ManagementTagsValue);
            List<string> added = [.. desiredKeys.Where(k => !previousKeys.Contains(k))];
            List<string> removed = [.. previousKeys.Where(k => !desiredKeys.Contains(k))];
            RedisValue[] argv =
            [
                json,
                AllIndexMember(name, discriminator!),
                added.Count,
                .. added.Select(static k => (RedisValue)k),
                .. removed.Select(static k => (RedisValue)k),
            ];
            await this.database.ScriptEvaluateAsync(UpdateScript, [BindKey(name, discriminator!)], argv).ConfigureAwait(false);
        }
        else
        {
            await this.database.StringSetAsync(BindKey(name, discriminator!), json).ConfigureAwait(false);
        }

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(name, AccessVerb.Write, context).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
        }

        RedisKey[] keys =
        [
            BindKey(name, discriminator!),
            NameIndexKey(name),
            AllIndexKey,
        ];

        // The label entries derive from the CURRENT doc tags (a re-tag re-pointed them in step), so the teardown
        // removes exactly the entries the row occupies.
        HashSet<string> labelSetKeys;
        using (ParsedJsonDocument<Environment> current = PersistedJson.ToPooledDocument<Environment>(existing))
        {
            labelSetKeys = RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.EnvironmentLabelPrefix, SecurityTagSet.CopyFrom(current.RootElement.ManagementTags));
        }

        RedisValue[] argv =
        [
            discriminator!,
            AllIndexMember(name, discriminator!),
            .. labelSetKeys.Select(static k => (RedisValue)k),
        ];

        await this.database.ScriptEvaluateAsync(DeleteScript, keys, argv).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<TenancyLedger>?> GetTenancyLedgerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue stored = await this.database.HashGetAsync(TenancyLedgerKey, "doc").ConfigureAwait(false);
        return stored.IsNull ? null : PersistedJson.ToPooledDocument<TenancyLedger>((byte[])stored!);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryCommitTenancyLedgerAsync(TenancyLedger current, ReadOnlyMemory<byte> admitting, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        WorkflowEtag etag = NewEtag();
        byte[] json = TenancyLedgerSerialization.SerializeCommitted(current, admitting, actor, this.timeProvider.GetUtcNow(), etag);
        RedisKey[] keys = [TenancyLedgerKey];
        RedisResult result = current.IsUndefined()
            ? await this.database.ScriptEvaluateAsync(TenancyInsertScript, keys, [json, etag.Value!]).ConfigureAwait(false)
            : await this.database.ScriptEvaluateAsync(TenancyCommitScript, keys, [json, etag.Value!, current.EtagValue.Value!]).ConfigureAwait(false);
        return (long)result == 1;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Orders the (unordered) all-index members into the stable total keyset order for ListAsync: the contractual name
    // primary order, made total by the discriminator tie-breaker — both compared ordinally to match the token's byte
    // order.
    private static readonly IComparer<(string Name, string Discriminator)> ByIdentity =
        Comparer<(string Name, string Discriminator)>.Create(static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Discriminator, b.Discriminator);
        });

    private static RedisKey BindKey(string name, string discriminator)
        => BindPrefix + name + Separator + discriminator;

    private static RedisKey NameIndexKey(string name)
        => NameIndexPrefix + name;

    private static RedisValue AllIndexMember(string name, string discriminator)
        => name + Separator + discriminator;

    private static (string Name, string Discriminator) SplitAllIndexMember(string member)
    {
        int sep = member.IndexOf(Separator, StringComparison.Ordinal);
        return (member[..sep], member[(sep + 1)..]);
    }

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the index member). An environment outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Discriminator)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context)
    {
        foreach (RedisValue member in await this.database.SetMembersAsync(NameIndexKey(name)).ConfigureAwait(false))
        {
            string discriminator = (string)member!;
            RedisValue value = await this.database.StringGetAsync(BindKey(name, discriminator)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            byte[] json = (byte[])value!;
            using ParsedJsonDocument<Environment> candidate = PersistedJson.ToPooledDocument<Environment>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, discriminator);
            }
        }

        return (null, null);
    }
}