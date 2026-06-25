// <copyright file="InMemoryAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The reference in-memory <see cref="IAccessRequestStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each request is held as its UTF-8 JSON document, keyed by id; reads hand
/// back a pooled <see cref="ParsedJsonDocument{T}"/> the caller disposes, and each decision stamps a fresh etag.
/// </summary>
public sealed class InMemoryAccessRequestStore : IAccessRequestStore
{
    // Singleton comparer for the list order: oldest first (FIFO for an approver queue), id as a stable tiebreak.
    private static readonly IComparer<ParsedJsonDocument<AccessRequest>> ByCreatedAt =
        Comparer<ParsedJsonDocument<AccessRequest>>.Create(static (a, b) =>
        {
            int byTime = a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue);
        });

    private readonly Lock gate = new();
    private readonly Dictionary<string, byte[]> requests = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long etagSequence;
    private long requestSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryAccessRequestStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryAccessRequestStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            string id = "req-" + (++this.requestSequence).ToString(CultureInfo.InvariantCulture);
            byte[] json = AccessRequestSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.requests[id] = json;
            return new ValueTask<ParsedJsonDocument<AccessRequest>>(PersistedJson.ToPooledDocument<AccessRequest>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<AccessRequest>?>(
                this.requests.TryGetValue(id, out byte[]? json) ? PersistedJson.ToPooledDocument<AccessRequest>(json) : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            var docs = new PooledDocumentList<AccessRequest>(this.requests.Count);
            foreach (byte[] json in this.requests.Values)
            {
                ParsedJsonDocument<AccessRequest> doc = PersistedJson.ToPooledDocument<AccessRequest>(json);
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
            return new ValueTask<PooledDocumentList<AccessRequest>>(docs);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            if (!this.requests.TryGetValue(id, out byte[]? existing))
            {
                return new ValueTask<ParsedJsonDocument<AccessRequest>?>((ParsedJsonDocument<AccessRequest>?)null);
            }

            // Parse the existing document NON-COPYING over the stored array (alive in the dictionary through this
            // synchronous decision under the lock) — the merge reads its etag + carried-forward fields, no per-read copy.
            using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(existing.AsMemory());
            byte[] json = AccessRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.requests[id] = json;
            return new ValueTask<ParsedJsonDocument<AccessRequest>?>(PersistedJson.ToPooledDocument<AccessRequest>(json));
        }
    }

    private static bool Matches(AccessRequest request, AccessRequestQuery query)
    {
        if (query.Status is { } status && !string.Equals(request.StatusValue, AccessRequestStatusNames.ToWire(status), StringComparison.Ordinal))
        {
            return false;
        }

        if (query.BaseWorkflowId.IsNotUndefined())
        {
            // baseWorkflowId arrives as the request's JSON value and is reified nowhere on the way here; compare it to the
            // row's persisted UTF-8 directly (no managed string).
            using UnescapedUtf8JsonString filterBaseWorkflowId = query.BaseWorkflowId.GetUtf8String();
            using UnescapedUtf8JsonString rowBaseWorkflowId = request.BaseWorkflowId.GetUtf8String();
            if (!rowBaseWorkflowId.Span.SequenceEqual(filterBaseWorkflowId.Span))
            {
                return false;
            }
        }

        if (query.SubjectClaimType is { } subjectType && !string.Equals(request.SubjectClaimTypeValue, subjectType, StringComparison.Ordinal))
        {
            return false;
        }

        return query.SubjectClaimValue is not { } subjectValue || string.Equals(request.SubjectClaimValueValue, subjectValue, StringComparison.Ordinal);
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}