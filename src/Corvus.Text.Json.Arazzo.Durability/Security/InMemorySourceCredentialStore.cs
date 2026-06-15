// <copyright file="InMemorySourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The reference in-memory <see cref="ISourceCredentialStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each binding is held as its UTF-8 JSON document (the "push JSON to the store"
/// shape every backend persists), keyed by (sourceName, environment); reads hand back a pooled
/// <see cref="ParsedJsonDocument{T}"/> the caller disposes. Every mutation stamps a fresh per-record etag.
/// </summary>
/// <remarks>
/// Holding only the reference documents, this store never sees secret material — consistent with the §13 trust
/// boundary. Secret resolution is the runner's concern (see <see cref="ISecretResolver"/>).
/// </remarks>
public sealed class InMemorySourceCredentialStore : ISourceCredentialStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string SourceName, string Environment), byte[]> bindings = new();
    private readonly TimeProvider timeProvider;
    private long etagSequence;
    private long idSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemorySourceCredentialStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemorySourceCredentialStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialDefinition definition, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            (string, string) key = (definition.SourceName, definition.Environment);
            if (this.bindings.ContainsKey(key))
            {
                throw new InvalidOperationException($"A source credential binding for '{KeyOf(definition.SourceName, definition.Environment)}' already exists.");
            }

            byte[] json = SourceCredentialSerialization.SerializeNew(this.NextId(), definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.bindings[key] = json;
            return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>>(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>(
                this.bindings.TryGetValue((sourceName, environment), out byte[]? json)
                    ? PersistedJson.ToPooledDocument<SourceCredentialBinding>(json)
                    : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SourceCredentialBinding>> ListAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            return new ValueTask<PooledDocumentList<SourceCredentialBinding>>(this.SortedBindings());
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            (string, string) key = (sourceName, environment);
            if (!this.bindings.TryGetValue(key, out byte[]? existing))
            {
                return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>((ParsedJsonDocument<SourceCredentialBinding>?)null);
            }

            byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, KeyOf(sourceName, environment), expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.bindings[key] = json;
            return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        lock (this.gate)
        {
            (string, string) key = (sourceName, environment);
            if (!this.bindings.TryGetValue(key, out byte[]? existing))
            {
                return new ValueTask<bool>(false);
            }

            if (!expectedEtag.IsNone)
            {
                SourceCredentialSerialization.EnsureEtag(KeyOf(sourceName, environment), expectedEtag, SourceCredentialSerialization.EtagOf(existing));
            }

            this.bindings.Remove(key);
            return new ValueTask<bool>(true);
        }
    }

    private static string KeyOf(string sourceName, string environment) => $"{sourceName}@{environment}";

    private PooledDocumentList<SourceCredentialBinding> SortedBindings()
    {
        int count = this.bindings.Count;
        var docs = new PooledDocumentList<SourceCredentialBinding>(count);
        foreach (byte[] json in this.bindings.Values)
        {
            docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
        }

        docs.Sort(BySourceThenEnvironment);
        return docs;
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));

    private string NextId() => $"scred-{++this.idSequence}";

    // Order the listing by sourceName then environment — stable, deterministic, and materializing only the two strings
    // each document already carries.
    private static readonly IComparer<ParsedJsonDocument<SourceCredentialBinding>> BySourceThenEnvironment =
        Comparer<ParsedJsonDocument<SourceCredentialBinding>>.Create(static (a, b) =>
        {
            int bySource = string.CompareOrdinal(a.RootElement.SourceNameValue, b.RootElement.SourceNameValue);
            return bySource != 0 ? bySource : string.CompareOrdinal(a.RootElement.EnvironmentValue, b.RootElement.EnvironmentValue);
        });
}