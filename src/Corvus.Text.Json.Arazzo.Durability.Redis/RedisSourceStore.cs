// <copyright file="RedisSourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="ISourceStore"/> (design §7.6): registered sources persisted as their
/// Corvus.Text.Json <see cref="RegisteredSource"/> documents. Each source is stored verbatim under a per-source key
/// composed from (Name and a discriminator over its immutable management tags) so reach-isolated sources that share
/// a name coexist; its etag travels inside the document, so optimistic concurrency is a read-compare-write.
/// </summary>
/// <remarks>
/// <para>Redis has no server-side filtering, so management reads/writes are reach-filtered by the caller's
/// <see cref="AccessContext"/> (§14.2) — applied in memory over the small candidate set for a name, enumerated through
/// index sets the way the other Redis stores maintain theirs.</para>
/// <para>Targets a single Redis instance (or a primary): an add touches the per-source key and two index sets,
/// which is not Redis-Cluster slot-safe.</para>
/// </remarks>
public sealed class RedisSourceStore : ISourceStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:source:";

    // Per-source value key: holds the RegisteredSource document bytes verbatim.
    private const string BindPrefix = Prefix + "bind:";

    // Index set per name: members are the source discriminators, so the management path can enumerate the small
    // candidate set for a name.
    private const string NameIndexPrefix = Prefix + "name:";

    // Global index set: members are the full identity tuples "{name}{discriminator}", so ListAsync can enumerate every
    // source.
    private const string AllIndexKey = Prefix + "all";

    // The control character SourceCredentialKey uses to separate canonical tag sets; reused here to join identity parts
    // into keys/members because it cannot appear in a name or tag.
    private const char Separator = '\u0001';

    // Write the source only if its per-source key does not already exist, and maintain the two index sets, all
    // atomically. KEYS: bind key, name index, all index. ARGV: document bytes, discriminator, all-index member.
    // Returns 1 on success, 0 if a source with that identity already exists.
    private const string AddScript =
        """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
        redis.call('SET', KEYS[1], ARGV[1])
        redis.call('SADD', KEYS[2], ARGV[2])
        redis.call('SADD', KEYS[3], ARGV[3])
        return 1
        """;

    // Remove the source and prune it from the two index sets, atomically. KEYS: bind key, name index, all index.
    // ARGV: discriminator, all-index member.
    private const string DeleteScript =
        """
        redis.call('DEL', KEYS[1])
        redis.call('SREM', KEYS[2], ARGV[1])
        redis.call('SREM', KEYS[3], ARGV[2])
        return 1
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisSourceStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
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

    /// <summary>Opens a source store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisSourceStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisSourceStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates a source store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisSourceStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisSourceStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>> AddAsync(RegisteredSource draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string discriminator = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);

        RedisKey[] keys =
        [
            BindKey(draft.NameValue, discriminator),
            NameIndexKey(draft.NameValue),
            AllIndexKey,
        ];
        RedisValue[] argv =
        [
            json,
            discriminator,
            AllIndexMember(draft.NameValue, discriminator),
        ];

        RedisResult result = await this.database.ScriptEvaluateAsync(AddScript, keys, argv).ConfigureAwait(false);
        if ((long)result == 0)
        {
            throw new InvalidOperationException($"A source named '{draft.NameValue}' with those security tags already exists.");
        }

        return PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<SourcePage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // Redis has no server-side ordering for a SET, so the keyset scan runs over the all-index members in memory: the
        // members are the small identity tuples (no documents), so sort them into the stable total order
        // (name, discriminator), seek past the cursor, then fetch each candidate's document lazily — only up to what the
        // page needs, never the whole table.
        RedisValue[] rawMembers = await this.database.SetMembersAsync(AllIndexKey).ConfigureAwait(false);
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

        var docs = new PooledDocumentList<RegisteredSource>(pageSize);
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
                ParsedJsonDocument<RegisteredSource> cand = PersistedJson.ToPooledDocument<RegisteredSource>(json);
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

            return hasMore ? SourcePage.Create(docs, lastName, lastDisc) : SourcePage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> UpdateAsync(string name, RegisteredSource draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
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

        byte[] json = SourceSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // The identity (name, security tags) is immutable, so the per-source key is unchanged and the index sets
        // need no maintenance; just overwrite the document bytes verbatim.
        await this.database.StringSetAsync(BindKey(name, discriminator!), json).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<RegisteredSource>(json);
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
            SourceSerialization.EnsureEtag(name, expectedEtag, SourceSerialization.EtagOf(existing));
        }

        RedisKey[] keys =
        [
            BindKey(name, discriminator!),
            NameIndexKey(name),
            AllIndexKey,
        ];
        RedisValue[] argv =
        [
            discriminator!,
            AllIndexMember(name, discriminator!),
        ];

        await this.database.ScriptEvaluateAsync(DeleteScript, keys, argv).ConfigureAwait(false);
        return true;
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

    // Finds the single source named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the index member). A source outside reach is invisible (non-disclosing).
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
            using ParsedJsonDocument<RegisteredSource> candidate = PersistedJson.ToPooledDocument<RegisteredSource>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, discriminator);
            }
        }

        return (null, null);
    }
}