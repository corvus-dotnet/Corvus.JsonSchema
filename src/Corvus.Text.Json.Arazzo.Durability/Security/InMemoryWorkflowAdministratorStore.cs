// <copyright file="InMemoryWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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
            return new ValueTask<ParsedJsonDocument<WorkflowAdministrators>>(PersistedJson.ToPooledDocument<WorkflowAdministrators>(json));
        }
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}