// <copyright file="InMemoryEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

/// <summary>
/// The reference in-memory <see cref="IEnvironmentRunnerAuthorizationStore"/> — the conformance baseline and a ready store
/// for single-node / local-development hosts. Each authorization is held as its UTF-8 JSON document, keyed by
/// <c>(environment, runnerId)</c>; reads hand back a pooled <see cref="ParsedJsonDocument{T}"/> the caller disposes, and each
/// decision stamps a fresh etag. Mirrors <see cref="Availability.InMemoryAvailabilityRequestStore"/>.
/// </summary>
public sealed class InMemoryEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string Environment, string RunnerId), byte[]> authorizations = [];
    private readonly TimeProvider timeProvider;
    private long etagSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryEnvironmentRunnerAuthorizationStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryEnvironmentRunnerAuthorizationStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runnerId);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            (string, string) key = (environment, runnerId);
            if (this.authorizations.TryGetValue(key, out byte[]? existing))
            {
                // Idempotent: an existing authorization is returned unchanged whatever its status (a re-registering
                // Authorized runner stays Authorized).
                return new ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>>(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(existing));
            }

            byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.authorizations[key] = json;
            return new ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>>(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?>(
                this.authorizations.TryGetValue((environment, runnerId), out byte[]? json) ? PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json) : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            var docs = new PooledDocumentList<EnvironmentRunnerAuthorization>(this.authorizations.Count);
            foreach (byte[] json in this.authorizations.Values)
            {
                ParsedJsonDocument<EnvironmentRunnerAuthorization> doc = PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
                if (Matches(doc.RootElement, query))
                {
                    docs.Add(doc);
                }
                else
                {
                    doc.Dispose();
                }
            }

            return new ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>>(docs);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> DecideAsync(string environment, string runnerId, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            (string, string) key = (environment, runnerId);
            if (!this.authorizations.TryGetValue(key, out byte[]? existing))
            {
                return new ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?>((ParsedJsonDocument<EnvironmentRunnerAuthorization>?)null);
            }

            // Parse the existing document NON-COPYING over the stored array (alive in the dictionary through this
            // synchronous decision under the lock) — the merge reads its etag + carried-forward fields, no per-read copy.
            using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(existing.AsMemory());
            byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.authorizations[key] = json;
            return new ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?>(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json));
        }
    }

    private static bool Matches(EnvironmentRunnerAuthorization authorization, RunnerAuthorizationQuery query)
    {
        if (query.Status is { } status && !string.Equals(authorization.StatusValue, RunnerAuthorizationStatusNames.ToWire(status), StringComparison.Ordinal))
        {
            return false;
        }

        if (query.Environment is { } environment && !string.Equals(authorization.EnvironmentValue, environment, StringComparison.Ordinal))
        {
            return false;
        }

        // The approver inbox: the row's environment must be one the caller administers (server-derived strings).
        return query.MatchesAdministeredSet(authorization.EnvironmentValue);
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}