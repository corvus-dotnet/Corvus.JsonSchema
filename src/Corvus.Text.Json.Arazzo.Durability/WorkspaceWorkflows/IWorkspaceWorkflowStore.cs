// <copyright file="IWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

/// <summary>
/// Durable storage for designer working copies (workflow-designer design §4.1): mutable Arazzo documents (plus designer
/// UI state) saved as many times as needed during development without ever minting a catalog version. A working copy is
/// keyed by its server-assigned <c>id</c>, minted by <see cref="AddAsync"/>; publish mints an immutable catalog version
/// from it (a separate concern — the catalog store).
/// </summary>
/// <remarks>
/// <para><strong>Reach (§14.2).</strong> Reads/writes are reach-filtered by each working copy's
/// <see cref="WorkspaceWorkflow.ManagementTagsValue"/> via the caller's <see cref="AccessContext"/> — a working copy
/// outside the caller's reach is reported as absent (non-disclosing). The deployment stamps the creator's internal
/// tenant tag onto the draft before <see cref="AddAsync"/>, so the new working copy is owned by the creator's slice of
/// the shell.</para>
/// <para><strong>Concurrency.</strong> Saves are long-lived edit sessions, so <see cref="UpdateAsync"/> takes the
/// expected <see cref="WorkflowEtag"/> the client read (pass <see cref="WorkflowEtag.None"/> to act unconditionally); a
/// stale etag throws <see cref="WorkspaceWorkflowConflictException"/> rather than clobbering a collaborator's save.
/// Deletes take an expected etag too (<see cref="WorkflowEtag.None"/> deletes unconditionally — a working copy is
/// development state; the catalog is the durable record).</para>
/// <para><strong>Ownership.</strong> Read/return methods hand back <strong>pooled documents whose lifetime the caller
/// owns</strong>: dispose the returned <see cref="ParsedJsonDocument{T}"/> / <see cref="WorkspaceWorkflowPage"/> once
/// read. Each returned working copy is the <em>full</em> stored document (the Arazzo document and designer state
/// included); the list returns full working copies too, and the control-plane handler field-selects the document-less
/// summary. The draft passed to <see cref="AddAsync"/>/<see cref="UpdateAsync"/> is read synchronously
/// (bytes-to-bytes), so a pooled draft may be disposed once the call returns.</para>
/// </remarks>
public interface IWorkspaceWorkflowStore
{
    /// <summary>Creates a working copy, minting its <c>id</c> and stamping createdBy/createdAt/etag. The draft carries
    /// the display name, the Arazzo document, optional provenance/designer state, and the resolved management tags (the
    /// deployment stamps the creator's tenant tag before calling). The store reads the draft's content bytes-to-bytes
    /// (the document included).</summary>
    /// <param name="draft">The draft working copy (name + document + optional provenance/designer state + resolved
    /// management tags) — typically built by
    /// <see cref="WorkspaceWorkflow.Draft(in JsonElement, in JsonElement, in JsonElement, in JsonElement, in JsonElement, in JsonElement, in SecurityTagSet)"/>
    /// or from a request body; read synchronously, so a pooled draft may be disposed once the call returns.</param>
    /// <param name="actor">The authenticated identity creating the working copy (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created working copy (its minted id included), as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets the working copy with the given <paramref name="id"/> that the caller's read reach admits, or
    /// <see langword="null"/> if none is visible (a working copy outside reach is reported as absent — non-disclosing).</summary>
    /// <param name="id">The working-copy id.</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The working copy as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Lists one keyset page of the working copies the caller's read reach admits, ascending by <c>id</c>.
    /// Reach is a per-row predicate evaluated in keyset order; the store seeks past <paramref name="pageToken"/> and
    /// streams rows applying reach until <paramref name="limit"/> visible working copies accumulate (or the data is
    /// exhausted), emitting a <see cref="WorkspaceWorkflowPage.NextPageToken"/> when more remain. The token is opaque
    /// and backend-scoped.</summary>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="limit">The maximum number of working copies to return (the store treats a non-positive value as 1).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="WorkspaceWorkflowPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page (visible working copies + an optional next-page token), as a disposable batch the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken);

    /// <summary>Counts the working copies the caller's read reach admits, bounded by <paramref name="cap"/> (for list
    /// footers): the same reach as <see cref="ListAsync"/>, but returning only a bounded total, never rows. The default
    /// reads one bounded page of at most <paramref name="cap"/> + 1 visible rows and counts them; a backend that can push
    /// the reach into its query overrides it with a native bounded <c>COUNT</c>.</summary>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cap">The maximum count to report; when the true total exceeds it, <c>Capped</c> is <see langword="true"/> and <c>Count</c> is <paramref name="cap"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bounded count, and whether the cap was hit (so the caller renders e.g. <c>100+</c>).</returns>
    async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        using WorkspaceWorkflowPage page = await this.ListAsync(context, cap + 1, default, cancellationToken).ConfigureAwait(false);
        int count = page.WorkingCopies.Count;
        return count > cap ? (cap, true) : (count, false);
    }

    /// <summary>Saves the working copy with the given <paramref name="id"/> the caller's write reach admits, under
    /// optimistic concurrency. The <c>id</c>, the provenance, the management tags, and the created-* audit fields are
    /// immutable; the document/designer state/name are replaced when the draft supplies them (otherwise carried
    /// forward) — all bytes-to-bytes from the draft / the stored working copy.</summary>
    /// <param name="id">The working-copy id.</param>
    /// <param name="draft">The new content as a draft (its provenance and tags are ignored — immutable, carried
    /// forward); read synchronously, so a pooled draft may be disposed once the call returns.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity saving the working copy (for audit).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The saved working copy as a pooled document the caller must dispose, or <see langword="null"/> if no working copy the caller may write exists for that id.</returns>
    /// <exception cref="WorkspaceWorkflowConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Deletes the working copy with the given <paramref name="id"/> the caller's write reach admits, under
    /// optimistic concurrency.</summary>
    /// <param name="id">The working-copy id.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a working copy the caller may write was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="WorkspaceWorkflowConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken);
}