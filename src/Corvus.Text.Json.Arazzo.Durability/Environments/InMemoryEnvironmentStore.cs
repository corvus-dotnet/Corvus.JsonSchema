// <copyright file="InMemoryEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// The reference in-memory <see cref="IEnvironmentStore"/> — the conformance baseline and a ready store for single-node
/// / local-development hosts. Each environment is held as its UTF-8 JSON document (the "push JSON to the store" shape
/// every backend persists), keyed by (name, management-tag discriminator) so reach-isolated environments that share a
/// name coexist; reads hand back a pooled <see cref="ParsedJsonDocument{T}"/> the caller disposes. Every mutation stamps
/// a fresh per-record etag.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) against each
/// environment's <see cref="Environment.ManagementTagsValue"/>. Administration (who may govern an environment) is a
/// separate store, exactly as a workflow's administrators are stored apart from its catalog versions.
/// </remarks>
public sealed class InMemoryEnvironmentStore : IEnvironmentStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string Name, string Tags), byte[]> environments = new();
    private readonly TimeProvider timeProvider;
    private long etagSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryEnvironmentStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryEnvironmentStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        lock (this.gate)
        {
            (string, string) key = (draft.NameValue, SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue));
            if (this.environments.ContainsKey(key))
            {
                throw new InvalidOperationException($"An environment named '{draft.NameValue}' with those security tags already exists.");
            }

            byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.environments[key] = json;
            return new ValueTask<ParsedJsonDocument<Environment>>(PersistedJson.ToPooledDocument<Environment>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? json = this.FindForManagement(name, AccessVerb.Read, context, out _);
            return new ValueTask<ParsedJsonDocument<Environment>?>(
                json is null ? null : PersistedJson.ToPooledDocument<Environment>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<EnvironmentPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = EnvironmentContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        lock (this.gate)
        {
            // Materialise the keyset (name, discriminator) for the in-memory set and order it — the same total order
            // every backend pages by, just without a DB index. Then scan past the cursor, applying the per-row reach
            // predicate, until the page is full (or the data is exhausted).
            var ordered = new List<(string Name, string Tags, byte[] Json)>(this.environments.Count);
            foreach (KeyValuePair<(string Name, string Tags), byte[]> entry in this.environments)
            {
                ordered.Add((entry.Key.Name, entry.Key.Tags, entry.Value));
            }

            ordered.Sort(static (x, y) => CompareKey(x.Name, x.Tags, y.Name, y.Tags));

            var docs = new PooledDocumentList<Environment>(Math.Min(pageSize, ordered.Count));
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
                    ParsedJsonDocument<Environment> cand = PersistedJson.ToPooledDocument<Environment>(row.Json);
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

                return new ValueTask<EnvironmentPage>(hasMore
                    ? EnvironmentPage.Create(docs, lastName, lastTags)
                    : EnvironmentPage.Create(docs));
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<Environment>?> UpdateAsync(string name, Environment draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? existing = this.FindForManagement(name, AccessVerb.Write, context, out (string, string) key);
            if (existing is null)
            {
                return new ValueTask<ParsedJsonDocument<Environment>?>((ParsedJsonDocument<Environment>?)null);
            }

            byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.environments[key] = json; // name + tags immutable → key unchanged
            return new ValueTask<ParsedJsonDocument<Environment>?>(PersistedJson.ToPooledDocument<Environment>(json));
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
                EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
            }

            this.environments.Remove(key);
            return new ValueTask<bool>(true);
        }
    }

    // The stable total order every backend pages by: name, then the tag discriminator.
    private static int CompareKey(string n1, string t1, string n2, string t2)
    {
        int c = string.CompareOrdinal(n1, n2);
        return c != 0 ? c : string.CompareOrdinal(t1, t2);
    }

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and key. An
    // environment outside reach is invisible (non-disclosing). Deployments keep environments for one name reach-disjoint,
    // so at most one is admitted; the first admitted is returned deterministically.
    private byte[]? FindForManagement(string name, AccessVerb verb, AccessContext context, out (string, string) key)
    {
        foreach (KeyValuePair<(string Name, string Tags), byte[]> entry in this.environments)
        {
            if (!string.Equals(entry.Key.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            using ParsedJsonDocument<Environment> candidate = PersistedJson.ToPooledDocument<Environment>(entry.Value);
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