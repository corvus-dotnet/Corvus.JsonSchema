// <copyright file="InMemoryWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Runtime.InteropServices;
using JsonMarshal = Corvus.Runtime.InteropServices.JsonMarshal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-memory reference implementation of <see cref="IWorkflowCatalogStore"/>: it keeps each version's
/// projected metadata and its canonical package bytes in a dictionary, exactly as
/// <see cref="InMemoryWorkflowStateStore"/> does for runs. It is the reference the shared catalog-conformance
/// suite runs against, and is usable for a single-process catalog that does not need to survive a restart.
/// </summary>
public sealed class InMemoryWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter
{
    // Keyed by sort key ("{baseWorkflowId}{versionNumber:D10}") so enumeration is stable for keyset paging.
    private readonly SortedDictionary<string, Stored> versions = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;
    private readonly Lock gate = new();

    /// <summary>Initializes a new instance of the <see cref="InMemoryWorkflowCatalogStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="metadataProvider">An optional provider that bakes the typed schema-metadata document into each
    /// added version; <see langword="null"/> to store packages without it.</param>
    /// <param name="executorProvider">An optional provider that compiles the workflow executor assembly into each
    /// added version; <see langword="null"/> to store packages without it.</param>
    public InMemoryWorkflowCatalogStore(TimeProvider? timeProvider = null, IWorkflowMetadataProvider? metadataProvider = null, IWorkflowExecutorProvider? executorProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        cancellationToken.ThrowIfCancellationRequested();

        // Project the package outside the lock — parsing/hashing is the expensive part.
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            int versionNumber = this.MaxVersion(baseWorkflowId) + 1;
            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);

            // Persist the version as its document BYTES (the durable form), then realize a pooled, disposable document
            // over those owned bytes for the return — the MetadataDb is rented (returned on the caller's Dispose), not
            // a standalone GC allocation. The stored byte[] outlives every returned document, so Parse may reference it.
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

            // The projection is the sole owner of its freshly-built canonical-package array, so take it directly rather
            // than copying — PackPooled returns an exact-sized array, so the ReadOnlyMemory wraps it whole.
            byte[] packageBytes = MemoryMarshal.TryGetArray(projection.CanonicalPackage, out ArraySegment<byte> segment)
                && segment.Offset == 0 && segment.Array is { } array && array.Length == segment.Count
                ? array
                : projection.CanonicalPackage.ToArray();

            this.versions[SortKey(baseWorkflowId, versionNumber)] = new Stored(versionDoc, packageBytes);
            return ValueTask.FromResult(ParsedJsonDocument<CatalogVersion>.Parse(versionDoc));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            return ValueTask.FromResult(
                this.versions.TryGetValue(SortKey(baseWorkflowId, versionNumber), out Stored stored)
                    ? ParsedJsonDocument<CatalogVersion>.Parse(stored.VersionDoc)
                    : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            return ValueTask.FromResult(
                this.versions.TryGetValue(SortKey(baseWorkflowId, versionNumber), out Stored stored) ? (ReadOnlyMemory<byte>?)stored.Package : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentName);
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? package;
        lock (this.gate)
        {
            package = this.versions.TryGetValue(SortKey(baseWorkflowId, versionNumber), out Stored stored) ? stored.Package : null;
        }

        return ValueTask.FromResult(package is null ? null : CatalogPackage.GetDocument(package, documentName));
    }

    /// <inheritdoc/>
    public ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        int limit = query.Limit <= 0 ? 100 : query.Limit;

