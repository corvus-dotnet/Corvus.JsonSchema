// <copyright file="RedisWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
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
/// <see cref="ConnectAsync(string, TimeProvider?, IWorkflowMetadataProvider?, IWorkflowExecutorProvider?, CancellationToken)"/> (or
/// <see cref="Connect(IConnectionMultiplexer, TimeProvider?, IWorkflowMetadataProvider?, IWorkflowExecutorProvider?, CancellationToken)"/>).
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
    /// <param name="metadataProvider">An optional provider used to bake schema metadata into the package at add time.</param>
    /// <param name="executorProvider">An optional provider that compiles the workflow executor assembly baked into each added version; <see langword="null"/> to store packages without it.</param>
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
    public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8, metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.HashGetAsync(VersionKey(baseWorkflowId, versionNumber), DocField).ConfigureAwait(false);
        if (value.IsNull)
        {
            return null;
        }

        byte[] bytes = (byte[])value!;

        // Realize a pooled, disposable document over the persisted bytes (the caller owns it) — never the
        // standalone CatalogVersion.FromJson value.
        return bytes is null ? null : ParsedJsonDocument<CatalogVersion>.Parse(bytes);
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
        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        int limit = query.Limit <= 0 ? 100 : query.Limit;

        RedisValue[] sortKeys = await this.database.SortedSetRangeByRankAsync(IndexKey).ConfigureAwait(false);
        Array.Sort(sortKeys, static (a, b) => string.CompareOrdinal((string)a!, (string)b!));

        // The page is a pooled batch of disposable version documents (the caller disposes the page). Each candidate is
        // parsed once; matches are kept in the batch, non-matches and the look-ahead row are disposed immediately.
        var matches = new PooledDocumentList<CatalogVersion>(limit);
        string? nextSortKey = null;
        try
        {
            foreach (RedisValue member in sortKeys)
            {
                string sortKey = (string)member!;
                if (after is not null && string.CompareOrdinal(sortKey, after) <= 0)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                RedisValue value = await this.database.HashGetAsync(VersionKey(sortKey), DocField).ConfigureAwait(false);
                if (value.IsNull)
                {
                    continue;
                }

                ParsedJsonDocument<CatalogVersion> candidate = ParsedJsonDocument<CatalogVersion>.Parse((byte[])value!);
                if (!Matches(candidate.RootElement, query))
                {
                    candidate.Dispose();
                    continue;
                }

                if (matches.Count == limit)
                {
                    // There is at least one more matching row beyond this page.
                    CatalogVersionRef last = matches[matches.Count - 1].Ref;
                    nextSortKey = SortKey(last.BaseWorkflowId, last.VersionNumber);
                    candidate.Dispose();
                    break;
                }

                matches.Add(candidate);
            }
        }
        catch
        {
            matches.Dispose();
            throw;
        }

        return nextSortKey is not null ? CatalogPage.Create(matches, nextSortKey) : CatalogPage.Create(matches);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        RedisKey key = VersionKey(baseWorkflowId, versionNumber);
        RedisValue value = await this.database.HashGetAsync(key, DocField).ConfigureAwait(false);
        if (value.IsNull)
        {
            return null;
        }

        byte[] updatedDoc;
        using (ParsedJsonDocument<CatalogVersion> currentDoc = ParsedJsonDocument<CatalogVersion>.Parse((byte[])value!))
        {
            // Patch only the changed governance fields through the mutable builder; every other field — including the
            // security tags — is carried bytes-to-bytes (no per-field string realisation, and no longer dropping the
            // securityTags the field-by-field rebuild used to strip).
            updatedDoc = CatalogVersion.CreatePatchedBytes(currentDoc.RootElement, patch, now);
        }

        await this.database.HashSetAsync(key, DocField, updatedDoc).ConfigureAwait(false);
        return ParsedJsonDocument<CatalogVersion>.Parse(updatedDoc);
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
            RedisValue value = await this.database.HashGetAsync(VersionKey((string)member!), DocField).ConfigureAwait(false);
            if (value.IsNull)
            {
                continue;
            }

            using ParsedJsonDocument<CatalogVersion> doc = ParsedJsonDocument<CatalogVersion>.Parse((byte[])value!);
            if (doc.RootElement.StatusValue == CatalogStatus.Obsolete)
            {
                // CatalogVersionRef materializes its strings, so it outlives the document.
                refs.Add(doc.RootElement.Ref);
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

    private static bool Matches(in CatalogVersion version, CatalogQuery query)
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
        if (query.Security is { } security && !security.IsSatisfiedBy(version.SecurityTagsValue))
        {
            return false;
        }

        return true;
    }

    private async ValueTask<ParsedJsonDocument<CatalogVersion>> AddCoreAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Reserve the next version number atomically via a per-base INCR counter.
        RedisResult reserved = await this.database.ScriptEvaluateAsync(ReserveVersionScript, [CounterKey(baseWorkflowId)]).ConfigureAwait(false);
        int versionNumber = (int)(long)reserved;

        CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);

        // Build the version document as its durable BYTES (not a standalone CatalogVersion value); store those verbatim
        // in the "doc" field, then realize a pooled, disposable document over the owned bytes for the caller-owned return.
        byte[] versionDoc = CatalogVersion.CreateBytes(
            baseWorkflowId: baseWorkflowId,
            versionNumber: versionNumber,
            workflowId: projection.WorkflowId,
            title: projection.Title,
            description: projection.Description,
            status: CatalogStatus.Active,
            tags: metadata.Tags,
            owner: metadata.Owner,
            sources: SourceSet.FromSources(projection.Sources),
            hash: projection.Hash,
            createdBy: metadata.CreatedBy,
            createdAt: now,
            runnable: projection.HasExecutor,
            securityTags: metadata.SecurityTags);

        // Store the CatalogVersion JSON document verbatim in the "doc" field and the package alongside; the
        // sorted-set index orders by sortKey for keyset paging.
        RedisValue[] argv =
        [
            SortKey(baseWorkflowId, versionNumber),
            DocField, versionDoc,
            PackageField, projection.CanonicalPackage,
        ];

        await this.database.ScriptEvaluateAsync(StoreScript, [VersionKey(sortKey: SortKey(baseWorkflowId, versionNumber)), IndexKey], argv).ConfigureAwait(false);
        return ParsedJsonDocument<CatalogVersion>.Parse(versionDoc);
    }
}