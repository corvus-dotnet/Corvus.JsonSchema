// <copyright file="NatsJetStreamWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
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
public sealed class NatsJetStreamWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter, IAsyncDisposable
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
    public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8, metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(baseWorkflowId, versionNumber), cancellationToken).ConfigureAwait(false);

        // The envelope header bytes ARE the version document JSON; realize a pooled, disposable document over them
        // (the byte-backend idiom) rather than cloning a bare value out.
        return entry is { Value: { } value }
            ? ParsedJsonDocument<CatalogVersion>.Parse(Envelope.DecodeHeader(value))
            : null;
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
        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        int limit = query.Limit <= 0 ? 100 : query.Limit;

        // The KV bucket has no server-side ordering or filtering, so collect the matching header bytes, sort them by
        // the composite sort key, then realize and page the result here — mirroring how the run store answers
        // visibility queries. Only the matching rows are parsed (each inspected once for filtering, with the inspection
        // document disposed before it is re-parsed into the owned page), so non-matches never enter the pool.
        var matches = new List<(string SortKey, byte[] Header)>();
        await foreach (byte[] header in this.ScanHeadersAsync(cancellationToken).ConfigureAwait(false))
        {
            using ParsedJsonDocument<CatalogVersion> candidate = ParsedJsonDocument<CatalogVersion>.Parse(header);
            CatalogVersionRef reference = candidate.RootElement.Ref;
            string sortKey = SortKey(reference.BaseWorkflowId, reference.VersionNumber);
            if (after is not null && string.CompareOrdinal(sortKey, after) <= 0)
            {
                continue;
            }

            if (Matches(candidate.RootElement, query))
            {
                matches.Add((sortKey, header));
            }
        }

        matches.Sort(static (a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));

        string? nextSortKey = null;
        if (matches.Count > limit)
        {
            nextSortKey = matches[limit - 1].SortKey;
            matches = matches.GetRange(0, limit);
        }

        // The page is a pooled batch of disposable version documents (the caller disposes the page); realize each
        // selected header into the batch, disposing a partially-built batch if a parse throws.
        var versions = new PooledDocumentList<CatalogVersion>(matches.Count);
        try
        {
            foreach ((_, byte[] header) in matches)
            {
                versions.Add(ParsedJsonDocument<CatalogVersion>.Parse(header));
            }
        }
        catch
        {
            versions.Dispose();
            throw;
        }

        return nextSortKey is not null ? CatalogPage.Create(versions, nextSortKey) : CatalogPage.Create(versions);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        string key = Key(baseWorkflowId, versionNumber);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } value })
        {
            return null;
        }

        byte[] package = Envelope.DecodePackage(value);

        // The envelope header bytes ARE the current version document JSON; inspect them through a short-lived pooled
        // document, then rebuild the updated header bytes — never cloning a bare value out of the read.
        byte[] updatedHeader;
        using (ParsedJsonDocument<CatalogVersion> currentDoc = ParsedJsonDocument<CatalogVersion>.Parse(Envelope.DecodeHeader(value)))
        {
            CatalogVersion current = currentDoc.RootElement;
            CatalogStatus currentStatus = current.StatusValue;
            CatalogStatus status = patch.Status ?? currentStatus;
            bool newlyObsolete = status == CatalogStatus.Obsolete && currentStatus != CatalogStatus.Obsolete;
            bool reactivated = status == CatalogStatus.Active && currentStatus == CatalogStatus.Obsolete;

            CatalogVersionRef reference = current.Ref;
            updatedHeader = CatalogVersion.CreateBytes(
                baseWorkflowId: reference.BaseWorkflowId,
                versionNumber: reference.VersionNumber,
                workflowId: reference.WorkflowId,
                title: (string)current.Title,
                description: current.DescriptionOrNull,
                status: status,
                tags: patch.Tags ?? current.TagsValue,
                owner: patch.Owner ?? current.OwnerValue,
                sources: current.SourcesValue,
                hash: (string)current.Hash,
                createdBy: (string)current.CreatedBy,
                createdAt: current.CreatedAtValue,
                lastUpdatedBy: patch.UpdatedBy,
                lastUpdatedAt: now,
                obsoletedBy: newlyObsolete ? patch.UpdatedBy : reactivated ? null : current.ObsoletedByOrNull,
                obsoletedAt: newlyObsolete ? now : reactivated ? null : current.ObsoletedAtValue,
                runnable: (bool)current.Runnable);
        }

        await this.catalog.PutAsync(key, Envelope.Encode(updatedHeader, package), cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParsedJsonDocument<CatalogVersion>.Parse(updatedHeader);
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
        await foreach (byte[] header in this.ScanHeadersAsync(cancellationToken).ConfigureAwait(false))
        {
            using ParsedJsonDocument<CatalogVersion> doc = ParsedJsonDocument<CatalogVersion>.Parse(header);
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

        if (query.Owner is { Length: > 0 } owner
            && version.OwnerValue.Name.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0
            && version.OwnerValue.Email.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (!query.Tags.AllContainedIn(version.TagsValue))
        {
            return false;
        }

        // Row-security reach (§14.2): the KV store has no server-side filtering, so apply the reach filter in
        // process over the version's persisted security tags — the only correct option for a key/value backend.
        if (query.Security is { } security && !security.IsSatisfiedBy(version.SecurityTagsValue))
        {
            return false;
        }

        return true;
    }

    private async ValueTask<ParsedJsonDocument<CatalogVersion>> AddCoreAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        TagSet tags = metadata.Tags;
        SecurityTagSet securityTags = metadata.SecurityTags;

        // Assign the next version number safely: compute the current max for the base id by scanning the bucket,
        // then optimistically Create the new key. A concurrent add that grabbed the same number makes Create
        // fail (the key already exists), so recompute and retry — the same revision-backed concurrency the run
        // store relies on for its saves.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int versionNumber = await this.MaxVersionAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false) + 1;
            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);

            // The envelope header IS the version document JSON; build those bytes directly (the byte-backend idiom),
            // store the envelope, and realize a pooled, disposable document over the same header bytes for the return.
            byte[] header = CatalogVersion.CreateBytes(
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

            byte[] value = Envelope.Encode(header, projection.CanonicalPackage.Span);
            try
            {
                await this.catalog.CreateAsync(Key(baseWorkflowId, versionNumber), value, cancellationToken: cancellationToken).ConfigureAwait(false);
                return ParsedJsonDocument<CatalogVersion>.Parse(header);
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
        await foreach (byte[] header in this.ScanHeadersAsync(cancellationToken).ConfigureAwait(false))
        {
            using ParsedJsonDocument<CatalogVersion> doc = ParsedJsonDocument<CatalogVersion>.Parse(header);
            CatalogVersionRef reference = doc.RootElement.Ref;
            if (reference.BaseWorkflowId == baseWorkflowId && reference.VersionNumber > max)
            {
                max = reference.VersionNumber;
            }
        }

        return max;
    }

    // Yields each stored version's header bytes (the version document JSON); callers parse a short-lived pooled
    // document over them to inspect fields, never cloning a bare value out of the scan.
    private async IAsyncEnumerable<byte[]> ScanHeadersAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (string key in this.catalog.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } value })
            {
                yield return Envelope.DecodeHeader(value);
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
        // The header IS the CatalogVersion JSON document (push-JSON-to-the-store); the value is
        // [4-byte little-endian header length][CatalogVersion JSON][package bytes]. The header bytes are built by the
        // byte-backend factory (CatalogVersion.CreateBytes) at the call site, so Encode just frames them.
        public static byte[] Encode(byte[] header, ReadOnlySpan<byte> package)
        {
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

        // Returns the version document JSON bytes from the envelope; the store realizes a pooled, disposable
        // document over them via ParsedJsonDocument<CatalogVersion>.Parse (never a bare-value clone).
        public static byte[] DecodeHeader(byte[] value)
        {
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(value);
            return value.AsSpan(4, headerLength).ToArray();
        }
    }
}