        // The page is a pooled batch of disposable version documents (the caller disposes the page). Each candidate is
        // parsed once; matches are kept in the batch, non-matches and the look-ahead row are disposed immediately.
        var matches = new PooledDocumentList<CatalogVersion>(limit);
        string? nextSortKey = null;
        try
        {
            lock (this.gate)
            {
                foreach (KeyValuePair<string, Stored> entry in this.versions)
                {
                    if (after is not null && string.CompareOrdinal(entry.Key, after) <= 0)
                    {
                        continue;
                    }

                    ParsedJsonDocument<CatalogVersion> candidate = ParsedJsonDocument<CatalogVersion>.Parse(entry.Value.VersionDoc);
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
        }
        catch
        {
            matches.Dispose();
            throw;
        }

        return ValueTask.FromResult(nextSortKey is not null ? CatalogPage.Create(matches, nextSortKey) : CatalogPage.Create(matches));
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            string key = SortKey(baseWorkflowId, versionNumber);
            if (!this.versions.TryGetValue(key, out Stored stored))
            {
                return ValueTask.FromResult<ParsedJsonDocument<CatalogVersion>?>(null);
            }

            byte[] updatedDoc;
            using (ParsedJsonDocument<CatalogVersion> currentDoc = ParsedJsonDocument<CatalogVersion>.Parse(stored.VersionDoc))
            {
                // Patch only the changed governance fields through the mutable builder; every other field — including the
                // security tags — is carried bytes-to-bytes from the current document (no per-field string realisation,
                // and no longer dropping securityTags as the field-by-field CreateBytes rebuild did).
                updatedDoc = CatalogVersion.CreatePatchedBytes(currentDoc.RootElement, patch, now);
            }

            this.versions[key] = stored with { VersionDoc = updatedDoc };
            return ValueTask.FromResult<ParsedJsonDocument<CatalogVersion>?>(ParsedJsonDocument<CatalogVersion>.Parse(updatedDoc));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            return ValueTask.FromResult(this.versions.Remove(SortKey(baseWorkflowId, versionNumber)));
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            var obsolete = new List<CatalogVersionRef>();
            foreach (Stored s in this.versions.Values)
            {
                using ParsedJsonDocument<CatalogVersion> doc = ParsedJsonDocument<CatalogVersion>.Parse(s.VersionDoc);
                if (doc.RootElement.StatusValue == CatalogStatus.Obsolete)
                {
                    // CatalogVersionRef materializes its strings, so it outlives the document.
                    obsolete.Add(doc.RootElement.Ref);
                }
            }

            return ValueTask.FromResult<IReadOnlyList<CatalogVersionRef>>(obsolete);
        }
    }

    /// <inheritdoc/>
    public ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            foreach (CatalogVersionRef reference in versions)
            {
                this.versions.Remove(SortKey(reference.BaseWorkflowId, reference.VersionNumber));
            }
        }

        return ValueTask.CompletedTask;
    }

    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    private static bool Matches(in CatalogVersion version, CatalogQuery query)
    {
        if (query.BaseWorkflowId is { } baseId && !((JsonElement)version.BaseWorkflowId).EqualsString(baseId))
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

        if (query.Text is { Length: > 0 } text)
        {
            string title = (string)version.Title;
            string? description = version.DescriptionOrNull;
            if (title.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0
                && (description is null || description.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }
        }

        if (query.Owner is { Length: > 0 } owner)
        {
            CatalogOwner ownerValue = version.OwnerValue;
            if (ownerValue.Name.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0
                && ownerValue.Email.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        if (!query.Tags.AllContainedIn(version.TagsValue))
        {
            return false;
        }

        if (query.Security is not { } security)
        {
            return true;
        }

        SecurityTagSet securityTags = version.SecurityTags.IsNotUndefined()
            ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(version.SecurityTags).Memory)
            : SecurityTagSet.Empty;
        return security.IsSatisfiedBy(securityTags);
    }

    private int MaxVersion(string baseWorkflowId)
    {
        int max = 0;
        foreach (Stored stored in this.versions.Values)
        {
            using ParsedJsonDocument<CatalogVersion> doc = ParsedJsonDocument<CatalogVersion>.Parse(stored.VersionDoc);
            CatalogVersionRef reference = doc.RootElement.Ref;
            if (reference.BaseWorkflowId == baseWorkflowId && reference.VersionNumber > max)
            {
                max = reference.VersionNumber;
            }
        }

        return max;
    }

    // The persisted version document bytes (the durable form) + the canonical package bytes. The typed CatalogVersion is
    // realized from VersionDoc only at the leaf (read/return), as a pooled, disposable document — never stored standalone.
    private readonly record struct Stored(byte[] VersionDoc, byte[] Package);
}