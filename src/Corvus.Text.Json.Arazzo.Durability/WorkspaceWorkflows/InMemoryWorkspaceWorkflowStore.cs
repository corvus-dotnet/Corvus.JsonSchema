// <copyright file="InMemoryWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

/// <summary>
/// The in-memory reference implementation of <see cref="IWorkspaceWorkflowStore"/>: working copies keyed by their
/// server-minted <c>id</c>, persisted as UTF-8 JSON documents, reach-filtered per row via the caller's
/// <see cref="AccessContext"/>, and keyset-paged ascending by id. The semantic template every durable backend follows.
/// </summary>
public sealed class InMemoryWorkspaceWorkflowStore : IWorkspaceWorkflowStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, byte[]> workingCopies = [];
    private readonly TimeProvider timeProvider;
    private long etagSequence;
    private long idSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemoryWorkspaceWorkflowStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryWorkspaceWorkflowStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        lock (this.gate)
        {
            string id = this.NextId();
            byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.workingCopies[id] = json;
            return new ValueTask<ParsedJsonDocument<WorkspaceWorkflow>>(PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? json = this.FindForManagement(id, AccessVerb.Read, context);
            return new ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?>(
                json is null ? null : PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Id, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = WorkspaceWorkflowContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        lock (this.gate)
        {
            // Materialise the keyset (the id — globally unique, so the total order needs no tie-breaker) and order it —
            // the same total order every backend pages by, just without a DB index. Then scan past the cursor, applying
            // the per-row reach predicate, until the page is full (or the data is exhausted).
            var ordered = new List<(string Id, byte[] Json)>(this.workingCopies.Count);
            foreach (KeyValuePair<string, byte[]> entry in this.workingCopies)
            {
                ordered.Add((entry.Key, entry.Value));
            }

            ordered.Sort(static (x, y) => string.CompareOrdinal(x.Id, y.Id));

            var docs = new PooledDocumentList<WorkspaceWorkflow>(Math.Min(pageSize, ordered.Count));
            bool hasMore = false;
            string lastId = string.Empty;
            try
            {
                foreach ((string Id, byte[] Json) row in ordered)
                {
                    if (hasCursor && string.CompareOrdinal(row.Id, cursor.Id) <= 0)
                    {
                        continue; // at or before the cursor — already returned in an earlier page
                    }

                    // Parse the row ONCE: reach-check it through a non-owning tag view (no per-row CopyFrom), and if it
                    // is both visible and within the page, hand that same pooled document to the page (no second parse).
                    // A skipped/over-the-page row is disposed in the finally; a kept row's ownership transfers to `docs`.
                    ParsedJsonDocument<WorkspaceWorkflow> cand = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(row.Json);
                    bool kept = false;
                    try
                    {
                        SecurityTagSet tags = cand.RootElement.ManagementTags.IsNotUndefined()
                            ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                            : SecurityTagSet.Empty;
                        if (!context.Admits(AccessVerb.Read, tags))
                        {
                            continue; // not reach-visible to this caller (cand disposed in finally)
                        }

                        if (docs.Count == pageSize)
                        {
                            // A further visible row exists → there is a next page; the token resumes after the last included row.
                            hasMore = true;
                            break; // cand disposed in finally
                        }

                        docs.Add(cand);
                        kept = true;
                        lastId = row.Id;
                    }
                    finally
                    {
                        if (!kept)
                        {
                            cand.Dispose();
                        }
                    }
                }

                return new ValueTask<WorkspaceWorkflowPage>(hasMore
                    ? WorkspaceWorkflowPage.Create(docs, lastId, string.Empty)
                    : WorkspaceWorkflowPage.Create(docs));
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? existing = this.FindForManagement(id, AccessVerb.Write, context);
            if (existing is null)
            {
                return new ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?>((ParsedJsonDocument<WorkspaceWorkflow>?)null);
            }

            byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.workingCopies[id] = json; // the id, provenance, and tags are immutable → key unchanged
            return new ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?>(PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        lock (this.gate)
        {
            byte[]? existing = this.FindForManagement(id, AccessVerb.Write, context);
            if (existing is null)
            {
                return new ValueTask<bool>(false);
            }

            if (!expectedEtag.IsNone)
            {
                WorkspaceWorkflowSerialization.EnsureEtag(id, expectedEtag, WorkspaceWorkflowSerialization.EtagOf(existing));
            }

            this.workingCopies.Remove(id);
            return new ValueTask<bool>(true);
        }
    }

    // Finds the working copy with the given id when the caller's reach for the verb admits it. A working copy outside
    // reach is invisible (non-disclosing).
    private byte[]? FindForManagement(string id, AccessVerb verb, AccessContext context)
    {
        if (!this.workingCopies.TryGetValue(id, out byte[]? json))
        {
            return null;
        }

        using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? json : null;
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));

    // Ids sort in creation order (zero-padded) so the keyset page order is stable and humane in tests; durable backends
    // mint their own (e.g. GUID-based) ids — the id is opaque to clients either way.
    private string NextId() => string.Create(CultureInfo.InvariantCulture, $"wc-{++this.idSequence:d10}");
}