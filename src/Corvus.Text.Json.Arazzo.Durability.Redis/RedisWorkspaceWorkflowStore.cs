// <copyright file="RedisWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working copies
/// persisted as their Corvus.Text.Json <see cref="WorkspaceWorkflow"/> documents so a working copy survives a restart.
/// Each working copy is stored verbatim under a per-copy key composed from its server-minted <c>id</c> alone (the id is
/// globally unique, so it is the whole key — no Name/Tags composite); its etag travels inside the document, so
/// optimistic concurrency is a read-compare-write.
/// </summary>
/// <remarks>
/// <para>Redis has no server-side filtering, so management reads/writes are reach-filtered by the caller's
/// <see cref="AccessContext"/> (§14.2) — applied in memory over the single value for an id (a working copy outside reach
/// is reported as absent, non-disclosing), and per member in keyset order for the list, enumerated through a global
/// index set the way the other Redis stores maintain theirs.</para>
/// <para>Targets a single Redis instance (or a primary): an add touches the per-copy key and one index set, which is not
/// Redis-Cluster slot-safe. The document is carried bytes-to-bytes (#803): values bind from the raw document bytes and
/// round-trip via <see cref="ParsedJsonDocument{T}"/> and the shared pooled serialization, never a per-op detached
/// clone.</para>
/// </remarks>
public sealed class RedisWorkspaceWorkflowStore : IWorkspaceWorkflowStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:wc:";

    // Per-working-copy value key: holds the WorkspaceWorkflow document bytes verbatim, keyed by the server-minted id.
    private const string BindPrefix = Prefix + "bind:";

    // Global index set: members are the working-copy ids, so ListAsync can enumerate every working copy without loading
    // the documents until the page needs them.
    private const string AllIndexKey = Prefix + "all";

    // Write the working copy's document and add its id to the index set, atomically. KEYS: bind key, all index.
    // ARGV: document bytes, id (the all-index member). The id is minted fresh per add, so it never collides.
    private const string AddScript =
        """
        redis.call('SET', KEYS[1], ARGV[1])
        redis.call('SADD', KEYS[2], ARGV[2])
        return 1
        """;

    // Remove the working copy and prune its id from the index set, atomically. KEYS: bind key, all index. ARGV: id.
    private const string DeleteScript =
        """
        redis.call('DEL', KEYS[1])
        redis.call('SREM', KEYS[2], ARGV[1])
        return 1
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisWorkspaceWorkflowStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
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

    /// <summary>Opens a working-copy store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisWorkspaceWorkflowStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisWorkspaceWorkflowStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates a working-copy store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisWorkspaceWorkflowStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisWorkspaceWorkflowStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();

        // The durable backend mints its own opaque id (the reference in-memory store's ids are creation-sequential; the
        // id is opaque to clients either way). Redis orders the index members ordinally in memory, matching the keyset
        // pager's id compare.
        string id = "wc-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), etag);

        RedisKey[] keys =
        [
            BindKey(id),
            AllIndexKey,
        ];
        RedisValue[] argv =
        [
            json,
            id,
        ];

        await this.database.ScriptEvaluateAsync(AddScript, keys, argv).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? json = await this.FindForManagementAsync(id, AccessVerb.Read, context).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : 1;
        (string Id, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = WorkspaceWorkflowContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // Redis has no server-side ordering for a SET, so the keyset scan runs over the all-index members in memory: the
        // members are the small ids (no documents), so sort them into the stable total order (id — globally unique, so
        // the id is the whole order and the tie-breaker is empty), seek past the cursor, then fetch each candidate's
        // document lazily — only up to what the page needs, never the whole table.
        RedisValue[] rawMembers = await this.database.SetMembersAsync(AllIndexKey).ConfigureAwait(false);
        var ids = new string[rawMembers.Length];
        for (int i = 0; i < rawMembers.Length; i++)
        {
            ids[i] = (string)rawMembers[i]!;
        }

        Array.Sort(ids, static (a, b) => string.CompareOrdinal(a, b));

        // Index of the first id strictly past the cursor in id order.
        int start = 0;
        if (hasCursor)
        {
            while (start < ids.Length && string.CompareOrdinal(ids[start], cursor.Id) <= 0)
            {
                start++;
            }
        }

        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            string lastId = string.Empty;
            for (int i = start; i < ids.Length; i++)
            {
                string id = ids[i];
                RedisValue value = await this.database.StringGetAsync(BindKey(id)).ConfigureAwait(false);
                if (value.IsNullOrEmpty)
                {
                    continue;
                }

                byte[] json = (byte[])value!;
                ParsedJsonDocument<WorkspaceWorkflow> cand = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
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
                    lastId = id;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
            }

            return hasMore
                ? WorkspaceWorkflowPage.Create(docs, lastId, string.Empty)
                : WorkspaceWorkflowPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // The id, provenance, and tags are immutable, so the per-copy key is unchanged and the index set needs no
        // maintenance; just overwrite the document bytes verbatim.
        await this.database.StringSetAsync(BindKey(id), json).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            WorkspaceWorkflowSerialization.EnsureEtag(id, expectedEtag, WorkspaceWorkflowSerialization.EtagOf(existing));
        }

        RedisKey[] keys =
        [
            BindKey(id),
            AllIndexKey,
        ];
        RedisValue[] argv =
        [
            id,
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

    private static RedisKey BindKey(string id) => BindPrefix + id;

    // Finds the single working copy with the given id the caller's reach for the verb admits, returning its bytes (the id
    // is the sole key, so a scalar lookup suffices). A working copy outside reach is invisible (non-disclosing).
    private async ValueTask<byte[]?> FindForManagementAsync(string id, AccessVerb verb, AccessContext context)
    {
        RedisValue value = await this.database.StringGetAsync(BindKey(id)).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        byte[] json = (byte[])value!;
        using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? json : null;
    }
}