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
    public ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialBinding draft, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            (string, string, string) key = (draft.SourceNameValue, draft.EnvironmentValue, SourceCredentialKey.Discriminator(draft.ManagementTagsValue, draft.UsageTagsValue));
            if (this.bindings.ContainsKey(key))
            {
                throw new InvalidOperationException($"A source credential binding for '{KeyOf(draft.SourceNameValue, draft.EnvironmentValue)}' with those security tags already exists.");
            }

            byte[] json = SourceCredentialSerialization.SerializeNew(this.NextId(), draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
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
    public ValueTask<SourceCredentialPage> ListAsync(AccessContext context, int limit, string? pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        bool hasCursor = SourceCredentialContinuationToken.TryDecode(pageToken, out (string SourceName, string Environment, string TieBreaker) cursor);

        lock (this.gate)
        {
            // Materialise the keyset (sourceName, environment, discriminator) for the in-memory set and order it — the
            // same total order every backend pages by, just without a DB index. Then scan past the cursor, applying the
            // per-row reach predicate, until the page is full (or the data is exhausted).
            var ordered = new List<(string Source, string Env, string Disc, byte[] Json)>(this.bindings.Count);
            foreach (byte[] json in this.bindings.Values)
            {
                using ParsedJsonDocument<SourceCredentialBinding> d = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
                SourceCredentialBinding b = d.RootElement;
                ordered.Add((b.SourceNameValue, b.EnvironmentValue, SourceCredentialKey.Discriminator(b.ManagementTagsValue, b.UsageTagsValue), json));
            }

            ordered.Sort(static (x, y) => CompareKey(x.Source, x.Env, x.Disc, y.Source, y.Env, y.Disc));

            var docs = new PooledDocumentList<SourceCredentialBinding>(Math.Min(pageSize, ordered.Count));
            string? nextToken = null;
            string lastSource = string.Empty, lastEnv = string.Empty, lastDisc = string.Empty;
            try
            {
                foreach ((string Source, string Env, string Disc, byte[] Json) row in ordered)
                {
                    if (hasCursor && CompareKey(row.Source, row.Env, row.Disc, cursor.SourceName, cursor.Environment, cursor.TieBreaker) <= 0)
                    {
                        continue; // at or before the cursor — already returned in an earlier page
                    }

                    using ParsedJsonDocument<SourceCredentialBinding> cand = PersistedJson.ToPooledDocument<SourceCredentialBinding>(row.Json);
                    if (!context.Admits(AccessVerb.Read, cand.RootElement.ManagementTagsValue))
                    {
                        continue; // not reach-visible to this caller
                    }

                    if (docs.Count == pageSize)
                    {
                        // A further visible row exists → there is a next page; the token resumes after the last included row.
                        nextToken = SourceCredentialContinuationToken.Encode(lastSource, lastEnv, lastDisc);
                        break;
                    }

                    docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(row.Json));
                    lastSource = row.Source;
                    lastEnv = row.Env;
                    lastDisc = row.Disc;
                }

                return new ValueTask<SourceCredentialPage>(new SourceCredentialPage(docs, nextToken));
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
    }

    // The stable total order every backend pages by: sourceName, then environment, then the tag discriminator.
    private static int CompareKey(string s1, string e1, string d1, string s2, string e2, string d2)
    {
        int c = string.CompareOrdinal(s1, s2);
        if (c != 0)
        {
            return c;
        }

        c = string.CompareOrdinal(e1, e2);
        return c != 0 ? c : string.CompareOrdinal(d1, d2);
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialBinding draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);

        lock (this.gate)
        {
            byte[]? existing = this.FindForManagement(sourceName, environment, AccessVerb.Write, context, out (string, string, string) key);
            if (existing is null)
            {
                return new ValueTask<ParsedJsonDocument<SourceCredentialBinding>?>((ParsedJsonDocument<SourceCredentialBinding>?)null);
            }

            byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, KeyOf(sourceName, environment), expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
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
}