// <copyright file="InMemoryWorkflowDeploymentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Publishing;

/// <summary>
/// The reference in-memory <see cref="IWorkflowDeploymentStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each deployment is held as its UTF-8 JSON document, keyed by its derived id; reads
/// hand back a pooled <see cref="ParsedJsonDocument{T}"/> the caller disposes, and each transition stamps a fresh etag.
/// Mirrors the in-memory availability-request store, keyed by the target-derived id rather than a random id.
/// </summary>
public sealed class InMemoryWorkflowDeploymentStore : IWorkflowDeploymentStore
{
    // Singleton comparer for the list order: oldest first (FIFO for a deploy queue), id as a stable tiebreak.
    private static readonly IComparer<ParsedJsonDocument<WorkflowDeployment>> ByCreatedAt =
        Comparer<ParsedJsonDocument<WorkflowDeployment>>.Create(static (a, b) =>
        {
            int byTime = a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue);
            if (byTime != 0)
            {
                return byTime;
            }

            // Tiebreak on id string-free: compare the JSON values' UTF-8 bytes (no id string is realised per compare).
            using UnescapedUtf8JsonString aId = a.RootElement.Id.GetUtf8String();
            using UnescapedUtf8JsonString bId = b.RootElement.Id.GetUtf8String();
            return aId.Span.SequenceCompareTo(bId.Span);
        });

    private readonly Lock gate = new();
    private readonly Dictionary<string, byte[]> deployments = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long etagSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryWorkflowDeploymentStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryWorkflowDeploymentStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkflowDeployment>> EnqueueAsync(WorkflowDeployment draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            // The id is derived from the target tuple, so a repeated enqueue overwrites the same key — an idempotent upsert
            // that resets the deployment to Queued (WriteNew omits startedAt/completedAt/failureReason/functionUrl/claimedBy,
            // clearing them).
            string id = WorkflowDeployment.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
            byte[] json = WorkflowDeploymentSerialization.SerializeNew(id, draft, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.deployments[id] = json;
            return new ValueTask<ParsedJsonDocument<WorkflowDeployment>>(PersistedJson.ToPooledDocument<WorkflowDeployment>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkflowDeployment>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<WorkflowDeployment>?>(
                this.deployments.TryGetValue(id, out byte[]? json) ? PersistedJson.ToPooledDocument<WorkflowDeployment>(json) : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkflowDeployment>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);

        lock (this.gate)
        {
            // One `now` guards both the lease-expiry test (which orphaned Deploying deployments are reclaimable) and the
            // stamped lease/startedAt, so the claim is evaluated at a single instant.
            DateTimeOffset now = this.timeProvider.GetUtcNow();

            // Find the oldest claimable deployment by (createdAt, id) without materialising a list: parse each row only to
            // test whether it is claimable (Queued, or Deploying with no live lease) and, when it is, compare it against the
            // running minimum, disposing every candidate immediately.
            string? oldestId = null;
            byte[]? oldestJson = null;
            DateTimeOffset oldestCreatedAt = default;
            foreach ((string id, byte[] json) in this.deployments)
            {
                using ParsedJsonDocument<WorkflowDeployment> doc = PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
                if (!doc.RootElement.IsClaimable(now))
                {
                    continue;
                }

                DateTimeOffset createdAt = doc.RootElement.CreatedAtValue;
                if (oldestJson is null || IsOlder(createdAt, id, oldestCreatedAt, oldestId!))
                {
                    oldestId = id;
                    oldestJson = json;
                    oldestCreatedAt = createdAt;
                }
            }

            if (oldestJson is null)
            {
                return new ValueTask<ParsedJsonDocument<WorkflowDeployment>?>((ParsedJsonDocument<WorkflowDeployment>?)null);
            }

            // Parse the chosen document NON-COPYING over the stored array (alive in the dictionary through this synchronous
            // claim under the lock) — WriteClaimed reads its target/created fields, no per-read copy.
            using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(oldestJson.AsMemory());
            byte[] claimed = WorkflowDeploymentSerialization.SerializeClaimed(current.RootElement, claimedBy, now, now + leaseTtl, this.NextEtag());
            this.deployments[oldestId!] = claimed;
            return new ValueTask<ParsedJsonDocument<WorkflowDeployment>?>(PersistedJson.ToPooledDocument<WorkflowDeployment>(claimed));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkflowDeployment>?> CompleteAsync(string id, WorkflowDeploymentCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (this.gate)
        {
            if (!this.deployments.TryGetValue(id, out byte[]? existing))
            {
                return new ValueTask<ParsedJsonDocument<WorkflowDeployment>?>((ParsedJsonDocument<WorkflowDeployment>?)null);
            }

            // Parse the existing document NON-COPYING over the stored array (alive in the dictionary through this
            // synchronous completion under the lock) — the merge reads its etag + carried-forward fields, no per-read copy.
            using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(existing.AsMemory());
            if (!current.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying))
            {
                ThrowHelper.ThrowWorkflowDeploymentNotDeployingForCompletion(id);
            }

            byte[] json = WorkflowDeploymentSerialization.SerializeCompletion(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.deployments[id] = json;
            return new ValueTask<ParsedJsonDocument<WorkflowDeployment>?>(PersistedJson.ToPooledDocument<WorkflowDeployment>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkflowDeployment>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (this.gate)
        {
            if (!this.deployments.TryGetValue(id, out byte[]? existing))
            {
                return new ValueTask<ParsedJsonDocument<WorkflowDeployment>?>((ParsedJsonDocument<WorkflowDeployment>?)null);
            }

            // Parse the existing document NON-COPYING over the stored array (alive in the dictionary through this
            // synchronous renewal under the lock) — the merge reads its etag + carried-forward fields, no per-read copy.
            using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(existing.AsMemory());
            if (!current.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying))
            {
                ThrowHelper.ThrowWorkflowDeploymentNotDeployingForRenewal(id);
            }

            byte[] json = WorkflowDeploymentSerialization.SerializeRenewal(current.RootElement, id, expectedEtag, this.timeProvider.GetUtcNow() + leaseTtl, this.NextEtag());
            this.deployments[id] = json;
            return new ValueTask<ParsedJsonDocument<WorkflowDeployment>?>(PersistedJson.ToPooledDocument<WorkflowDeployment>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<WorkflowDeployment>> ListAsync(WorkflowDeploymentQuery query, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            var docs = new PooledDocumentList<WorkflowDeployment>(this.deployments.Count);
            foreach (byte[] json in this.deployments.Values)
            {
                ParsedJsonDocument<WorkflowDeployment> doc = PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
                if (query.Matches(doc.RootElement))
                {
                    docs.Add(doc);
                }
                else
                {
                    doc.Dispose();
                }
            }

            docs.Sort(ByCreatedAt);
            return new ValueTask<PooledDocumentList<WorkflowDeployment>>(docs);
        }
    }

    /// <inheritdoc/>
    public ValueTask<(int Count, bool Capped)> CountAsync(WorkflowDeploymentQuery query, int cap, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            // Bounded scan: parse each stored row only to apply the same Matches predicate as the list, disposing it
            // immediately (no PooledDocumentList grows); stop once a (cap+1)th match is seen and report Capped.
            int count = 0;
            foreach (byte[] json in this.deployments.Values)
            {
                using ParsedJsonDocument<WorkflowDeployment> doc = PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
                if (query.Matches(doc.RootElement) && ++count > cap)
                {
                    return new ValueTask<(int Count, bool Capped)>((cap, true));
                }
            }

            return new ValueTask<(int Count, bool Capped)>((count, false));
        }
    }

    // True when (candidateAt, candidateId) sorts strictly before (currentAt, currentId) in the (createdAt asc, id ordinal asc) order.
    private static bool IsOlder(DateTimeOffset candidateAt, string candidateId, DateTimeOffset currentAt, string currentId)
    {
        int byTime = candidateAt.CompareTo(currentAt);
        return byTime != 0 ? byTime < 0 : string.CompareOrdinal(candidateId, currentId) < 0;
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}