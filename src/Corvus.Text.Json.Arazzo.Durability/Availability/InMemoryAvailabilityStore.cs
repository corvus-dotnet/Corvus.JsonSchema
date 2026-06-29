// <copyright file="InMemoryAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>
/// The reference in-memory <see cref="IAvailabilityStore"/> — the conformance baseline and a ready store for single-node
/// / local-development hosts. Each entry is held as its UTF-8 JSON document, keyed by (baseWorkflowId, versionNumber,
/// environment); reads hand back pooled <see cref="ParsedJsonDocument{T}"/> the caller disposes. AvailabilityEntry carries no
/// security tags — authorization and readiness are the surface's concern, not the store's.
/// </summary>
public sealed class InMemoryAvailabilityStore : IAvailabilityStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string BaseWorkflowId, int VersionNumber, string Environment), byte[]> entries = new();
    private readonly TimeProvider timeProvider;
    private long etagSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryAvailabilityStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryAvailabilityStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(actor);
        lock (this.gate)
        {
            (string, int, string) key = (baseWorkflowId, versionNumber, environment);
            if (this.entries.TryGetValue(key, out byte[]? existing))
            {
                return new ValueTask<(ParsedJsonDocument<AvailabilityEntry>, bool)>((PersistedJson.ToPooledDocument<AvailabilityEntry>(existing), false));
            }

            using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft(baseWorkflowId, versionNumber, environment);
            byte[] json = AvailabilitySerialization.SerializeNew(draft.RootElement, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.entries[key] = json;
            return new ValueTask<(ParsedJsonDocument<AvailabilityEntry>, bool)>((PersistedJson.ToPooledDocument<AvailabilityEntry>(json), true));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<AvailabilityEntry>?>(
                this.entries.TryGetValue((baseWorkflowId, versionNumber, environment), out byte[]? json)
                    ? PersistedJson.ToPooledDocument<AvailabilityEntry>(json)
                    : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        lock (this.gate)
        {
            return new ValueTask<bool>(this.entries.Remove((baseWorkflowId, versionNumber, environment)));
        }
    }

    /// <inheritdoc/>
    public ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        lock (this.gate)
        {
            // Rows for this exact version, ordered by environment (the only varying key part on this axis).
            var rows = new List<(string BaseWorkflowId, int VersionNumber, string Environment, byte[] Json)>();
            foreach (KeyValuePair<(string BaseWorkflowId, int VersionNumber, string Environment), byte[]> entry in this.entries)
            {
                if (string.Equals(entry.Key.BaseWorkflowId, baseWorkflowId, StringComparison.Ordinal) && entry.Key.VersionNumber == versionNumber)
                {
                    rows.Add((entry.Key.BaseWorkflowId, entry.Key.VersionNumber, entry.Key.Environment, entry.Value));
                }
            }

            rows.Sort(static (x, y) => string.CompareOrdinal(x.Environment, y.Environment));
            return new ValueTask<AvailabilityPage>(BuildPage(rows, pageSize, hasCursor, static (row, cursor) => string.CompareOrdinal(row.Environment, cursor.Environment), cursor));
        }
    }

    /// <inheritdoc/>
    public ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        lock (this.gate)
        {
            // Rows for this environment, ordered by base workflow id then version number.
            var rows = new List<(string BaseWorkflowId, int VersionNumber, string Environment, byte[] Json)>();
            foreach (KeyValuePair<(string BaseWorkflowId, int VersionNumber, string Environment), byte[]> entry in this.entries)
            {
                if (string.Equals(entry.Key.Environment, environment, StringComparison.Ordinal))
                {
                    rows.Add((entry.Key.BaseWorkflowId, entry.Key.VersionNumber, entry.Key.Environment, entry.Value));
                }
            }

            rows.Sort(static (x, y) => CompareWorkflowVersion(x.BaseWorkflowId, x.VersionNumber, y.BaseWorkflowId, y.VersionNumber));
            return new ValueTask<AvailabilityPage>(BuildPage(rows, pageSize, hasCursor, static (row, cursor) => CompareWorkflowVersion(row.BaseWorkflowId, row.VersionNumber, cursor.BaseWorkflowId, cursor.VersionNumber), cursor));
        }
    }

    // The by-environment total order: base workflow id (ordinal), then version number (numeric).
    private static int CompareWorkflowVersion(string b1, int v1, string b2, int v2)
    {
        int c = string.CompareOrdinal(b1, b2);
        return c != 0 ? c : v1.CompareTo(v2);
    }

    private static bool TryDecodeCursor(JsonString pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        cursor = default;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        return AvailabilityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
    }

    // Scans the sorted rows past the cursor (per the axis comparer), takes a page, and emits a token from the last
    // included row's full key when more rows remain. Each page row is parsed into a pooled document the caller owns.
    private static AvailabilityPage BuildPage(
        List<(string BaseWorkflowId, int VersionNumber, string Environment, byte[] Json)> sorted,
        int pageSize,
        bool hasCursor,
        Func<(string BaseWorkflowId, int VersionNumber, string Environment), (string BaseWorkflowId, int VersionNumber, string Environment), int> compareToCursor,
        (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        var docs = new PooledDocumentList<AvailabilityEntry>(Math.Min(pageSize, sorted.Count));
        bool hasMore = false;
        string lastBaseWorkflowId = string.Empty, lastEnvironment = string.Empty;
        int lastVersionNumber = 0;
        try
        {
            foreach ((string BaseWorkflowId, int VersionNumber, string Environment, byte[] Json) row in sorted)
            {
                if (hasCursor && compareToCursor((row.BaseWorkflowId, row.VersionNumber, row.Environment), cursor) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(row.Json));
                lastBaseWorkflowId = row.BaseWorkflowId;
                lastVersionNumber = row.VersionNumber;
                lastEnvironment = row.Environment;
            }

            return hasMore
                ? AvailabilityPage.Create(docs, lastBaseWorkflowId, lastVersionNumber, lastEnvironment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}