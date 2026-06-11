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
public sealed class RedisWorkflowCatalogStore : IWorkflowCatalogStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:catalog:";
    private const string IndexKey = Prefix + "index";

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
    private readonly bool ownsConnection;

    private RedisWorkflowCatalogStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection, IWorkflowMetadataProvider? metadataProvider)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
        this.ownsConnection = ownsConnection;
        this.metadataProvider = metadataProvider;
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisWorkflowCatalogStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true, metadataProvider);
    }

    /// <summary>Creates a catalog store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="metadataProvider">An optional provider used to bake schema metadata into the package at add time.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The store.</returns>
    public static RedisWorkflowCatalogStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null, IWorkflowMetadataProvider? metadataProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        cancellationToken.ThrowIfCancellationRequested();
        return new RedisWorkflowCatalogStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false, metadataProvider);
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
                continuation = WorkflowContinuationToken.Encode(SortKey(matches[^1].BaseWorkflowId, matches[^1].VersionNumber));
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

        CatalogVersion current = ReadVersion(entries);
        CatalogStatus status = patch.Status ?? current.Status;
        bool newlyObsolete = status == CatalogStatus.Obsolete && current.Status != CatalogStatus.Obsolete;
        bool reactivated = status == CatalogStatus.Active && current.Status == CatalogStatus.Obsolete;

        CatalogOwner owner = patch.Owner ?? current.Owner;
        IReadOnlyList<string> tags = patch.Tags is { } t ? [.. t] : current.Tags;
        string? obsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : current.ObsoletedBy;
        DateTimeOffset? obsoletedAt = newlyObsolete ? now : reactivated ? null : current.ObsoletedAt;

        CatalogVersion updated = current with
        {
            Owner = owner,
            Tags = tags,
            Status = status,
            LastUpdatedBy = patch.UpdatedBy,
            LastUpdatedAt = now,
            ObsoletedBy = obsoletedBy,
            ObsoletedAt = obsoletedAt,
        };

        await this.database.HashSetAsync(
            key,
            [
                new HashEntry("status", updated.Status.ToString()),
                new HashEntry("tags", EncodeTags(updated.Tags)),
                new HashEntry("owner_name", updated.Owner.Name),
                new HashEntry("owner_email", updated.Owner.Email),
                new HashEntry("owner_team", updated.Owner.Team ?? string.Empty),
                new HashEntry("owner_url", updated.Owner.Url ?? string.Empty),
                new HashEntry("last_updated_by", updated.LastUpdatedBy ?? string.Empty),
                new HashEntry("last_updated_at", now.ToUnixTimeMilliseconds()),
                new HashEntry("obsoleted_by", obsoletedBy ?? string.Empty),
                new HashEntry("obsoleted_at", obsoletedAt is { } oa ? oa.ToUnixTimeMilliseconds() : string.Empty),
            ]).ConfigureAwait(false);

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
            if (version.Status == CatalogStatus.Obsolete)
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

    private static string EncodeTags(IReadOnlyList<string> tags)
        => tags is { Count: > 0 } ? "\u001F" + string.Join('\u001F', tags) + "\u001F" : string.Empty;

    private static IReadOnlyList<string> DecodeTags(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return [];
        }

        string[] parts = encoded.Trim('\u001F').Split('\u001F', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? [] : parts;
    }

    private static string EncodeSources(IReadOnlyList<CatalogSourceRef> sources)
    {
        if (sources.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < sources.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\u001E');
            }

            builder.Append(sources[i].Name);
            if (sources[i].Type is { } type)
            {
                builder.Append('\u001F').Append(type);
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<CatalogSourceRef> DecodeSources(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return [];
        }

        string[] records = encoded.Split('\u001E', StringSplitOptions.RemoveEmptyEntries);
        var sources = new List<CatalogSourceRef>(records.Length);
        foreach (string record in records)
        {
            int sep = record.IndexOf('\u001F', StringComparison.Ordinal);
            sources.Add(sep < 0
                ? new CatalogSourceRef(record, null)
                : new CatalogSourceRef(record[..sep], record[(sep + 1)..]));
        }

        return sources;
    }

    private static bool Matches(CatalogVersion version, CatalogQuery query)
    {
        if (query.BaseWorkflowId is { } baseId && version.BaseWorkflowId != baseId)
        {
            return false;
        }

        if (query.WorkflowIdPrefix is { Length: > 0 } prefix
            && !version.WorkflowId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.Status is { } status && version.Status != status)
        {
            return false;
        }

        if (query.Text is { Length: > 0 } text
            && version.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0
            && (version.Description is null || version.Description.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0))
        {
            return false;
        }

        if (query.Owner is { Length: > 0 } owner
            && version.Owner.Name.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0
            && version.Owner.Email.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (query.Tags is { Count: > 0 } queryTags && !queryTags.All(version.Tags.Contains))
        {
            return false;
        }

        return true;
    }

    private static CatalogVersion ReadVersion(HashEntry[] entries)
    {
        var fields = new Dictionary<string, RedisValue>(entries.Length, StringComparer.Ordinal);
        foreach (HashEntry entry in entries)
        {
            fields[(string)entry.Name!] = entry.Value;
        }

        string? OptString(string field)
            => fields.TryGetValue(field, out RedisValue v) && !v.IsNull && ((string)v!).Length > 0 ? (string)v! : null;

        DateTimeOffset? OptTime(string field)
            => OptString(field) is { } s ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(s, CultureInfo.InvariantCulture)) : null;

        return new CatalogVersion(
            BaseWorkflowId: (string)fields["base_workflow_id"]!,
            VersionNumber: (int)(long)fields["version_number"],
            WorkflowId: (string)fields["workflow_id"]!,
            Title: (string)fields["title"]!,
            Description: OptString("description"),
            Status: Enum.Parse<CatalogStatus>((string)fields["status"]!),
            Tags: DecodeTags(OptString("tags")),
            Owner: new CatalogOwner(
                (string)fields["owner_name"]!,
                (string)fields["owner_email"]!,
                OptString("owner_team"),
                OptString("owner_url")),
            Sources: DecodeSources(OptString("sources")),
            Hash: (string)fields["hash"]!,
            CreatedBy: (string)fields["created_by"]!,
            CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds((long)fields["created_at"]),
            LastUpdatedBy: OptString("last_updated_by"),
            LastUpdatedAt: OptTime("last_updated_at"),
            ObsoletedBy: OptString("obsoleted_by"),
            ObsoletedAt: OptTime("obsoleted_at"));
    }

    private async ValueTask<CatalogVersion> AddCoreAsync(string baseWorkflowId, byte[] packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Reserve the next version number atomically via a per-base INCR counter.
        RedisResult reserved = await this.database.ScriptEvaluateAsync(ReserveVersionScript, [CounterKey(baseWorkflowId)]).ConfigureAwait(false);
        int versionNumber = (int)(long)reserved;

        CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider);
        IReadOnlyList<string> tags = metadata.Tags is { Count: > 0 } t ? [.. t] : [];
        var version = new CatalogVersion(
            BaseWorkflowId: baseWorkflowId,
            VersionNumber: versionNumber,
            WorkflowId: projection.WorkflowId,
            Title: projection.Title,
            Description: projection.Description,
            Status: CatalogStatus.Active,
            Tags: tags,
            Owner: metadata.Owner,
            Sources: projection.Sources,
            Hash: projection.Hash,
            CreatedBy: metadata.CreatedBy,
            CreatedAt: now);

        RedisValue[] argv =
        [
            SortKey(baseWorkflowId, versionNumber),
            "base_workflow_id", version.BaseWorkflowId,
            "version_number", version.VersionNumber,
            "workflow_id", version.WorkflowId,
            "title", version.Title,
            "description", version.Description ?? string.Empty,
            "status", version.Status.ToString(),
            "tags", EncodeTags(version.Tags),
            "owner_name", version.Owner.Name,
            "owner_email", version.Owner.Email,
            "owner_team", version.Owner.Team ?? string.Empty,
            "owner_url", version.Owner.Url ?? string.Empty,
            "sources", EncodeSources(version.Sources),
            "hash", version.Hash,
            "created_by", version.CreatedBy,
            "created_at", version.CreatedAt.ToUnixTimeMilliseconds(),
            "package", projection.CanonicalPackage.ToArray(),
        ];

        await this.database.ScriptEvaluateAsync(StoreScript, [VersionKey(sortKey: SortKey(baseWorkflowId, versionNumber)), IndexKey], argv).ConfigureAwait(false);
        return version;
    }
}