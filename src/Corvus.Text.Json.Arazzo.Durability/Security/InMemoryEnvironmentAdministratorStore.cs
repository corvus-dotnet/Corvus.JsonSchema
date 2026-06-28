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
        }

        return default;
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}