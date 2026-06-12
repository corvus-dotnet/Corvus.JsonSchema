// <copyright file="InMemoryWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-memory reference implementation of <see cref="IWorkflowCatalogStore"/>: it keeps each version's
/// projected metadata and its canonical package bytes in a dictionary, exactly as
/// <see cref="InMemoryWorkflowStateStore"/> does for runs. It is the reference the shared catalog-conformance
/// suite runs against, and is usable for a single-process catalog that does not need to survive a restart.
/// </summary>
public sealed class InMemoryWorkflowCatalogStore : IWorkflowCatalogStore
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
    public ValueTask<CatalogVersion> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        cancellationToken.ThrowIfCancellationRequested();

        // Project the package outside the lock — parsing/hashing is the expensive part.
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            int versionNumber = this.MaxVersion(baseWorkflowId) + 1;
            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);
            var version = new CatalogVersion(
                BaseWorkflowId: baseWorkflowId,
                VersionNumber: versionNumber,
                WorkflowId: projection.WorkflowId,
                Title: projection.Title,
                Description: projection.Description,
                Status: CatalogStatus.Active,
                Tags: metadata.Tags is { Count: > 0 } tags ? [.. tags] : [],
                Owner: metadata.Owner,
                Sources: projection.Sources,
                Hash: projection.Hash,
                CreatedBy: metadata.CreatedBy,
                CreatedAt: now,
                Runnable: projection.HasExecutor);

            this.versions[SortKey(baseWorkflowId, versionNumber)] = new Stored(version, projection.CanonicalPackage.ToArray());
            return ValueTask.FromResult(version);
        }
    }

    /// <inheritdoc/>
    public ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            return ValueTask.FromResult(
                this.versions.TryGetValue(SortKey(baseWorkflowId, versionNumber), out Stored stored) ? stored.Version : null);
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
        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        int limit = query.Limit <= 0 ? 100 : query.Limit;
        var matches = new List<CatalogVersion>();
        string? continuation = null;
        lock (this.gate)
        {
            foreach (KeyValuePair<string, Stored> entry in this.versions)
            {
                if (after is not null && string.CompareOrdinal(entry.Key, after) <= 0)
                {
                    continue;
                }

                CatalogVersion candidate = entry.Value.Version;
                if (!Matches(candidate, query))
                {
                    continue;
                }

                if (matches.Count == limit)
                {
                    // There is at least one more matching row beyond this page.
                    continuation = WorkflowContinuationToken.Encode(SortKey(matches[^1].BaseWorkflowId, matches[^1].VersionNumber));
                    break;
                }

                matches.Add(candidate);
            }
        }

        return ValueTask.FromResult(new CatalogPage(matches, continuation));
    }

    /// <inheritdoc/>
    public ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            string key = SortKey(baseWorkflowId, versionNumber);
            if (!this.versions.TryGetValue(key, out Stored stored))
            {
                return ValueTask.FromResult<CatalogVersion?>(null);
            }

            CatalogVersion current = stored.Version;
            CatalogStatus status = patch.Status ?? current.Status;
            bool newlyObsolete = status == CatalogStatus.Obsolete && current.Status != CatalogStatus.Obsolete;
            bool reactivated = status == CatalogStatus.Active && current.Status == CatalogStatus.Obsolete;

            CatalogVersion updated = current with
            {
                Owner = patch.Owner ?? current.Owner,
                Tags = patch.Tags is { } tags ? [.. tags] : current.Tags,
                Status = status,
                LastUpdatedBy = patch.UpdatedBy,
                LastUpdatedAt = now,
                ObsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : current.ObsoletedBy,
                ObsoletedAt = newlyObsolete ? now : reactivated ? null : current.ObsoletedAt,
            };

            this.versions[key] = stored with { Version = updated };
            return ValueTask.FromResult<CatalogVersion?>(updated);
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
            IReadOnlyList<CatalogVersionRef> obsolete = this.versions.Values
                .Where(s => s.Version.Status == CatalogStatus.Obsolete)
                .Select(s => s.Version.Ref)
                .ToList();
            return ValueTask.FromResult(obsolete);
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

    private int MaxVersion(string baseWorkflowId)
    {
        int max = 0;
        foreach (Stored stored in this.versions.Values)
        {
            if (stored.Version.BaseWorkflowId == baseWorkflowId && stored.Version.VersionNumber > max)
            {
                max = stored.Version.VersionNumber;
            }
        }

        return max;
    }

    private readonly record struct Stored(CatalogVersion Version, byte[] Package);
}