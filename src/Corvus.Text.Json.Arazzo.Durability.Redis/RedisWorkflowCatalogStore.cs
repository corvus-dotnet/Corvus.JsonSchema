// <copyright file="RedisWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IWorkflowCatalogStore"/>. Each version is a hash holding the projected metadata
/// fields plus the canonical package bytes; a per-base counter assigns the version number atomically, and a
/// global sorted set indexes versions by their (base workflow id, version number) sort key so the catalog can be
/// ordered and keyset-paged client-side — the same authoritative-store-with-index pattern
/// <see cref="RedisWorkflowStateStore"/> uses for runs.
/// </summary>
/// <remarks>
/// Targets a single Redis instance (or a primary): an add touches the per-base counter, the version hash and the
/// shared index set, which is not Redis-Cluster slot-safe. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> (or
/// <see cref="Connect(IConnectionMultiplexer, TimeProvider?)"/>).
/// </remarks>
public sealed class RedisWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string Prefix = "arazzo:catalog:";
    private const string IndexKey = Prefix + "index";
    private const string DocField = "doc";
    private const string PackageField = "package";

    // Reserve the next version number for a base id, atomically. KEYS: per-base counter. Returns the new number.
    private const string ReserveVersionScript = "return redis.call('INCR', KEYS[1])";

    // Store the version hash and add it to the ordering index, atomically. KEYS: version hash, index zset.
    // ARGV: sortKey, then the (field, value) pairs to HSET.
    private const string StoreScript =
        """
        redis.call('ZADD', KEYS[2], 0, ARGV[1])
        local args = {}
        for i = 2, #ARGV do args[#args + 1] = ARGV[i] end
        redis.call('HSET', KEYS[1], unpack(args))
        return 1
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;
    private readonly bool ownsConnection;

    private RedisWorkflowCatalogStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
        this.ownsConnection = ownsConnection;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
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

    /// <summary>Opens a catalog store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisWorkflowCatalogStore> ConnectAsync(
        string configuration,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisWorkflowCatalogStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true, metadataProvider, executorProvider);
    }

    /// <summary>Creates a catalog store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="metadataProvider">An optional provider used to bake schema metadata into the package at add time.</param>
    /// <param name="executorProvider">An optional provider that compiles the workflow executor assembly baked into each added version; <see langword="null"/> to store packages without it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The store.</returns>
    public static RedisWorkflowCatalogStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null, IWorkflowMetadataProvider? metadataProvider = null, IWorkflowExecutorProvider? executorProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        cancellationToken.ThrowIfCancellationRequested();
        return new RedisWorkflowCatalogStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false, metadataProvider, executorProvider);
    }

    /// <inheritdoc/>
    public ValueTask<CatalogVersion> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8.ToArray(), metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HashEntry[] entries = await this.database.HashGetAllAsync(VersionKey(baseWorkflowId, versionNumber)).ConfigureAwait(false);
        return entries.Length == 0 ? null : ReadVersion(entries);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.HashGetAsync(VersionKey(baseWorkflowId, versionNumber), "package").ConfigureAwait(false);
        if (value.IsNull)
        {
            return null;
        }

        byte[] bytes = (byte[])value!;
        return bytes is null ? null : (ReadOnlyMemory<byte>?)bytes;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentName);
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.HashGetAsync(VersionKey(baseWorkflowId, versionNumber), "package").ConfigureAwait(false);
        if (value.IsNull)
        {
            return null;
        }

        byte[] bytes = (byte[])value!;
        return bytes is null ? null : CatalogPackage.GetDocument(bytes, documentName);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        // Redis has no server-side ordering over the metadata, so page over the lexicographically-ordered index
        // sort keys and filter client-side, mirroring the run store's keyset paging.
        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        int limit = query.Limit <= 0 ? 100 : query.Limit;

        RedisValue[] sortKeys = await this.database.SortedSetRangeByRankAsync(IndexKey).ConfigureAwait(false);
        Array.Sort(sortKeys, static (a, b) => string.CompareOrdinal((string)a!, (string)b!));

        var matches = new List<CatalogVersion>();
        string? continuation = null;
        foreach (RedisValue member in sortKeys)
        {
            string sortKey = (string)member!;
            if (after is not null && string.CompareOrdinal(sortKey, after) <= 0)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            HashEntry[] entries = await this.database.HashGetAllAsync(VersionKey(sortKey)).ConfigureAwait(false);
            if (entries.Length == 0)
            {
                continue;
            }

            CatalogVersion candidate = ReadVersion(entries);
            if (!Matches(candidate, query))
            {
                continue;
            }

            if (matches.Count == limit)
            {
                continuation = WorkflowContinuationToken.Encode(SortKey(matches[^1].Ref.BaseWorkflowId, matches[^1].Ref.VersionNumber));
                break;
            }

            matches.Add(candidate);
        }

        return new CatalogPage(matches, continuation);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        RedisKey key = VersionKey(baseWorkflowId, versionNumber);
        HashEntry[] entries = await this.database.HashGetAllAsync(key).ConfigureAwait(false);
        if (entries.Length == 0)
        {
            return null;
        }

        CatalogVersion cur = ReadVersion(entries);
        CatalogStatus currentStatus = cur.StatusValue;
        CatalogStatus status = patch.Status ?? currentStatus;
        bool newlyObsolete = status == CatalogStatus.Obsolete && currentStatus != CatalogStatus.Obsolete;
        bool reactivated = status == CatalogStatus.Active && currentStatus == CatalogStatus.Obsolete;

        CatalogOwner owner = patch.Owner ?? cur.OwnerValue;
        TagSet tags = patch.Tags ?? cur.TagsValue;
        string? obsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : cur.ObsoletedByOrNull;
        DateTimeOffset? obsoletedAt = newlyObsolete ? now : reactivated ? null : cur.ObsoletedAtValue;

        CatalogVersion updated = CatalogVersion.Create(
            baseWorkflowId: cur.Ref.BaseWorkflowId,
            versionNumber: cur.Ref.VersionNumber,
            workflowId: (string)cur.WorkflowId,
            title: (string)cur.Title,
            description: cur.DescriptionOrNull,
            status: status,
            tags: tags,
            owner: owner,
            sources: cur.SourcesValue,
            hash: (string)cur.Hash,
            createdBy: (string)cur.CreatedBy,
            createdAt: cur.CreatedAtValue,
            lastUpdatedBy: patch.UpdatedBy,
            lastUpdatedAt: now,
            obsoletedBy: obsoletedBy,
            obsoletedAt: obsoletedAt,
            runnable: (bool)cur.Runnable);

        await this.database.HashSetAsync(key, DocField, updated.ToJsonBytes()).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool existed = await this.database.KeyDeleteAsync(VersionKey(baseWorkflowId, versionNumber)).ConfigureAwait(false);
        await this.database.SortedSetRemoveAsync(IndexKey, SortKey(baseWorkflowId, versionNumber)).ConfigureAwait(false);
        return existed;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue[] sortKeys = await this.database.SortedSetRangeByRankAsync(IndexKey).ConfigureAwait(false);
        var refs = new List<CatalogVersionRef>();
        foreach (RedisValue member in sortKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HashEntry[] entries = await this.database.HashGetAllAsync(VersionKey((string)member!)).ConfigureAwait(false);
            if (entries.Length == 0)
            {
                continue;
            }

            CatalogVersion version = ReadVersion(entries);
            if (version.StatusValue == CatalogStatus.Obsolete)
            {
                refs.Add(version.Ref);
            }
        }

        return refs;
    }

    /// <inheritdoc/>
    public async ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (CatalogVersionRef reference in versions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.database.KeyDeleteAsync(VersionKey(reference.BaseWorkflowId, reference.VersionNumber)).ConfigureAwait(false);
            await this.database.SortedSetRemoveAsync(IndexKey, SortKey(reference.BaseWorkflowId, reference.VersionNumber)).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RedisKey VersionKey(string baseWorkflowId, int versionNumber)
        => VersionKey(SortKey(baseWorkflowId, versionNumber));

    private static RedisKey VersionKey(string sortKey)
        => Prefix + "ver:" + sortKey;

    private static RedisKey CounterKey(string baseWorkflowId)
        => Prefix + "counter:" + baseWorkflowId;

    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    private static bool Matches(CatalogVersion version, CatalogQuery query)
    {
        if (query.BaseWorkflowId is { } baseId && version.Ref.BaseWorkflowId != baseId)
        {
            return false;
        }

        if (query.WorkflowIdPrefix is { Length: > 0 } prefix
            && !((string)version.WorkflowId).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.Status is { } status && version.StatusValue != status)
        {
            return false;
        }

        if (query.Text is { Length: > 0 } text
            && ((string)version.Title).IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0
            && (version.DescriptionOrNull is null || version.DescriptionOrNull.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0))
        {
            return false;
        }

        CatalogOwner owner = version.OwnerValue;
        if (query.Owner is { Length: > 0 } ownerQuery
            && owner.Name.IndexOf(ownerQuery, StringComparison.OrdinalIgnoreCase) < 0
            && owner.Email.IndexOf(ownerQuery, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (!query.Tags.AllContainedIn(version.TagsValue))
        {
            return false;
        }

        // Row-security reach (§14.2): Redis has no server-side filtering, so apply the reach filter in process
        // over the version's persisted security tags — the only correct option for a key/value backend.
        if (query.Security is { } security && !security.IsSatisfiedBy(version.SecurityTagsValue.ToList()))
        {
            return false;
        }

        return true;
    }

    private static CatalogVersion ReadVersion(HashEntry[] entries)
    {
        foreach (HashEntry entry in entries)
        {
            if (entry.Name == DocField)
            {
                return CatalogVersion.FromJson((byte[])entry.Value!);
            }
        }

        throw new InvalidOperationException("The catalog version hash is missing its document field.");
    }

    private async ValueTask<CatalogVersion> AddCoreAsync(string baseWorkflowId, byte[] packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Reserve the next version number atomically via a per-base INCR counter.
        RedisResult reserved = await this.database.ScriptEvaluateAsync(ReserveVersionScript, [CounterKey(baseWorkflowId)]).ConfigureAwait(false);
        int versionNumber = (int)(long)reserved;

        CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);
        TagSet tags = metadata.Tags;
        SecurityTagSet securityTags = metadata.SecurityTags;

        CatalogVersion version = CatalogVersion.Create(
            baseWorkflowId: baseWorkflowId,
            versionNumber: versionNumber,
            workflowId: projection.WorkflowId,
            title: projection.Title,
            description: projection.Description,
            status: CatalogStatus.Active,
            tags: tags,
            owner: metadata.Owner,
            sources: SourceSet.FromSources(projection.Sources),
            hash: projection.Hash,
            createdBy: metadata.CreatedBy,
            createdAt: now,
            runnable: projection.HasExecutor,
            securityTags: securityTags);

        // Store the CatalogVersion JSON document verbatim in the "doc" field and the package alongside; the
        // sorted-set index orders by sortKey for keyset paging.
        RedisValue[] argv =
        [
            SortKey(baseWorkflowId, versionNumber),
            DocField, version.ToJsonBytes(),
            PackageField, projection.CanonicalPackage.ToArray(),
        ];

        await this.database.ScriptEvaluateAsync(StoreScript, [VersionKey(sortKey: SortKey(baseWorkflowId, versionNumber)), IndexKey], argv).ConfigureAwait(false);
        return version;
    }
}