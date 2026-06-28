// <copyright file="InMemorySourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Sources;

/// <summary>
/// The reference in-memory <see cref="ISourceStore"/> — the conformance baseline and a ready store for single-node /
/// local-development hosts. Each source is held as its UTF-8 JSON document (the "push JSON to the store" shape every
/// backend persists, the registered document included), keyed by (name, management-tag discriminator) so reach-isolated
/// sources that share a name coexist; reads hand back a pooled <see cref="ParsedJsonDocument{T}"/> the caller disposes.
/// Every mutation stamps a fresh per-record etag.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) against each source's
/// <see cref="RegisteredSource.ManagementTagsValue"/>. The per-environment credentials that authenticate calls to a source are a
/// separate store, and there is no administrator set — reach membership is the management gate.
/// </remarks>
public sealed class InMemorySourceStore : ISourceStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string Name, string Tags), byte[]> sources = new();
    private readonly TimeProvider timeProvider;
    private long etagSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemorySourceStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemorySourceStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<RegisteredSource>> AddAsync(RegisteredSource draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        lock (this.gate)
        {
            (string, string) key = (draft.NameValue, SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue));
            if (this.sources.ContainsKey(key))
            {
                throw new InvalidOperationException($"A source named '{draft.NameValue}' with those security tags already exists.");
            }

            byte[] json = SourceSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.sources[key] = json;
            return new ValueTask<ParsedJsonDocument<RegisteredSource>>(PersistedJson.ToPooledDocument<RegisteredSource>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<RegisteredSource>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? json = this.FindForManagement(name, AccessVerb.Read, context, out _);
            return new ValueTask<ParsedJsonDocument<RegisteredSource>?>(
                json is null ? null : PersistedJson.ToPooledDocument<RegisteredSource>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SourcePage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        lock (this.gate)
        {
            // Materialise the keyset (name, discriminator) for the in-memory set and order it — the same total order
            // every backend pages by, just without a DB index. Then scan past the cursor, applying the per-row reach
            // predicate, until the page is full (or the data is exhausted).
            var ordered = new List<(string Name, string Tags, byte[] Json)>(this.sources.Count);
            foreach (KeyValuePair<(string Name, string Tags), byte[]> entry in this.sources)
            {
                ordered.Add((entry.Key.Name, entry.Key.Tags, entry.Value));
            }

            ordered.Sort(static (x, y) => CompareKey(x.Name, x.Tags, y.Name, y.Tags));

            var docs = new PooledDocumentList<RegisteredSource>(Math.Min(pageSize, ordered.Count));
            bool hasMore = false;
            string lastName = string.Empty, lastTags = string.Empty;
            try
            {
                foreach ((string Name, string Tags, byte[] Json) row in ordered)
                {
                    if (hasCursor && CompareKey(row.Name, row.Tags, cursor.Name, cursor.TieBreaker) <= 0)
                    {
                        continue; // at or before the cursor — already returned in an earlier page
                    }

                    // Parse the row ONCE: reach-check it through a non-owning tag view (no per-row CopyFrom), and if it is
                    // both visible and within the page, hand that same pooled document to the page (no second parse). A
                    // skipped/over-the-page row is disposed in the finally; a kept row's ownership transfers to `docs`.
                    ParsedJsonDocument<RegisteredSource> cand = PersistedJson.ToPooledDocument<RegisteredSource>(row.Json);
                    bool kept = false;
                    try
                    {
                        SecurityTagSet tags = cand.RootElement.ManagementTags.IsNotUndefined()
                            ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                            : SecurityTagSet.Empty;
                        if (!context.Admits(AccessVerb.Read, tags))
                        {
                            continue; // not reach-visible to this caller (cand disposed in finally)
                        }

                        if (docs.Count == pageSize)
                        {
                            // A further visible row exists → there is a next page; the token resumes after the last included row.
                            hasMore = true;
                            break; // cand disposed in finally
                        }

                        docs.Add(cand);
                        kept = true;
                        lastName = row.Name;
                        lastTags = row.Tags;
                    }
                    finally
                    {
                        if (!kept)
                        {
                            cand.Dispose();
                        }
                    }
                }

                return new ValueTask<SourcePage>(hasMore
                    ? SourcePage.Create(docs, lastName, lastTags)
                    : SourcePage.Create(docs));
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<RegisteredSource>?> UpdateAsync(string name, RegisteredSource draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? existing = this.FindForManagement(name, AccessVerb.Write, context, out (string, string) key);
            if (existing is null)
            {
                return new ValueTask<ParsedJsonDocument<RegisteredSource>?>((ParsedJsonDocument<RegisteredSource>?)null);
            }

            byte[] json = SourceSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.sources[key] = json; // name + type + tags immutable → key unchanged
            return new ValueTask<ParsedJsonDocument<RegisteredSource>?>(PersistedJson.ToPooledDocument<RegisteredSource>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? existing = this.FindForManagement(name, AccessVerb.Write, context, out (string, string) key);
            if (existing is null)
            {
                return new ValueTask<bool>(false);
            }

            if (!expectedEtag.IsNone)
            {
                SourceSerialization.EnsureEtag(name, expectedEtag, SourceSerialization.EtagOf(existing));
            }

            this.sources.Remove(key);
            return new ValueTask<bool>(true);
        }
    }

    // The stable total order every backend pages by: name, then the tag discriminator.
    private static int CompareKey(string n1, string t1, string n2, string t2)
    {
        int c = string.CompareOrdinal(n1, n2);
        return c != 0 ? c : string.CompareOrdinal(t1, t2);
    }

    // Finds the single source named `name` the caller's reach for the verb admits, returning its bytes and key. A source
    // outside reach is invisible (non-disclosing). Deployments keep sources for one name reach-disjoint, so at most one is
    // admitted; the first admitted is returned deterministically.
    private byte[]? FindForManagement(string name, AccessVerb verb, AccessContext context, out (string, string) key)
    {
        foreach (KeyValuePair<(string Name, string Tags), byte[]> entry in this.sources)
        {
            if (!string.Equals(entry.Key.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            using ParsedJsonDocument<RegisteredSource> candidate = PersistedJson.ToPooledDocument<RegisteredSource>(entry.Value);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                key = entry.Key;
                return entry.Value;
            }
        }

        key = default;
        return null;
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}