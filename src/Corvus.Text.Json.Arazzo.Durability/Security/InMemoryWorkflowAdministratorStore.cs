// <copyright file="InMemoryWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The reference in-memory <see cref="IWorkflowAdministratorStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each administration record is held as its UTF-8 JSON document (the "push JSON to
/// the store" shape every backend persists), keyed by base workflow id; reads hand back a pooled
/// <see cref="ParsedJsonDocument{T}"/> the caller disposes. Every mutation stamps a fresh per-record etag and
/// <see cref="PutAsync"/> enforces optimistic concurrency.
/// </summary>
public sealed class InMemoryWorkflowAdministratorStore : IWorkflowAdministratorStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, byte[]> records = new(StringComparer.Ordinal);

    // The reverse administration index (design §15.4): administrator-identity digest → the base ids it administers,
    // ordered by base id for keyset paging — the in-memory analogue of a backend's indexed digest column (the
    // InMemoryObservedIdentityStore.byDigest pattern). administeredDigests holds each base id's current administrator
    // digests so a PutAsync that changes the administrator set can retract the stale digests before indexing the new ones.
    private readonly Dictionary<string, SortedSet<string>> byDigest = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> administeredDigests = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long etagSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryWorkflowAdministratorStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryWorkflowAdministratorStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<WorkflowAdministrators>?>(
                this.records.TryGetValue(baseWorkflowId, out byte[]? json)
                    ? PersistedJson.ToPooledDocument<WorkflowAdministrators>(json)
                    : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration record requires at least one administrator identity.", nameof(administrators));
        }

        lock (this.gate)
        {
            byte[] json;
            if (this.records.TryGetValue(baseWorkflowId, out byte[]? existing))
            {
                // Parse the existing document ONCE, NON-COPYING over the stored array (alive in the dictionary through this
                // synchronous update under the lock) — used for both the etag check and the carried-forward merge.
                using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(existing.AsMemory());

                // A record already exists: the caller must hold its current etag (None means "I expected no record").
                if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
                {
                    throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
                }

                json = WorkflowAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            }
            else
            {
                // No record yet: materialization is only valid against the None etag (the v1-derived default).
                if (!expectedEtag.IsNone)
                {
                    throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
                }

                json = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            }

            this.records[baseWorkflowId] = json;
            this.ReindexAdministered(baseWorkflowId, administrators);
            return new ValueTask<ParsedJsonDocument<WorkflowAdministrators>>(PersistedJson.ToPooledDocument<WorkflowAdministrators>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : WorkflowAdministeredPage.DefaultPageSize;

        // Decode the keyset cursor (the base id to page strictly after) from the request's token; the in-memory storage
        // leaf is a string key, so the cursor reifies once here (never per row) — like InMemoryObservedIdentityStore.
        string? cursor = WorkflowAdministeredContinuationToken.DecodeCursorToString(pageToken);

        lock (this.gate)
        {
            if (!this.byDigest.TryGetValue(adminDigest, out SortedSet<string>? ids))
            {
                return new ValueTask<WorkflowAdministeredPage>(WorkflowAdministeredPage.Create([]));
            }

            // The SortedSet enumerates base ids in ordinal order — the keyset order every backend pages by. Take the
            // pageSize+1 smallest past the cursor; the +1 lookahead detects "more remain" and seeds the next token.
            var page = new List<string>(Math.Min(pageSize + 1, ids.Count));
            foreach (string id in ids)
            {
                if (cursor is not null && string.CompareOrdinal(id, cursor) <= 0)
                {
                    continue; // at or before the cursor — already returned on an earlier page
                }

                page.Add(id);
                if (page.Count > pageSize)
                {
                    break;
                }
            }

            bool more = page.Count > pageSize;
            if (!more)
            {
                return new ValueTask<WorkflowAdministeredPage>(WorkflowAdministeredPage.Create(page));
            }

            page.RemoveAt(page.Count - 1); // drop the lookahead row; resume after the last included row

            // Encode the continuation token from the last returned base id's UTF-8 (bytes-native; scratch sized by the
            // worst-case UTF-8 length and sliced by the actual bytes written).
            string last = page[^1];
            byte[] scratch = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(last.Length));
            try
            {
                int written = Encoding.UTF8.GetBytes(last, scratch);
                return new ValueTask<WorkflowAdministeredPage>(WorkflowAdministeredPage.Create(page, scratch.AsSpan(0, written)));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(scratch);
            }
        }
    }

    // Refreshes the reverse administration index for a base id whose administrator set was just written (§15.4): retract
    // its previous administrator digests, then index its current ones (deduped; the empty identity has no digest and is
    // not indexed). Called under the gate, right after the record is stored. Two identities are set-equal iff their
    // digests are equal, so the same digest the forward IsAdministeredBy compares is the index key the inbox queries.
    private void ReindexAdministered(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators)
    {
        if (this.administeredDigests.Remove(baseWorkflowId, out string[]? previous))
        {
            foreach (string digest in previous)
            {
                if (this.byDigest.TryGetValue(digest, out SortedSet<string>? holders))
                {
                    holders.Remove(baseWorkflowId);
                    if (holders.Count == 0)
                    {
                        this.byDigest.Remove(digest);
                    }
                }
            }
        }

        var current = new List<string>(administrators.Count);
        foreach (WorkflowAdministrators.AdministratorIdentity administrator in administrators)
        {
            if (SecurityIdentityDigest.Compute(SecurityTagSet.CopyFrom(administrator.Tags)) is not { } digest || current.Contains(digest))
            {
                continue;
            }

            current.Add(digest);
            if (!this.byDigest.TryGetValue(digest, out SortedSet<string>? holders))
            {
                holders = new SortedSet<string>(StringComparer.Ordinal);
                this.byDigest[digest] = holders;
            }

            holders.Add(baseWorkflowId);
        }

        this.administeredDigests[baseWorkflowId] = [.. current];
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}