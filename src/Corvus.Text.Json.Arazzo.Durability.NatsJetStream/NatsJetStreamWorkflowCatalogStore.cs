// <copyright file="NatsJetStreamWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream key/value-backed <see cref="IWorkflowCatalogStore"/>. Each version's value is an envelope —
/// a small JSON metadata header followed by the canonical package bytes — held under a single KV key per version
/// (<c>{base64url(baseWorkflowId)}.{versionNumber}</c>); the KV entry's native revision provides the
/// optimistic-create concurrency the version-number assignment relies on, exactly as the run store
/// (<see cref="NatsJetStreamWorkflowStateStore"/>) uses revisions for its save concurrency.
/// </summary>
/// <remarks>
/// Search/visibility queries scan the bucket's keys and filter on the decoded metadata header, mirroring the run
/// store's scan-and-page approach. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with
/// <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class NatsJetStreamWorkflowCatalogStore : IWorkflowCatalogStore, IAsyncDisposable
{
    private const string CatalogBucket = "arazzo_catalog";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore catalog;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;

    private NatsJetStreamWorkflowCatalogStore(NatsConnection? ownedConnection, INatsKVStore catalog, TimeProvider timeProvider, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider)
    {
        this.ownedConnection = ownedConnection;
        this.catalog = catalog;
        this.timeProvider = timeProvider;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
    }

    /// <summary>
    /// Provisions the catalog's key/value bucket. Creating a KV bucket creates a JetStream stream, which
    /// requires stream-management permissions, so run this once at deploy/migration time, separately from the
    /// least-privileged account used to <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> the
    /// store for operation (which needs only get/put/delete on the bucket's subjects).
    /// </summary>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(CatalogBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the catalog store for operation, binding to its already-provisioned key/value bucket.</summary>
    /// <remarks>
    /// This creates no streams/buckets, so it is safe to use a least-privileged account granted only
    /// get/put/delete on the bucket's subjects. Call <see cref="PrepareAsync(string, CancellationToken)"/> once
    /// beforehand — with a stream-management account — to create the bucket.
    /// </remarks>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamWorkflowCatalogStore> ConnectAsync(
        string url,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore catalog = await kv.GetStoreAsync(CatalogBucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamWorkflowCatalogStore(connection, catalog, timeProvider ?? TimeProvider.System, metadataProvider, executorProvider);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Provisions the catalog's key/value bucket over a caller-supplied connection.</summary>
    /// <remarks>
    /// Supply a connection the caller configured (for example with a creds file, nkey, or token) so
    /// provisioning runs under a deliberate, stream-management-capable account. The caller retains ownership.
    /// </remarks>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(CatalogBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the catalog store over a caller-supplied connection (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a connection the caller configured — for example with a least-privileged operational account
    /// (get/put/delete on the bucket's subjects) — so the store runs under a least-privileged principal. This
    /// creates no buckets; call <see cref="PrepareAsync(INatsConnection, CancellationToken)"/> once beforehand.
    /// </remarks>
    /// <param name="connection">A NATS connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamWorkflowCatalogStore> ConnectAsync(
        INatsConnection connection,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore catalog = await kv.GetStoreAsync(CatalogBucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamWorkflowCatalogStore(ownedConnection: null, catalog, timeProvider ?? TimeProvider.System, metadataProvider, executorProvider);
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
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(baseWorkflowId, versionNumber), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } value } ? Envelope.DecodeMetadata(value) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(baseWorkflowId, versionNumber), cancellationToken).ConfigureAwait(false);
        byte[]? bytes = entry is { Value: { } value } ? Envelope.DecodePackage(value) : null;
        return bytes is null ? null : (ReadOnlyMemory<byte>?)bytes;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(documentName);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(baseWorkflowId, versionNumber), cancellationToken).ConfigureAwait(false);
        byte[]? package = entry is { Value: { } value } ? Envelope.DecodePackage(value) : null;
        return package is null ? null : CatalogPackage.GetDocument(package, documentName);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        int limit = query.Limit <= 0 ? 100 : query.Limit;

        // The KV bucket has no server-side ordering or filtering, so collect matches, sort by the composite
        // sort key, and keyset-page here — mirroring how the run store answers visibility queries.
        var matches = new List<(string SortKey, CatalogVersion Version)>();
        await foreach (CatalogVersion candidate in this.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            string sortKey = SortKey(candidate.Ref.BaseWorkflowId, candidate.Ref.VersionNumber);
            if (after is not null && string.CompareOrdinal(sortKey, after) <= 0)
            {
                continue;
            }

            if (Matches(candidate, query))
            {
                matches.Add((sortKey, candidate));
            }
        }

        matches.Sort(static (a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));

        string? continuation = null;
        if (matches.Count > limit)
        {
            matches = matches.GetRange(0, limit);
            continuation = WorkflowContinuationToken.Encode(matches[^1].SortKey);
        }

        return new CatalogPage(matches.ConvertAll(static m => m.Version), continuation);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        string key = Key(baseWorkflowId, versionNumber);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } value })
        {
            return null;
        }

        CatalogVersion? current = Envelope.DecodeMetadata(value);
        byte[] package = Envelope.DecodePackage(value);

        if (current is not { } cur)
        {
            return null;
        }

        CatalogStatus status = patch.Status ?? cur.StatusValue;
        bool newlyObsolete = status == CatalogStatus.Obsolete && cur.StatusValue != CatalogStatus.Obsolete;
        bool reactivated = status == CatalogStatus.Active && cur.StatusValue == CatalogStatus.Obsolete;

        CatalogVersionRef reference = cur.Ref;
        CatalogVersion updated = CatalogVersion.Create(
            baseWorkflowId: reference.BaseWorkflowId,
            versionNumber: reference.VersionNumber,
            workflowId: reference.WorkflowId,
            title: (string)cur.Title,
            description: cur.DescriptionOrNull,
            status: status,
            tags: patch.Tags is { } tags ? [.. tags] : cur.TagsValue,
            owner: patch.Owner ?? cur.OwnerValue,
            sources: cur.SourcesValue,
            hash: (string)cur.Hash,
            createdBy: (string)cur.CreatedBy,
            createdAt: cur.CreatedAtValue,
            lastUpdatedBy: patch.UpdatedBy,
            lastUpdatedAt: now,
            obsoletedBy: newlyObsolete ? patch.UpdatedBy : reactivated ? null : cur.ObsoletedByOrNull,
            obsoletedAt: newlyObsolete ? now : reactivated ? null : cur.ObsoletedAtValue,
            runnable: (bool)cur.Runnable);

        await this.catalog.PutAsync(key, Envelope.Encode(updated, package), cancellationToken: cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        string key = Key(baseWorkflowId, versionNumber);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: not null })
        {
            return false;
        }

        await this.PurgeAsync(key, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        var refs = new List<CatalogVersionRef>();
        await foreach (CatalogVersion version in this.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
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
        foreach (CatalogVersionRef reference in versions)
        {
            await this.PurgeAsync(Key(reference.BaseWorkflowId, reference.VersionNumber), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string Key(string baseWorkflowId, int versionNumber)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(baseWorkflowId))}.{versionNumber}");

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

        if (query.Owner is { Length: > 0 } owner
            && version.OwnerValue.Name.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0
            && version.OwnerValue.Email.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (query.Tags is { Count: > 0 } queryTags && !queryTags.All(version.TagsValue.Contains))
        {
            return false;
        }

        return true;
    }

    private async ValueTask<CatalogVersion> AddCoreAsync(string baseWorkflowId, byte[] packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        IReadOnlyList<string> tags = metadata.Tags is { Count: > 0 } t ? [.. t] : [];

        // Assign the next version number safely: compute the current max for the base id by scanning the bucket,
        // then optimistically Create the new key. A concurrent add that grabbed the same number makes Create
        // fail (the key already exists), so recompute and retry — the same revision-backed concurrency the run
        // store relies on for its saves.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int versionNumber = await this.MaxVersionAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false) + 1;
            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);
            CatalogVersion version = CatalogVersion.Create(
                baseWorkflowId: baseWorkflowId,
                versionNumber: versionNumber,
                workflowId: projection.WorkflowId,
                title: projection.Title,
                description: projection.Description,
                status: CatalogStatus.Active,
                tags: tags,
                owner: metadata.Owner,
                sources: projection.Sources,
                hash: projection.Hash,
                createdBy: metadata.CreatedBy,
                createdAt: now,
                runnable: projection.HasExecutor);

            byte[] value = Envelope.Encode(version, projection.CanonicalPackage.Span);
            try
            {
                await this.catalog.CreateAsync(Key(baseWorkflowId, versionNumber), value, cancellationToken: cancellationToken).ConfigureAwait(false);
                return version;
            }
            catch (NatsKVException)
            {
                // Another writer claimed this version number concurrently; recompute and retry.
            }
        }
    }

    private async ValueTask<int> MaxVersionAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        int max = 0;
        await foreach (CatalogVersion version in this.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            CatalogVersionRef reference = version.Ref;
            if (reference.BaseWorkflowId == baseWorkflowId && reference.VersionNumber > max)
            {
                max = reference.VersionNumber;
            }
        }

        return max;
    }

    private async IAsyncEnumerable<CatalogVersion> ScanAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (string key in this.catalog.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } value })
            {
                yield return Envelope.DecodeMetadata(value);
            }
        }
    }

    private async ValueTask<NatsKVEntry<byte[]>?> TryGetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await this.catalog.GetEntryAsync<byte[]>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
            return null;
        }
        catch (NatsKVKeyDeletedException)
        {
            return null;
        }
    }

    private async ValueTask PurgeAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await this.catalog.PurgeAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }

    private static class Envelope
    {
        public static byte[] Encode(CatalogVersion version, ReadOnlySpan<byte> package)
        {
            var headerBuffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(headerBuffer))
            {
                CatalogVersionRef reference = version.Ref;
                CatalogOwner owner = version.OwnerValue;

                writer.WriteStartObject();
                writer.WriteString("baseWorkflowId", reference.BaseWorkflowId);
                writer.WriteNumber("versionNumber", reference.VersionNumber);
                writer.WriteString("workflowId", reference.WorkflowId);
                writer.WriteString("title", (string)version.Title);
                if (version.DescriptionOrNull is { } description)
                {
                    writer.WriteString("description", description);
                }

                writer.WriteString("status", version.StatusValue.ToString());

                writer.WriteStartArray("tags");
                foreach (string tag in version.TagsValue)
                {
                    writer.WriteStringValue(tag);
                }

                writer.WriteEndArray();

                writer.WriteStartObject("owner");
                writer.WriteString("name", owner.Name);
                writer.WriteString("email", owner.Email);
                if (owner.Team is { } team)
                {
                    writer.WriteString("team", team);
                }

                if (owner.Url is { } url)
                {
                    writer.WriteString("url", url);
                }

                writer.WriteEndObject();

                writer.WriteStartArray("sources");
                foreach (CatalogSourceRef source in version.SourcesValue)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", source.Name);
                    if (source.Type is { } type)
                    {
                        writer.WriteString("type", type);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();

                writer.WriteString("hash", (string)version.Hash);
                writer.WriteBoolean("runnable", (bool)version.Runnable);
                writer.WriteString("createdBy", (string)version.CreatedBy);
                writer.WriteNumber("createdAt", version.CreatedAtValue.ToUnixTimeMilliseconds());
                if (version.LastUpdatedByOrNull is { } lastUpdatedBy)
                {
                    writer.WriteString("lastUpdatedBy", lastUpdatedBy);
                }

                if (version.LastUpdatedAtValue is { } lastUpdatedAt)
                {
                    writer.WriteNumber("lastUpdatedAt", lastUpdatedAt.ToUnixTimeMilliseconds());
                }

                if (version.ObsoletedByOrNull is { } obsoletedBy)
                {
                    writer.WriteString("obsoletedBy", obsoletedBy);
                }

                if (version.ObsoletedAtValue is { } obsoletedAt)
                {
                    writer.WriteNumber("obsoletedAt", obsoletedAt.ToUnixTimeMilliseconds());
                }

                writer.WriteEndObject();
            }

            ReadOnlySpan<byte> header = headerBuffer.WrittenSpan;
            var result = new byte[4 + header.Length + package.Length];
            BinaryPrimitives.WriteInt32LittleEndian(result, header.Length);
            header.CopyTo(result.AsSpan(4));
            package.CopyTo(result.AsSpan(4 + header.Length));
            return result;
        }

        public static byte[] DecodePackage(byte[] value)
        {
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(value);
            return value.AsSpan(4 + headerLength).ToArray();
        }

        public static CatalogVersion DecodeMetadata(byte[] value)
        {
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(value);
            using var document = JsonDocument.Parse(value.AsMemory(4, headerLength));
            System.Text.Json.JsonElement root = document.RootElement;

            System.Text.Json.JsonElement ownerElement = root.GetProperty("owner");
            var owner = new CatalogOwner(
                ownerElement.GetProperty("name").GetString()!,
                ownerElement.GetProperty("email").GetString()!,
                ownerElement.TryGetProperty("team", out System.Text.Json.JsonElement team) ? team.GetString() : null,
                ownerElement.TryGetProperty("url", out System.Text.Json.JsonElement url) ? url.GetString() : null);

            return CatalogVersion.Create(
                baseWorkflowId: root.GetProperty("baseWorkflowId").GetString()!,
                versionNumber: root.GetProperty("versionNumber").GetInt32(),
                workflowId: root.GetProperty("workflowId").GetString()!,
                title: root.GetProperty("title").GetString()!,
                description: root.TryGetProperty("description", out System.Text.Json.JsonElement description) ? description.GetString() : null,
                status: Enum.Parse<CatalogStatus>(root.GetProperty("status").GetString()!),
                tags: DecodeTags(root),
                owner: owner,
                sources: DecodeSources(root),
                hash: root.GetProperty("hash").GetString()!,
                createdBy: root.GetProperty("createdBy").GetString()!,
                createdAt: DateTimeOffset.FromUnixTimeMilliseconds(root.GetProperty("createdAt").GetInt64()),
                lastUpdatedBy: root.TryGetProperty("lastUpdatedBy", out System.Text.Json.JsonElement lastUpdatedBy) ? lastUpdatedBy.GetString() : null,
                lastUpdatedAt: root.TryGetProperty("lastUpdatedAt", out System.Text.Json.JsonElement lastUpdatedAt) ? DateTimeOffset.FromUnixTimeMilliseconds(lastUpdatedAt.GetInt64()) : null,
                obsoletedBy: root.TryGetProperty("obsoletedBy", out System.Text.Json.JsonElement obsoletedBy) ? obsoletedBy.GetString() : null,
                obsoletedAt: root.TryGetProperty("obsoletedAt", out System.Text.Json.JsonElement obsoletedAt) ? DateTimeOffset.FromUnixTimeMilliseconds(obsoletedAt.GetInt64()) : null,
                runnable: root.TryGetProperty("runnable", out System.Text.Json.JsonElement runnable) && runnable.GetBoolean());
        }

        private static IReadOnlyList<string> DecodeTags(System.Text.Json.JsonElement root)
        {
            if (!root.TryGetProperty("tags", out System.Text.Json.JsonElement tags))
            {
                return [];
            }

            var list = new List<string>();
            foreach (System.Text.Json.JsonElement tag in tags.EnumerateArray())
            {
                if (tag.GetString() is { } value)
                {
                    list.Add(value);
                }
            }

            return list;
        }

        private static IReadOnlyList<CatalogSourceRef> DecodeSources(System.Text.Json.JsonElement root)
        {
            if (!root.TryGetProperty("sources", out System.Text.Json.JsonElement sources))
            {
                return [];
            }

            var list = new List<CatalogSourceRef>();
            foreach (System.Text.Json.JsonElement source in sources.EnumerateArray())
            {
                list.Add(new CatalogSourceRef(
                    source.GetProperty("name").GetString()!,
                    source.TryGetProperty("type", out System.Text.Json.JsonElement type) ? type.GetString() : null));
            }

            return list;
        }
    }
}