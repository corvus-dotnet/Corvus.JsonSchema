// <copyright file="InMemoryAvailabilityRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>
/// The reference in-memory <see cref="IAvailabilityRequestStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each request is held as its UTF-8 JSON document, keyed by id; reads hand back a
/// pooled <see cref="ParsedJsonDocument{T}"/> the caller disposes, and each decision stamps a fresh etag. Mirrors
/// <see cref="Security.InMemoryAccessRequestStore"/>.
/// </summary>
public sealed class InMemoryAvailabilityRequestStore : IAvailabilityRequestStore
{
    // Singleton comparer for the list order: oldest first (FIFO for an approver inbox), id as a stable tiebreak.
    private static readonly IComparer<ParsedJsonDocument<AvailabilityRequest>> ByCreatedAt =
        Comparer<ParsedJsonDocument<AvailabilityRequest>>.Create(static (a, b) =>
        {
            int byTime = a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue);
        });

    private readonly Lock gate = new();
    private readonly Dictionary<string, byte[]> requests = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long etagSequence;
    private long requestSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryAvailabilityRequestStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryAvailabilityRequestStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AvailabilityRequest>> CreateAsync(AvailabilityRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            string id = "areq-" + (++this.requestSequence).ToString(CultureInfo.InvariantCulture);
            byte[] json = AvailabilityRequestSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.requests[id] = json;
            return new ValueTask<ParsedJsonDocument<AvailabilityRequest>>(PersistedJson.ToPooledDocument<AvailabilityRequest>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AvailabilityRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<AvailabilityRequest>?>(
                this.requests.TryGetValue(id, out byte[]? json) ? PersistedJson.ToPooledDocument<AvailabilityRequest>(json) : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<AvailabilityRequest>> ListAsync(AvailabilityRequestQuery query, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            var docs = new PooledDocumentList<AvailabilityRequest>(this.requests.Count);
            foreach (byte[] json in this.requests.Values)
            {
                ParsedJsonDocument<AvailabilityRequest> doc = PersistedJson.ToPooledDocument<AvailabilityRequest>(json);
                if (Matches(doc.RootElement, query))
                {
                    docs.Add(doc);
                }
                else
                {
                    doc.Dispose();
                }
            }

            docs.Sort(ByCreatedAt);
            return new ValueTask<PooledDocumentList<AvailabilityRequest>>(docs);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AvailabilityRequest>?> DecideAsync(string id, AvailabilityRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            if (!this.requests.TryGetValue(id, out byte[]? existing))
            {
                return new ValueTask<ParsedJsonDocument<AvailabilityRequest>?>((ParsedJsonDocument<AvailabilityRequest>?)null);
            }

            // Parse the existing document NON-COPYING over the stored array (alive in the dictionary through this
            // synchronous decision under the lock) — the merge reads its etag + carried-forward fields, no per-read copy.
            using ParsedJsonDocument<AvailabilityRequest> current = ParsedJsonDocument<AvailabilityRequest>.Parse(existing.AsMemory());
            byte[] json = AvailabilityRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.requests[id] = json;
            return new ValueTask<ParsedJsonDocument<AvailabilityRequest>?>(PersistedJson.ToPooledDocument<AvailabilityRequest>(json));
        }
    }

    private static bool Matches(AvailabilityRequest request, AvailabilityRequestQuery query)
    {
        if (query.Status is { } status && !string.Equals(request.StatusValue, AvailabilityRequestStatusNames.ToWire(status), StringComparison.Ordinal))
        {
            return false;
        }

        if (query.Environment is { } environment && !string.Equals(request.EnvironmentValue, environment, StringComparison.Ordinal))
        {
            return false;
        }

        // The approver inbox: the row's environment must be one the caller administers (server-derived strings).
        if (!query.MatchesAdministeredSet(request.EnvironmentValue))
        {
            return false;
        }

        // The "mine" view: the row was created by the caller.
        return query.CreatedBy is not { } createdBy || string.Equals(request.CreatedByValue, createdBy, StringComparison.Ordinal);
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}