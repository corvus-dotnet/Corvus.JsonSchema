// <copyright file="InMemorySourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Linq;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The reference in-memory <see cref="ISourceCredentialStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each binding is held as its UTF-8 JSON document (the "push JSON to the store"
/// shape every backend persists), keyed by (sourceName, environment, security-tags) so tenant-scoped bindings for the
/// same source/environment coexist; reads hand back a pooled <see cref="ParsedJsonDocument{T}"/> the caller disposes.
/// Every mutation stamps a fresh per-record etag.
/// </summary>
/// <remarks>
/// Holding only the reference documents, this store never sees secret material — consistent with the §13 trust
/// boundary. Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2); the usage
/// path (<see cref="ResolveForUsageAsync"/>) matches a run to its entitled binding by label-superset. Secret resolution
/// is the runner's concern (see <see cref="ISecretResolver"/>).
/// </remarks>
public sealed class InMemorySourceCredentialStore : ISourceCredentialStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string SourceName, string Environment, string Tags), byte[]> bindings = new();
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
            (string, string, string) key = (definition.SourceName, definition.Environment, DiscriminatorOf(definition.ManagementTags, definition.UsageTags));
            if (this.bindings.ContainsKey(key))
            {
                throw new InvalidOperationException($"A source credential binding for '{KeyOf(definition.SourceName, definition.Environment)}' with those security tags already exists.");
            }

            byte[] json = SourceCredentialSerialization.SerializeNew(this.NextId(), definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.bindings[key] = json;
            return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>>(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? json = this.FindForManagement(sourceName, environment, AccessVerb.Read, context, out _);
            return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>(
                json is null ? null : PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SourceCredentialBinding>> ListAsync(AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            var docs = new PooledDocumentList<SourceCredentialBinding>(this.bindings.Count);
            try
            {
                foreach (byte[] json in this.bindings.Values)
                {
                    using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
                    if (context.Admits(AccessVerb.Read, candidate.RootElement.ManagementTagsValue))
                    {
                        docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
                    }
                }

                docs.Sort(BySourceThenEnvironment);
                return new ValueTask<PooledDocumentList<SourceCredentialBinding>>(docs);
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialDefinition definition, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);

        lock (this.gate)
        {
            byte[]? existing = this.FindForManagement(sourceName, environment, AccessVerb.Write, context, out (string, string, string) key);
            if (existing is null)
            {
                return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>((ParsedJsonDocument<SourceCredentialBinding>?)null);
            }

            byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, KeyOf(sourceName, environment), expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.bindings[key] = json; // tags immutable → key unchanged
            return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? existing = this.FindForManagement(sourceName, environment, AccessVerb.Write, context, out (string, string, string) key);
            if (existing is null)
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

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        lock (this.gate)
        {
            foreach (KeyValuePair<(string SourceName, string Environment, string Tags), byte[]> entry in this.bindings)
            {
                if (!string.Equals(entry.Key.SourceName, sourceName, StringComparison.Ordinal) || !string.Equals(entry.Key.Environment, environment, StringComparison.Ordinal))
                {
                    continue;
                }

                ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(entry.Value);
                if (candidate.RootElement.IsUsableBy(runTags))
                {
                    return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>(candidate);
                }

                candidate.Dispose();
            }

            return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>((ParsedJsonDocument<SourceCredentialBinding>?)null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<CredentialSourceAccess> EvaluateSourceAccessAsync(string sourceName, SecurityTagSet tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        lock (this.gate)
        {
            bool any = false;
            foreach (KeyValuePair<(string SourceName, string Environment, string Tags), byte[]> entry in this.bindings)
            {
                if (!string.Equals(entry.Key.SourceName, sourceName, StringComparison.Ordinal))
                {
                    continue;
                }

                any = true;
                using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(entry.Value);
                if (candidate.RootElement.IsUsableBy(tags))
                {
                    return new ValueTask<CredentialSourceAccess>(CredentialSourceAccess.Granted);
                }
            }

            return new ValueTask<CredentialSourceAccess>(any ? CredentialSourceAccess.Denied : CredentialSourceAccess.Unconfigured);
        }
    }

    private static string KeyOf(string sourceName, string environment) => $"{sourceName}@{environment}";

    // The binding's uniqueness discriminator over (sourceName, environment): the canonical form of BOTH its management
    // and usage tag sets, so two bindings that differ in either coexist while an exact duplicate is rejected.
    private static string DiscriminatorOf(SecurityTagSet managementTags, SecurityTagSet usageTags)
        => $"{CanonicalTags(managementTags)}{CanonicalTags(usageTags)}";

    // Canonical, order-independent string form of a tag set.
    private static string CanonicalTags(SecurityTagSet tags)
    {
        if (tags.IsEmpty)
        {
            return string.Empty;
        }

        List<SecurityTag> list = tags.ToList();
        list.Sort(static (a, b) =>
        {
            int byKey = string.CompareOrdinal(a.Key, b.Key);
            return byKey != 0 ? byKey : string.CompareOrdinal(a.Value, b.Value);
        });
        return string.Join(";", list.Select(t => $"{t.Key}={t.Value}"));
    }

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its bytes
    // and key. A binding outside reach is invisible (non-disclosing). Deployments keep bindings for one (sourceName,
    // environment) reach-disjoint, so at most one is admitted; the first admitted is returned deterministically.
    private byte[]? FindForManagement(string sourceName, string environment, AccessVerb verb, AccessContext context, out (string, string, string) key)
    {
        foreach (KeyValuePair<(string SourceName, string Environment, string Tags), byte[]> entry in this.bindings)
        {
            if (!string.Equals(entry.Key.SourceName, sourceName, StringComparison.Ordinal) || !string.Equals(entry.Key.Environment, environment, StringComparison.Ordinal))
            {
                continue;
            }

            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(entry.Value);
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