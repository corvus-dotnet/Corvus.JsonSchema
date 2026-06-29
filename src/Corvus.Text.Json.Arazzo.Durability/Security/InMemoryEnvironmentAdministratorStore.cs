// <copyright file="InMemoryEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The reference in-memory <see cref="IEnvironmentAdministratorStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each administration record is held as its UTF-8 JSON document (the "push JSON to
/// the store" shape every backend persists), keyed by environment name; reads hand back a pooled
/// <see cref="ParsedJsonDocument{T}"/> the caller disposes. Every mutation stamps a fresh per-record etag and
/// <see cref="PutAsync"/> enforces optimistic concurrency. Mirrors <see cref="InMemoryWorkflowAdministratorStore"/>.
/// </summary>
public sealed class InMemoryEnvironmentAdministratorStore : IEnvironmentAdministratorStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, byte[]> records = new(StringComparer.Ordinal);

    // The reverse administration index (design §7.8): administrator-identity digest → the environment names it administers,
    // ordered by name for keyset paging — the in-memory analogue of a backend's indexed digest column (mirrors
    // InMemoryWorkflowAdministratorStore). administeredDigests holds each environment's current administrator digests so a
    // PutAsync that changes the administrator set can retract the stale digests before indexing the new ones.
    private readonly Dictionary<string, SortedSet<string>> byDigest = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> administeredDigests = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long etagSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryEnvironmentAdministratorStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryEnvironmentAdministratorStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?>(
                this.records.TryGetValue(environmentName, out byte[]? json)
                    ? PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json)
                    : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> PutAsync(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("An environment administration record requires at least one administrator identity.", nameof(administrators));
        }

        lock (this.gate)
        {
            byte[] json;
            if (this.records.TryGetValue(environmentName, out byte[]? existing))
            {
                // Parse the existing document ONCE, NON-COPYING over the stored array (alive in the dictionary through this
                // synchronous update under the lock) — used for both the etag check and the carried-forward merge.
                using ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(existing.AsMemory());

                // A record already exists: the caller must hold its current etag (None means "I expected no record").
                if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
                {
                    throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
                }

                json = EnvironmentAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            }
            else
            {
                // No record yet: materialization is only valid against the None etag.
                if (!expectedEtag.IsNone)
                {
                    throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
                }

                json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            }

            this.records[environmentName] = json;
            this.ReindexAdministered(environmentName, administrators);
            return new ValueTask<ParsedJsonDocument<EnvironmentAdministrators>>(PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        lock (this.gate)
        {
            this.records.Remove(environmentName);
            this.RetractAdministered(environmentName);
        }

        return default;
    }

    /// <inheritdoc/>
    public ValueTask<EnvironmentAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : EnvironmentAdministeredPage.DefaultPageSize;

        // Decode the keyset cursor (the name to page strictly after) from the request's token; the in-memory storage leaf is
        // a string key, so the cursor reifies once here (never per row) — like InMemoryWorkflowAdministratorStore.
        string? cursor = EnvironmentAdministeredContinuationToken.DecodeCursorToString(pageToken);

        lock (this.gate)
        {
            // The SortedSet enumerates names in ordinal order — the keyset order every backend pages by. Take the
            // pageSize+1 smallest past the cursor into a flat list; ToPage trims the lookahead and seeds the next token.
            var rows = new List<string>(pageSize + 1);
            if (this.byDigest.TryGetValue(adminDigest, out SortedSet<string>? names))
            {
                foreach (string name in names)
                {
                    if (cursor is not null && string.CompareOrdinal(name, cursor) <= 0)
                    {
                        continue; // at or before the cursor — already returned on an earlier page
                    }

                    rows.Add(name);
                    if (rows.Count > pageSize)
                    {
                        break;
                    }
                }
            }

            return new ValueTask<EnvironmentAdministeredPage>(EnvironmentAdministeredPaging.ToPage(rows, pageSize));
        }
    }

    // Refreshes the reverse administration index for an environment whose administrator set was just written (§7.8): retract
    // its previous administrator digests, then index its current ones (the shared DistinctDigests skips the empty identity).
    // Called under the gate, right after the record is stored. Two identities are set-equal iff their digests are equal, so
    // the same digest the forward IsAdministeredBy compares is the index key the inbox queries.
    private void ReindexAdministered(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators)
    {
        this.RetractAdministered(environmentName);
        IReadOnlyList<string> current = EnvironmentAdministeredPaging.DistinctDigests(administrators);
        foreach (string digest in current)
        {
            if (!this.byDigest.TryGetValue(digest, out SortedSet<string>? holders))
            {
                holders = new SortedSet<string>(StringComparer.Ordinal);
                this.byDigest[digest] = holders;
            }

            holders.Add(environmentName);
        }

        this.administeredDigests[environmentName] = [.. current];
    }

    // Drops an environment's current index entries (on delete, or before reindexing a changed set). Called under the gate.
    private void RetractAdministered(string environmentName)
    {
        if (this.administeredDigests.Remove(environmentName, out string[]? previous))
        {
            foreach (string digest in previous)
            {
                if (this.byDigest.TryGetValue(digest, out SortedSet<string>? holders))
                {
                    holders.Remove(environmentName);
                    if (holders.Count == 0)
                    {
                        this.byDigest.Remove(digest);
                    }
                }
            }
        }
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}